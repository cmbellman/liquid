using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace com.swecial.code {
    /// <summary>One value of a rule, valid from one date to another (inclusive, "" = open).</summary>
    public class RuleValue {
        public string from;
        public string to = "";
        /// <summary>A percentage, an amount in öre, or tenths of a percent (Rules.Permille).</summary>
        public long value;
        /// <summary>The last day this value is known to hold (checked against the source); "" = assumed, not checked.</summary>
        public string checkedUntil = "";
    }

    public class Rule {
        public string key;
        /// <summary>"percent", "amount" (öre) or "permille".</summary>
        public string unit;
        /// <summary>Where the rule comes from: a law, a regulation or Skatteverket's page (a link when it starts with http).</summary>
        public string source;
        public List<RuleValue> values;
    }

    /// <summary>
    /// The figures from laws and Skatteverket that change over time, in one place: each rule has its
    /// values by date and a source, and every value says until when it has been checked. Code reads
    /// them by date (Rules.value), the pages pass them on to their scripts (Rules.json), and Baren asks
    /// for a check when today's value of a rule hasn't been checked (`Rules.notChecked`). Update the
    /// table at least once a year: add next year's values with checkedUntil, or extend checkedUntil.
    /// </summary>
    public static class Rules {
        public const string FoodVat = "foodVat";
        public const string RepresentationBase = "representationBase";
        public const string AlcoholSchablon = "alcoholSchablon";
        public const string PriceBaseAmount = "priceBaseAmount";
        public const string CashMethodLimit = "cashMethodLimit";
        public const string YearlyVatLimit = "yearlyVatLimit";
        public const string MonthlyVatLimit = "monthlyVatLimit";
        public const string FundStandardIncome = "fundStandardIncome";

        public const string Percent = "percent";
        public const string Amount = "amount";
        /// <summary>Tenths of a percent (4 = 0,4 %), for rates below one percent.</summary>
        public const string Permille = "permille";

        static RuleValue v(string from, string to, long value, string checkedUntil) {
            return new RuleValue { from = from, to = to, value = value, checkedUntil = checkedUntil };
        }

        public static readonly List<Rule> All = new List<Rule> {
            // Food (also in restaurants) is 6 % from 1 April 2026 to 31 December 2027, then 12 % again unless extended
            new Rule { key = FoodVat, unit = Percent, source = "Mervärdesskattelagen (2023:200) 9 kap., tillfälligt sänkt matmoms 2026-04-01–2027-12-31",
                values = new List<RuleValue> {
                    v("2007-01-01", "2026-03-31", 12, "2026-03-31"),
                    v("2026-04-01", "2027-12-31", 6, "2027-12-31"),
                    v("2028-01-01", "", 12, ""),
                } },
            // Representation: VAT is deducted on at most 300 kr excl. VAT per person and occasion
            new Rule { key = RepresentationBase, unit = Amount, source = "https://www.skatteverket.se/foretag/moms/kopavarorochtjanster/avdragformomsvidrepresentation.4.15532c7b1442f256baec84b.html",
                values = new List<RuleValue> { v("2017-01-01", "", 300_00, "2026-12-31") } },
            // Skatteverket's schablon for a meal with alcohol, per person: follows the food VAT
            new Rule { key = AlcoholSchablon, unit = Amount, source = "https://www.skatteverket.se/foretag/moms/kopavarorochtjanster/avdragformomsvidrepresentation.4.15532c7b1442f256baec84b.html",
                values = new List<RuleValue> {
                    v("2017-01-01", "2026-03-31", 46_00, "2026-03-31"),
                    v("2026-04-01", "2027-12-31", 33_00, "2027-12-31"),
                    v("2028-01-01", "", 46_00, ""),
                } },
            // Prisbasbelopp: half of it is the limit for equipment booked as a cost (förbrukningsinventarier)
            new Rule { key = PriceBaseAmount, unit = Amount, source = "Skatteverket: Belopp och procent (prisbasbelopp enligt socialförsäkringsbalken)",
                values = new List<RuleValue> {
                    v("2025-01-01", "2025-12-31", 58_800_00, "2025-12-31"),
                    v("2026-01-01", "2026-12-31", 59_200_00, "2026-12-31"),
                } },
            // Bokföringslagen: the cash method is allowed up to 3 million kr net turnover a year
            new Rule { key = CashMethodLimit, unit = Amount, source = "Bokföringsnämnden BFNAR 2013:2 Bokföring, kontantmetoden",
                values = new List<RuleValue> { v("2013-01-01", "", 3_000_000_00, "2026-12-31") } },
            // Mervärdesskattelagen: a yearly VAT return for taxable sales up to 1 million kr, monthly above 40 million kr
            new Rule { key = YearlyVatLimit, unit = Amount, source = "Skatteförfarandelagen 26 kap.: redovisningsperioder för moms",
                values = new List<RuleValue> { v("2010-01-01", "", 1_000_000_00, "2026-12-31") } },
            new Rule { key = MonthlyVatLimit, unit = Amount, source = "Skatteförfarandelagen 26 kap.: redovisningsperioder för moms",
                values = new List<RuleValue> { v("2010-01-01", "", 40_000_000_00, "2026-12-31") } },
            // Fund units owned at the start of the calendar year: a standard income of 0,4 % of their value (INK2S 4.6 b)
            new Rule { key = FundStandardIncome, unit = Permille, source = "https://www.skatteverket.se/download/18.569165a01749e7ae7892301/1603721577162/skatteregler-for-aktie-och-handelsbolag-skv294-utgava19.pdf",
                values = new List<RuleValue> { v("2012-01-01", "", 4, "2026-12-31") } },
        };

        public static Rule find(string key) {
            return All.First(r => r.key == key);
        }

        /// <summary>The value that applies on a date; past the last value, the last one (and Baren asks for a check).</summary>
        public static RuleValue at(string key, string date) {
            var values = find(key).values;
            return values.FirstOrDefault(x => covers(x, date))
                ?? values.Where(x => string.CompareOrdinal(x.from, date) <= 0).LastOrDefault()
                ?? values.First();
        }

        public static long value(string key, string date) {
            return at(key, date).value;
        }

        public static long value(string key) {
            return value(key, Dates.today());
        }

        static bool covers(RuleValue x, string date) {
            return string.CompareOrdinal(x.from, date) <= 0 && (x.to.Length == 0 || string.CompareOrdinal(date, x.to) <= 0);
        }

        /// <summary>True when the value for the date has been checked to hold on that date.</summary>
        public static bool isChecked(string key, string date) {
            var x = find(key).values.FirstOrDefault(y => covers(y, date));
            return x != null && x.checkedUntil.Length > 0 && string.CompareOrdinal(date, x.checkedUntil) <= 0;
        }

        /// <summary>The rules whose value for today hasn't been checked.</summary>
        public static List<Rule> notChecked() {
            string today = Dates.today();
            return All.Where(r => !isChecked(r.key, today)).ToList();
        }

        /// <summary>A rule's periods for a page script: [[from, to, value], …].</summary>
        public static string json(string key) {
            return JsonConvert.SerializeObject(find(key).values.Select(x => new object[] { x.from, x.to, x.value }));
        }
    }
}
