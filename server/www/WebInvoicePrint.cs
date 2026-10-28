using com.swecial.code;
using com.swecial.server;
using System.Collections.Generic;
using System.Linq;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/invoice-print.html?id= : the invoice as a printable A4 page.
    /// Reflects the company's chosen invoice layout (nordic, executive, studio) and colour accent,
    /// while remaining a neutral Swedish legal document for the customer.
    /// </summary>
    public class WebInvoicePrint : LiquidPage {
        public override void processPage() {
            set("found", false);
            if (!requireCompany()) return;
            resp.header.add("Cache-Control: no-cache, no-store, must-revalidate, max-age=0");
            resp.header.add("Pragma: no-cache");
            resp.header.add("Expires: 0");
            var invoice = Db.getOwned("invoice", query("id"), companyId);
            if (invoice == null) {
                redirect(BasePath + "/invoices.html");
                return;
            }
            bool isDraft = Db.str(invoice, "status") == Invoicing.Draft || Db.num(invoice, "number") == 0;
            long number = Db.num(invoice, "number");
            title(isDraft ? "Fakturautkast" : "Faktura " + number);
            var customer = Db.linked(invoice, "customer", "customer");
            var lines = Invoicing.rows(invoice);
            var totals = Invoicing.totals(lines);

            string style = query("style").Length > 0 ? Settings.cleanStyle(query("style")) : Settings.invoiceStyle(company);
            string accent = query("accent").Length > 0 ? Settings.cleanAccent(query("accent")) : Settings.invoiceAccent(company);
            string activeTheme = query("theme").Length > 0 ? query("theme").ToLowerInvariant() : (theme?.id ?? Themes.Default);

            set("found", true);
            set("invoiceStyle", h(style));
            set("invoiceAccent", h(accent));
            set("invoiceTheme", h(activeTheme));
            set("isStudio", style == Settings.StyleStudio);
            set("isNordic", style == Settings.StyleNordic);
            set("isExecutive", style == Settings.StyleExecutive);
            set("docTitle", isDraft ? "Fakturautkast" : "Faktura");
            set("number", isDraft ? "Utkast" : number.ToString());

            string compName = Db.str(company, "name");
            val("companyName", compName);
            val("monogram", InvoicePdf.monogram(compName));
            set("companyAddress", spans(company, "address", "postalAddress"));
            val("invoiceDate", Db.str(invoice, "invoiceDate"));
            val("dueDate", Db.str(invoice, "dueDate"));
            set("hasOurReference", Db.str(invoice, "ourReference").Length > 0);
            val("ourReference", Db.str(invoice, "ourReference"));
            string yourRef = Db.str(invoice, "yourReference");
            if (yourRef.Length == 0 && customer != null) yourRef = Db.str(customer, "reference");
            set("hasYourReference", yourRef.Length > 0);
            val("yourReference", yourRef);
            string poNumber = Db.str(invoice, "poNumber");
            if (poNumber.Length == 0 && customer != null) poNumber = Db.str(customer, "orderRef");
            set("hasPoNumber", poNumber.Length > 0);
            val("poNumber", poNumber);
            string poLabel = Db.str(invoice, "poLabel");
            if (poLabel.Length == 0 && customer != null) poLabel = Db.str(customer, "orderRefLabel");
            if (poLabel.Length == 0) poLabel = "PO#";
            val("poLabel", poLabel);
            string bankgiro = Db.str(company, "bankgiro");
            set("hasBankgiro", bankgiro.Length > 0);
            val("bankgiro", bankgiro.Length > 0 ? bankgiro : "—");
            val("customerName", Db.str(invoice, "customerName"));
            set("customerAddress", spans(customer, "address", "postalAddress"));
            string customerOrg = customer != null ? Db.str(customer, "orgNumber") : "";
            set("hasCustomerOrg", customerOrg.Length > 0);
            val("customerOrg", customerOrg);

            bool isHourly = Db.str(invoice, "isHourly") == "1";
            set("qtyHeader", isHourly ? "Timmar" : "Antal");
            set("priceHeader", isHourly ? "Timarvode" : "À-pris");

            rows("lines", lines.Select(l => row(("text", h(l.text)), ("qty", Money.formatDecimal(l.quantity)), ("price", Money.formatOrBlank(l.unitPrice)),
                ("vat", l.vatRate.ToString()), ("net", Money.formatOrBlank(l.net)))));
            set("net", Money.formatOrBlank(totals.net));
            rows("vatLines", totals.byRate.Where(r => r.Key > 0).Reverse().Select(r => row(
                ("rate", r.Key.ToString()),
                ("base", Money.format(r.Value.net)),
                ("vat", Money.formatOrBlank(r.Value.vat)))));
            set("total", Money.format(totals.total));

            // Required wording when no Swedish VAT is charged (mervärdesskattelagen 17 kap.)
            string treatment = Db.str(invoice, "vatTreatment");
            bool goods = Invoicing.isGoods(invoice);
            string vatNote = treatment == Invoicing.EuReverseCharge
                ? (goods ? "Unionsintern leverans, undantagen från skatteplikt (artikel 138 i mervärdesskattedirektivet). Intra-Community supply, exempt (Article 138 VAT Directive)."
                    : "Omvänd betalningsskyldighet (artikel 196 i mervärdesskattedirektivet). Reverse charge (Article 196 VAT Directive).")
                : treatment == Invoicing.Export
                ? (goods ? "Export, undantagen från skatteplikt. Export, exempt from VAT." : "Tjänsten är inte skattepliktig i Sverige. Service not subject to Swedish VAT.")
                : "";
            set("hasVatNote", vatNote.Length > 0);
            val("vatNote", vatNote);
            string customerVat = Db.str(invoice, "customerVatNumber").Length > 0 ? Db.str(invoice, "customerVatNumber") : Db.str(customer, "vatNumber");
            set("hasCustomerVat", customerVat.Length > 0);
            val("customerVat", customerVat);
            set("hasNote", Db.str(invoice, "note").Length > 0);
            val("note", Db.str(invoice, "note"));
            set("paid", Db.str(invoice, "status") == Invoicing.Paid);
            val("paidDate", Db.str(invoice, "paidDate"));

            var col1 = new List<string>();
            if (compName.Length > 0) col1.Add(compName);
            if (Db.str(company, "address").Length > 0) col1.Add(Db.str(company, "address"));
            if (Db.str(company, "postalAddress").Length > 0) col1.Add(Db.str(company, "postalAddress"));

            var col2 = new List<string>();
            if (Db.str(company, "email").Length > 0) col2.Add(Db.str(company, "email"));
            if (Db.str(company, "phone").Length > 0) col2.Add(Db.str(company, "phone"));

            var col3 = new List<string>();
            if (Db.str(company, "orgNumber").Length > 0) col3.Add("Org.nr: " + Db.str(company, "orgNumber"));
            if (Db.str(company, "vatNumber").Length > 0) col3.Add("Momsreg.nr: " + Db.str(company, "vatNumber"));
            if (Db.str(company, "fskatt") != "0") col3.Add("Godkänd för F-skatt");

            var col4 = new List<string>();
            if (bankgiro.Length > 0) col4.Add(bankgiro);

            var footerCols = new List<(string label, List<string> items)> {
                ("Adress", col1),
                ("Kontakt", col2),
                ("Företagsuppgifter", col3),
                ("Bankgiro", col4)
            }.Where(c => c.items.Count > 0).ToList();

            rows("footer", footerCols.Select(c => row(
                ("label", h(c.label)),
                ("value", string.Concat(c.items.Select(x => "<span>" + h(x) + "</span>")))
            )));
        }

        static string spans(SwecialObject o, params string[] keys) {
            return string.Concat(keys.Where(k => Db.str(o, k).Length > 0).Select(k => "<span>" + h(Db.str(o, k)) + "</span>"));
        }
    }
}
