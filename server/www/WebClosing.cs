using com.swecial.code;
using com.swecial.server;
using System;
using System.Collections.Generic;
using System.Linq;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/closing.html?year= : the year-end closing (Closing), one panel per step with what is
    /// left to book and a button that books it. Before the year has ended it is a preview (the tax so
    /// far). POST action=book&amp;step=, action=input&amp;step=&amp;amount= (the tax account, the allocation),
    /// action=carried (the deficit and fund losses from a year not closed in Liquid), action=close
    /// (with bank=1) and action=reopen.
    /// </summary>
    public class WebClosing : LiquidPage {
        int year;

        public override void processPage() {
            if (!requireCompany()) return;
            title(Tr("closing.title"));
            var years = Closing.years(companyId);
            string asked = isPost ? form("year") : query("year");
            year = int.TryParse(asked, out int y) && years.Contains(y) ? y : Closing.defaultYear(companyId);

            if (isPost) {
                string action = form("action"), ok = "booked";
                if (action == "book") {
                    Closing.book(company, year, form("step"), account.id);
                } else if (action == "input") {
                    Closing.setInput(company, year, form("step"), Money.parse(form("amount")));
                    ok = "saved";
                } else if (action == "carried") {
                    Closing.setCarriedIn(company, year, Money.parse(form("deficit")), Money.parse(form("capitalLoss")));
                    ok = "saved";
                } else if (action == "close") {
                    Closing.close(company, year, form("bank") == "1", account.id);
                    ok = "closed";
                } else if (action == "reopen") {
                    Closing.reopen(company, year);
                    ok = "reopened";
                }
                redirect(BasePath + "/closing.html?year=" + year + "&ok=" + ok + "#" + u(form("step").Length > 0 ? form("step") : action));
                return;
            }
            setValues(years);
        }

        protected override void renderAfterError() {
            setValues(Closing.years(companyId));
        }

        void setValues(List<int> years) {
            set("year", year.ToString());
            set("subtitle", T("closing.subtitle", year));
            set("yearOptions", options(Enumerable.Reverse(years).Select(y => (y.ToString(), y + (Closing.isClosed(companyId, y) ? " — " + Tr("closing.closedShort") : ""))), year.ToString()));

            var closing = Closing.find(companyId, year);
            bool closed = Closing.isClosed(companyId, year);
            bool ended = Closing.yearEnded(year);
            bool canBook = ended && !closed;
            set("closed", closed);
            set("preview", !ended);
            set("canBook", canBook);
            set("disabled", canBook ? "" : "disabled");
            set("closedText", T("closing.closedText", year, h(Db.str(closing, "closedAt"))));

            var steps = Closing.steps(company, year).ToDictionary(s => s.key);
            foreach (var s in steps.Values) {
                set(s.key + "Done", s.done);
                set(s.key + "State", s.done ? "paid" : "sent");
                set(s.key + "StateText", Tr(s.done ? "closing.state.done" : "closing.state.todo"));
            }
            var t = Closing.tax(company, year);
            setChecks(t);
            setReceivables();
            setVat();
            setTaxAccount(closing);
            setReserves(closing, t);
            setTax(t);
            set("resultAmount", Money.format(Statements.result(Statements.incomeStatement(companyId, Closing.start(year), Closing.end(year)))));
            set("resultLead", T("closing.result.lead", year));

            long bank = Statements.movements(companyId, "", Closing.end(year)).TryGetValue(Bas.Bank, out long b) ? b : 0;
            set("bankBalance", T("closing.lock.bank", Closing.end(year), Money.format(bank)));
            set("allDone", steps.Values.All(s => s.done) && !t.fundValueMissing);
        }

        /// <summary>Before the closing: things that change the books or the tax, worth sorting first.</summary>
        void setChecks(TaxCalculation t) {
            var items = new List<SwecialObject>();
            void add(string text, string href) => items.Add(row(("text", text), ("href", href)));
            int bag = Expenses.inbox(companyId).Count;
            if (bag > 0) add(Tn("closing.check.bag", bag), BasePath + "/pant.html");
            int drafts = Db.forCompany("invoice", companyId).Count(i => Db.str(i, "status") == Invoicing.Draft && Db.str(i, "invoiceDate").StartsWith(year + "-"));
            if (drafts > 0) add(Tn("closing.check.drafts", drafts), BasePath + "/invoices.html");
            if (t.fundValueMissing) add(T("closing.check.fundValue", year), BasePath + "/securities.html#yearStart");
            if (Closing.years(companyId).Contains(year - 1) && Ledger.vouchers(companyId).Any(v => Db.str(v, "date").StartsWith((year - 1) + "-")) && !Closing.isClosed(companyId, year - 1)) {
                add(T("closing.check.previous", year - 1), BasePath + "/closing.html?year=" + (year - 1));
            }
            foreach (var key in new[] { Rules.CorporateTax, Rules.TaxAllocationShare, Rules.GovernmentBondRate }) {
                if (!Rules.isChecked(key, Closing.end(year))) add(T("closing.check.rule", T("rules." + key)), BasePath + "/rules.html");
            }
            rows("checks", items);
        }

        void setReceivables() {
            bool cash = Settings.accountingMethod(company, year) == Settings.CashMethod;
            set("cashMethod", cash);
            var invoices = Closing.unpaidInvoices(company, year);
            rows("unpaid", invoices.Select(i => row(("id", u(i.id)), ("number", Db.num(i, "number").ToString()), ("customer", h(Db.str(i, "customerName"))),
                ("date", h(Db.str(i, "invoiceDate"))), ("total", Money.format(Db.num(i, "total"))))));
            set("receivablesLead", T(cash ? "closing.receivables.lead" : "closing.receivables.accrual", year));
        }

        void setVat() {
            var balances = Closing.vatBalances(companyId, year);
            rows("vatRows", balances.Select(b => row(("account", h(b.account)), ("name", h(accountName(b.account))), ("balance", Money.format(-b.balance)))));
            long net = -balances.Sum(b => b.balance);
            set("vatNet", T(net >= 0 ? "closing.vat.pay" : "closing.vat.refund", Money.format(Math.Abs(net))));
        }

        void setTaxAccount(SwecialObject closing) {
            set("preliminaryTax", Money.input(Db.num(closing, "preliminaryTax")));
            set("vatPaid", Money.input(Db.num(closing, "vatPaid")));
            set("preliminaryTaxBooked", T("closing.booked", Money.format(Db.num(closing, "preliminaryTaxBooked"))));
            set("vatPaidBooked", T("closing.booked", Money.format(Db.num(closing, "vatPaidBooked"))));
            long balance = Statements.movements(companyId, "", Closing.end(year)).TryGetValue(Closing.TaxAccount, out long b) ? b : 0;
            set("taxAccountBalance", T("closing.taxAccount.balance", Closing.end(year), Money.format(balance)));
        }

        void setReserves(SwecialObject closing, TaxCalculation t) {
            var toReverse = Closing.reservesToReverse(companyId, year);
            rows("reversals", toReverse.Select(r => row(("year", r.year.ToString()), ("amount", Money.format(r.amount)))));
            rows("reserves", Closing.reserves(companyId, Closing.end(year)).Select(r => row(("year", r.year.ToString()), ("amount", Money.format(r.amount)))));
            set("allocationMax", T("closing.allocation.max", Money.format(t.allocationMax), Rules.value(Rules.TaxAllocationShare, Closing.end(year))));
            set("allocation", Money.input(Db.num(closing, "allocation")));
            set("allocationBooked", T("closing.booked", Money.format(t.allocationBooked)));
            set("allocationSaves", T("closing.allocation.saves", Money.format(t.allocationMax * Rules.value(Rules.CorporateTax, Closing.end(year)) / 1000 / 100 * 100)));
        }

        void setTax(TaxCalculation t) {
            var lines = new List<SwecialObject>();
            void line(string key, long amount, string ink, bool always = false) {
                if (amount != 0 || always) lines.Add(row(("label", T("closing.tax." + key)), ("ink", ink), ("amount", Money.format(amount)), ("class", "")));
            }
            line("resultBeforeTax", t.resultBeforeTax, "", true);
            line("nonDeductible", t.nonDeductible, "4.3 c");
            line("taxFree", -t.taxFree, "4.5 c");
            line("reserveIncome", t.reserveIncome, "4.6 a");
            line("fundIncome", t.fundIncome, "4.6 b");
            line("reserveUplift", t.reserveUplift, "4.6 d");
            line("capitalLossAddedBack", t.capitalLossAddedBack, "4.7");
            line("capitalLossUsed", -t.capitalLossUsed, "4.7");
            line("deficitIn", -t.deficitIn, "4.14 a");
            lines.Add(row(("label", T(t.surplus >= 0 ? "closing.tax.surplus" : "closing.tax.deficit")), ("ink", t.surplus >= 0 ? "4.15" : "4.16"), ("amount", Money.format(t.surplus)), ("class", "total")));
            rows("taxLines", lines);
            set("fundValueMissing", t.fundValueMissing);
            set("fundValueText", T("closing.check.fundValue", year));
            set("taxable", Money.format(t.taxable));
            set("taxAmount", Money.format(t.tax));
            set("taxRate", Money.formatDecimal(Rules.value(Rules.CorporateTax, Closing.end(year)) / 10m));
            set("deficitOut", t.deficitOut > 0);
            set("deficitOutText", T("closing.tax.deficitOut", Money.format(t.deficitOut), year + 1));
            set("capitalLossOut", t.capitalLossOut > 0);
            set("capitalLossOutText", T("closing.tax.capitalLossOut", Money.format(t.capitalLossOut)));

            var carried = Closing.carriedIn(company, year);
            set("carriedEditable", !carried.fromClosing && !Closing.isClosed(companyId, year));
            set("carriedLead", T("closing.carried.lead", year - 1));
            set("deficitIn", Money.input(carried.deficit));
            set("capitalLossIn", Money.input(carried.capitalLoss));
        }
    }
}
