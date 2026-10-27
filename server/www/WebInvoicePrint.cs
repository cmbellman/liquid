using com.swecial.code;
using com.swecial.server;
using System.Collections.Generic;
using System.Linq;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/invoice-print.html?id= : the invoice as a printable A4 page. Only sent invoices have one.
    /// The invoice is a document for the customer, so it is Swedish and neutral whatever the theme.
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

            bool isHourly = Db.str(invoice, "isHourly") == "1";
            set("qtyHeader", isHourly ? "Timmar" : "Antal");
            set("priceHeader", isHourly ? "Timarvode" : "À-pris");

            rows("lines", lines.Select(l => row(("text", h(l.text)), ("qty", Money.formatDecimal(l.quantity)), ("price", Money.formatOrBlank(l.unitPrice)),
                ("vat", l.vatRate.ToString()), ("net", Money.formatOrBlank(l.net)))));
            set("net", Money.formatOrBlank(totals.net));
            rows("vatLines", totals.byRate.Where(r => r.Key > 0).Reverse().Select(r => row(("rate", r.Key.ToString()), ("vat", Money.formatOrBlank(r.Value.vat)))));
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
            var footer = new List<(string label, string value)>();
            if (Db.str(company, "fskatt") != "0") footer.Add(("F-skatt", "Godkänd för F-skatt"));
            if (Db.str(company, "orgNumber").Length > 0) footer.Add(("Org.nr", Db.str(company, "orgNumber")));
            if (Db.str(company, "vatNumber").Length > 0) footer.Add(("Momsreg.nr", Db.str(company, "vatNumber")));
            if (Db.str(company, "bankgiro").Length > 0) footer.Add(("Bankgiro", Db.str(company, "bankgiro")));
            if (Db.str(company, "email").Length > 0) footer.Add(("E-post", Db.str(company, "email")));
            if (Db.str(company, "phone").Length > 0) footer.Add(("Telefon", Db.str(company, "phone")));

            rows("footer", footer.Select(f => row(("label", f.label), ("value", h(f.value)))));
        }

        static string spans(SwecialObject o, params string[] keys) {
            return string.Concat(keys.Where(k => Db.str(o, k).Length > 0).Select(k => "<span>" + h(Db.str(o, k)) + "</span>"));
        }
    }
}
