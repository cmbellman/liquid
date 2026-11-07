using com.swecial.code;
using com.swecial.server;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/invoice-pdf.html?id= : generates and downloads or previews the invoice as a PDF file.
    /// Supports both sent invoices and drafts (preview), plus ?sample=1 (&amp;style=&amp;accent=&amp;theme=) for previewing
    /// invoice templates from Inställningar. &amp;inline=1 displays it in-browser instead of downloading.
    /// </summary>
    public class WebInvoicePdf : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;

            string styleOverride = query("style").Length > 0 ? query("style") : null;
            string accentOverride = query("accent").Length > 0 ? query("accent") : null;
            string activeTheme = query("theme").Length > 0 ? query("theme") : (theme?.id ?? Themes.Default);

            if (query("sample") == "1") {
                byte[] samplePdf = InvoicePdf.generateSample(company, styleOverride, accentOverride, activeTheme);
                resp.header.add("Content-Type: application/pdf");
                resp.header.add("Content-Disposition: inline; filename=\"faktura-exempel.pdf\"");
                resp.header.add("Cache-Control: no-cache, no-store, must-revalidate, max-age=0");
                resp.header.add("Pragma: no-cache");
                resp.header.add("Expires: 0");
                resp.header.add("X-Content-Type-Options: nosniff");
                resp.binaryData = samplePdf;
                return;
            }

            var invoice = Db.getOwned("invoice", query("id"), companyId);
            if (invoice == null) {
                redirect(BasePath + "/invoices.html");
                return;
            }
            bool isDraft = Db.str(invoice, "status") == Invoicing.Draft || Db.num(invoice, "number") == 0;
            long number = Db.num(invoice, "number");
            string disposition = (query("inline") == "1" || isDraft) && query("download") != "1" ? "inline" : "attachment";
            string filename = isDraft ? "faktura-utkast.pdf" : "faktura-" + number + ".pdf";

            // A sent, unpaid invoice is served as the PDF saved when it was sent. A paid one is made
            // again from the kept details, so it carries the paid stamp.
            bool hasOverrides = styleOverride != null || accentOverride != null || query("theme").Length > 0;
            if (Db.str(invoice, "status") == Invoicing.Sent && !hasOverrides) {
                byte[] savedData = Invoicing.invoicePdfFile(invoice)?.data;
                if (savedData != null && savedData.Length > 0) {
                    resp.header.add("Content-Type: application/pdf");
                    resp.header.add("Content-Disposition: " + disposition + "; filename=\"" + filename + "\"");
                    resp.header.add("Cache-Control: no-cache, no-store, must-revalidate, max-age=0");
                    resp.header.add("Pragma: no-cache");
                    resp.header.add("Expires: 0");
                    resp.header.add("X-Content-Type-Options: nosniff");
                    resp.binaryData = savedData;
                    return;
                }
            }

            var customer = Db.linked(invoice, "customer", "customer");
            var lines = Invoicing.rows(invoice);
            var totals = Invoicing.totals(lines);

            // A sent invoice keeps the theme it was sent with (layout and colour: InvoicePdf.generate)
            if (query("theme").Length == 0 && !isDraft && Db.str(invoice, "invoiceTheme").Length > 0) activeTheme = Db.str(invoice, "invoiceTheme");

            byte[] pdf = InvoicePdf.generate(company, invoice, customer, lines, totals, styleOverride, accentOverride, activeTheme);

            resp.header.add("Content-Type: application/pdf");
            resp.header.add("Content-Disposition: " + disposition + "; filename=\"" + filename + "\"");
            resp.header.add("Cache-Control: no-cache, no-store, must-revalidate, max-age=0");
            resp.header.add("Pragma: no-cache");
            resp.header.add("Expires: 0");
            resp.header.add("X-Content-Type-Options: nosniff");
            resp.binaryData = pdf;
        }
    }
}
