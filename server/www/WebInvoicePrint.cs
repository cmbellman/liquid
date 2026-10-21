using com.swecial.code;
using com.swecial.server;
using System.Linq;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/invoice-print.html?id= : the invoice as a printable A4 page. The server has no PDF
    /// library, so the browser's print dialog makes the PDF. Only sent invoices have one. The
    /// invoice is a document for the customer, so it is Swedish and neutral whatever the theme.
    /// </summary>
    public class WebInvoicePrint : LiquidPage {
        public override void processPage() {
            set("found", false);
            if (!requireCompany()) return;
            var invoice = Db.getOwned("invoice", query("id"), companyId);
            if (invoice == null || Db.str(invoice, "status") == Invoicing.Draft) {
                redirect(BasePath + "/invoices.html");
                return;
            }
            long number = Db.num(invoice, "number");
            title("Faktura " + number);
            var customer = Db.getOwned("customer", Db.str(invoice, "customerId"), companyId);
            var lines = Invoicing.rows(invoice);
            var totals = Invoicing.totals(lines);

            set("found", true);
            set("number", number.ToString());
            val("companyName", Db.str(company, "name"));
            set("companyAddress", spans(company, "address", "postalAddress"));
            val("invoiceDate", Db.str(invoice, "invoiceDate"));
            val("dueDate", Db.str(invoice, "dueDate"));
            set("hasOurReference", Db.str(invoice, "ourReference").Length > 0);
            val("ourReference", Db.str(invoice, "ourReference"));
            set("hasYourReference", Db.str(customer, "reference").Length > 0);
            val("yourReference", Db.str(customer, "reference"));
            val("customerName", Db.str(invoice, "customerName"));
            set("customerAddress", spans(customer, "address", "postalAddress"));
            set("hasCustomerVat", Db.str(customer, "vatNumber").Length > 0);
            val("customerVat", Db.str(customer, "vatNumber"));

            rows("lines", lines.Select(l => row(("text", h(l.text)), ("qty", Money.formatDecimal(l.quantity)), ("price", Money.formatOrBlank(l.unitPrice)),
                ("vat", l.vatRate.ToString()), ("net", Money.formatOrBlank(l.net)))));
            set("net", Money.formatOrBlank(totals.net));
            rows("vatLines", totals.byRate.Where(r => r.Key > 0).Reverse().Select(r => row(("rate", r.Key.ToString()), ("vat", Money.formatOrBlank(r.Value.vat)))));
            set("total", Money.format(totals.total));
            set("hasNote", Db.str(invoice, "note").Length > 0);
            val("note", Db.str(invoice, "note"));
            set("paid", Db.str(invoice, "status") == Invoicing.Paid);
            val("paidDate", Db.str(invoice, "paidDate"));
            rows("footer", new[] { ("Bankgiro", "bankgiro"), ("Org.nr", "orgNumber"), ("Momsreg.nr", "vatNumber"), ("E-post", "email"), ("Telefon", "phone") }
                .Where(f => Db.str(company, f.Item2).Length > 0)
                .Select(f => row(("label", f.Item1), ("value", h(Db.str(company, f.Item2))))));
        }

        static string spans(SwecialObject o, params string[] keys) {
            return string.Concat(keys.Where(k => Db.str(o, k).Length > 0).Select(k => "<span>" + h(Db.str(o, k)) + "</span>"));
        }
    }
}
