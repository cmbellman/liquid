using com.swecial.code;
using com.swecial.server;
using System.Linq;
using System.Text;

namespace com.swecial.web {
    /// <summary>/liquid/expenses.html : expenses, newest first, with this year's sum and missing receipts.</summary>
    public class WebExpenses : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            title("Utgifter");
            var expenses = Expenses.list(companyId);
            string year = Dates.today().Substring(0, 4);
            var thisYear = expenses.Where(e => Db.str(e, "date").StartsWith(year)).ToList();
            int missing = expenses.Count(e => Db.str(e, "receiptFileId").Length == 0);

            var sb = new StringBuilder("<section class='panel'><div class='heading'><h1>Utgifter</h1>");
            sb.Append("<a class='button' href='" + BasePath + "/expense.html'>Ny utgift</a></div>");
            if (expenses.Count == 0) {
                sb.Append("<p class='empty'>Inga utgifter än. Lägg till ett inköp som betalats från företagskontot, med kvittot som foto eller PDF.</p>");
            } else {
                sb.Append("<p class='lead'>" + Money.format(thisYear.Sum(e => Db.num(e, "total"))) + " kr i år, varav svensk moms " + Money.format(thisYear.Where(e => Db.str(e, "foreign").Length == 0).Sum(e => Db.num(e, "vat"))) + " kr.");
                if (missing > 0) sb.Append(" <span class='status overdue'>" + missing + " saknar kvitto</span>");
                sb.Append("</p>");
                sb.Append("<table><thead><tr><th>Datum</th><th>Leverantör</th><th>Vad</th><th class='num'>Belopp</th><th class='num'>Moms</th><th>Kvitto</th></tr></thead><tbody>");
                foreach (var e in expenses) {
                    string link = BasePath + "/expense.html?id=" + u(e.id);
                    string categoryName = Categories.find(Db.str(e, "category"))?.name ?? "";
                    bool hasReceipt = Db.str(e, "receiptFileId").Length > 0;
                    sb.Append("<tr><td>" + h(Db.str(e, "date")) + "</td><td><a href='" + link + "'>" + h(Db.str(e, "supplier")) + "</a></td>");
                    sb.Append("<td>" + h(categoryName) + (Db.str(e, "foreign").Length > 0 ? " <span class='status'>utlandet</span>" : "") + "</td>");
                    sb.Append(amountCell(Db.num(e, "total")) + amountCell(Db.num(e, "vat")));
                    sb.Append("<td>" + (hasReceipt ? "<a href='" + BasePath + "/receipt.html?id=" + u(e.id) + "&v=" + u(Db.str(e, "receiptFileId")) + "' target='_blank'>Visa</a>" : "<a class='missing' href='" + link + "'>Saknas</a>") + "</td></tr>");
                }
                sb.Append("</tbody></table>");
            }
            sb.Append("</section>");
            render(sb.ToString());
        }
    }
}
