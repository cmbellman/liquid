using com.swecial.code;
using com.swecial.server;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/invoice.html?id= : edits a draft (customer, dates, rows) and moves the invoice on.
    /// The form's action button decides what happens: save, send (number + book), delete the draft,
    /// or register the payment of a sent invoice.
    /// </summary>
    public class WebInvoice : LiquidPage {
        /// <summary>Empty rows shown under the filled ones; more appear after saving.</summary>
        const int BlankRows = 3;
        const int MaxRows = 200;

        SwecialObject invoice;

        public override void processPage() {
            if (!requireCompany()) return;
            invoice = Db.getOwned("invoice", isPost ? form("id") : query("id"), companyId);
            if (invoice == null) {
                redirect(BasePath + "/invoices.html");
                return;
            }
            long number = Db.num(invoice, "number");
            title(number > 0 ? "Faktura " + number : "Fakturautkast");

            if (isPost) {
                string action = form("action");
                if (action == "paid") {
                    Invoicing.markPaid(invoice, form("paidDate"), account.id);
                    redirect(here() + "&ok=paid");
                    return;
                }
                Invoicing.requireDraft(invoice);
                if (action == "delete") {
                    invoice.delete();
                    redirect(BasePath + "/invoices.html?ok=deleted");
                    return;
                }
                readForm();
                Db.commit(invoice);
                if (action == "send") {
                    Invoicing.send(invoice, account.id);
                    redirect(here() + "&ok=sent");
                    return;
                }
                redirect(here() + "&ok=saved");
                return;
            }
            renderPage();
        }

        protected override void renderAfterError() {
            renderPage();
        }

        string here() {
            return BasePath + "/invoice.html?id=" + u(invoice.id);
        }

        void readForm() {
            string customerId = form("customerId");
            if (customerId.Length > 0 && Db.getOwned("customer", customerId, companyId) == null) throw new LiquidException("Kunden finns inte.");
            invoice.set("customerId", customerId);
            invoice.set("invoiceDate", Dates.clean(form("invoiceDate")));
            invoice.set("dueDate", Dates.clean(form("dueDate")));
            invoice.set("ourReference", form("ourReference").Trim());
            invoice.set("note", form("note").Trim());

            var rows = new List<InvoiceRow>();
            for (int i = 0; i < rowCount(); i++) {
                string text = form("text" + i).Trim();
                string price = form("price" + i);
                if (text.Length == 0 && price.Trim().Length == 0) continue;
                int vat = int.TryParse(form("vat" + i), out int v) && Bas.VatRates.Contains(v) ? v : 25;
                decimal quantity = form("qty" + i).Trim().Length == 0 ? 1 : Money.parseDecimal(form("qty" + i), "antal");
                rows.Add(new InvoiceRow { text = text, quantity = quantity, unitPrice = Money.parse(price), vatRate = vat });
            }
            Invoicing.setRows(invoice, rows);
        }

        int rowCount() {
            return int.TryParse(form("rowCount"), out int n) ? System.Math.Clamp(n, 0, MaxRows) : 0;
        }

        void renderPage() {
            string status = Db.str(invoice, "status");
            bool draft = status == Invoicing.Draft;
            var rows = Invoicing.rows(invoice);
            var totals = Invoicing.totals(rows);
            long number = Db.num(invoice, "number");

            var sb = new StringBuilder();
            sb.Append("<section class='panel'><div class='heading'><h1>" + (number > 0 ? "Faktura " + number : "Fakturautkast") + "</h1>");
            sb.Append("<span class='status " + h(status) + "'>" + Invoicing.statusName(status) + "</span>");
            if (!draft) sb.Append("<a class='button secondary' href='" + BasePath + "/invoice-print.html?id=" + u(invoice.id) + "' target='_blank'>Skriv ut / PDF</a>");
            sb.Append("</div>");

            if (draft) {
                sb.Append(editForm(rows, totals));
            } else {
                sb.Append(summary(rows, totals));
            }
            sb.Append("</section>");
            render(sb.ToString());
        }

        string editForm(List<InvoiceRow> rows, InvoiceTotals totals) {
            var customers = Db.forCompany("customer", companyId).OrderBy(c => Db.str(c, "name"))
                .Select(c => (c.id, Db.str(c, "name"))).Prepend(("", "Välj kund")).ToList();
            string value(string key) => isPost ? form(key) : Db.str(invoice, key);

            var sb = new StringBuilder("<form method='post'>");
            sb.Append(input("id", invoice.id, "hidden"));
            sb.Append("<div class='grid'>");
            sb.Append(field("Kund", select("customerId", customers, value("customerId"))));
            sb.Append(field("Fakturadatum", input("invoiceDate", value("invoiceDate"), "date", "required")));
            sb.Append(field("Förfallodatum", input("dueDate", value("dueDate"), "date", "required")));
            sb.Append(field("Vår referens", input("ourReference", value("ourReference"))));
            sb.Append("</div>");
            if (customers.Count == 1) sb.Append("<p class='hint'>Lägg först upp en kund under <a href='" + BasePath + "/customers.html'>Kunder</a>.</p>");

            sb.Append("<table class='rows'><thead><tr><th>Beskrivning</th><th class='num'>Antal</th><th class='num'>À-pris</th><th>Moms</th><th class='num'>Belopp</th></tr></thead><tbody>");
            int count = isPost ? rowCount() : rows.Count + BlankRows;
            for (int i = 0; i < count; i++) {
                InvoiceRow row = !isPost && i < rows.Count ? rows[i] : null;
                string text = isPost ? form("text" + i) : row?.text ?? "";
                string qty = isPost ? form("qty" + i) : row != null ? Money.formatDecimal(row.quantity) : "";
                string price = isPost ? form("price" + i) : row != null ? Money.input(row.unitPrice) : "";
                string vat = isPost ? form("vat" + i) : (row?.vatRate ?? 25).ToString();
                sb.Append("<tr><td>" + input("text" + i, text) + "</td>");
                sb.Append("<td>" + input("qty" + i, qty, "text", "inputmode='decimal' class='num' placeholder='1'") + "</td>");
                sb.Append("<td>" + input("price" + i, price, "text", "inputmode='decimal' class='num'") + "</td>");
                sb.Append("<td>" + select("vat" + i, Bas.VatRates.Select(r => (r.ToString(), r + " %")), vat) + "</td>");
                sb.Append(row != null ? amountCell(row.net) : "<td></td>");
                sb.Append("</tr>");
            }
            sb.Append("</tbody>" + totalsFooter(totals) + "</table>");
            sb.Append(input("rowCount", count.ToString(), "hidden"));
            sb.Append(field("Meddelande på fakturan", "<textarea name='note' rows='2'>" + h(value("note")) + "</textarea>"));
            sb.Append("<div class='actions'>");
            sb.Append("<button type='submit' name='action' value='save' class='secondary'>Spara</button>");
            sb.Append("<button type='submit' name='action' value='send'>Spara och skicka</button>");
            sb.Append("<button type='submit' name='action' value='delete' class='danger' formnovalidate>Ta bort utkast</button>");
            sb.Append("</div><p class='hint'>När fakturan skickas får den nummer och bokförs (1510 mot försäljning och utgående moms). Därefter kan den inte ändras.</p>");
            sb.Append("</form>");
            return sb.ToString();
        }

        string summary(List<InvoiceRow> rows, InvoiceTotals totals) {
            var sb = new StringBuilder();
            sb.Append("<dl class='facts'>");
            sb.Append("<dt>Kund</dt><dd>" + h(Db.str(invoice, "customerName")) + "</dd>");
            sb.Append("<dt>Fakturadatum</dt><dd>" + h(Db.str(invoice, "invoiceDate")) + "</dd>");
            sb.Append("<dt>Förfallodatum</dt><dd>" + h(Db.str(invoice, "dueDate")) + "</dd>");
            if (Db.str(invoice, "paidDate").Length > 0) sb.Append("<dt>Betald</dt><dd>" + h(Db.str(invoice, "paidDate")) + "</dd>");
            sb.Append("<dt>Bokföring</dt><dd>" + voucherLink("voucherId"));
            if (Db.str(invoice, "paymentVoucherId").Length > 0) sb.Append(", " + voucherLink("paymentVoucherId"));
            sb.Append("</dd></dl>");

            sb.Append("<table class='rows'><thead><tr><th>Beskrivning</th><th class='num'>Antal</th><th class='num'>À-pris</th><th class='num'>Moms</th><th class='num'>Belopp</th></tr></thead><tbody>");
            foreach (var row in rows) {
                sb.Append("<tr><td>" + h(row.text) + "</td><td class='num'>" + Money.formatDecimal(row.quantity) + "</td>" + amountCell(row.unitPrice));
                sb.Append("<td class='num'>" + row.vatRate + " %</td>" + amountCell(row.net) + "</tr>");
            }
            sb.Append("</tbody>" + totalsFooter(totals) + "</table>");

            if (Db.str(invoice, "status") == Invoicing.Sent) {
                sb.Append("<form method='post' class='inline'>" + input("id", invoice.id, "hidden"));
                sb.Append(field("Betaldatum", input("paidDate", isPost ? form("paidDate") : Dates.today(), "date", "required")));
                sb.Append("<button type='submit' name='action' value='paid'>Registrera betalning</button></form>");
                sb.Append("<p class='hint'>Bokför hela beloppet " + Money.format(totals.total) + " kr som inbetalt till 1930.</p>");
            }
            return sb.ToString();
        }

        string voucherLink(string key) {
            var voucher = Db.getOwned("voucher", Db.str(invoice, key), companyId);
            return voucher == null ? "" : "<a href='" + BasePath + "/voucher.html?id=" + u(voucher.id) + "'>" + h(Ledger.label(voucher)) + "</a>";
        }

        static string totalsFooter(InvoiceTotals totals) {
            var sb = new StringBuilder("<tfoot>");
            sb.Append("<tr><td colspan='4'>Summa exkl. moms</td>" + amountCell(totals.net) + "</tr>");
            foreach (var rate in totals.byRate.Where(r => r.Key > 0).Reverse()) {
                sb.Append("<tr><td colspan='4'>Moms " + rate.Key + " % på " + Money.format(rate.Value.net) + "</td>" + amountCell(rate.Value.vat) + "</tr>");
            }
            sb.Append("<tr class='total'><td colspan='4'>Att betala</td><td class='num'>" + Money.format(totals.total) + "</td></tr>");
            sb.Append("</tfoot>");
            return sb.ToString();
        }
    }
}
