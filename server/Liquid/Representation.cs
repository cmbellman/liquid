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

        /// <summary>The VAT base allowed per person and occasion, in öre.</summary>
        public const long BasePerPerson = 300_00;

        /// <summary>Food VAT is 6 % from 1 April 2026 to 31 December 2027, otherwise 12 %.</summary>
        public static int foodRate(string date) {
            return string.CompareOrdinal(date, "2026-04-01") >= 0 && string.CompareOrdinal(date, "2028-01-01") < 0 ? 6 : 12;
        }

        /// <summary>Skatteverket's schablon per person for a meal with alcohol, in öre.</summary>
        public static long alcoholSchablon(string date) {
            return foodRate(date) == 6 ? 33_00 : 46_00;
        }

        /// <summary>The VAT that may be deducted, from the receipt's total and VAT.</summary>
        public static long deductibleVat(string date, long total, long receiptVat, int participants, bool alcohol) {
            if (participants < 1 || receiptVat <= 0) return 0;
            long net = total - receiptVat;
            if (net <= BasePerPerson * participants) return receiptVat;
            long cap = alcohol
                ? alcoholSchablon(date) * participants
                : (long)Math.Round(BasePerPerson * participants * foodRate(date) / 100m, 0, MidpointRounding.AwayFromZero);
            return Math.Min(receiptVat, cap);
        }
    }
}
