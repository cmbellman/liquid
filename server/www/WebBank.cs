using com.swecial.code;
using com.swecial.server;
using System.Linq;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/bank.html : Kassan, the company account (1930) in plain words: what came in and went
    /// out, a check against the bank's balance (?date=&amp;bank=), and a form for money that isn't an
    /// invoice or an expense (bank events).
    /// </summary>
    public class WebBank : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            title(Tr("bank.title"));
            if (isPost) {
                if (form("action") == "remove") {
                    var bankEvent = Db.getOwned("bank_event", form("id"), companyId) ?? throw new LiquidException("Händelsen finns inte.", "EVENT NOT FOUND.");
                    BankEvents.remove(bankEvent, account.id);
                    redirect(BasePath + "/bank.html?ok=deleted");
                    return;
                }
                if (form("action") == "bookPending") {
                    var bankEvent = Db.getOwned("bank_event", form("id"), companyId) ?? throw new LiquidException("Händelsen finns inte.", "EVENT NOT FOUND.");
                    BankEvents.bookPending(bankEvent, account.id);
                    redirect(BasePath + "/bank.html?ok=saved");
                    return;
                }
                BankEvents.book(companyId, form("kind"), form("date"), Money.parse(form("amount")), form("note"), account.id);
                redirect(BasePath + "/bank.html?ok=saved");
                return;
            }
            setValues();
        }

        protected override void renderAfterError() {
            setValues();
        }

        void setValues() {
            string today = Dates.today();
            var lines = Reports.generalLedger(companyId, Bas.Bank, "0000-01-01", "9999-12-31", out _);
            long bankBalance = lines.Count > 0 ? lines.Last().balance : 0;
            set("balance", Money.format(bankBalance));

            var secLines = Reports.generalLedger(companyId, Bas.Securities, "0000-01-01", "9999-12-31", out _);
            long secBalance = secLines.Count > 0 ? secLines.Last().balance : 0;
            bool hasSecurities = secBalance != 0;
            set("securitiesAny", hasSecurities);
            if (hasSecurities) {
                set("securitiesBalance", Money.format(secBalance));
                set("totalBalance", Money.format(bankBalance + secBalance));
            }

            // The check against the bank
            string date = query("date").Length > 0 ? query("date") : today;
            val("reconcileDate", date);
            val("reconcileBank", query("bank"));
            set("reconcile", "");
            if (query("bank").Length > 0) {
                string cleanDate = Dates.clean(date);
                long bank = Money.parse(query("bank"));
                var upTo = lines.Where(l => string.CompareOrdinal(Db.str(l.voucher, "date"), cleanDate) <= 0).ToList();
                long liquid = upTo.Count > 0 ? upTo.Last().balance : 0;
                long diff = bank - liquid;
                set("reconcile", diff == 0 ? "match" : "mismatch");
                set("reconcileText", diff == 0
                    ? T("bank.match", Money.format(liquid), h(cleanDate))
                    : T(diff > 0 ? "bank.mismatch.in" : "bank.mismatch.out", Money.format(bank), Money.format(liquid), h(cleanDate), Money.format(diff)));
            }

            // The form for other events
            set("kinds", BankEvents.Kinds.Select(k => row(("value", k.key), ("hint", T("bankEvent." + k.key + ".hint")),
                ("selected", selected(form("kind") == k.key)),
                ("text", T("bankEvent." + k.key + ".name") + " (" + T(k.incoming ? "bar.chart.in" : "bar.chart.out") + ")"))).ToList());
            val("date", isPost ? form("date") : today);
            val("amount", form("amount"));
            val("note", form("note"));

            var pending = BankEvents.pending(companyId);
            set("pendingAny", pending.Count > 0);
            rows("pendingEvents", pending.Select(e => {
                string pDate = Db.str(e, "date");
                bool isDue = string.CompareOrdinal(pDate, today) <= 0;
                var kind = BankEvents.find(Db.str(e, "kind"));
                return row(
                    ("id", h(e.id)),
                    ("date", h(pDate)),
                    ("name", h(kind?.name ?? Db.str(e, "kind"))),
                    ("note", h(Db.str(e, "note"))),
                    ("amount", Money.format(Db.num(e, "amount"))),
                    ("isDue", isDue),
                    ("statusText", T(isDue ? "bank.pending.due" : "bank.pending.scheduled"))
                );
            }));

            // A changed or removed expense/event leaves a voucher and its reversal on the same date;
            // they cancel out, so only what still counts is listed (the balances include everything)
            rows("movements", Enumerable.Reverse(lines)
                .Where(l => Db.linkedId(l.voucher, "reversedBy").Length == 0 && Db.linkedId(l.voucher, "reverses").Length == 0)
                .Select(l => {
                    var bankEvent = Db.str(l.voucher, "sourceType") == "bank_event" ? Db.linked(l.voucher, "source", "bank_event") : null;
                    return row(("date", h(Db.str(l.voucher, "date"))), ("href", source(l.voucher)), ("text", h(Db.str(l.voucher, "text"))),
                        ("in", Money.formatOrBlank(l.debit)), ("out", Money.formatOrBlank(l.credit)), ("balance", Money.format(l.balance)),
                        ("removable", bankEvent != null && Db.str(bankEvent, "status") != "removed"), ("eventId", h(bankEvent?.id ?? "")));
                }));
        }

        /// <summary>Where a bank movement came from: the invoice, expense, fund event or voucher.</summary>
        static string source(SwecialObject voucher) {
            string sourceId = Db.linkedId(voucher, "source");
            switch (Db.str(voucher, "sourceType")) {
                case "invoice": return BasePath + "/invoice.html?id=" + u(sourceId);
                case "expense": return BasePath + "/expense.html?id=" + u(sourceId);
                case "security_event": return BasePath + "/securities.html";
                default: return BasePath + "/voucher.html?id=" + u(voucher.id);
            }
        }
    }
}
