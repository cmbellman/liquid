using com.swecial.server;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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

        /// <summary>Invoice visual layouts.</summary>
        public const string StyleNordic = "nordic";
        public const string StyleExecutive = "executive";
        public const string StyleStudio = "studio";

        /// <summary>Invoice colour accents (AccentTheme adapts to the user's active theme).</summary>
        public const string AccentTheme = "theme";
        public const string AccentTeal = "teal";
        public const string AccentNavy = "navy";
        public const string AccentSlate = "slate";
        public const string AccentBurgundy = "burgundy";
        public const string AccentForest = "forest";

        /// <summary>The counter behind serial invoice numbers.</summary>
        public const string SerialCounter = "invoice";
        /// <summary>Serial numbers stay below the date numbers (2000010101 and up), so the two never collide.</summary>
        public const long MaxSerial = 999_999_999;

        public static string invoiceNumbering(SwecialObject company) {
            return Db.str(company, "invoiceNumbering") == DateNumbers ? DateNumbers : SerialNumbers;
        }

        public static string invoiceStyle(SwecialObject company) {
            return cleanStyle(Db.str(company, "invoiceStyle"));
        }

        public static string cleanStyle(string style) {
            return style == StyleExecutive || style == StyleStudio ? style : StyleNordic;
        }

        public static string invoiceAccent(SwecialObject company) {
            return cleanAccent(Db.str(company, "invoiceAccent"));
        }

        public static string cleanAccent(string accent) {
            return accent == AccentTeal || accent == AccentNavy || accent == AccentSlate || accent == AccentBurgundy || accent == AccentForest
                ? accent : AccentTheme;
        }

        /// <summary>
        /// The accounting method for a fiscal year (calendar year). It is chosen per year
        /// (methodsJson, {"2026":"cash"}); a year without a choice has the latest earlier year's,
        /// or the company's old single setting.
        /// </summary>
        public static string accountingMethod(SwecialObject company, int year) {
            string chosen = methods(company).Where(p => p.Key <= year).OrderByDescending(p => p.Key).Select(p => p.Value).FirstOrDefault();
            return clean(chosen ?? Db.str(company, "accountingMethod"));
        }

        public static string accountingMethod(SwecialObject company, string date) {
            return accountingMethod(company, Dates.parse(date).Year);
        }

        public static Dictionary<int, string> methods(SwecialObject company) {
            string json = Db.str(company, "methodsJson");
            return json.Length == 0 ? new Dictionary<int, string>() : JsonConvert.DeserializeObject<Dictionary<int, string>>(json);
        }

        /// <summary>Sets the method for a year (the caller commits the company).</summary>
        public static void setAccountingMethod(SwecialObject company, int year, string method) {
            var all = methods(company);
            all[year] = clean(method);
            company.set("methodsJson", JsonConvert.SerializeObject(all));
        }

        public static string clean(string method) {
            return method == AccrualMethod ? AccrualMethod : CashMethod;
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
