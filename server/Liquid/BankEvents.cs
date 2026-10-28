using com.swecial.server;
using System.Collections.Generic;
using System.Linq;

namespace com.swecial.code {
    public class BankEventKind {
        public string key;
        public string name;
        /// <summary>True when money comes into the company account.</summary>
        public bool incoming;
        /// <summary>The account on the other side of 1930.</summary>
        public string account;
        public string hint = "";
    }

    /// <summary>
    /// Money in or out of the company account that isn't an invoice or an expense, as plain choices.
    /// Booked (series B) as 1930 against the kind's account. Like expenses, an event owns its voucher.
    /// </summary>
    public static class BankEvents {
        public const string Booked = "booked";
        public const string Pending = "pending";
        public const string Removed = "removed";

        public static readonly List<BankEventKind> Kinds = new List<BankEventKind> {
            new BankEventKind { key = "shareCapital", name = "Insättning av aktiekapital", incoming = true, account = Bas.ShareCapital,
                hint = "Pengarna du satte in när bolaget bildades (eller vid en nyemission)." },
            new BankEventKind { key = "ownerLoanIn", name = "Jag lånar ut pengar till bolaget", incoming = true, account = Bas.OwnerLoans },
            new BankEventKind { key = "ownerLoanOut", name = "Bolaget betalar tillbaka lån till mig", incoming = false, account = Bas.OwnerLoans },
            new BankEventKind { key = "taxPayment", name = "Inbetalning till skattekontot", incoming = false, account = Bas.TaxAccount,
                hint = "Till exempel preliminärskatt (F-skatt) eller moms. Hur den används stäms av i bokslutet." },
            new BankEventKind { key = "taxRefund", name = "Utbetalning från skattekontot", incoming = true, account = Bas.TaxAccount },
            new BankEventKind { key = "bankFee", name = "Bankavgift", incoming = false, account = Bas.BankFees },
            new BankEventKind { key = "interest", name = "Ränta på kontot", incoming = true, account = Bas.InterestIncome },
            new BankEventKind { key = "dividend", name = "Utbetald utdelning", incoming = false, account = Bas.RetainedEarnings,
                hint = "Bara efter beslut på årsstämman, och högst det fria egna kapitalet." },
        };

        public static BankEventKind find(string key) {
            return Kinds.FirstOrDefault(k => k.key == key);
        }

        public static List<SwecialObject> pending(string companyId) {
            return Db.forCompany("bank_event", companyId)
                .Where(e => Db.str(e, "status") == Pending)
                .OrderBy(e => Db.str(e, "date"))
                .ToList();
        }

        public static List<SwecialObject> due(string companyId, string today = null) {
            string cutoff = today ?? Dates.today();
            return pending(companyId)
                .Where(e => string.CompareOrdinal(Db.str(e, "date"), cutoff) <= 0)
                .ToList();
        }

        public static SwecialObject schedule(string companyId, string kindKey, string date, long amount, string note, string userId, string fileId = null) {
            var kind = find(kindKey) ?? throw new LiquidException("Välj vad det gäller.", "SELECT TRANSACTION TYPE.");
            if (amount <= 0) throw new LiquidException("Fyll i beloppet.", "AMOUNT REQUIRED.");
            date = Dates.clean(date);
            note = (note ?? "").Trim();
            lock (Db.Sync) {
                var e = Db.create("bank_event", companyId);
                e.set("kind", kind.key);
                e.set("date", date);
                e.set("amount", amount);
                e.set("note", note);
                e.set("status", Pending);
                e.set("createdBy", userId);
                if (!string.IsNullOrEmpty(fileId)) e.set("fileId", fileId);
                Db.commit(e);
                return e;
            }
        }

        public static SwecialObject bookPending(SwecialObject pendingEvent, string userId) {
            string companyId = Db.str(pendingEvent, "companyId");
            string kindKey = Db.str(pendingEvent, "kind");
            var kind = find(kindKey) ?? throw new LiquidException("Välj vad det gäller.", "SELECT TRANSACTION TYPE.");
            string date = Dates.clean(Db.str(pendingEvent, "date"));
            long amount = Db.num(pendingEvent, "amount");
            if (amount <= 0) throw new LiquidException("Fyll i beloppet.", "AMOUNT REQUIRED.");
            string note = Db.str(pendingEvent, "note");
            string text = kind.name + (note.Length > 0 ? ": " + note : "");
            var rows = kind.incoming
                ? new List<VoucherRow> { new VoucherRow { account = Bas.Bank, debit = amount }, new VoucherRow { account = kind.account, credit = amount } }
                : new List<VoucherRow> { new VoucherRow { account = kind.account, debit = amount }, new VoucherRow { account = Bas.Bank, credit = amount } };
            Ledger.validate(companyId, date, text, rows);

            lock (Db.Sync) {
                var voucher = Ledger.book(companyId, Ledger.BankEvents, date, text, rows, userId, "bank_event", pendingEvent.id);
                pendingEvent.set("voucherId", voucher.id);
                pendingEvent.set("status", Booked);
                Db.commit(pendingEvent);
                return pendingEvent;
            }
        }

        public static SwecialObject book(string companyId, string kindKey, string date, long amount, string note, string userId) {
            var kind = find(kindKey) ?? throw new LiquidException("Välj vad det gäller.", "SELECT TRANSACTION TYPE.");
            if (amount <= 0) throw new LiquidException("Fyll i beloppet.", "AMOUNT REQUIRED.");
            date = Dates.clean(date);
            note = (note ?? "").Trim();
            string text = kind.name + (note.Length > 0 ? ": " + note : "");
            var rows = kind.incoming
                ? new List<VoucherRow> { new VoucherRow { account = Bas.Bank, debit = amount }, new VoucherRow { account = kind.account, credit = amount } }
                : new List<VoucherRow> { new VoucherRow { account = kind.account, debit = amount }, new VoucherRow { account = Bas.Bank, credit = amount } };
            Ledger.validate(companyId, date, text, rows);

            lock (Db.Sync) {
                var e = Db.create("bank_event", companyId);
                e.set("kind", kind.key);
                e.set("date", date);
                e.set("amount", amount);
                e.set("note", note);
                e.set("status", Booked);
                var voucher = Ledger.book(companyId, Ledger.BankEvents, date, text, rows, userId, "bank_event", e.id);
                e.set("voucherId", voucher.id);
                Db.commit(e);
                return e;
            }
        }

        public static void remove(SwecialObject bankEvent, string userId) {
            lock (Db.Sync) {
                if (Db.str(bankEvent, "status") == Removed) return;
                string vId = Db.str(bankEvent, "voucherId");
                if (vId.Length > 0) {
                    var voucher = Db.getOwned("voucher", vId, Db.str(bankEvent, "companyId"));
                    if (voucher != null && Db.str(voucher, "reversedById").Length == 0) {
                        Ledger.reverse(voucher, Db.str(voucher, "date"), userId, fromSource: true);
                    }
                }
                bankEvent.set("status", Removed);
                Db.commit(bankEvent);
            }
        }
    }
}
