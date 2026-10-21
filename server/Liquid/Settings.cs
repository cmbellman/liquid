using com.swecial.server;
using System;
using System.Linq;

namespace com.swecial.code {
    /// <summary>
    /// The company's choices, stored on the company object. Defaults are what a small AB usually
    /// has: invoice numbers in a plain series, the cash method and a yearly VAT return.
    /// </summary>
    public static class Settings {
        /// <summary>Invoice numbers 1, 2, 3 …</summary>
        public const string SerialNumbers = "serial";
        /// <summary>Invoice numbers from the invoice date and a serial for the day: 2026102101.</summary>
        public const string DateNumbers = "date";

        /// <summary>Kontantmetoden: invoices are booked when they are paid.</summary>
        public const string CashMethod = "cash";
        /// <summary>Faktureringsmetoden: invoices are booked when they are sent, the payment clears the receivable.</summary>
        public const string AccrualMethod = "accrual";

        public const string VatYear = "year";
        public const string VatQuarter = "quarter";
        public const string VatMonth = "month";

        /// <summary>The counter behind serial invoice numbers.</summary>
        public const string SerialCounter = "invoice";
        /// <summary>Serial numbers stay below the date numbers (2000010101 and up), so the two never collide.</summary>
        public const long MaxSerial = 999_999_999;

        public static string invoiceNumbering(SwecialObject company) {
            return Db.str(company, "invoiceNumbering") == DateNumbers ? DateNumbers : SerialNumbers;
        }

        public static string accountingMethod(SwecialObject company) {
            return Db.str(company, "accountingMethod") == AccrualMethod ? AccrualMethod : CashMethod;
        }

        public static string vatPeriod(SwecialObject company) {
            string p = Db.str(company, "vatPeriod");
            return p == VatQuarter || p == VatMonth ? p : VatYear;
        }

        /// <summary>The highest serial invoice number in use (date numbers aren't counted).</summary>
        public static long highestSerial(string companyId) {
            long counter = Db.num(Db.forCompany("counter", companyId).FirstOrDefault(c => Db.str(c, "key") == SerialCounter), "value");
            long used = Db.forCompany("invoice", companyId).Select(i => Db.num(i, "number")).Where(n => n > 0 && n <= MaxSerial).DefaultIfEmpty(0).Max();
            return Math.Max(counter, used);
        }

        /// <summary>Lets the serial series continue from a number (e.g. where an earlier system ended).</summary>
        public static void setNextSerial(string companyId, long next) {
            lock (Db.Sync) {
                if (next < 1 || next > MaxSerial) throw new LiquidException("Nästa fakturanummer ska vara mellan 1 och " + MaxSerial + ".", "NEXT INVOICE NUMBER MUST BE BETWEEN 1 AND " + MaxSerial + ".");
                long highest = highestSerial(companyId);
                if (next <= highest) throw new LiquidException("Nästa fakturanummer måste vara större än " + highest + ", som redan är använt.", "NEXT INVOICE NUMBER MUST EXCEED " + highest + ", ALREADY IN USE.");
                var counter = Db.forCompany("counter", companyId).FirstOrDefault(c => Db.str(c, "key") == SerialCounter);
                if (counter == null) {
                    counter = Db.create("counter", companyId);
                    counter.set("key", SerialCounter);
                }
                counter.set("value", next - 1);
                Db.commit(counter);
            }
        }

        /// <summary>The VAT return period that contains the date, as (first day, last day).</summary>
        public static (DateTime from, DateTime to) vatPeriodOf(SwecialObject company, DateTime date) {
            switch (vatPeriod(company)) {
                case VatMonth: {
                    var from = new DateTime(date.Year, date.Month, 1);
                    return (from, from.AddMonths(1).AddDays(-1));
                }
                case VatQuarter: {
                    var from = new DateTime(date.Year, (date.Month - 1) / 3 * 3 + 1, 1);
                    return (from, from.AddMonths(3).AddDays(-1));
                }
                default:
                    return (new DateTime(date.Year, 1, 1), new DateTime(date.Year, 12, 31));
            }
        }
    }
}
