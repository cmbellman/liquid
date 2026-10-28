using com.swecial.code;
using com.swecial.server;
using System;
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
                    Invoicing.markPaid(invoice, form("paidDate"), account.id, form("incomeWhenPaid") == "1");
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
                if (action == "preview") {
                    redirect(BasePath + "/invoice-pdf.html?id=" + u(invoice.id) + "&inline=1&v=" + System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    return;
                }
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
            invoice.set("yourReference", form("yourReference").Trim());
            invoice.set("poLabel", form("poLabel").Trim());
            invoice.set("poNumber", form("poNumber").Trim());
            invoice.set("note", form("note").Trim());
            if (Db.str(invoice, "documentFileId").Length > 0) invoice.set("issuedNumber", form("issuedNumber").Trim());
            invoice.set("isHourly", form("isHourly") == "1" ? "1" : "0");

            invoice.set("supplyType", form("supplyType") == Invoicing.Goods ? Invoicing.Goods : Invoicing.Services);
            // Customers abroad get no Swedish VAT
            bool noVat = Invoicing.vatTreatmentOf(invoice) != Invoicing.Domestic;
            var rows = new List<InvoiceRow>();
            for (int i = 0; i < rowCount(); i++) {
                string text = form("text" + i).Trim();
                string price = form("price" + i);
                if (text.Length == 0 && price.Trim().Length == 0) continue;
                int vat = noVat ? 0 : int.TryParse(form("vat" + i), out int v) && Bas.VatRates.Contains(v) ? v : 25;
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

            bool isHourly = (isPost ? form("isHourly") == "1" : Db.str(invoice, "isHourly") == "1");
            set("isHourly", isHourly);
            set("isHourlyChecked", isChecked(isHourly));
            set("qtyLabel", isHourly ? T("invoice.col.hours") : T("invoice.col.quantity"));
            set("priceLabel", isHourly ? T("invoice.col.hourlyRate") : T("invoice.col.unitPrice"));

            set("id", u(invoice.id));
            string pdfVersion = invoice.rev.ToString() + "_" + (company?.rev.ToString() ?? "") + "_" + System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            set("pdfVersion", pdfVersion);
            set("heading", number > 0 ? T("invoice.title", number) : T("invoice.draft"));
            set("statusClass", h(status));
            set("status", T("status." + status));
            set("draft", draft);
            set("sent", status == Invoicing.Sent);
            set("paid", Db.str(invoice, "paidDate").Length > 0);
            set("isPaid", status == Invoicing.Paid);
            val("invoiceDate", value("invoiceDate"));
            val("dueDate", value("dueDate"));

            string customerId = isPost ? form("customerId") : Db.str(invoice, "customerId");
            var customerList = draft ? Db.forCompany("customer", companyId).OrderBy(c => Db.str(c, "name")).ToList() : null;
            if (draft && string.IsNullOrEmpty(customerId) && customerList.Count == 1) {
                customerId = customerList[0].id;
                if (string.IsNullOrEmpty(Db.str(invoice, "customerId"))) {
                    invoice.set("customerId", customerId);
                    invoice.set("customerName", Db.str(customerList[0], "name"));
                    Db.commit(invoice);
                }
            }
            var customer = customerId.Length > 0 ? Db.getOwned("customer", customerId, companyId) : null;

            string ourRef = value("ourReference");
            if (string.IsNullOrEmpty(ourRef)) {
                ourRef = account?.fullName ?? account?.firstName ?? "";
                if (string.IsNullOrEmpty(ourRef)) {
                    var prev = Db.forCompany("invoice", companyId).OrderByDescending(i => i.createdAt).FirstOrDefault(i => Db.str(i, "ourReference").Length > 0);
                    if (prev != null) ourRef = Db.str(prev, "ourReference");
                }
                if (draft && ourRef.Length > 0 && string.IsNullOrEmpty(Db.str(invoice, "ourReference"))) {
                    invoice.set("ourReference", ourRef);
                    Db.commit(invoice);
                }
            }
            val("ourReference", ourRef);
            set("hasOurReference", ourRef.Length > 0);

            string customerReference = customer != null ? Db.str(customer, "reference") : "";
            val("customerReference", customerReference);
            string yourRef = value("yourReference");
            if (string.IsNullOrEmpty(yourRef) && customerReference.Length > 0) yourRef = customerReference;
            val("yourReference", yourRef);
            set("hasYourReference", yourRef.Length > 0);

            string customerOrderRef = customer != null ? Db.str(customer, "orderRef") : "";
            string customerOrderRefLabel = customer != null ? Db.str(customer, "orderRefLabel") : "";
            if (customerOrderRefLabel.Length == 0) customerOrderRefLabel = "PO#";

            string effectivePoNumber = value("poNumber");
            if (string.IsNullOrEmpty(effectivePoNumber) && customerOrderRef.Length > 0) effectivePoNumber = customerOrderRef;
            string effectivePoLabel = value("poLabel");
            if (string.IsNullOrEmpty(effectivePoLabel)) effectivePoLabel = customerOrderRefLabel;

            val("poNumber", effectivePoNumber);
            val("poLabel", effectivePoLabel);
            set("hasPoNumber", effectivePoNumber.Length > 0);
            val("note", value("note"));
            val("customerName", Db.str(invoice, "customerName"));
            val("paidDate", Db.str(invoice, "paidDate"));
            val("paidDateInput", isPost ? form("paidDate") : Dates.today());
            string documentId = Db.str(invoice, "documentFileId");
            set("hasDocument", documentId.Length > 0);
            // VAT for customers abroad: reverse charge (EU business) or export
            string treatment = Invoicing.vatTreatmentOf(invoice);
            set("foreignVat", treatment != Invoicing.Domestic);
            set("treatmentText", treatment == Invoicing.Domestic ? "" : T("invoice.treatment." + treatment));
            set("supplyOptions", options(new[] { (Invoicing.Services, Tr("invoice.supply.services")), (Invoicing.Goods, Tr("invoice.supply.goods")) },
                isPost ? form("supplyType") : Invoicing.isGoods(invoice) ? Invoicing.Goods : Invoicing.Services));
            set("supplyText", Tr(Invoicing.isGoods(invoice) ? "invoice.supply.goods" : "invoice.supply.services"));
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
                set("noCustomers", customerList.Count == 0);
                string selectedCustId = customerId;
                var custRows = customerList.Select(c => row(
                    ("value", u(c.id)),
                    ("text", h(Db.str(c, "name"))),
                    ("selected", c.id == selectedCustId ? "selected" : ""),
                    ("rate", Money.input(Db.num(c, "hourlyRate"))),
                    ("ref", h(Db.str(c, "reference"))),
                    ("poLabel", h(Db.str(c, "orderRefLabel").Length > 0 ? Db.str(c, "orderRefLabel") : "PO#")),
                    ("poNumber", h(Db.str(c, "orderRef")))
                )).ToList();

                if (customerList.Count != 1) {
                    custRows.Insert(0, row(
                        ("value", ""),
                        ("text", Tr("invoice.chooseCustomer")),
                        ("selected", string.IsNullOrEmpty(selectedCustId) ? "selected" : ""),
                        ("rate", ""),
                        ("ref", ""),
                        ("poLabel", "PO#"),
                        ("poNumber", "")
                    ));
                }
                rows("customers", custRows);
                int count = isPost ? rowCount() : lines.Count + BlankRows;
                set("rowCount", count.ToString());
                rows("lines", Enumerable.Range(0, count).Select(i => {
                    InvoiceRow line = !isPost && i < lines.Count ? lines[i] : null;
                    string priceVal = isPost ? form("price" + i) : line != null ? Money.input(line.unitPrice) : "";
                    if (i == 0 && string.IsNullOrEmpty(priceVal) && isHourly && selectedCustId.Length > 0) {
                        var cust = Db.getOwned("customer", selectedCustId, companyId);
                        long r = Db.num(cust, "hourlyRate");
                        if (r > 0) priceVal = Money.input(r);
                    }
                    return row(("i", i.ToString()),
                        ("text", h(isPost ? form("text" + i) : line?.text ?? "")),
                        ("qty", h(isPost ? form("qty" + i) : line != null ? Money.formatDecimal(line.quantity) : "")),
                        ("price", h(priceVal)),
                        ("vatRates", vatOptions(isPost ? form("vat" + i) : (line?.vatRate ?? 25).ToString())),
                        ("net", line != null ? Money.formatOrBlank(line.net) : ""));
                }));
                bool accrual = Settings.accountingMethod(company, DateTime.TryParse(value("invoiceDate"), out DateTime d) ? d.Year : DateTime.Today.Year) == Settings.AccrualMethod;
                set("sendHint", T(accrual ? "invoice.sendHint.accrual" : "invoice.sendHint.cash"));
            } else {
                rows("lines", lines.Select(line => row(("text", h(line.text)), ("qty", Money.formatDecimal(line.quantity)),
                    ("price", Money.formatOrBlank(line.unitPrice)), ("vat", line.vatRate.ToString()), ("net", Money.formatOrBlank(line.net)))));
                // Booked when sent (accrual method) and/or when paid
                var links = new[] { voucherLink("voucherId"), voucherLink("paymentVoucherId") }.Where(l => l.Length > 0).ToList();
                set("bookkeeping", links.Count > 0 ? string.Join(", ", links) : T("invoice.bookedWhenPaid"));
                string invoiceYear = Db.str(invoice, "invoiceDate").Substring(0, 4);
                bool overYearEnd = status == Invoicing.Sent && !Invoicing.bookedAsReceivable(invoice) && Invoicing.overYearEnd(Db.str(invoice, "invoiceDate"), Dates.today());
                bool mayTakeUp = overYearEnd && Invoicing.mayTakeUpWhenPaid(invoice);
                string hint = Invoicing.bookedAsReceivable(invoice) ? "invoice.paymentHint.accrual"
                    : mayTakeUp ? "invoice.paymentHint.cash" : overYearEnd ? "invoice.paymentHint.closing" : "invoice.paymentHint.cash";
                set("paymentHint", T(hint, Money.format(totals.total), invoiceYear));
                set("mayTakeUp", mayTakeUp);
                set("takeUpLabel", T("invoice.takeUp.label", invoiceYear));
                set("takeUpChecked", isChecked(!isPost || form("incomeWhenPaid") == "1"));
            }
        }

        string voucherLink(string key) {
            var voucher = Db.getOwned("voucher", Db.str(invoice, key), companyId);
            return voucher == null ? "" : "<a href='" + BasePath + "/voucher.html?id=" + u(voucher.id) + "'>" + h(Ledger.label(voucher)) + "</a>";
        }
    }
}
