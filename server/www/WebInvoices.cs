using com.swecial.code;
using com.swecial.server;
using System.Linq;
using System.Text;

namespace com.swecial.web {
    /// <summary>/liquid/invoices.html : all invoices, drafts first. A POST creates a new draft.</summary>
    public class WebInvoices : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            title("Fakturor");
            if (isPost) {
                var invoice = Invoicing.createDraft(company);
                redirect(BasePath + "/invoice.html?id=" + u(invoice.id));
                return;
            }

            var invoices = Db.forCompany("invoice", companyId)
                .OrderBy(i => Db.str(i, "status") == Invoicing.Draft ? 0 : 1)
                .ThenByDescending(i => Db.num(i, "number"))
                .ThenByDescending(i => i.createdAt)
                .ToList();

            var sb = new StringBuilder();
            sb.Append(heading("Fakturor", "Det du har fakturerat dina stamgäster", "<form method='post'><button type='submit'>Ny faktura</button></form>"));
            sb.Append("<section class='panel'>");
            if (invoices.Count == 0) {
                sb.Append("<p class='empty'>Inga fakturor än.</p>");
            } else {
                sb.Append("<table><thead><tr><th>Nr</th><th>Kund</th><th>Fakturadatum</th><th>Förfaller</th><th>Status</th><th class='num'>Belopp</th></tr></thead><tbody>");
                foreach (var i in invoices) {
                    string status = Db.str(i, "status");
                    long number = Db.num(i, "number");
                    var customer = Db.getOwned("customer", Db.str(i, "customerId"), companyId);
                    string customerName = status == Invoicing.Draft ? Db.str(customer, "name") : Db.str(i, "customerName");
                    bool overdue = status == Invoicing.Sent && string.CompareOrdinal(Db.str(i, "dueDate"), Dates.today()) < 0;
                    sb.Append("<tr><td><a href='" + BasePath + "/invoice.html?id=" + u(i.id) + "'>" + (number > 0 ? number.ToString() : "–") + "</a></td>");
                    sb.Append("<td>" + h(customerName) + "</td><td>" + h(Db.str(i, "invoiceDate")) + "</td><td>" + h(Db.str(i, "dueDate")) + "</td>");
                    sb.Append("<td><span class='status " + h(status) + (overdue ? " overdue" : "") + "'>" + (overdue ? "Förfallen" : Invoicing.statusName(status)) + "</span></td>");
                    sb.Append(amountCell(Db.num(i, "total")) + "</tr>");
                }
                sb.Append("</tbody></table>");
            }
            sb.Append("</section>");
            render(sb.ToString());
        }
    }
}
