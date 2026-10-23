using System;
using System.Collections.Generic;
using System.Linq;

namespace com.swecial.code {
    public class StatementAccount {
        public string account;
        public long amount;
        public long previous;
    }

    /// <summary>
    /// A row of a financial statement: "part" (Tillgångar, resets the running total), "heading" (starts a
    /// section), "line" (a K2 item with its accounts), "sum" (the section) or "total" (everything since
    /// the start or the latest part).
    /// </summary>
    public class StatementRow {
        public string kind;
        public string key;
        public long amount;
        public long previous;
        public List<StatementAccount> accounts = new List<StatementAccount>();
    }

    /// <summary>
    /// The income statement (resultaträkning, by nature of expense) and the balance sheet in the K2
    /// layout (ÅRL bilaga 2 and 3), from the BAS accounts, with the year before as comparison.
    /// Income and equity and liabilities are credit-positive, costs negative, assets debit-positive.
    /// Accounts 8990–8999 (the year's result moved to the balance sheet by the closing) are left out of
    /// the income statement. Equity's free part is worked out so the balance sheet always balances:
    /// the year's result is the income statement for the fiscal year (the calendar year) up to the date,
    /// and everything else on 2000–2079, 2090–2099 and in earlier years' results not yet moved there
    /// is the retained result.
    /// </summary>
    public static class Statements {
        class Item {
            public string kind, key;
            public (int from, int to)[] ranges;
            public Item(string kind, string key, params (int, int)[] ranges) { this.kind = kind; this.key = key; this.ranges = ranges; }
            public bool covers(int account) => ranges.Any(r => account >= r.from && account <= r.to);
        }

        static Item Part(string key) => new Item("part", key);
        static Item Heading(string key) => new Item("heading", key);
        static Item Line(string key, params (int, int)[] ranges) => new Item("line", key, ranges);
        static Item Sum(string key) => new Item("sum", key);
        static Item Total(string key) => new Item("total", key);

        public const string RetainedResult = "sheet.retained";
        public const string YearResult = "sheet.yearResult";

        static readonly Item[] IncomeLayout = {
            Heading("income.operatingIncome"),
            Line("income.netSales", (3000, 3799)),
            Line("income.inventoryChange", (4900, 4999)),
            Line("income.capitalisedWork", (3800, 3899)),
            Line("income.otherOperatingIncome", (3900, 3999)),
            Sum("income.sumOperatingIncome"),
            Heading("income.operatingExpenses"),
            Line("income.materials", (4000, 4899)),
            Line("income.otherExternal", (5000, 6999)),
            Line("income.personnel", (7000, 7699)),
            Line("income.depreciation", (7700, 7899)),
            Line("income.otherOperatingExpenses", (7900, 7999)),
            Sum("income.sumOperatingExpenses"),
            Total("income.operatingResult"),
            Heading("income.financial"),
            Line("income.groupCompanies", (8000, 8099)),
            Line("income.associatedCompanies", (8100, 8199)),
            Line("income.financialFixedAssets", (8200, 8299)),
            Line("income.interestIncome", (8300, 8399)),
            Line("income.interestExpenses", (8400, 8799)),
            Sum("income.sumFinancial"),
            Total("income.resultAfterFinancial"),
            Heading("income.appropriations"),
            Line("income.taxAllocationReserves", (8810, 8819)),
            Line("income.excessDepreciation", (8850, 8859)),
            Line("income.otherAppropriations", (8800, 8809), (8820, 8849), (8860, 8899)),
            Sum("income.sumAppropriations"),
            Total("income.resultBeforeTax"),
            Line("income.tax", (8900, 8989)),
            Total("income.yearResult"),
        };

        static readonly Item[] SheetLayout = {
            Part("sheet.assets"),
            Heading("sheet.fixedAssets"),
            Line("sheet.intangible", (1000, 1099)),
            Line("sheet.buildings", (1100, 1199)),
            Line("sheet.machinery", (1200, 1299)),
            Line("sheet.financialFixedAssets", (1300, 1399)),
            Sum("sheet.sumFixedAssets"),
            Heading("sheet.currentAssets"),
            Line("sheet.inventory", (1400, 1499)),
            Line("sheet.customerReceivables", (1500, 1599)),
            Line("sheet.otherReceivables", (1600, 1699)),
            Line("sheet.prepaid", (1700, 1799)),
            Line("sheet.shortInvestments", (1800, 1899)),
            Line("sheet.cash", (1900, 1999)),
            Sum("sheet.sumCurrentAssets"),
            Total("sheet.sumAssets"),
            Part("sheet.equityAndLiabilities"),
            Heading("sheet.equity"),
            Line("sheet.restrictedEquity", (2080, 2089)),
            Line(RetainedResult),
            Line(YearResult),
            Sum("sheet.sumEquity"),
            Heading("sheet.untaxedReserves"),
            Line("sheet.taxAllocationReserves", (2110, 2149)),
            Line("sheet.excessDepreciation", (2150, 2159)),
            Line("sheet.otherUntaxedReserves", (2100, 2109), (2160, 2199)),
            Sum("sheet.sumUntaxedReserves"),
            Heading("sheet.provisions"),
            Line("sheet.provisionsLine", (2200, 2299)),
            Sum("sheet.sumProvisions"),
            Heading("sheet.longTermLiabilities"),
            Line("sheet.longTermLine", (2300, 2399)),
            Sum("sheet.sumLongTermLiabilities"),
            Heading("sheet.currentLiabilities"),
            Line("sheet.suppliers", (2440, 2449)),
            Line("sheet.taxLiabilities", (2500, 2599)),
            Line("sheet.otherCurrentLiabilities", (2400, 2439), (2450, 2499), (2600, 2899)),
            Line("sheet.accrued", (2900, 2999)),
            Sum("sheet.sumCurrentLiabilities"),
            Total("sheet.sumEquityAndLiabilities"),
        };

        /// <summary>The income statement for the period, with the same period a year earlier.</summary>
        public static List<StatementRow> incomeStatement(string companyId, string from, string to) {
            var now = movements(companyId, from, to);
            var before = movements(companyId, yearEarlier(from), yearEarlier(to));
            return build(IncomeLayout, account => credit(now, account), account => credit(before, account), (key, previous) => 0);
        }

        /// <summary>The balance sheet at the end of the day, with the same day a year earlier.</summary>
        public static List<StatementRow> balanceSheet(string companyId, string date) {
            var now = SheetFigures.at(companyId, date);
            var before = SheetFigures.at(companyId, yearEarlier(date));
            return build(SheetLayout,
                account => account < 2000 ? -credit(now.balances, account) : credit(now.balances, account),
                account => account < 2000 ? -credit(before.balances, account) : credit(before.balances, account),
                (key, previous) => (previous ? before : now).computed(key));
        }

        /// <summary>The year's result for the period (Årets resultat on the income statement).</summary>
        public static long result(List<StatementRow> statement) => statement.Last(r => r.kind == "total").amount;

        class SheetFigures {
            public Dictionary<string, long> balances;
            long freeEquity, yearResult;

            public static SheetFigures at(string companyId, string date) {
                var all = movements(companyId, "", date);
                var year = movements(companyId, date.Substring(0, 4) + "-01-01", date);
                var f = new SheetFigures { balances = all };
                foreach (var (account, amount) in all) {
                    int n = number(account);
                    if ((n >= 2000 && n <= 2079) || (n >= 2090 && n <= 2099) || n >= 3000) f.freeEquity -= amount;
                }
                foreach (var (account, amount) in year) {
                    int n = number(account);
                    if (n >= 3000 && !(n >= 8990 && n <= 8999)) f.yearResult -= amount;
                }
                return f;
            }

            public long computed(string key) => key == YearResult ? yearResult : key == RetainedResult ? freeEquity - yearResult : 0;
        }

        static List<StatementRow> build(Item[] layout, Func<int, long> amount, Func<int, long> previous, Func<string, bool, long> computed) {
            var rows = new List<StatementRow>();
            StatementRow section = null, running = new StatementRow();
            foreach (var item in layout) {
                var row = new StatementRow { kind = item.kind, key = item.key };
                switch (item.kind) {
                    case "part":
                        running = new StatementRow();
                        break;
                    case "heading":
                        section = new StatementRow();
                        break;
                    case "line":
                        if (item.ranges.Length == 0) {
                            row.amount = computed(item.key, false);
                            row.previous = computed(item.key, true);
                        }
                        break;
                    case "sum":
                        row.amount = section.amount;
                        row.previous = section.previous;
                        section = null;
                        break;
                    case "total":
                        row.amount = running.amount;
                        row.previous = running.previous;
                        break;
                }
                rows.Add(row);
                if (item.kind != "line") continue;
                foreach (var account in AccountsCovered(item)) {
                    var a = new StatementAccount { account = account.ToString(), amount = amount(account), previous = previous(account) };
                    if (a.amount == 0 && a.previous == 0) continue;
                    row.accounts.Add(a);
                    row.amount += a.amount;
                    row.previous += a.previous;
                }
                if (section != null) { section.amount += row.amount; section.previous += row.previous; }
                running.amount += row.amount;
                running.previous += row.previous;
            }
            return rows;
        }

        static IEnumerable<int> AccountsCovered(Item item) {
            foreach (var (from, to) in item.ranges) {
                for (int n = from; n <= to; n++) yield return n;
            }
        }

        /// <summary>Debit minus credit per account for the vouchers dated in the period ("" = from the start).</summary>
        static Dictionary<string, long> movements(string companyId, string from, string to) {
            var result = new Dictionary<string, long>();
            foreach (var voucher in Ledger.vouchers(companyId)) {
                string date = Db.str(voucher, "date");
                if (string.CompareOrdinal(date, from) < 0 || string.CompareOrdinal(date, to) > 0) continue;
                foreach (var row in Ledger.rows(voucher)) {
                    result.TryGetValue(row.account, out long sum);
                    result[row.account] = sum + row.debit - row.credit;
                }
            }
            return result;
        }

        static long credit(Dictionary<string, long> movements, int account) {
            return movements.TryGetValue(account.ToString(), out long amount) ? -amount : 0;
        }

        static int number(string account) => account.Length == 4 && int.TryParse(account, out int n) ? n : 0;

        public static string yearEarlier(string date) => Dates.format(Dates.parse(date).AddYears(-1));
    }
}
