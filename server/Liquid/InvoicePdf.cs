using com.swecial.server;
using PdfSharpCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace com.swecial.code {
    /// <summary>
    /// Generates a clean, neutral A4 invoice as a PDF document using PdfSharpCore.
    /// The invoice is a customer-facing legal document, always neutral Swedish layout.
    /// </summary>
    public static class InvoicePdf {
        static readonly XColor ColorText = XColor.FromArgb(0x1d, 0x23, 0x30);
        static readonly XColor ColorMuted = XColor.FromArgb(0x67, 0x70, 0x85);
        static readonly XColor ColorLine = XColor.FromArgb(0xe2, 0xe5, 0xeb);
        static readonly XColor ColorBrand = XColor.FromArgb(0x0b, 0x6e, 0x69);
        static readonly XColor ColorPaid = XColor.FromArgb(0x1d, 0x7a, 0x3a);

        public static byte[] generate(
            SwecialObject company,
            SwecialObject invoice,
            SwecialObject customer,
            List<InvoiceRow> lines,
            InvoiceTotals totals) {

            bool isDraft = Db.str(invoice, "status") == Invoicing.Draft || Db.num(invoice, "number") == 0;
            long number = Db.num(invoice, "number");

            var doc = new PdfDocument();
            doc.Info.Title = isDraft ? "Fakturautkast" : "Faktura " + number;
            doc.Info.Author = Db.str(company, "name");

            var page = doc.AddPage();
            page.Size = PageSize.A4;

            using (var gfx = XGraphics.FromPdfPage(page)) {
                renderInvoice(gfx, company, invoice, customer, lines, totals);
            }

            using (var ms = new MemoryStream()) {
                doc.Save(ms);
                return ms.ToArray();
            }
        }

        static void renderInvoice(
            XGraphics gfx,
            SwecialObject company,
            SwecialObject invoice,
            SwecialObject customer,
            List<InvoiceRow> lines,
            InvoiceTotals totals) {

            double left = 45;
            double right = 550;
            double width = right - left; // 505 pt

            var brushText = new XSolidBrush(ColorText);
            var brushMuted = new XSolidBrush(ColorMuted);
            var brushBrand = new XSolidBrush(ColorBrand);
            var brushPaid = new XSolidBrush(ColorPaid);

            var penLine = new XPen(ColorLine, 0.75);
            var penTotal = new XPen(ColorText, 1.25);

            var fontTitle = new XFont("Helvetica", 20, XFontStyle.Bold);
            var fontCompany = new XFont("Helvetica", 14, XFontStyle.Bold);
            var fontRegular = new XFont("Helvetica", 9.5, XFontStyle.Regular);
            var fontBold = new XFont("Helvetica", 9.5, XFontStyle.Bold);
            var fontHeader = new XFont("Helvetica", 8.5, XFontStyle.Bold);
            var fontSmall = new XFont("Helvetica", 8.5, XFontStyle.Regular);
            var fontTiny = new XFont("Helvetica", 8, XFontStyle.Regular);
            var fontTotal = new XFont("Helvetica", 11, XFontStyle.Bold);

            // 1. Header: Company info (left) & Invoice info (right)
            double yLeft = 50;
            string compName = Db.str(company, "name");
            gfx.DrawString(compName, fontCompany, brushText, new XPoint(left, yLeft));
            yLeft += 18;

            string compAddr = Db.str(company, "address");
            if (compAddr.Length > 0) {
                gfx.DrawString(compAddr, fontRegular, brushText, new XPoint(left, yLeft));
                yLeft += 14;
            }
            string compPostal = Db.str(company, "postalAddress");
            if (compPostal.Length > 0) {
                gfx.DrawString(compPostal, fontRegular, brushText, new XPoint(left, yLeft));
                yLeft += 14;
            }

            // Right header
            double yRight = 50;
            bool isDraft = Db.str(invoice, "status") == Invoicing.Draft || Db.num(invoice, "number") == 0;
            string docTitle = isDraft ? "FAKTURAUTKAST" : "FAKTURA";
            gfx.DrawString(docTitle, fontTitle, brushBrand, new XPoint(380, yRight));
            yRight += 22;

            string invDate = Db.str(invoice, "invoiceDate");
            if (invDate.Length == 0) invDate = Dates.today();
            string dueDate = Db.str(invoice, "dueDate");
            if (dueDate.Length == 0) {
                try {
                    dueDate = Dates.format(Dates.parse(invDate).AddDays(30));
                } catch {
                    dueDate = invDate;
                }
            }

            var meta = new List<(string label, string val)> {
                ("Fakturanummer", isDraft ? "Utkast" : Db.num(invoice, "number").ToString()),
                ("Fakturadatum", invDate),
                ("Förfallodatum", dueDate)
            };
            string ourRef = Db.str(invoice, "ourReference");
            if (ourRef.Length > 0) meta.Add(("Vår referens", ourRef));
            string yourRef = customer != null ? Db.str(customer, "reference") : "";
            if (yourRef.Length > 0) meta.Add(("Er referens", yourRef));

            foreach (var item in meta) {
                gfx.DrawString(item.label, fontRegular, brushMuted, new XPoint(380, yRight));
                gfx.DrawString(item.val, fontRegular, brushText, new XPoint(465, yRight));
                yRight += 14;
            }

            // 2. Customer section ("to")
            double y = Math.Max(yLeft, yRight) + 20;
            string custName = Db.str(invoice, "customerName");
            if (custName.Length == 0 && customer != null) custName = Db.str(customer, "name");
            if (custName.Length > 0) {
                gfx.DrawString(custName, fontBold, brushText, new XPoint(left, y));
                y += 14;
            }
            string custAddr = customer != null ? Db.str(customer, "address") : "";
            if (custAddr.Length > 0) {
                gfx.DrawString(custAddr, fontRegular, brushText, new XPoint(left, y));
                y += 14;
            }
            string custPostal = customer != null ? Db.str(customer, "postalAddress") : "";
            if (custPostal.Length > 0) {
                gfx.DrawString(custPostal, fontRegular, brushText, new XPoint(left, y));
                y += 14;
            }
            string custVat = Db.str(invoice, "customerVatNumber").Length > 0
                ? Db.str(invoice, "customerVatNumber")
                : (customer != null ? Db.str(customer, "vatNumber") : "");
            if (custVat.Length > 0) {
                gfx.DrawString("Momsreg.nr " + custVat, fontSmall, brushMuted, new XPoint(left, y));
                y += 14;
            }

            // 3. Lines Table
            y += 15;
            bool isHourly = Db.str(invoice, "isHourly") == "1";
            string qtyHeader = isHourly ? "Timmar" : "Antal";
            string priceHeader = isHourly ? "Timarvode" : "À-pris";

            // Column coordinates
            double colDescX = left;
            double colDescW = 245;
            double colQtyX = left + colDescW;
            double colQtyW = 55;
            double colPriceX = colQtyX + colQtyW;
            double colPriceW = 75;
            double colVatX = colPriceX + colPriceW;
            double colVatW = 50;
            double colNetX = colVatX + colVatW;
            double colNetW = 80;

            // Table header
            gfx.DrawLine(penLine, left, y - 6, right, y - 6);
            gfx.DrawString("Beskrivning", fontHeader, brushMuted, new XPoint(colDescX, y + 8));
            drawRight(gfx, qtyHeader, fontHeader, brushMuted, colQtyX + colQtyW, y + 8);
            drawRight(gfx, priceHeader, fontHeader, brushMuted, colPriceX + colPriceW, y + 8);
            drawRight(gfx, "Moms", fontHeader, brushMuted, colVatX + colVatW, y + 8);
            drawRight(gfx, "Belopp", fontHeader, brushMuted, colNetX + colNetW, y + 8);
            y += 18;
            gfx.DrawLine(penLine, left, y, right, y);
            y += 6;

            // Table rows
            foreach (var line in lines) {
                double rowY = y + 10;
                gfx.DrawString(line.text ?? "", fontRegular, brushText, new XPoint(colDescX, rowY));
                drawRight(gfx, Money.formatDecimal(line.quantity), fontRegular, brushText, colQtyX + colQtyW, rowY);
                drawRight(gfx, Money.formatOrBlank(line.unitPrice), fontRegular, brushText, colPriceX + colPriceW, rowY);
                drawRight(gfx, line.vatRate.ToString() + " %", fontRegular, brushText, colVatX + colVatW, rowY);
                drawRight(gfx, Money.formatOrBlank(line.net), fontRegular, brushText, colNetX + colNetW, rowY);

                y += 18;
                gfx.DrawLine(penLine, left, y, right, y);
            }

            // Totals
            y += 8;
            double totalsLabelX = colVatX + colVatW;
            double totalsValueX = colNetX + colNetW;

            drawRight(gfx, "Summa exkl. moms", fontRegular, brushMuted, totalsLabelX, y + 8);
            drawRight(gfx, Money.formatOrBlank(totals.net), fontRegular, brushText, totalsValueX, y + 8);
            y += 16;

            foreach (var r in totals.byRate.Where(r => r.Key > 0).Reverse()) {
                drawRight(gfx, "Moms " + r.Key + " %", fontRegular, brushMuted, totalsLabelX, y + 8);
                drawRight(gfx, Money.formatOrBlank(r.Value.vat), fontRegular, brushText, totalsValueX, y + 8);
                y += 16;
            }

            y += 2;
            gfx.DrawLine(penTotal, left, y, right, y);
            y += 4;
            drawRight(gfx, "Att betala (SEK)", fontTotal, brushText, totalsLabelX, y + 11);
            drawRight(gfx, Money.format(totals.total), fontTotal, brushText, totalsValueX, y + 11);
            y += 16;
            gfx.DrawLine(penTotal, left, y, right, y);
            y += 18;

            // 4. Notes
            string treatment = Db.str(invoice, "vatTreatment");
            bool goods = Invoicing.isGoods(invoice);
            string vatNote = treatment == Invoicing.EuReverseCharge
                ? (goods ? "Unionsintern leverans, undantagen från skatteplikt (artikel 138 i mervärdesskattedirektivet). Intra-Community supply, exempt (Article 138 VAT Directive)."
                    : "Omvänd betalningsskyldighet (artikel 196 i mervärdesskattedirektivet). Reverse charge (Article 196 VAT Directive).")
                : treatment == Invoicing.Export
                ? (goods ? "Export, undantagen från skatteplikt. Export, exempt from VAT." : "Tjänsten är inte skattepliktig i Sverige. Service not subject to Swedish VAT.")
                : "";

            if (vatNote.Length > 0) {
                y = drawTextWrap(gfx, vatNote, fontSmall, brushMuted, left, y, width, 12) + 10;
            }

            string note = Db.str(invoice, "note");
            if (note.Length > 0) {
                y = drawTextWrap(gfx, note, fontRegular, brushText, left, y, width, 14) + 12;
            }

            if (Db.str(invoice, "status") == Invoicing.Paid) {
                string paidDate = Db.str(invoice, "paidDate");
                string paidText = paidDate.Length > 0 ? "Betald " + paidDate : "Betald";
                gfx.DrawString(paidText, fontBold, brushPaid, new XPoint(left, y + 6));
                y += 18;
            }

            // 5. Footer (pinned at bottom)
            double footerY = 780;
            gfx.DrawLine(penLine, left, footerY, right, footerY);
            footerY += 8;

            var footer = new List<(string label, string val)>();
            if (Db.str(company, "fskatt") != "0") footer.Add(("F-skatt", "Godkänd för F-skatt"));
            if (Db.str(company, "orgNumber").Length > 0) footer.Add(("Org.nr", Db.str(company, "orgNumber")));
            if (Db.str(company, "vatNumber").Length > 0) footer.Add(("Momsreg.nr", Db.str(company, "vatNumber")));
            if (Db.str(company, "bankgiro").Length > 0) footer.Add(("Bankgiro", Db.str(company, "bankgiro")));
            if (Db.str(company, "email").Length > 0) footer.Add(("E-post", Db.str(company, "email")));
            if (Db.str(company, "phone").Length > 0) footer.Add(("Telefon", Db.str(company, "phone")));

            if (footer.Count > 0) {
                double colW = width / footer.Count;
                for (int i = 0; i < footer.Count; i++) {
                    double colX = left + i * colW;
                    gfx.DrawString(footer[i].label, fontTiny, brushMuted, new XPoint(colX, footerY + 6));
                    gfx.DrawString(footer[i].val, fontSmall, brushText, new XPoint(colX, footerY + 18));
                }
            }
        }

        static double drawTextWrap(XGraphics gfx, string text, XFont font, XBrush brush, double x, double y, double maxWidth, double lineHeight) {
            if (string.IsNullOrEmpty(text)) return y;
            var paragraphs = text.Replace("\r\n", "\n").Split('\n');
            foreach (var p in paragraphs) {
                var words = p.Split(' ');
                var line = "";
                foreach (var word in words) {
                    var candidate = line.Length == 0 ? word : line + " " + word;
                    if (gfx.MeasureString(candidate, font).Width > maxWidth && line.Length > 0) {
                        gfx.DrawString(line, font, brush, new XPoint(x, y));
                        y += lineHeight;
                        line = word;
                    } else {
                        line = candidate;
                    }
                }
                if (line.Length > 0) {
                    gfx.DrawString(line, font, brush, new XPoint(x, y));
                    y += lineHeight;
                }
            }
            return y;
        }

        static void drawRight(XGraphics gfx, string text, XFont font, XBrush brush, double rightX, double y) {
            if (string.IsNullOrEmpty(text)) return;
            var size = gfx.MeasureString(text, font);
            gfx.DrawString(text, font, brush, new XPoint(rightX - size.Width, y));
        }
    }
}
