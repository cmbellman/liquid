using com.swecial.code;
using com.swecial.server;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/invoice-pdf.html?id= : generates and downloads the sent invoice as a PDF file.
    /// Only sent invoices have one. &amp;inline=1 displays it in-browser instead of triggering a download.
    /// </summary>
    public class WebInvoicePdf : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            var invoice = Db.getOwned("invoice", query("id"), companyId);
            if (invoice == null || Db.str(invoice, "status") == Invoicing.Draft) {
                redirect(BasePath + "/invoices.html");
                return;
            }
            long number = Db.num(invoice, "number");
            var customer = Db.getOwned("customer", Db.str(invoice, "customerId"), companyId);
            var lines = Invoicing.rows(invoice);
            var totals = Invoicing.totals(lines);

            byte[] pdf = InvoicePdf.generate(company, invoice, customer, lines, totals);

            string disposition = query("inline") == "1" ? "inline" : "attachment";
            resp.header.add("Content-Type: application/pdf");
            resp.header.add("Content-Disposition: " + disposition + "; filename=\"faktura-" + number + ".pdf\"");
            resp.header.add("Cache-Control: private, max-age=86400");
            resp.header.add("X-Content-Type-Options: nosniff");
            resp.binaryData = pdf;
        }
    }
}
