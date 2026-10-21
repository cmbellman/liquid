using System;
using System.Globalization;

namespace com.swecial.code {
    /// <summary>
    /// Amounts are whole öre in a long everywhere (never double), formatted and parsed the Swedish way.
    /// </summary>
    public static class Money {
        static readonly CultureInfo Sv = CultureInfo.GetCultureInfo("sv-SE");

        public static string format(long ore) {
            return (ore / 100m).ToString("#,##0.00", Sv);
        }

        /// <summary>Whole kronor, rounded, for big figures where öre are noise ("65 000").</summary>
        public static string formatWhole(long ore) {
            return Math.Round(ore / 100m, 0, MidpointRounding.AwayFromZero).ToString("#,##0", Sv);
        }

        /// <summary>Empty for zero, for debit/credit columns.</summary>
        public static string formatOrBlank(long ore) {
            return ore == 0 ? "" : format(ore);
        }

        /// <summary>The value for an input field: no thousand separators, comma decimals.</summary>
        public static string input(long ore) {
            return ore == 0 ? "" : (ore / 100m).ToString("0.00", Sv);
        }

        /// <summary>Parses "1 234,50", "1234.50" or "-12" into öre. Empty is 0.</summary>
        public static long parse(string text) {
            decimal value = parseDecimal(text, "belopp");
            decimal ore = Math.Round(value * 100m, 0, MidpointRounding.AwayFromZero);
            if (ore != value * 100m) throw new LiquidException("Belopp kan inte ha fler än två decimaler: " + text, "AMOUNTS ALLOW TWO DECIMALS: " + text);
            return (long)ore;
        }

        public static decimal parseDecimal(string text, string what) {
            string s = (text ?? "").Trim().Replace(" ", "").Replace(" ", "").Replace(" ", "").Replace(',', '.');
            if (s.Length == 0) return 0;
            if (!decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value)) {
                throw new LiquidException("Ogiltigt " + what + ": " + text, "INVALID INPUT: " + text);
            }
            return value;
        }

        public static string formatDecimal(decimal value) {
            return value.ToString("0.##", Sv);
        }

        /// <summary>round(amount × rate / 100), half away from zero.</summary>
        public static long percent(long ore, int rate) {
            return (long)Math.Round(ore * (decimal)rate / 100m, 0, MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>Dates are stored as "yyyy-MM-dd" strings.</summary>
    public static class Dates {
        public const string Format = "yyyy-MM-dd";

        public static string today() {
            return DateTime.Today.ToString(Format, CultureInfo.InvariantCulture);
        }

        public static string format(DateTime date) {
            return date.ToString(Format, CultureInfo.InvariantCulture);
        }

        public static DateTime parse(string text) {
            if (!DateTime.TryParseExact((text ?? "").Trim(), Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)) {
                throw new LiquidException("Ogiltigt datum (ÅÅÅÅ-MM-DD): " + text, "INVALID DATE (YYYY-MM-DD): " + text);
            }
            return date;
        }

        /// <summary>Normalizes user input to "yyyy-MM-dd" or throws.</summary>
        public static string clean(string text) {
            return format(parse(text));
        }
    }
}
