using com.swecial.code;
using com.swecial.server;
using System.Linq;
using System.Text;

namespace com.swecial.web {
    /// <summary>/liquid/vouchers.html : the voucher journal (verifikationslista), newest first.</summary>
    public class WebVouchers : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            title("Verifikationer");
            var vouchers = Ledger.vouchers(companyId);
            vouchers.Reverse();

            var sb = new StringBuilder();
            sb.Append("<section class='panel'><div class='heading'><h1>Verifikationer</h1>");
            sb.Append("<a class='button' href='" + BasePath + "/voucher.html'>Ny verifikation</a></div>");
            if (vouchers.Count == 0) {
                sb.Append("<p class='empty'>Inga verifikationer än.</p>");
            } else {
                sb.Append("<table><thead><tr><th>Ver</th><th>Datum</th><th>Text</th><th class='num'>Belopp</th></tr></thead><tbody>");
                foreach (var v in vouchers) {
                    bool reversed = Db.str(v, "reversedById").Length > 0;
                    sb.Append("<tr" + (reversed ? " class='reversed'" : "") + "><td><a href='" + BasePath + "/voucher.html?id=" + u(v.id) + "'>" + h(Ledger.label(v)) + "</a></td>");
                    sb.Append("<td>" + h(Db.str(v, "date")) + "</td><td>" + h(Db.str(v, "text")) + (reversed ? " <span class='status'>Makulerad</span>" : "") + "</td>");
                    sb.Append(amountCell(Db.num(v, "total")) + "</tr>");
                }
                sb.Append("</tbody></table>");
            }
            sb.Append("</section>");
            render(sb.ToString());
        }
    }
}
