using com.swecial.code;
using com.swecial.server;
using System.Collections.Generic;
using System.Linq;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/reports.html?report=balance|ledger|vat|eu|income|sheet&amp;from=&amp;to=&amp;account=&amp;accounts=1 :
    /// trial balance, general ledger for one account, the VAT return boxes, the EU sales list, the income
    /// statement and the balance sheet (K2, with the accounts under each item when accounts=1). The period
    /// defaults to this year (the VAT return to the company's VAT period, the balance sheet to today).
    /// </summary>
    public class WebReports : LiquidPage {
        string report, from, to, accountNumber;
        bool showAccounts;

        public override void processPage() {
            if (!requireCompany()) return;
            title(Tr("reports.title"));
            string year = Dates.today().Substring(0, 4);
            report = query("report");
            if (report != "ledger" && report != "vat" && report != "eu" && report != "income" && report != "sheet") report = "balance";
            var vatPeriod = Settings.vatPeriodOf(company, System.DateTime.Today);
            from = query("from").Length > 0 ? query("from") : report == "vat" || report == "eu" ? Dates.format(vatPeriod.from) : year + "-01-01";
            to = query("to").Length > 0 ? query("to") : report == "vat" || report == "eu" ? Dates.format(vatPeriod.to) : report == "sheet" ? Dates.today() : year + "-12-31";
            accountNumber = query("account");
            showAccounts = query("accounts") == "1";
            setFilter();

            from = Dates.clean(from);
            to = Dates.clean(to);
            if (report == "sheet") from = to.Substring(0, 4) + "-01-01";
            if (string.CompareOrdinal(from, to) > 0) throw new LiquidException("Från-datum är efter till-datum.", "START DATE IS AFTER END DATE.");
            set("shown", report);
            if (report == "balance") setBalance();
            else if (report == "ledger") setLedger();
            else if (report == "eu") setEuSales();
            else if (report == "income") setIncomeStatement();
            else if (report == "sheet") setBalanceSheet();
            else setVat();
        }

        void setFilter() {
            set("shown", "");
            set("reportOptions", options(new[] { ("balance", Tr("reports.balance")), ("ledger", Tr("reports.ledger")), ("vat", Tr("reports.vat")), ("eu", Tr("reports.eu")),
                ("income", Tr("reports.income")), ("sheet", Tr("reports.sheet")) }, report));
            val("from", from);
            val("to", to);
            set("isLedger", report == "ledger");
            set("isSheet", report == "sheet");
            set("isStatement", report == "income" || report == "sheet");
            set("accountsChecked", isChecked(showAccounts));
            set("accountOptions", options(accountChoices(), accountNumber));
        }

        void setBalance() {
            var balances = Reports.trialBalance(companyId, from, to);
            rows("balances", balances.Select(b => row(("account", h(b.account)), ("name", h(accountName(b.account))), ("ledgerHref", ledgerLink(b.account)),
                ("opening", Money.formatOrBlank(b.opening)), ("debit", Money.formatOrBlank(b.debit)), ("credit", Money.formatOrBlank(b.credit)), ("closing", Money.format(b.closing)))));
            set("sumDebit", Money.formatOrBlank(balances.Sum(b => b.debit)));
            set("sumCredit", Money.formatOrBlank(balances.Sum(b => b.credit)));
            set("sumClosing", Money.format(balances.Sum(b => b.closing)));
            set("result", Money.format(-balances.Where(b => !Bas.isBalanceAccount(b.account)).Sum(b => b.closing)));
        }

        void setLedger() {
            set("hasAccount", accountNumber.Length > 0);
            if (accountNumber.Length == 0) return;
            var lines = Reports.generalLedger(companyId, accountNumber, from, to, out long opening);
            val("account", accountNumber);
            val("accountName", accountName(accountNumber));
            set("opening", Money.format(opening));
            rows("ledgerLines", lines.Select(l => row(("id", u(l.voucher.id)), ("label", h(Ledger.label(l.voucher))), ("date", h(Db.str(l.voucher, "date"))),
                ("text", h(Db.str(l.voucher, "text"))), ("debit", Money.formatOrBlank(l.debit)), ("credit", Money.formatOrBlank(l.credit)), ("balance", Money.format(l.balance)))));
            set("sumDebit", Money.formatOrBlank(lines.Sum(l => l.debit)));
            set("sumCredit", Money.formatOrBlank(lines.Sum(l => l.credit)));
            set("closing", Money.format(lines.Count > 0 ? lines.Last().balance : opening));
        }

        void setVat() {
            rows("boxes", Reports.vatReturn(companyId, from, to).Select(b => row(("box", b.box), ("label", T("vat.box" + b.box)),
                ("amount", Money.format(b.amount)), ("class", b.box == "49" ? "total" : ""))));
        }

        /// <summary>The EU sales list (periodisk sammanställning) to file with Skatteverket.</summary>
        void setEuSales() {
            var list = Invoicing.euSalesList(companyId, from, to);
            set("euAny", list.Count > 0);
            rows("euRows", list.Select(r => row(("vatNumber", h(r.vatNumber)), ("customer", h(r.customer)),
                ("goods", Money.formatOrBlank(r.goods)), ("services", Money.formatOrBlank(r.services)))));
            set("euGoods", Money.formatOrBlank(list.Sum(r => r.goods)));
            set("euServices", Money.formatOrBlank(list.Sum(r => r.services)));
        }

        void setIncomeStatement() {
            set("periodLabel", h(from + " – " + to));
            set("previousLabel", h(Statements.yearEarlier(from) + " – " + Statements.yearEarlier(to)));
            setStatement(Statements.incomeStatement(companyId, from, to), from);
        }

        /// <summary>The balance sheet at the end of the "to" day; the accounts link to their ledger for that year.</summary>
        void setBalanceSheet() {
            set("periodLabel", h(to));
            set("previousLabel", h(Statements.yearEarlier(to)));
            var sheet = Statements.balanceSheet(companyId, to);
            long assets = sheet.First(r => r.key == "sheet.sumAssets").amount;
            long equityAndLiabilities = sheet.First(r => r.key == "sheet.sumEquityAndLiabilities").amount;
            set("unbalanced", assets != equityAndLiabilities);
            set("difference", Money.format(assets - equityAndLiabilities));
            setStatement(sheet, to.Substring(0, 4) + "-01-01");
        }

        /// <summary>
        /// The statement's rows, leaving out items that are zero both years and sections with no items
        /// left; parts and totals always show.
        /// </summary>
        void setStatement(List<StatementRow> statement, string ledgerFrom) {
            var shown = new List<StatementRow>();
            List<StatementRow> section = null;
            foreach (var r in statement) {
                bool empty = r.amount == 0 && r.previous == 0;
                if (r.kind == "heading") {
                    section = new List<StatementRow> { r };
                } else if (section != null) {
                    if (r.kind == "line" && empty) continue;
                    section.Add(r);
                    if (r.kind == "sum") {
                        if (section.Any(x => x.kind == "line")) shown.AddRange(section);
                        section = null;
                    }
                } else if (r.kind != "line" || !empty) {
                    shown.Add(r);
                }
            }
            rows("statement", shown.Select(r => row(("class", r.kind), ("label", T("statement." + r.key)),
                ("amount", r.kind == "part" || r.kind == "heading" ? "" : Money.format(r.amount)),
                ("previous", r.kind == "part" || r.kind == "heading" ? "" : Money.format(r.previous)),
                ("accounts", showAccounts ? r.accounts.Select(a => row(("account", h(a.account)), ("name", h(accountName(a.account))),
                    ("href", ledgerLink(a.account, ledgerFrom)), ("accountAmount", Money.format(a.amount)), ("accountPrevious", Money.format(a.previous)))).ToList()
                    : new List<SwecialObject>()))));
        }

        string ledgerLink(string account, string ledgerFrom = null) {
            return BasePath + "/reports.html?report=ledger&amp;account=" + u(account) + "&amp;from=" + u(ledgerFrom ?? from) + "&amp;to=" + u(to);
        }
    }
}
