using com.swecial.code;
using com.swecial.server;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/company.html : creates the user's company (with the starter chart of accounts) or
    /// edits its details, which are printed on the invoices.
    /// </summary>
    public class WebCompany : LiquidPage {
        static readonly (string key, string label)[] Fields = {
            ("name", "Företagsnamn"),
            ("orgNumber", "Organisationsnummer"),
            ("vatNumber", "Momsregistreringsnummer"),
            ("address", "Adress"),
            ("postalAddress", "Postnummer och ort"),
            ("email", "E-post"),
            ("phone", "Telefon"),
            ("bankgiro", "Bankgiro"),
        };

        public override void processPage() {
            if (!requireAccount()) return;
            title(company == null ? "Nytt bolag" : "Bolagsinställningar");

            if (isPost) {
                string name = form("name").Trim();
                if (name.Length == 0) throw new LiquidException("Fyll i företagsnamnet.");
                int terms = parseTerms(form("paymentTerms"));
                bool isNew = company == null;
                if (isNew) company = Companies.create(account.id, name);
                foreach (var (key, _) in Fields) company.set(key, form(key).Trim());
                company.set("paymentTerms", terms);
                Db.commit(company);
                redirect(BasePath + (isNew ? "/" : "/company.html?ok=saved"));
                return;
            }
            renderForm();
        }

        protected override void renderAfterError() {
            renderForm();
        }

        void renderForm() {
            var sb = new System.Text.StringBuilder();
            sb.Append("<section class='panel'><h1>" + (company == null ? "Skapa ditt bolag" : "Bolagsinställningar") + "</h1>");
            if (company == null) sb.Append("<p class='lead'>Bolaget får en grundläggande BAS-kontoplan som du kan bygga på.</p>");
            sb.Append("<form method='post' class='grid'>");
            foreach (var (key, label) in Fields) {
                string value = isPost ? form(key) : Db.str(company, key);
                sb.Append(field(label, input(key, value, key == "email" ? "email" : "text", key == "name" ? "required" : "")));
            }
            string terms = isPost ? form("paymentTerms") : (company == null ? "30" : Db.num(company, "paymentTerms").ToString());
            sb.Append(field("Betalningsvillkor (dagar)", input("paymentTerms", terms, "number", "min='0' max='365'")));
            sb.Append("<div class='actions'><button type='submit'>" + (company == null ? "Skapa bolag" : "Spara") + "</button></div>");
            sb.Append("</form></section>");
            render(sb.ToString());
        }

        static int parseTerms(string text) {
            if (!int.TryParse((text ?? "").Trim(), out int days) || days < 0 || days > 365) {
                throw new LiquidException("Betalningsvillkoret ska vara 0–365 dagar.");
            }
            return days;
        }
    }
}
