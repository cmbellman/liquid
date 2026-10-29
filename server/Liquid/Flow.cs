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
        public long accumulated;
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

        public const string Quarter = "kvartal";
        public const string Year = "ar";
        public const string TwelveMonths = "12m";
        public const string FiveYears = "5ar";

        /// <summary>
        /// Money in (debit) and out (credit) on 1930 per voucher date, without reversal pairs. saving
        /// marks money moved to or from funds (series V): it isn't spent, so Glaset's pace and chart out leave it out.
        /// liquidNet is the net change in liquid assets (1930 Bank + 1350 Securities).
        /// </summary>
        static List<(DateTime date, long moneyIn, long moneyOut, bool saving, long liquidNet)> movements(string companyId) {
            var result = new List<(DateTime, long, long, bool, long)>();
            foreach (var voucher in Ledger.vouchers(companyId)) {
                if (Db.linkedId(voucher, "reversedBy").Length > 0 || Db.linkedId(voucher, "reverses").Length > 0) continue;
                long moneyIn = 0, moneyOut = 0;
                long secNet = 0;
                foreach (var row in Ledger.rows(voucher)) {
                    if (row.account == Bas.Bank) {
                        moneyIn += row.debit;
                        moneyOut += row.credit;
                    } else if (row.account == Bas.Securities) {
                        secNet += row.debit - row.credit;
                    }
                }
                long bankNet = moneyIn - moneyOut;
                if (moneyIn != 0 || moneyOut != 0 || secNet != 0) {
                    result.Add((Dates.parse(Db.str(voucher, "date")), moneyIn, moneyOut, Db.str(voucher, "series") == Ledger.Securities, bankNet + secNet));
                }
            }
            return result;
        }

        /// <summary>
        /// Operating income (3000–3999) and operating expenses excluding depreciation and tax
        /// (4000–7699, 7900–7999) per voucher date, without reversal pairs, for the EBITDA chart.
        /// </summary>
        static List<(DateTime date, long income, long costs)> ebitdaMovements(string companyId) {
            var result = new List<(DateTime, long, long)>();
            foreach (var voucher in Ledger.vouchers(companyId)) {
                if (Db.linkedId(voucher, "reversedBy").Length > 0 || Db.linkedId(voucher, "reverses").Length > 0) continue;
                long income = 0, costs = 0;
                foreach (var row in Ledger.rows(voucher)) {
                    if (row.account.Length != 4 || !int.TryParse(row.account, out int n)) continue;
                    if (n >= 3000 && n <= 3999) {
                        income += row.credit - row.debit;
                    } else if ((n >= 4000 && n <= 7699) || (n >= 7900 && n <= 7999)) {
                        costs += row.debit - row.credit;
                    }
                }
                if (income != 0 || costs != 0) {
                    result.Add((Dates.parse(Db.str(voucher, "date")), income, costs));
                }
            }
            return result;
        }

        static List<FlowBucket> createBuckets(string period, DateTime today, string language, DateTime? earliest) {
            var culture = CultureInfo.GetCultureInfo(language == "en" ? "en-GB" : "sv-SE");
            var buckets = new List<FlowBucket>();
            if (period == Quarter) {
                DateTime monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
                for (int i = -12; i <= 4; i++) {
                    DateTime start = monday.AddDays(7 * i);
                    buckets.Add(new FlowBucket { start = start, end = start.AddDays(7), label = (language == "en" ? "W" : "v") + ISOWeek.GetWeekOfYear(start) });
                }
            } else if (period == FiveYears) {
                DateTime fiveYearsAgo = new DateTime(today.Year - 5, today.Month, 1);
                DateTime startMonth = fiveYearsAgo;
                if (earliest.HasValue) {
                    DateTime earliestYearStart = new DateTime(earliest.Value.Year, 1, 1);
                    if (earliestYearStart > fiveYearsAgo) startMonth = earliestYearStart;
                }
                int totalMonths = (today.Year - startMonth.Year) * 12 + (today.Month - startMonth.Month) + 4;
                for (int i = 0; i < totalMonths; i++) {
                    DateTime start = startMonth.AddMonths(i);
                    buckets.Add(new FlowBucket { start = start, end = start.AddMonths(1), label = start.ToString("MMM yyyy", culture).TrimEnd('.') });
                }
            } else {
                DateTime month = new DateTime(today.Year, today.Month, 1);
                int from = period == Year ? 1 - today.Month : -11;
                int to = period == Year ? 12 - today.Month : 3;
                for (int i = from; i <= to; i++) {
                    DateTime start = month.AddMonths(i);
                    buckets.Add(new FlowBucket { start = start, end = start.AddMonths(1), label = start.ToString("MMM", culture).TrimEnd('.') });
                }
            }
            foreach (var b in buckets) b.forecast = b.start > today;
            return buckets;
        }

        /// <summary>
        /// Buckets for a period: "kvartal" is weeks from 12 weeks back to 4 ahead, "ar" the months of
        /// this year, "12m" (default) months from 11 back to 3 ahead, and "5ar" months up to 5 years back
        /// (or all available months since start). Future buckets are a forecast of the unpaid invoices
        /// by due date; overdue ones are expected in the current bucket.
        /// </summary>
        public static List<FlowBucket> buckets(string companyId, string period, DateTime today, string language = "sv") {
            var all = movements(companyId);
            var buckets = createBuckets(period, today, language, all.Count > 0 ? all.Min(m => m.date) : (DateTime?)null);

            foreach (var (date, moneyIn, moneyOut, saving, _) in all) {
                var b = buckets.FirstOrDefault(x => date >= x.start && date < x.end);
                if (b == null || b.forecast) continue;
                if (!saving) {
                    b.moneyIn += moneyIn;
                    b.moneyOut += moneyOut;
                }
            }

            foreach (var b in buckets) {
                if (!b.forecast) {
                    DateTime cutoff = b.end <= today ? b.end : today.AddDays(1);
                    b.accumulated = all.Where(m => m.date < cutoff).Sum(m => m.liquidNet);
                }
            }

            var current = buckets.FirstOrDefault(x => today >= x.start && today < x.end);
            foreach (var invoice in Db.forCompany("invoice", companyId).Where(i => Db.str(i, "status") == Invoicing.Sent)) {
                DateTime due = Dates.parse(Db.str(invoice, "dueDate"));
                var b = due <= today ? current : buckets.FirstOrDefault(x => due >= x.start && due < x.end);
                if (b != null && (b.forecast || b == current)) b.moneyIn += Db.num(invoice, "total");
            }
            foreach (var pe in BankEvents.pending(companyId)) {
                DateTime d = Dates.parse(Db.str(pe, "date"));
                var kind = BankEvents.find(Db.str(pe, "kind"));
                var b = d <= today ? current : buckets.FirstOrDefault(x => d >= x.start && d < x.end);
                if (b != null && (b.forecast || b == current)) {
                    if (kind?.incoming == true) b.moneyIn += Db.num(pe, "amount");
                    else b.moneyOut += Db.num(pe, "amount");
                }
            }

            long runningAcc = current != null ? current.accumulated : 0;
            long currentPaidIn = all.Where(m => current != null && m.date >= current.start && m.date <= today && !m.saving).Sum(m => m.moneyIn);
            long currentPendingIn = current != null ? Math.Max(0, current.moneyIn - currentPaidIn) : 0;
            runningAcc += currentPendingIn;
            foreach (var b in buckets.Where(x => x.forecast)) {
                runningAcc += b.moneyIn - b.moneyOut;
                b.accumulated = runningAcc;
            }
            return buckets;
        }

        /// <summary>
        /// EBITDA buckets for the period: operating income (moneyIn), operating expenses excluding
        /// depreciation and tax (moneyOut), and rolling 12-month EBITDA (accumulated).
        /// Sent invoices not yet booked (cash method) are forecast on their due date exkl. VAT.
        /// </summary>
        public static List<FlowBucket> ebitdaBuckets(string companyId, string period, DateTime today, string language = "sv") {
            var all = ebitdaMovements(companyId);
            var cashAll = movements(companyId);
            DateTime? earliest = all.Count > 0 ? all.Min(m => m.date) : (cashAll.Count > 0 ? cashAll.Min(m => m.date) : (DateTime?)null);
            var buckets = createBuckets(period, today, language, earliest);

            foreach (var (date, income, costs) in all) {
                if (date > today) continue;
                var b = buckets.FirstOrDefault(x => date >= x.start && date < x.end);
                if (b == null || b.forecast) continue;
                b.moneyIn += income;
                b.moneyOut += costs;
            }

            foreach (var b in buckets) {
                if (!b.forecast) {
                    DateTime cutoff = b.end <= today ? b.end : today.AddDays(1);
                    DateTime windowStart = period == Quarter ? b.end.AddYears(-1) : b.end.AddMonths(-12);
                    b.accumulated = all.Where(m => m.date >= windowStart && m.date < cutoff).Sum(m => m.income - m.costs);
                }
            }

            var current = buckets.FirstOrDefault(x => today >= x.start && today < x.end);
            long currentBookedIn = current != null ? current.moneyIn : 0;
            foreach (var invoice in Db.forCompany("invoice", companyId).Where(i => Db.str(i, "status") == Invoicing.Sent && !Invoicing.bookedAsReceivable(i))) {
                DateTime due = Dates.parse(Db.str(invoice, "dueDate"));
                var b = due <= today ? current : buckets.FirstOrDefault(x => due >= x.start && due < x.end);
                if (b != null && (b.forecast || b == current)) {
                    b.moneyIn += Invoicing.totals(Invoicing.rows(invoice)).net;
                }
            }

            long runningAcc = current != null ? current.accumulated : 0;
            long currentPendingIn = current != null ? Math.Max(0, current.moneyIn - currentBookedIn) : 0;
            runningAcc += currentPendingIn;
            foreach (var b in buckets.Where(x => x.forecast)) {
                runningAcc += b.moneyIn - b.moneyOut;
                b.accumulated = runningAcc;
            }
            return buckets;
        }

        /// <summary>Sums of what actually happened in a period (no forecast), excluding fund transfers.</summary>
        public static (long moneyIn, long moneyOut) totals(List<FlowBucket> buckets, string companyId, DateTime today) {
            var past = buckets.Where(b => !b.forecast).ToList();
            if (past.Count == 0) return (0, 0);
            DateTime from = past.First().start;
            var inPeriod = movements(companyId).Where(m => m.date >= from && m.date <= today && !m.saving).ToList();
            return (inPeriod.Sum(m => m.moneyIn), inPeriod.Sum(m => m.moneyOut));
        }

        /// <summary>Sums of booked operating income and operating expenses (excl. depreciation and tax) in the period.</summary>
        public static (long income, long costs) ebitdaTotals(List<FlowBucket> buckets, string companyId, DateTime today) {
            var past = buckets.Where(b => !b.forecast).ToList();
            if (past.Count == 0) return (0, 0);
            DateTime from = past.First().start;
            var inPeriod = ebitdaMovements(companyId).Where(m => m.date >= from && m.date <= today).ToList();
            return (inPeriod.Sum(m => m.income), inPeriod.Sum(m => m.costs));
        }

        public static Runway runway(string companyId, DateTime today) {
            var all = movements(companyId);
            var r = new Runway {
                balance = all.Where(m => m.date <= today).Sum(m => m.moneyIn - m.moneyOut),
                owedToYou = Db.forCompany("invoice", companyId).Where(i => Db.str(i, "status") == Invoicing.Sent).Sum(i => Db.num(i, "total")),
            };
            long out90 = all.Where(m => !m.saving && m.date > today.AddDays(-90) && m.date <= today).Sum(m => m.moneyOut);
            r.monthlyOut = out90 / 3;
            if (r.monthlyOut > 0) r.months = Math.Max(0, (decimal)r.balance / r.monthlyOut);
            return r;
        }
    }
}
