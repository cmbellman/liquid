using com.swecial.code;
using com.swecial.server;
using System.Linq;

namespace com.swecial.web {
    /// <summary>/liquid/invoices.html : all invoices, drafts first. A POST creates a new draft.</summary>
    public class WebInvoices : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            title(Tr("invoices.title"));
            if (isPost) {
                var invoice = Invoicing.createDraft(company);
                var customers = Db.forCompany("customer", companyId).ToList();
                if (customers.Count == 1) {
                    var c = customers[0];
                    invoice.set("customerId", c.id);
                    invoice.set("customerName", Db.str(c, "name"));
                    string refVal = Db.str(c, "reference");
                    if (refVal.Length > 0) invoice.set("yourReference", refVal);
                    string orderRef = Db.str(c, "orderRef");
                    if (orderRef.Length > 0) invoice.set("poNumber", orderRef);
                    string orderRefLbl = Db.str(c, "orderRefLabel");
                    if (orderRefLbl.Length > 0) invoice.set("poLabel", orderRefLbl);
                }
                if (account != null) {
                    string ourRef = account.fullName ?? account.firstName;
                    if (!string.IsNullOrEmpty(ourRef)) invoice.set("ourReference", ourRef);
                }
                Db.commit(invoice);
                redirect(BasePath + "/invoice.html?id=" + u(invoice.id));
                return;
            }

            string today = Dates.today();
            rows("invoices", Db.forCompany("invoice", companyId)
                .OrderBy(i => Db.str(i, "status") == Invoicing.Draft ? 0 : 1)
                .ThenByDescending(i => Db.num(i, "number"))
                .ThenByDescending(i => i.createdAt)
                .Select(i => {
                    string status = Db.str(i, "status");
                    long number = Db.num(i, "number");
                    bool draft = status == Invoicing.Draft;
                    string customer = draft ? Db.str(Db.getOwned("customer", Db.str(i, "customerId"), companyId), "name") : Db.str(i, "customerName");
                    bool overdue = status == Invoicing.Sent && string.CompareOrdinal(Db.str(i, "dueDate"), today) < 0;
                    return row(("id", u(i.id)), ("number", number > 0 ? number.ToString() : "–"), ("customer", h(customer)),
                        ("invoiceDate", h(Db.str(i, "invoiceDate"))), ("dueDate", h(Db.str(i, "dueDate"))),
                        ("statusClass", h(status) + (overdue ? " overdue" : "")), ("status", T(overdue ? "status.overdue" : "status." + status)),
                        ("total", Money.formatOrBlank(Db.num(i, "total"))));
                }));
        }
    }
}
