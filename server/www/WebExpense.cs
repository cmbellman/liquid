using com.swecial.code;
using com.swecial.server;
using System.Linq;
using System.Text;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/expense.html : a new expense, or ?id= to change or remove one. The form is
    /// multipart so the receipt (photo or PDF) comes with it. The category picks the account and
    /// suggests the VAT rate; the VAT amount is computed while typing and can be changed.
    /// </summary>
    public class WebExpense : LiquidPage {
        SwecialObject expense;

        public override void processPage() {
            if (!requireCompany()) return;
            string id = isPost ? form("id") : query("id");
            if (id.Length > 0) {
                expense = Db.getOwned("expense", id, companyId);
                if (expense == null || Db.str(expense, "status") == "removed") {
                    redirect(BasePath + "/expenses.html");
                    return;
                }
            }
            title(expense == null ? "Ny utgift" : "Utgift");

            if (isPost) {
                if (form("action") == "remove") {
                    Expenses.remove(expense, account.id);
                    redirect(BasePath + "/expenses.html?ok=deleted");
                    return;
                }
                req.body.files.TryGetValue("receipt", out var upload);
                Expenses.checkReceipt(upload);
                var input = new Expenses.Input {
                    date = form("date"),
                    supplier = form("supplier"),
                    category = form("category"),
                    total = Money.parse(form("total")),
                    vat = form("vat").Trim().Length > 0 ? Money.parse(form("vat")) : (long?)null,
                    vatRate = int.TryParse(form("vatRate"), out int rate) ? rate : -1,
                    foreign = form("foreign"),
                    note = form("note"),
                };
                bool isNew = expense == null;
                expense = Expenses.save(companyId, expense, input, account.id);
                Expenses.attachReceipt(expense, upload);
                redirect(isNew && form("action") == "another" ? BasePath + "/expense.html?ok=saved" : BasePath + "/expenses.html?ok=saved");
                return;
            }
            renderForm();
        }

        protected override void renderAfterError() {
            renderForm();
        }

        string value(string key) {
            if (isPost) return form(key);
            if (expense == null) return "";
            if (key == "total" || key == "vat") return Money.input(Db.num(expense, key));
            return Db.str(expense, key);
        }

        void renderForm() {
            string category = value("category");
            var sb = new StringBuilder("<section class='panel'><h1>" + (expense == null ? "Ny utgift" : "Utgift") + "</h1>");
            sb.Append("<form method='post' enctype='multipart/form-data' id='expense'>");
            if (expense != null) sb.Append(input("id", expense.id, "hidden"));

            sb.Append("<div class='grid'>");
            sb.Append(field("Datum då pengarna drogs", input("date", expense == null && !isPost ? Dates.today() : value("date"), "date", "required")));
            sb.Append(field("Var handlade du?", input("supplier", value("supplier"), "text", "required placeholder='T.ex. Clas Ohlson' autocomplete='off'")));
            sb.Append("</div>");

            sb.Append("<label>Vad gäller det?<select name='category' required><option value=''>Välj</option>");
            foreach (var c in Categories.All) {
                sb.Append("<option value='" + c.key + "' data-vat='" + c.vatRate + "' data-hint='" + h(c.hint) + "'" + (c.key == category ? " selected" : "") + ">" + h(c.name) + "</option>");
            }
            sb.Append("</select></label><p class='hint' id='categoryHint'></p>");

            string rate = value("vatRate");
            if (rate.Length == 0) rate = (Categories.find(category)?.vatRate ?? 25).ToString();
            sb.Append("<div class='grid'>");
            sb.Append(field("Belopp inkl. moms (kr)", input("total", value("total"), "text", "inputmode='decimal' class='num' required")));
            sb.Append(field("Momssats", select("vatRate", Bas.VatRates.Select(r => (r.ToString(), r + " %")), rate)));
            sb.Append(field("Varav moms (kr)", input("vat", value("vat"), "text", "inputmode='decimal' class='num'")));
            sb.Append(field("Köpt från utlandet utan svensk moms?", select("foreign", new[] {
                ("", "Nej"), (Expenses.ForeignEu, "Ja, från ett annat EU-land"), (Expenses.ForeignNonEu, "Ja, från ett land utanför EU") }, value("foreign"))));
            sb.Append("</div>");
            sb.Append("<p class='hint' id='foreignHint'>Liquid räknar då ut den svenska momsen (25 %) själv. Den dras av i samma momsdeklaration, så den kostar inget.</p>");

            sb.Append("<label>Kvitto (foto eller PDF)<input type='file' name='receipt' accept='image/*,application/pdf'></label>");
            if (expense != null && Db.str(expense, "receiptFileId").Length > 0) {
                string src = BasePath + "/receipt.html?id=" + u(expense.id) + "&v=" + u(Db.str(expense, "receiptFileId"));
                var file = Expenses.receipt(Db.str(expense, "receiptFileId"), companyId);
                sb.Append("<p class='receipt'>");
                if (file != null && file.fileType != "pdf" && file.fileType != "heic") sb.Append("<a href='" + src + "' target='_blank'><img src='" + src + "' alt='Kvitto'></a>");
                else sb.Append("<a href='" + src + "' target='_blank'>Visa kvittot</a>");
                sb.Append("<span class='hint'>Välj en ny fil för att byta kvittot.</span></p>");
            }
            sb.Append(field("Anteckning (valfritt)", input("note", value("note"), "text", "placeholder='T.ex. vilka som var med på lunchen'")));

            sb.Append("<div class='actions'><button type='submit' name='action' value='save'>Spara</button>");
            if (expense == null) sb.Append("<button type='submit' name='action' value='another' class='secondary'>Spara och lägg till en till</button>");
            sb.Append("<a href='" + BasePath + "/expenses.html'>Avbryt</a>");
            if (expense != null) sb.Append("<button type='submit' name='action' value='remove' class='danger' formnovalidate>Ta bort</button>");
            sb.Append("</div>");
            if (expense != null) sb.Append("<p class='hint'>En ändring bokförs som en rättelse av den gamla bokföringen plus en ny, så allt går att följa i efterhand.</p>");
            sb.Append("</form></section>");
            sb.Append(Script);
            render(sb.ToString());
        }

        /// <summary>
        /// Category → suggested rate and hint; amount and rate → VAT, until the VAT is typed by hand.
        /// Inline: the server doesn't serve .js files.
        /// </summary>
        const string Script = @"<script>
(function () {
  var f = document.getElementById('expense'), cat = f.category, total = f.total, rate = f.vatRate, vat = f.vat, foreign = f.foreign;
  var hint = document.getElementById('categoryHint'), foreignHint = document.getElementById('foreignHint');
  var typed = vat.value !== '';
  function n(v) { v = (v || '').replace(/[\s ]/g, '').replace(',', '.'); return Math.round((parseFloat(v) || 0) * 100); }
  function fmt(ore) { return (ore / 100).toFixed(2).replace('.', ','); }
  function computeVat() {
    if (typed) return;
    var t = n(total.value), r = parseInt(rate.value, 10) || 0;
    vat.value = t ? fmt(Math.round(foreign.value ? t * r / 100 : t * r / (100 + r))) : '';
  }
  function showHints() {
    var o = cat.options[cat.selectedIndex];
    hint.textContent = o ? (o.getAttribute('data-hint') || '') : '';
    hint.style.display = hint.textContent ? '' : 'none';
    foreignHint.style.display = foreign.value ? '' : 'none';
    vat.readOnly = !!foreign.value;
  }
  cat.addEventListener('change', function () {
    var o = cat.options[cat.selectedIndex];
    if (o && o.getAttribute('data-vat') !== null) rate.value = o.getAttribute('data-vat');
    typed = false; computeVat(); showHints();
  });
  foreign.addEventListener('change', function () { if (foreign.value) rate.value = '25'; typed = false; computeVat(); showHints(); });
  total.addEventListener('input', computeVat);
  rate.addEventListener('change', function () { typed = false; computeVat(); });
  vat.addEventListener('input', function () { typed = vat.value !== ''; });
  showHints();
})();
</script>";
    }
}
