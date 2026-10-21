using com.swecial.code;
using com.swecial.server;
using System.Linq;
using System.Text;

namespace com.swecial.web {
    /// <summary>/liquid/ : key figures for the company and the latest vouchers.</summary>
    public class WebDashboard : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            title("Översikt");
            string today = Dates.today();
            string year = today.Substring(0, 4);
            var balances = Reports.trialBalance(companyId, year + "-01-01", year + "-12-31").ToDictionary(b => b.account);
            long balance(string account) => balances.TryGetValue(account, out var b) ? b.closing : 0;
            long result = -balances.Values.Where(b => !Bas.isBalanceAccount(b.account)).Sum(b => b.closing);

            var invoices = Db.forCompany("invoice", companyId);
            var unpaid = invoices.Where(i => Db.str(i, "status") == Invoicing.Sent).ToList();
            var overdue = unpaid.Where(i => string.CompareOrdinal(Db.str(i, "dueDate"), today) < 0).ToList();
            int drafts = invoices.Count(i => Db.str(i, "status") == Invoicing.Draft);

            var sb = new StringBuilder("<section class='panel'><h1>" + h(Db.str(company, "name")) + "</h1><div class='tiles'>");
            tile(sb, "Företagskonto (1930)", Money.format(balance(Bas.Bank)) + " kr", "");
            tile(sb, "Obetalda fakturor", Money.format(unpaid.Sum(i => Db.num(i, "total"))) + " kr", unpaid.Count + " st" + (overdue.Count > 0 ? ", " + overdue.Count + " förfallna" : ""));
            tile(sb, "Resultat " + year, Money.format(result) + " kr", "intäkter − kostnader");
            tile(sb, "Utkast", drafts.ToString(), "fakturor som inte skickats");
            sb.Append("</div></section>");

            var latest = Ledger.vouchers(companyId);
            latest.Reverse();
            sb.Append("<section class='panel'><div class='heading'><h2>Senaste verifikationer</h2><a href='" + BasePath + "/vouchers.html'>Alla</a></div>");
            if (latest.Count == 0) {
                sb.Append("<p class='empty'>Inget bokfört än. Börja med en <a href='" + BasePath + "/invoices.html'>faktura</a> eller en <a href='" + BasePath + "/voucher.html'>verifikation</a>.</p>");
            } else {
                sb.Append("<table><tbody>");
                foreach (var v in latest.Take(8)) {
                    sb.Append("<tr><td><a href='" + BasePath + "/voucher.html?id=" + u(v.id) + "'>" + h(Ledger.label(v)) + "</a></td><td>" + h(Db.str(v, "date")) + "</td>");
                    sb.Append("<td>" + h(Db.str(v, "text")) + "</td>" + amountCell(Db.num(v, "total")) + "</tr>");
                }
                sb.Append("</tbody></table>");
            }
            sb.Append("</section>");
            render(sb.ToString());
        }

        static void tile(StringBuilder sb, string label, string value, string note) {
            sb.Append("<div class='tile'><small>" + h(label) + "</small><strong>" + h(value) + "</strong>" + (note.Length > 0 ? "<span>" + h(note) + "</span>" : "") + "</div>");
        }
    }
}
