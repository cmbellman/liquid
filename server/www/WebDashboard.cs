using com.swecial.code;
using com.swecial.server;
using System;
using System.Linq;
using System.Text;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/ : Baren. Kranen (money in and out over time, ?period=), In/Ut for the period, Glaset
    /// (how long the money lasts), Notan (unpaid invoices, ticked off when paid) and Pantpåsen (throw
    /// receipts in, sort later). POST action=paid ticks off an invoice; action=bag puts receipts in the bag.
    /// </summary>
    public class WebDashboard : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            title("Baren");
            if (isPost) {
                if (form("action") == "paid") {
                    var invoice = Db.getOwned("invoice", form("id"), companyId) ?? throw new LiquidException("Fakturan finns inte.");
                    Invoicing.markPaid(invoice, form("paidDate"), account.id);
                    redirect(BasePath + "/?ok=paid");
                    return;
                }
                if (form("action") == "bag") {
                    WebPant.bag(req, companyId, account.id);
                    redirect(BasePath + "/pant.html?ok=bagged");
                    return;
                }
            }
            renderPage();
        }

        protected override void renderAfterError() {
            renderPage();
        }

        void renderPage() {
            DateTime today = DateTime.Today;
            string period = query("period");
            if (period != Flow.Quarter && period != Flow.Year) period = Flow.TwelveMonths;
            var buckets = Flow.buckets(companyId, period, today);
            var (moneyIn, moneyOut) = Flow.totals(buckets, companyId, today);
            var runway = Flow.runway(companyId, today);
            string periodText = period == Flow.Quarter ? "senaste 12 veckorna" : period == Flow.Year ? "i år" : "senaste 12 månaderna";

            var sb = new StringBuilder();
            sb.Append(heading(Db.str(company, "name"), "Baren: allt viktigt på ett ställe"));
            sb.Append("<div class='bar-grid'><div>");

            // Kranen
            sb.Append("<section class='panel'><div class='tap-head'><div><h2>Kranen</h2><p class='sub'>Pengar in och ut på företagskontot</p></div>");
            sb.Append("<div class='chips'>" + chip(Flow.Quarter, "Kvartal", period) + chip(Flow.TwelveMonths, "12 mån", period) + chip(Flow.Year, "I år", period) + "</div></div>");
            sb.Append(FlowChart.svg(buckets));
            sb.Append("<div class='legend'><span><i class='li'></i>In</span><span><i class='lo'></i>Ut</span><span><i class='lf'></i>Väntas in (obetalda fakturor)</span></div>");
            sb.Append("<div class='inout'>");
            sb.Append("<div class='big in'><small>In</small><strong>" + Money.formatWhole(moneyIn) + "</strong><span>kr " + periodText + "</span></div>");
            sb.Append("<div class='big out'><small>Ut</small><strong>" + Money.formatWhole(moneyOut) + "</strong><span>kr " + periodText + "</span></div>");
            sb.Append(glass(runway));
            sb.Append("</div></section>");

            sb.Append(todo());
            sb.Append("</div><div>");
            sb.Append(notan(today));
            sb.Append(WebPant.dropZone(Expenses.inbox(companyId).Count, BasePath + "/", true));
            sb.Append("</div></div>");
            sb.Append(WebPant.DropScript);
            render(sb.ToString());
        }

        static string chip(string key, string text, string current) {
            return "<a href='" + BasePath + "/?period=" + key + "'" + (key == current ? " class='on'" : "") + ">" + text + "</a>";
        }

        static string glass(Runway r) {
            string headline, note;
            bool low = false;
            if (r.balance <= 0) {
                headline = "Glaset är tomt";
                note = "Saldot är " + Money.format(r.balance) + " kr.";
                low = true;
            } else if (r.months == null) {
                headline = "Fullt glas";
                note = "Inget har gått ut de senaste tre månaderna.";
            } else {
                decimal m = r.months.Value;
                headline = m >= 24 ? "Räcker i över två år" : m >= 1 ? "Räcker i ca " + Money.formatDecimal(Math.Round(m, 1)) + " mån" : "Räcker i ca " + Math.Max(1, (int)Math.Round(m * 30)) + " dagar";
                note = Money.formatWhole(r.balance) + " kr på kontot, ut ca " + Money.formatWhole(r.monthlyOut) + " kr/mån.";
                low = m < 1;
            }
            if (r.owedToYou > 0) note += " Plus " + Money.formatWhole(r.owedToYou) + " kr på notan.";
            return "<div class='glass" + (low ? " low" : "") + "' title='Saldot delat med vad som gått ut per månad de senaste tre månaderna'>" + FlowChart.glass(r)
                + "<div><small>Glaset</small><b>" + h(headline) + "</b><small>" + h(note) + "</small></div></div>";
        }

        /// <summary>Notan: sent invoices not paid yet, each ticked off with the day the money came in.</summary>
        string notan(DateTime today) {
            var unpaid = Db.forCompany("invoice", companyId)
                .Where(i => Db.str(i, "status") == Invoicing.Sent)
                .OrderBy(i => Db.str(i, "dueDate"))
                .ToList();
            var sb = new StringBuilder("<section class='panel'><div class='heading'><div><h2>Notan</h2><p class='sub'>De är skyldiga dig</p></div></div>");
            if (unpaid.Count == 0) {
                sb.Append("<p class='empty'>Ingen står på notan. <a href='" + BasePath + "/invoices.html'>Skriv en faktura</a>.</p>");
            } else {
                sb.Append("<div class='tab-list'>");
                foreach (var i in unpaid) {
                    DateTime due = Dates.parse(Db.str(i, "dueDate"));
                    int days = (int)(due - today).TotalDays;
                    string dueText = days < 0 ? "Förföll för " + (-days) + (days == -1 ? " dag" : " dagar") + " sen"
                        : days == 0 ? "Förfaller i dag" : "Förfaller om " + days + (days == 1 ? " dag" : " dagar");
                    sb.Append("<div class='tab'>");
                    sb.Append("<div class='who'><a href='" + BasePath + "/invoice.html?id=" + u(i.id) + "'>" + h(Db.str(i, "customerName")) + "</a></div>");
                    sb.Append("<div class='amount'>" + Money.format(Db.num(i, "total")) + " kr</div>");
                    sb.Append("<div class='due" + (days < 0 ? " late" : "") + "'>Faktura " + Db.num(i, "number") + " · " + dueText + "</div>");
                    sb.Append("<details><summary><span class='chip-btn'>Pricka av</span></summary>");
                    sb.Append("<form method='post'>" + input("id", i.id, "hidden") + input("action", "paid", "hidden"));
                    sb.Append(field("Betald den", input("paidDate", Dates.today(), "date", "required")));
                    sb.Append("<button type='submit'>Betald</button></form></details>");
                    sb.Append("</div>");
                }
                sb.Append("</div><div class='tab-total'><span>Totalt på notan</span><b>" + Money.format(unpaid.Sum(i => Db.num(i, "total"))) + " kr</b></div>");
            }
            sb.Append("</section>");
            return sb.ToString();
        }

        string todo() {
            var invoices = Db.forCompany("invoice", companyId);
            int drafts = invoices.Count(i => Db.str(i, "status") == Invoicing.Draft);
            int missingReceipts = Expenses.list(companyId).Count(e => Db.str(e, "receiptFileId").Length == 0);
            int bag = Expenses.inbox(companyId).Count;
            var items = new StringBuilder();
            if (bag > 0) items.Append("<li><a href='" + BasePath + "/pant.html'>" + bag + (bag == 1 ? " kvitto" : " kvitton") + " i pantpåsen</a> väntar på att sorteras.</li>");
            if (drafts > 0) items.Append("<li><a href='" + BasePath + "/invoices.html'>" + drafts + " fakturautkast</a> har inte skickats.</li>");
            if (missingReceipts > 0) items.Append("<li><a href='" + BasePath + "/expenses.html'>" + missingReceipts + (missingReceipts == 1 ? " utgift saknar" : " utgifter saknar") + " kvitto.</a> Kvittot ska sparas i sju år.</li>");
            if (items.Length == 0) return "";
            return "<section class='panel'><h2>Att göra</h2><ul class='todo'>" + items + "</ul></section>";
        }
    }
}
