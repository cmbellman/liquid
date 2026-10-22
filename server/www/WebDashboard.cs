using com.swecial.code;
using com.swecial.server;
using System;
using System.Linq;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/ : Baren. Kranen (money in and out over time, ?period=), In/Ut for the period, the
    /// gauge (how long the money lasts), Notan (unpaid invoices, ticked off when paid) and the
    /// Pantpåsen drop zone. POST action=paid ticks off an invoice; action=bag puts receipts in the bag.
    /// </summary>
    public class WebDashboard : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            title(Tr("bar.title"));
            if (isPost) {
                if (form("action") == "paid") {
                    var invoice = Db.getOwned("invoice", form("id"), companyId) ?? throw new LiquidException("Fakturan finns inte.", "INVOICE NOT FOUND.");
                    Invoicing.markPaid(invoice, form("paidDate"), account.id, form("incomeWhenPaid") == "1");
                    redirect(BasePath + "/?ok=paid");
                    return;
                }
                if (form("action") == "bag") {
                    WebPant.bag(req, companyId, account.id);
                    redirect(BasePath + "/pant.html?ok=bagged");
                    return;
                }
            }
            setValues();
        }

        protected override void renderAfterError() {
            setValues();
        }

        void setValues() {
            DateTime today = DateTime.Today;
            string period = query("period");
            if (period != Flow.Quarter && period != Flow.Year) period = Flow.TwelveMonths;
            var buckets = Flow.buckets(companyId, period, today, english ? "en" : "sv");
            var (moneyIn, moneyOut) = Flow.totals(buckets, companyId, today);

            set("chipQuarter", period == Flow.Quarter ? "on" : "");
            set("chip12m", period == Flow.TwelveMonths ? "on" : "");
            set("chipYear", period == Flow.Year ? "on" : "");
            set("chart", FlowChart.svg(buckets, Tr("bar.chart.label"), Tr("bar.chart.now"), Tr("bar.chart.expected"), Tr("bar.chart.in"), Tr("bar.chart.out"), Tr("common.currency")));
            set("moneyIn", Money.formatWhole(moneyIn));
            set("moneyOut", Money.formatWhole(moneyOut));
            set("periodText", T("bar.amount.period", T("bar.period." + period)));
            setGauge(Flow.runway(companyId, today));
            setTab(today);
            setTodo();

            int bag = Expenses.inbox(companyId).Count;
            set("bagAny", bag > 0);
            set("bagWaiting", T("pant.waiting", bag));
            set("bagAction", BasePath + "/");
            set("today", Dates.today());
        }

        /// <summary>The gauge: how long the money lasts, drawn as the theme's gauge (beer glass, tank …).</summary>
        void setGauge(Runway r) {
            string headline, note;
            bool low = false;
            if (r.balance <= 0) {
                headline = Tr("bar.gauge.empty");
                note = Tr("bar.gauge.balance", Money.format(r.balance));
                low = true;
            } else if (r.months == null) {
                headline = Tr("bar.gauge.full");
                note = Tr("bar.gauge.nothingOut");
            } else {
                decimal m = r.months.Value;
                headline = m >= 24 ? Tr("bar.gauge.years")
                    : m >= 1 ? Tr("bar.gauge.months", Money.formatDecimal(Math.Round(m, 1)))
                    : Tr("bar.gauge.days", Math.Max(1, (int)Math.Round(m * 30)));
                note = Tr("bar.gauge.pace", Money.formatWhole(r.balance), Money.formatWhole(r.monthlyOut));
                low = m < 1;
            }
            if (r.owedToYou > 0) note += " " + Tr("bar.gauge.owed", Money.formatWhole(r.owedToYou));
            set("gauge", FlowChart.gauge(r, Themes.setting(theme, t => t.gauge) ?? "beer"));
            set("gaugeClass", low ? "low" : "");
            val("gaugeHeadline", headline);
            val("gaugeNote", note);
        }

        /// <summary>Notan: sent invoices not paid yet, each ticked off with the day the money came in.</summary>
        void setTab(DateTime today) {
            var unpaid = Db.forCompany("invoice", companyId)
                .Where(i => Db.str(i, "status") == Invoicing.Sent)
                .OrderBy(i => Db.str(i, "dueDate"))
                .ToList();
            rows("tab", unpaid.Select(i => {
                int days = (int)(Dates.parse(Db.str(i, "dueDate")) - today).TotalDays;
                string due = days < 0 ? Tn("bar.tab.overdue", -days) : days == 0 ? T("bar.tab.dueToday") : Tn("bar.tab.dueIn", days);
                return row(("id", u(i.id)), ("customer", h(Db.str(i, "customerName"))), ("amount", Money.format(Db.num(i, "total"))),
                    ("number", T("bar.tab.invoice", Db.num(i, "number"))), ("due", due), ("late", days < 0 ? "late" : ""),
                    ("mayTakeUp", Invoicing.overYearEnd(Db.str(i, "invoiceDate"), Dates.today()) && Invoicing.mayTakeUpWhenPaid(i)),
                    ("takeUpLabel", T("invoice.takeUp.label", Db.str(i, "invoiceDate").Substring(0, 4))));
            }));
            set("tabTotal", Money.format(unpaid.Sum(i => Db.num(i, "total"))));
        }

        void setTodo() {
            int drafts = Db.forCompany("invoice", companyId).Count(i => Db.str(i, "status") == Invoicing.Draft);
            int missingReceipts = Expenses.list(companyId).Count(e => Db.str(e, "receiptFileId").Length == 0);
            int bag = Expenses.inbox(companyId).Count;
            var items = new System.Collections.Generic.List<SwecialObject>();
            if (bag > 0) items.Add(row(("href", BasePath + "/pant.html"), ("link", Tn("bar.todo.bag", bag)), ("rest", T("bar.todo.bagRest"))));
            if (drafts > 0) items.Add(row(("href", BasePath + "/invoices.html"), ("link", Tn("bar.todo.drafts", drafts)), ("rest", T("bar.todo.draftsRest"))));
            if (missingReceipts > 0) items.Add(row(("href", BasePath + "/expenses.html"), ("link", Tn("bar.todo.receipts", missingReceipts)), ("rest", T("bar.todo.receiptsRest"))));
            // Figures from laws and Skatteverket that haven't been checked for this year
            int rules = Rules.notChecked().Count;
            if (rules > 0) items.Add(row(("href", BasePath + "/rules.html"), ("link", Tn("bar.todo.rules", rules)), ("rest", T("bar.todo.rulesRest"))));
            rows("todo", items);
        }
    }
}
