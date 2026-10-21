using com.swecial.code;
using com.swecial.server;
using System.Linq;
using System.Text;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/customers.html : the customer list and a form for a new customer. ?id= edits one.
    /// </summary>
    public class WebCustomers : LiquidPage {
        static readonly (string key, string label)[] Fields = {
            ("name", "Namn"),
            ("orgNumber", "Organisationsnummer"),
            ("vatNumber", "Momsregistreringsnummer"),
            ("address", "Adress"),
            ("postalAddress", "Postnummer och ort"),
            ("email", "E-post"),
            ("reference", "Er referens"),
        };

        SwecialObject customer;

        public override void processPage() {
            if (!requireCompany()) return;
            title("Stamgästerna");
            string id = isPost ? form("id") : query("id");
            if (id.Length > 0) {
                customer = Db.getOwned("customer", id, companyId);
                if (customer == null) throw new LiquidException("Kunden finns inte.");
            }

            if (isPost) {
                string name = form("name").Trim();
                if (name.Length == 0) throw new LiquidException("Fyll i kundens namn.");
                if (customer == null) customer = Db.create("customer", companyId);
                foreach (var (key, _) in Fields) customer.set(key, form(key).Trim());
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
            var sb = new StringBuilder();
            sb.Append(heading("Stamgästerna", "Dina kunder"));
            sb.Append("<section class='panel'>");
            var customers = Db.forCompany("customer", companyId).OrderBy(c => Db.str(c, "name")).ToList();
            if (customers.Count == 0) {
                sb.Append("<p class='empty'>Inga stamgäster än. Lägg till din första kund nedan.</p>");
            } else {
                sb.Append("<table><thead><tr><th>Namn</th><th>Org.nr</th><th>E-post</th><th></th></tr></thead><tbody>");
                foreach (var c in customers) {
                    sb.Append("<tr><td>" + h(Db.str(c, "name")) + "</td><td>" + h(Db.str(c, "orgNumber")) + "</td><td>" + h(Db.str(c, "email")) + "</td>");
                    sb.Append("<td class='num'><a href='" + BasePath + "/customers.html?id=" + u(c.id) + "'>Ändra</a></td></tr>");
                }
                sb.Append("</tbody></table>");
            }
            sb.Append("</section>");

            sb.Append("<section class='panel'><h2>" + (customer == null ? "Ny kund" : "Ändra " + h(Db.str(customer, "name"))) + "</h2>");
            sb.Append("<form method='post' class='grid'>");
            if (customer != null) sb.Append(input("id", customer.id, "hidden"));
            foreach (var (key, label) in Fields) {
                string value = isPost ? form(key) : Db.str(customer, key);
                sb.Append(field(label, input(key, value, key == "email" ? "email" : "text", key == "name" ? "required" : "")));
            }
            sb.Append("<div class='actions'><button type='submit'>Spara</button>");
            if (customer != null) sb.Append("<a href='" + BasePath + "/customers.html'>Avbryt</a>");
            sb.Append("</div></form></section>");
            render(sb.ToString());
        }
    }
}
