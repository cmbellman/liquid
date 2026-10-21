using com.swecial.code;
using com.swecial.server;
using System.Linq;
using System.Text;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/invoice-print.html?id= : the invoice as a printable A4 page. The server has no PDF
    /// library, so the browser's print dialog makes the PDF. Only sent invoices have one.
    /// </summary>
    public class WebInvoicePrint : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            var invoice = Db.getOwned("invoice", query("id"), companyId);
            if (invoice == null || Db.str(invoice, "status") == Invoicing.Draft) {
                redirect(BasePath + "/invoices.html");
                return;
            }
            long number = Db.num(invoice, "number");
            title("Faktura " + number);
            var customer = Db.getOwned("customer", Db.str(invoice, "customerId"), companyId);
            var rows = Invoicing.rows(invoice);
            var totals = Invoicing.totals(rows);

            var sb = new StringBuilder("<article class='invoice'>");
            sb.Append("<header><div><h1>" + h(Db.str(company, "name")) + "</h1>" + lines(company, "address", "postalAddress") + "</div>");
            sb.Append("<div class='doc'><h2>Faktura</h2><dl>");
            sb.Append("<dt>Fakturanummer</dt><dd>" + number + "</dd>");
            sb.Append("<dt>Fakturadatum</dt><dd>" + h(Db.str(invoice, "invoiceDate")) + "</dd>");
            sb.Append("<dt>Förfallodatum</dt><dd>" + h(Db.str(invoice, "dueDate")) + "</dd>");
            if (Db.str(invoice, "ourReference").Length > 0) sb.Append("<dt>Vår referens</dt><dd>" + h(Db.str(invoice, "ourReference")) + "</dd>");
            if (Db.str(customer, "reference").Length > 0) sb.Append("<dt>Er referens</dt><dd>" + h(Db.str(customer, "reference")) + "</dd>");
            sb.Append("</dl></div></header>");

            sb.Append("<section class='to'><strong>" + h(Db.str(invoice, "customerName")) + "</strong>" + lines(customer, "address", "postalAddress"));
            if (Db.str(customer, "vatNumber").Length > 0) sb.Append("<span>Momsreg.nr " + h(Db.str(customer, "vatNumber")) + "</span>");
            sb.Append("</section>");

            sb.Append("<table><thead><tr><th>Beskrivning</th><th class='num'>Antal</th><th class='num'>À-pris</th><th class='num'>Moms</th><th class='num'>Belopp</th></tr></thead><tbody>");
            foreach (var row in rows) {
                sb.Append("<tr><td>" + h(row.text) + "</td><td class='num'>" + Money.formatDecimal(row.quantity) + "</td>" + amountCell(row.unitPrice));
                sb.Append("<td class='num'>" + row.vatRate + " %</td>" + amountCell(row.net) + "</tr>");
            }
            sb.Append("</tbody><tfoot><tr><td colspan='4'>Summa exkl. moms</td>" + amountCell(totals.net) + "</tr>");
            foreach (var rate in totals.byRate.Reverse()) {
                if (rate.Key > 0) sb.Append("<tr><td colspan='4'>Moms " + rate.Key + " %</td>" + amountCell(rate.Value.vat) + "</tr>");
            }
            sb.Append("<tr class='total'><td colspan='4'>Att betala (SEK)</td><td class='num'>" + Money.format(totals.total) + "</td></tr></tfoot></table>");

            if (Db.str(invoice, "note").Length > 0) sb.Append("<p class='note'>" + h(Db.str(invoice, "note")) + "</p>");
            if (Db.str(invoice, "status") == Invoicing.Paid) sb.Append("<p class='paid'>Betald " + h(Db.str(invoice, "paidDate")) + "</p>");

            sb.Append("<footer>");
            footerItem(sb, "Bankgiro", Db.str(company, "bankgiro"));
            footerItem(sb, "Org.nr", Db.str(company, "orgNumber"));
            footerItem(sb, "Momsreg.nr", Db.str(company, "vatNumber"));
            footerItem(sb, "E-post", Db.str(company, "email"));
            footerItem(sb, "Telefon", Db.str(company, "phone"));
            sb.Append("</footer></article>");
            sb.Append("<p class='noprint'><button onclick='window.print()'>Skriv ut / spara som PDF</button></p>");
            render(sb.ToString());
        }

        static string lines(SwecialObject o, params string[] keys) {
            var sb = new StringBuilder();
            foreach (var key in keys) {
                if (Db.str(o, key).Length > 0) sb.Append("<span>" + h(Db.str(o, key)) + "</span>");
            }
            return sb.ToString();
        }

        static void footerItem(StringBuilder sb, string label, string value) {
            if (value.Length > 0) sb.Append("<div><small>" + label + "</small>" + h(value) + "</div>");
        }
    }
}
