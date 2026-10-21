using com.swecial.server;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace com.swecial.code {
    public class FlowBucket {
        public DateTime start;
        /// <summary>Exclusive.</summary>
        public DateTime end;
        public string label;
        public long moneyIn;
        public long moneyOut;
        /// <summary>After today: what is expected, not what happened.</summary>
        public bool forecast;
    }

    public class Runway {
        public long balance;
        /// <summary>Average money out per month over the last three months.</summary>
        public long monthlyOut;
        /// <summary>Months the balance lasts at that pace; null when nothing went out.</summary>
        public decimal? months;
        /// <summary>Unpaid customer invoices (Notan).</summary>
        public long owedToYou;
    }

    /// <summary>
    /// Money in and out of the company account over time (Kranen) and how long it lasts (Glaset).
    /// Everything comes from the bank account's vouchers (1930), so it shows what is registered in
    /// Liquid. A voucher and its reversal (a changed or removed expense) are left out: they cancel out
    /// and would otherwise show up as both money in and out.
    /// </summary>
    public static class Flow {
        static readonly CultureInfo Sv = CultureInfo.GetCultureInfo("sv-SE");

        public const string Quarter = "kvartal";
        public const string Year = "ar";
        public const string TwelveMonths = "12m";

        /// <summary>Money in (debit) and out (credit) on 1930 per voucher date, without reversal pairs.</summary>
        static List<(DateTime date, long moneyIn, long moneyOut)> movements(string companyId) {
            var result = new List<(DateTime, long, long)>();
            foreach (var voucher in Ledger.vouchers(companyId)) {
                if (Db.str(voucher, "reversedById").Length > 0 || Db.str(voucher, "reversesId").Length > 0) continue;
                long moneyIn = 0, moneyOut = 0;
                foreach (var row in Ledger.rows(voucher).Where(r => r.account == Bas.Bank)) {
                    moneyIn += row.debit;
                    moneyOut += row.credit;
                }
                if (moneyIn != 0 || moneyOut != 0) result.Add((Dates.parse(Db.str(voucher, "date")), moneyIn, moneyOut));
            }
            return result;
        }

        /// <summary>
        /// Buckets for a period: "kvartal" is weeks from 12 weeks back to 4 ahead, "ar" the months of
        /// this year, and "12m" (default) months from 11 back to 3 ahead. Future buckets are a forecast
        /// of the unpaid invoices by due date; overdue ones are expected in the current bucket.
        /// </summary>
        public static List<FlowBucket> buckets(string companyId, string period, DateTime today) {
            var buckets = new List<FlowBucket>();
            if (period == Quarter) {
                DateTime monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
                for (int i = -12; i <= 4; i++) {
                    DateTime start = monday.AddDays(7 * i);
                    buckets.Add(new FlowBucket { start = start, end = start.AddDays(7), label = "v" + ISOWeek.GetWeekOfYear(start) });
                }
            } else {
                DateTime month = new DateTime(today.Year, today.Month, 1);
                int from = period == Year ? 1 - today.Month : -11;
                int to = period == Year ? 12 - today.Month : 3;
                for (int i = from; i <= to; i++) {
                    DateTime start = month.AddMonths(i);
                    buckets.Add(new FlowBucket { start = start, end = start.AddMonths(1), label = start.ToString("MMM", Sv).TrimEnd('.') });
                }
            }
            foreach (var b in buckets) b.forecast = b.start > today;

            foreach (var (date, moneyIn, moneyOut) in movements(companyId)) {
                var b = buckets.FirstOrDefault(x => date >= x.start && date < x.end);
                if (b == null || b.forecast) continue;
                b.moneyIn += moneyIn;
                b.moneyOut += moneyOut;
            }

            var current = buckets.FirstOrDefault(x => today >= x.start && today < x.end);
            foreach (var invoice in Db.forCompany("invoice", companyId).Where(i => Db.str(i, "status") == Invoicing.Sent)) {
                DateTime due = Dates.parse(Db.str(invoice, "dueDate"));
                var b = due <= today ? current : buckets.FirstOrDefault(x => due >= x.start && due < x.end);
                if (b != null && (b.forecast || b == current)) b.moneyIn += Db.num(invoice, "total");
            }
            return buckets;
        }

        /// <summary>Sums of what actually happened in a period (no forecast).</summary>
        public static (long moneyIn, long moneyOut) totals(List<FlowBucket> buckets, string companyId, DateTime today) {
            var past = buckets.Where(b => !b.forecast).ToList();
            if (past.Count == 0) return (0, 0);
            DateTime from = past.First().start;
            var inPeriod = movements(companyId).Where(m => m.date >= from && m.date <= today).ToList();
            return (inPeriod.Sum(m => m.moneyIn), inPeriod.Sum(m => m.moneyOut));
        }

        public static Runway runway(string companyId, DateTime today) {
            var all = movements(companyId);
            var r = new Runway {
                balance = all.Where(m => m.date <= today).Sum(m => m.moneyIn - m.moneyOut),
                owedToYou = Db.forCompany("invoice", companyId).Where(i => Db.str(i, "status") == Invoicing.Sent).Sum(i => Db.num(i, "total")),
            };
            long out90 = all.Where(m => m.date > today.AddDays(-90) && m.date <= today).Sum(m => m.moneyOut);
            r.monthlyOut = out90 / 3;
            if (r.monthlyOut > 0) r.months = Math.Max(0, (decimal)r.balance / r.monthlyOut);
            return r;
        }
    }
}
