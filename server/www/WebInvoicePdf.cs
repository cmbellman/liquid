using com.swecial.code;
using com.swecial.server;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/invoice-pdf.html?id= : generates and downloads or previews the invoice as a PDF file.
    /// Supports both sent invoices and drafts (preview). &amp;inline=1 displays it in-browser instead of triggering a download.
    /// </summary>
    public class WebInvoicePdf : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            var invoice = Db.getOwned("invoice", query("id"), companyId);
            if (invoice == null) {
                redirect(BasePath + "/invoices.html");
                return;
            }
            bool isDraft = Db.str(invoice, "status") == Invoicing.Draft || Db.num(invoice, "number") == 0;
            long number = Db.num(invoice, "number");
            var customer = Db.getOwned("customer", Db.str(invoice, "customerId"), companyId);
            var lines = Invoicing.rows(invoice);
            var totals = Invoicing.totals(lines);

            byte[] pdf = InvoicePdf.generate(company, invoice, customer, lines, totals);

            string disposition = (query("inline") == "1" || isDraft) && query("download") != "1" ? "inline" : "attachment";
            string filename = isDraft ? "faktura-utkast.pdf" : "faktura-" + number + ".pdf";
            resp.header.add("Content-Type: application/pdf");
            resp.header.add("Content-Disposition: " + disposition + "; filename=\"" + filename + "\"");
            resp.header.add("Cache-Control: private, max-age=86400");
            resp.header.add("X-Content-Type-Options: nosniff");
            resp.binaryData = pdf;
        }
    }
}
