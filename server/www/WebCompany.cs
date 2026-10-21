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
        static readonly string[] Fields = { "name", "orgNumber", "vatNumber", "address", "postalAddress", "email", "phone", "bankgiro" };

        public override void processPage() {
            if (!requireAccount()) return;
            title(Tr(company == null ? "settings.create.title" : "settings.title"));

            if (isPost) {
                string name = form("name").Trim();
                if (name.Length == 0) throw new LiquidException("Fyll i företagsnamnet.", "COMPANY NAME REQUIRED.");
                if (company == null) {
                    company = Companies.create(account.id, name);
                    foreach (var key in Fields) company.set(key, form(key).Trim());
                    Db.commit(company);
                    redirect(BasePath + "/");
                    return;
                }
                save(name);
                redirect(BasePath + "/company.html?ok=saved");
                return;
            }
            setValues();
        }

        protected override void renderAfterError() {
            setValues();
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
                if (!long.TryParse(nextSerial, out long next)) throw new LiquidException("Nästa fakturanummer ska vara ett heltal.", "NEXT INVOICE NUMBER MUST BE AN INTEGER.");
                Settings.setNextSerial(companyId, next);
            }

            foreach (var key in Fields) company.set(key, form(key).Trim());
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

        void setValues() {
            bool creating = company == null;
            set("creating", creating);
            rows("fields", Fields.Select(key => row(("name", key), ("label", T("company.field." + key)), ("type", key == "email" ? "email" : "text"),
                ("value", h(creating ? form(key) : value(key))), ("required", key == "name" ? (creating ? "required autofocus" : "required") : ""),
                ("vat", key == "vatNumber"))));
            if (creating) return;

            set("subtitle", T("settings.subtitle", h(Db.str(company, "name"))));
            string numbering = isPost ? (form("invoiceNumbering") == Settings.DateNumbers ? Settings.DateNumbers : Settings.SerialNumbers) : Settings.invoiceNumbering(company);
            string example = DateTime.Today.ToString("yyyyMMdd") + "01";
            val("nextSerial", isPost ? form("nextSerial") : (Settings.highestSerial(companyId) + 1).ToString());
            set("maxSerial", Settings.MaxSerial.ToString());
            rows("numbering", new[] {
                choice("invoiceNumbering", Settings.SerialNumbers, numbering, T("settings.numbering.serial"), T("settings.numbering.serialText"), serial: true),
                choice("invoiceNumbering", Settings.DateNumbers, numbering, T("settings.numbering.date"), T("settings.numbering.dateText", example, example.Substring(0, 8) + "02")),
            });
            val("paymentTerms", isPost ? form("paymentTerms") : Invoicing.paymentTerms(company).ToString());

            string method = isPost ? (form("accountingMethod") == Settings.AccrualMethod ? Settings.AccrualMethod : Settings.CashMethod) : Settings.accountingMethod(company);
            rows("methods", new[] {
                choice("accountingMethod", Settings.CashMethod, method, T("settings.method.cash"), T("settings.method.cashText")),
                choice("accountingMethod", Settings.AccrualMethod, method, T("settings.method.accrual"), T("settings.method.accrualText")),
            });
            string vat = isPost ? form("vatPeriod") : Settings.vatPeriod(company);
            rows("vatPeriods", new[] { Settings.VatYear, Settings.VatQuarter, Settings.VatMonth }.Select(p =>
                choice("vatPeriod", p, vat, T("settings.vat." + p), T("settings.vat." + p + "Text"))));

            // Every theme in server/themes describes itself
            string current = isPost ? form("theme") : theme.id;
            rows("themes", Themes.all().Select(t => choice("theme", t.id, current, h(t.name ?? t.id), h(t.description ?? ""))));
        }

        /// <summary>One option card: a radio button, a title and an explanation (both already encoded).</summary>
        static SwecialObject choice(string name, string value, string current, string title, string text, bool serial = false) {
            return row(("name", name), ("value", h(value)), ("checked", isChecked(value == current)), ("title", title), ("text", text), ("serial", serial));
        }

        static int parseTerms(string text) {
            if (!int.TryParse((text ?? "").Trim(), out int days) || days < 0 || days > 365) {
                throw new LiquidException("Betalningsvillkoret ska vara 0–365 dagar.", "PAYMENT TERMS MUST BE 0–365 DAYS.");
            }
            return days;
        }

    }
}
