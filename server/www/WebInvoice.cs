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
            title(number > 0 ? Tr("invoice.title", number) : Tr("invoice.draft"));

            if (isPost) {
                string action = form("action");
                if (action == "paid") {
                    Invoicing.markPaid(invoice, form("paidDate"), account.id);
                    redirect(here() + "&ok=paid");
                    return;
                }
                if (action == "unpaid") {
                    Invoicing.unmarkPaid(invoice, account.id);
                    redirect(here() + "&ok=unpaid");
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
                if (action == "registerIssued") {
                    Invoicing.registerIssued(invoice, form("issuedNumber"), account.id);
                    redirect(here() + "&ok=registered");
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
            if (customerId.Length > 0 && Db.getOwned("customer", customerId, companyId) == null) throw new LiquidException("Kunden finns inte.", "CLIENT NOT FOUND.");
            invoice.set("customerId", customerId);
            invoice.set("invoiceDate", Dates.clean(form("invoiceDate")));
            invoice.set("dueDate", Dates.clean(form("dueDate")));
            invoice.set("ourReference", form("ourReference").Trim());
            invoice.set("note", form("note").Trim());
            if (Db.str(invoice, "documentFileId").Length > 0) invoice.set("issuedNumber", form("issuedNumber").Trim());

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
            var lines = Invoicing.rows(invoice);
            var totals = Invoicing.totals(lines);
            long number = Db.num(invoice, "number");
            string value(string key) => isPost ? form(key) : Db.str(invoice, key);

            set("id", u(invoice.id));
            set("heading", number > 0 ? T("invoice.title", number) : T("invoice.draft"));
            set("statusClass", h(status));
            set("status", T("status." + status));
            set("draft", draft);
            set("sent", status == Invoicing.Sent);
            set("paid", Db.str(invoice, "paidDate").Length > 0);
            set("isPaid", status == Invoicing.Paid);
            val("invoiceDate", value("invoiceDate"));
            val("dueDate", value("dueDate"));
            val("ourReference", value("ourReference"));
            val("note", value("note"));
            val("customerName", Db.str(invoice, "customerName"));
            val("paidDate", Db.str(invoice, "paidDate"));
            val("paidDateInput", isPost ? form("paidDate") : Dates.today());
            string documentId = Db.str(invoice, "documentFileId");
            set("hasDocument", documentId.Length > 0);
            set("documentSrc", BasePath + "/receipt.html?invoice=" + u(invoice.id) + "&amp;v=" + u(documentId));
            // Issued outside Liquid (from Pantpåsen): registered with the document's own number
            set("issued", draft && documentId.Length > 0);
            val("issuedNumber", isPost ? form("issuedNumber") : Db.str(invoice, "issuedNumber"));
            long documentTotal = Db.num(invoice, "documentTotal");
            bool mismatch = draft && documentId.Length > 0 && documentTotal > 0 && documentTotal != totals.total;
            set("totalMismatch", mismatch ? T("invoice.issued.mismatch", Money.format(documentTotal), Money.format(totals.total)) : "");

            // Totals for the table footer
            set("net", Money.formatOrBlank(totals.net));
            rows("vatLines", totals.byRate.Where(r => r.Key > 0).Reverse().Select(r =>
                row(("label", T("invoice.vatOn", r.Key, Money.format(r.Value.net))), ("vat", Money.formatOrBlank(r.Value.vat)))));
            set("totalDue", Money.format(totals.total));

            if (draft) {
                var customers = Db.forCompany("customer", companyId).OrderBy(c => Db.str(c, "name")).Select(c => (c.id, Db.str(c, "name"))).ToList();
                set("noCustomers", customers.Count == 0);
                set("customers", options(customers.Prepend(("", Tr("invoice.chooseCustomer"))), value("customerId")));
                int count = isPost ? rowCount() : lines.Count + BlankRows;
                set("rowCount", count.ToString());
                rows("lines", Enumerable.Range(0, count).Select(i => {
                    InvoiceRow line = !isPost && i < lines.Count ? lines[i] : null;
                    return row(("i", i.ToString()),
                        ("text", h(isPost ? form("text" + i) : line?.text ?? "")),
                        ("qty", h(isPost ? form("qty" + i) : line != null ? Money.formatDecimal(line.quantity) : "")),
                        ("price", h(isPost ? form("price" + i) : line != null ? Money.input(line.unitPrice) : "")),
                        ("vatRates", vatOptions(isPost ? form("vat" + i) : (line?.vatRate ?? 25).ToString())),
                        ("net", line != null ? Money.formatOrBlank(line.net) : ""));
                }));
                bool accrual = Settings.accountingMethod(company) == Settings.AccrualMethod;
                set("sendHint", T(accrual ? "invoice.sendHint.accrual" : "invoice.sendHint.cash"));
            } else {
                rows("lines", lines.Select(line => row(("text", h(line.text)), ("qty", Money.formatDecimal(line.quantity)),
                    ("price", Money.formatOrBlank(line.unitPrice)), ("vat", line.vatRate.ToString()), ("net", Money.formatOrBlank(line.net)))));
                // Booked when sent (accrual method) and/or when paid
                var links = new[] { voucherLink("voucherId"), voucherLink("paymentVoucherId") }.Where(l => l.Length > 0).ToList();
                set("bookkeeping", links.Count > 0 ? string.Join(", ", links) : T("invoice.bookedWhenPaid"));
                bool bookedWhenSent = Db.str(invoice, "voucherId").Length > 0 || Db.str(invoice, "closingVoucherId").Length > 0;
                string hint = bookedWhenSent ? "invoice.paymentHint.accrual"
                    : Invoicing.overYearEnd(Db.str(invoice, "invoiceDate"), Dates.today()) ? "invoice.paymentHint.closing" : "invoice.paymentHint.cash";
                set("paymentHint", T(hint, Money.format(totals.total)));
            }
        }

        string voucherLink(string key) {
            var voucher = Db.getOwned("voucher", Db.str(invoice, key), companyId);
            return voucher == null ? "" : "<a href='" + BasePath + "/voucher.html?id=" + u(voucher.id) + "'>" + h(Ledger.label(voucher)) + "</a>";
        }
    }
}
