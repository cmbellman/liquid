using com.swecial.code;
using com.swecial.server;
using System.Linq;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/reports.html?report=balance|ledger|vat&amp;from=&amp;to=&amp;account= : trial balance,
    /// general ledger for one account and the VAT return boxes. The period defaults to this year
    /// (the VAT return to the company's VAT period).
    /// </summary>
    public class WebReports : LiquidPage {
        string report, from, to, accountNumber;

        public override void processPage() {
            if (!requireCompany()) return;
            title(Tr("reports.title"));
            string year = Dates.today().Substring(0, 4);
            report = query("report");
            if (report != "ledger" && report != "vat" && report != "eu") report = "balance";
            var vatPeriod = Settings.vatPeriodOf(company, System.DateTime.Today);
            from = query("from").Length > 0 ? query("from") : report == "vat" || report == "eu" ? Dates.format(vatPeriod.from) : year + "-01-01";
            to = query("to").Length > 0 ? query("to") : report == "vat" || report == "eu" ? Dates.format(vatPeriod.to) : year + "-12-31";
            accountNumber = query("account");
            setFilter();

            from = Dates.clean(from);
            to = Dates.clean(to);
            if (string.CompareOrdinal(from, to) > 0) throw new LiquidException("Från-datum är efter till-datum.", "START DATE IS AFTER END DATE.");
            set("shown", report);
            if (report == "balance") setBalance();
            else if (report == "ledger") setLedger();
            else if (report == "eu") setEuSales();
            else setVat();
        }

        void setFilter() {
            set("shown", "");
            set("reportOptions", options(new[] { ("balance", Tr("reports.balance")), ("ledger", Tr("reports.ledger")), ("vat", Tr("reports.vat")), ("eu", Tr("reports.eu")) }, report));
            val("from", from);
            val("to", to);
            set("isLedger", report == "ledger");
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

        string ledgerLink(string account) {
            return BasePath + "/reports.html?report=ledger&amp;account=" + u(account) + "&amp;from=" + u(from) + "&amp;to=" + u(to);
        }
    }
}
