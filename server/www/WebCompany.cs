using com.swecial.code;
using com.swecial.server;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/company.html : without a company, creates it (with the starter chart of accounts).
    /// Otherwise Inställningar: the company's details (printed on invoices), invoice numbering and
    /// payment terms, and the bookkeeping choices (accounting method, VAT period). Every choice is
    /// explained in plain words; the rules that keep the books right live in Settings and Invoicing.
    /// The look (Themes) is saved on the user, not the company.
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
            title(company == null ? "Nytt bolag" : "Inställningar");

            if (isPost) {
                string name = form("name").Trim();
                if (name.Length == 0) throw new LiquidException("Fyll i företagsnamnet.");
                if (company == null) {
                    company = Companies.create(account.id, name);
                    foreach (var (key, _) in Fields) company.set(key, form(key).Trim());
                    Db.commit(company);
                    redirect(BasePath + "/");
                    return;
                }
                save(name);
                redirect(BasePath + "/company.html?ok=saved");
                return;
            }
            render(company == null ? createForm() : settingsForm());
        }

        protected override void renderAfterError() {
            render(company == null ? createForm() : settingsForm());
        }

        void save(string name) {
            int terms = parseTerms(form("paymentTerms"));
            string numbering = form("invoiceNumbering") == Settings.DateNumbers ? Settings.DateNumbers : Settings.SerialNumbers;
            string method = form("accountingMethod") == Settings.AccrualMethod ? Settings.AccrualMethod : Settings.CashMethod;
            string vatPeriod = form("vatPeriod");
            if (vatPeriod != Settings.VatQuarter && vatPeriod != Settings.VatMonth) vatPeriod = Settings.VatYear;

            // Only when it was changed: the field shows the next number by default
            string nextSerial = form("nextSerial").Trim();
            if (numbering == Settings.SerialNumbers && nextSerial.Length > 0 && nextSerial != (Settings.highestSerial(companyId) + 1).ToString()) {
                if (!long.TryParse(nextSerial, out long next)) throw new LiquidException("Nästa fakturanummer ska vara ett heltal.");
                Settings.setNextSerial(companyId, next);
            }

            foreach (var (key, _) in Fields) company.set(key, form(key).Trim());
            company.set("name", name);
            company.set("paymentTerms", terms);
            company.set("invoiceNumbering", numbering);
            company.set("accountingMethod", method);
            company.set("vatPeriod", vatPeriod);
            Db.commit(company);
            Themes.set(account, form("theme"));
        }

        string value(string key) {
            return isPost ? form(key) : Db.str(company, key);
        }

        string createForm() {
            var sb = new StringBuilder(heading("Skapa ditt bolag", "Bolaget får en grundläggande kontoplan. Resten ställer du in under Inställningar."));
            sb.Append("<section class='panel'><form method='post' class='grid'>");
            foreach (var (key, label) in Fields) {
                sb.Append(field(label, input(key, form(key), key == "email" ? "email" : "text", key == "name" ? "required autofocus" : "")));
            }
            sb.Append("<div class='actions'><button type='submit'>Skapa bolag</button></div></form></section>");
            return sb.ToString();
        }

        string settingsForm() {
            var sb = new StringBuilder(heading("Inställningar", "Hur Liquid fungerar för " + Db.str(company, "name")));
            sb.Append("<form method='post' id='settings'>");

            // --- The company ---
            sb.Append("<section class='panel'><div class='heading'><div><h2>Bolaget</h2><p class='sub'>Står på dina fakturor</p></div></div><div class='grid'>");
            foreach (var (key, label) in Fields) {
                string control = input(key, value(key), key == "email" ? "email" : "text", key == "name" ? "required" : "");
                if (key == "vatNumber") control += "<button type='button' class='link' id='vatSuggest' hidden></button>";
                sb.Append(field(label, control));
            }
            sb.Append("</div><p class='hint'>Momsregistreringsnumret är SE + organisationsnumret utan bindestreck + 01, om bolaget är momsregistrerat.</p></section>");

            // --- Invoices ---
            string numbering = isPost ? (form("invoiceNumbering") == Settings.DateNumbers ? Settings.DateNumbers : Settings.SerialNumbers) : Settings.invoiceNumbering(company);
            long nextSerial = Settings.highestSerial(companyId) + 1;
            string example = DateTime.Today.ToString("yyyyMMdd") + "01";
            sb.Append("<section class='panel'><div class='heading'><div><h2>Fakturor</h2><p class='sub'>Fakturanummer och betalningsvillkor</p></div></div>");
            sb.Append("<p class='label'>Fakturanummer</p><div class='choices'>");
            sb.Append(choice("invoiceNumbering", Settings.SerialNumbers, numbering, "Löpnummer",
                "1, 2, 3 … Varje faktura får nästa nummer i serien.",
                field("Nästa nummer", input("nextSerial", isPost ? form("nextSerial") : nextSerial.ToString(), "number", "min='1' max='" + Settings.MaxSerial + "'"))));
            sb.Append(choice("invoiceNumbering", Settings.DateNumbers, numbering, "Fakturadatum + löpnummer",
                "Datumet och ett nummer för dagen, till exempel " + example + ". Nästa faktura samma dag får " + example.Substring(0, 8) + "02.", ""));
            sb.Append("</div><p class='hint'>Numret sätts när fakturan skickas. Du kan byta när som helst: serierna kan aldrig krocka, så inget nummer används två gånger.</p>");
            sb.Append("<div class='grid'>" + field("Betalningsvillkor (dagar)", input("paymentTerms", isPost ? form("paymentTerms") : Invoicing.paymentTerms(company).ToString(), "number", "min='0' max='365'")) + "</div>");
            sb.Append("</section>");

            // --- Bookkeeping ---
            string method = isPost ? (form("accountingMethod") == Settings.AccrualMethod ? Settings.AccrualMethod : Settings.CashMethod) : Settings.accountingMethod(company);
            string vat = isPost ? form("vatPeriod") : Settings.vatPeriod(company);
            sb.Append("<section class='panel'><div class='heading'><div><h2>Bokföring</h2><p class='sub'>Liquid bokför åt dig, här väljer du hur</p></div></div>");
            sb.Append("<p class='label'>När ska fakturor bokföras?</p><div class='choices'>");
            sb.Append(choice("accountingMethod", Settings.CashMethod, method, "När pengarna kommer in (kontantmetoden)",
                "Enklast. Fakturan bokförs när du prickar av den på notan. Obetalda fakturor bokas upp i bokslutet. Får användas om omsättningen är under 3 miljoner kr per år.", ""));
            sb.Append(choice("accountingMethod", Settings.AccrualMethod, method, "När fakturan skickas (faktureringsmetoden)",
                "Fakturan bokförs som en fordran direkt när den skickas, och betalningen bokförs när den kommer in. Krävs för större bolag.", ""));
            sb.Append("</div><p class='hint'>Metoden ska gälla ett helt räkenskapsår, så byt helst vid årsskiftet. Ett byte gäller nya fakturor; de som redan skickats bokförs som när de skickades. Utgifter bokförs alltid när de betalas.</p>");
            sb.Append("<p class='label'>Hur ofta redovisar du moms?</p><div class='choices three'>");
            sb.Append(choice("vatPeriod", Settings.VatYear, vat, "En gång per år", "Vanligt om den momspliktiga försäljningen är under 1 miljon kr per år.", ""));
            sb.Append(choice("vatPeriod", Settings.VatQuarter, vat, "Varje kvartal", "Fyra momsdeklarationer per år.", ""));
            sb.Append(choice("vatPeriod", Settings.VatMonth, vat, "Varje månad", "Krävs om försäljningen är över 40 miljoner kr per år.", ""));
            sb.Append("</div><p class='hint'>Det är Skatteverket som bestämt hur ofta, det står i registreringsbeviset. Momsrapporten visar den här perioden från början.</p>");
            sb.Append("</section>");

            // --- Look (per user) ---
            string theme = isPost ? form("theme") : Themes.of(account);
            sb.Append("<section class='panel'><div class='heading'><div><h2>Utseende</h2><p class='sub'>Gäller bara dig</p></div></div><div class='choices'>");
            sb.Append(choice("theme", Themes.Neon, theme, "Neon", "Mörk pub med cyan och gult ljus.", ""));
            sb.Append(choice("theme", Themes.Nostromo, theme, "Nostromo", "Grön fosforskärm som i ett gammalt rymdskepp.", ""));
            sb.Append("</div></section>");

            sb.Append("<div class='actions sticky'><button type='submit'>Spara inställningar</button></div></form>");
            sb.Append(Script);
            return sb.ToString();
        }

        /// <summary>One option as a card: a radio button, a title, an explanation and optional extra fields.</summary>
        static string choice(string name, string value, string current, string title, string text, string extra) {
            return "<label class='choice'><input type='radio' name='" + name + "' value='" + value + "'" + (value == current ? " checked" : "") + ">"
                + "<b>" + h(title) + "</b><span>" + h(text) + "</span>" + extra + "</label>";
        }

        static int parseTerms(string text) {
            if (!int.TryParse((text ?? "").Trim(), out int days) || days < 0 || days > 365) {
                throw new LiquidException("Betalningsvillkoret ska vara 0–365 dagar.");
            }
            return days;
        }

        /// <summary>
        /// Suggests the VAT number from the organisation number when it's empty, and greys out the
        /// next-number field when date numbers are chosen. Inline: no .js files on the server.
        /// </summary>
        const string Script = @"<script>
(function () {
  var f = document.getElementById('settings'), btn = document.getElementById('vatSuggest');
  function suggest() {
    var digits = (f.orgNumber.value || '').replace(/\D/g, '');
    var ok = digits.length === 10 && !f.vatNumber.value.trim();
    btn.hidden = !ok;
    if (ok) btn.textContent = 'Använd SE' + digits + '01';
  }
  btn.addEventListener('click', function () { f.vatNumber.value = btn.textContent.replace('Använd ', ''); suggest(); });
  f.orgNumber.addEventListener('input', suggest); f.vatNumber.addEventListener('input', suggest); suggest();
  function serial() { f.nextSerial.disabled = !f.querySelector('input[name=invoiceNumbering][value=serial]').checked; }
  f.querySelectorAll('input[name=invoiceNumbering]').forEach(function (r) { r.addEventListener('change', serial); }); serial();
})();
</script>";
    }
}
