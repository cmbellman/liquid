using com.swecial.server;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace com.swecial.code {
    public class VoucherRow {
        public string account;
        public long debit;
        public long credit;
    }

    /// <summary>
    /// Vouchers (verifikationer). A voucher is never changed or deleted once booked: a mistake is
    /// corrected with a reversing voucher (reverse), as the Swedish Bookkeeping Act requires. The
    /// rows live inside the voucher as JSON, so a voucher is always written as a whole.
    ///
    /// Series: A manual vouchers, F customer invoices booked when sent (accrual method), I customer
    /// payments, U expenses, B other bank events. Numbers run per series and calendar year (the
    /// fiscal year is the calendar year for now).
    /// </summary>
    public static class Ledger {
        public const string Manual = "A";
        public const string Invoices = "F";
        public const string Payments = "I";
        public const string Expenses = "U";
        public const string BankEvents = "B";

        public static List<VoucherRow> rows(SwecialObject voucher) {
            string json = Db.str(voucher, "rowsJson");
            return json.Length == 0 ? new List<VoucherRow>() : JsonConvert.DeserializeObject<List<VoucherRow>>(json);
        }

        public static List<SwecialObject> vouchers(string companyId) {
            return Db.forCompany("voucher", companyId)
                .OrderBy(v => Db.str(v, "date"))
                .ThenBy(v => Db.str(v, "series"))
                .ThenBy(v => Db.num(v, "number"))
                .ToList();
        }

        public static string label(SwecialObject voucher) {
            return Db.str(voucher, "series") + Db.num(voucher, "number");
        }

        /// <summary>
        /// Checks a voucher before booking and returns the rows to book: rows with neither debit nor
        /// credit are dropped; the rest must balance and use accounts in the company's chart.
        /// </summary>
        public static List<VoucherRow> validate(string companyId, string date, string text, List<VoucherRow> input) {
            Dates.parse(date);
            if ((text ?? "").Trim().Length == 0) throw new LiquidException("Verifikationen behöver en text.", "VOUCHER DESCRIPTION REQUIRED.");

            var rows = input.Where(r => r.debit != 0 || r.credit != 0).ToList();
            if (rows.Count < 2) throw new LiquidException("En verifikation behöver minst två rader.", "A VOUCHER REQUIRES AT LEAST TWO LINES.");

            var accounts = Db.forCompany("account", companyId).Select(a => Db.str(a, "number")).ToHashSet();
            foreach (var row in rows) {
                row.account = (row.account ?? "").Trim();
                if (!accounts.Contains(row.account)) throw new LiquidException("Kontot finns inte i kontoplanen: " + row.account, "ACCOUNT NOT IN CHART OF ACCOUNTS: " + row.account);
                if (row.debit < 0 || row.credit < 0) throw new LiquidException("Debet och kredit kan inte vara negativa (konto " + row.account + ").", "DEBIT AND CREDIT CANNOT BE NEGATIVE (ACCOUNT " + row.account + ").");
                if (row.debit != 0 && row.credit != 0) throw new LiquidException("En rad kan inte ha både debet och kredit (konto " + row.account + ").", "A LINE CANNOT CARRY BOTH DEBIT AND CREDIT (ACCOUNT " + row.account + ").");
            }
            long debit = rows.Sum(r => r.debit);
            long credit = rows.Sum(r => r.credit);
            if (debit != credit) {
                throw new LiquidException("Verifikationen balanserar inte: debet " + Money.format(debit) + ", kredit " + Money.format(credit) + ".", "VOUCHER OUT OF BALANCE: DEBIT " + Money.format(debit) + ", CREDIT " + Money.format(credit) + ".");
            }
            return rows;
        }

        /// <summary>Validates, numbers and books a voucher.</summary>
        public static SwecialObject book(string companyId, string series, string date, string text, List<VoucherRow> input,
                string userId, string sourceType = null, string sourceId = null) {
            var rows = validate(companyId, date, text, input);
            date = Dates.clean(date);
            text = text.Trim();
            long debit = rows.Sum(r => r.debit);

            lock (Db.Sync) {
                int year = Dates.parse(date).Year;
                var voucher = Db.create("voucher", companyId);
                voucher.set("series", series);
                voucher.set("year", year);
                voucher.set("number", Db.nextNumber(companyId, "voucher-" + series + "-" + year));
                voucher.set("date", date);
                voucher.set("text", text);
                voucher.set("rowsJson", JsonConvert.SerializeObject(rows));
                voucher.set("total", debit);
                voucher.set("createdBy", userId);
                if (sourceType != null) {
                    voucher.set("sourceType", sourceType);
                    voucher.set("sourceId", sourceId);
                }
                Db.commit(voucher);
                return voucher;
            }
        }

        /// <summary>
        /// Books the opposite of a voucher and links the two. A voucher can be reversed once. Vouchers
        /// booked from an expense or bank event are reversed by that object (fromSource), so the two
        /// stay in step; from the voucher page they can't be.
        /// </summary>
        public static SwecialObject reverse(SwecialObject original, string date, string userId, bool fromSource = false) {
            lock (Db.Sync) {
                if (Db.str(original, "reversedById").Length > 0) throw new LiquidException("Verifikationen är redan makulerad.", "VOUCHER ALREADY VOIDED.");
                if (Db.str(original, "reversesId").Length > 0) throw new LiquidException("En rättelse kan inte makuleras.", "A REVERSAL ENTRY CANNOT BE VOIDED.");
                // The invoice's status would no longer match its vouchers (credit notes come later)
                if (!fromSource && Db.str(original, "sourceType").Length > 0) throw new LiquidException("Verifikationen är bokförd från en faktura, utgift eller bankhändelse. Ändra eller ta bort den där i stället.", "VOUCHER ORIGINATES FROM AN INVOICE, EXPENDITURE OR TREASURY EVENT. AMEND IT AT THE SOURCE.");
                var swapped = rows(original).Select(r => new VoucherRow { account = r.account, debit = r.credit, credit = r.debit }).ToList();
                var reversal = book(Db.str(original, "companyId"), Db.str(original, "series"), date,
                    "Rättelse av " + label(original) + ": " + Db.str(original, "text"), swapped, userId);
                reversal.set("reversesId", original.id);
                Db.commit(reversal);
                original.set("reversedById", reversal.id);
                Db.commit(original);
                return reversal;
            }
        }
    }
}
