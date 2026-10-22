using com.swecial.code;
using com.swecial.server;
using System.Linq;
using System.Text;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/customers.html : the customer list and a form for a new customer. ?id= edits one.
    /// </summary>
    public class WebCustomers : LiquidPage {
        static readonly string[] Fields = { "name", "orgNumber", "vatNumber", "address", "postalAddress", "email", "reference" };

        SwecialObject customer;

        public override void processPage() {
            if (!requireCompany()) return;
            title(Tr("customers.title"));
            string id = isPost ? form("id") : query("id");
            if (id.Length > 0) {
                customer = Db.getOwned("customer", id, companyId);
                if (customer == null) throw new LiquidException("Kunden finns inte.", "CLIENT NOT FOUND.");
            }

            if (isPost) {
                string name = form("name").Trim();
                if (name.Length == 0) throw new LiquidException("Fyll i kundens namn.", "CLIENT NAME REQUIRED.");
                if (customer == null) customer = Db.create("customer", companyId);
                foreach (var key in Fields) customer.set(key, form(key).Trim());
                string region = form("region");
                customer.set("region", region == Invoicing.RegionEu || region == Invoicing.RegionOutsideEu ? region : Invoicing.RegionSweden);
                if (region == Invoicing.RegionEu && form("vatNumber").Trim().Length == 0) {
                    Db.commit(customer);
                    redirect(BasePath + "/customers.html?ok=saved&id=" + u(customer.id) + "&novat=1");
                    return;
                }
                Db.commit(customer);
                redirect(BasePath + "/customers.html?ok=saved");
                return;
            }
            renderPage();
        }

        protected override void renderAfterError() {
            renderPage();
        }

        void renderPage() {
            rows("customers", Db.forCompany("customer", companyId).OrderBy(c => Db.str(c, "name")).Select(c =>
                row(("id", u(c.id)), ("name", h(Db.str(c, "name"))), ("orgNumber", h(Db.str(c, "orgNumber"))), ("email", h(Db.str(c, "email"))))));
            set("editing", customer != null);
            set("id", customer != null ? h(customer.id) : "");
            set("formTitle", customer == null ? T("customers.new") : T("customers.edit", h(Db.str(customer, "name"))));
            rows("fields", Fields.Select(key => row(("name", key), ("label", T("field." + key)), ("type", key == "email" ? "email" : "text"),
                ("value", h(isPost ? form(key) : Db.str(customer, key))), ("required", key == "name" ? "required" : ""))));
            string region = isPost ? form("region") : Db.str(customer, "region");
            set("regions", options(new[] { (Invoicing.RegionSweden, Tr("customers.region.se")), (Invoicing.RegionEu, Tr("customers.region.eu")), (Invoicing.RegionOutsideEu, Tr("customers.region.noneu")) },
                region.Length > 0 ? region : Invoicing.RegionSweden));
            set("noVatWarning", query("novat") == "1");
        }
    }
}
