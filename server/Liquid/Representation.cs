using System;

namespace com.swecial.code {
    /// <summary>
    /// Food and drink as representation (Skatteverket's rules): the cost is never deductible (6072),
    /// but VAT may be deducted on at most 300 kr excl. VAT per person and occasion. Above that, a meal
    /// without alcohol gives the food rate on 300 kr per person; a meal with alcohol may use
    /// Skatteverket's schablon (46 kr per person at 12 % food VAT, 33 kr at 6 %). Never more than the
    /// VAT on the receipt. The participants, their roles and the purpose are kept on the expense as the
    /// documentation the rules require.
    /// </summary>
    public static class Representation {
        public const string CategoryKey = "representation";
        public const string Internal = "internal";
        public const string External = "external";

        // The figures (food VAT, the 300 kr base, the schablon) come from Rules, by date

        public static int foodRate(string date) {
            return (int)Rules.value(Rules.FoodVat, date.Length > 0 ? date : Dates.today());
        }

        /// <summary>The VAT that may be deducted, from the receipt's total and VAT.</summary>
        public static long deductibleVat(string date, long total, long receiptVat, int participants, bool alcohol) {
            if (participants < 1 || receiptVat <= 0) return 0;
            long net = total - receiptVat;
            long basePerPerson = Rules.value(Rules.RepresentationBase, date);
            if (net <= basePerPerson * participants) return receiptVat;
            long cap = alcohol
                ? Rules.value(Rules.AlcoholSchablon, date) * participants
                : (long)Math.Round(basePerPerson * participants * foodRate(date) / 100m, 0, MidpointRounding.AwayFromZero);
            return Math.Min(receiptVat, cap);
        }
    }
}
