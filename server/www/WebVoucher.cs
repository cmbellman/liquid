using com.swecial.code;
using com.swecial.server;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/voucher.html : without ?id= a form for a new manual voucher (series A); with ?id= the
    /// booked voucher, read-only, with a button that reverses it.
    /// </summary>
    public class WebVoucher : LiquidPage {
        const int FormRows = 8;

        SwecialObject voucher;

        public override void processPage() {
            if (!requireCompany()) return;
            string id = isPost ? form("id") : query("id");
            if (id.Length > 0) {
                voucher = Db.getOwned("voucher", id, companyId);
                if (voucher == null) {
                    redirect(BasePath + "/vouchers.html");
                    return;
                }
            }

            if (voucher != null) {
                title("Verifikation " + Ledger.label(voucher));
                if (isPost && form("action") == "reverse") {
                    var reversal = Ledger.reverse(voucher, form("date"), account.id);
                    redirect(BasePath + "/voucher.html?id=" + u(reversal.id) + "&ok=reversed");
                    return;
                }
                renderVoucher();
                return;
            }

            title("Ny verifikation");
            if (isPost) {
                var rows = new List<VoucherRow>();
                for (int i = 0; i < FormRows; i++) {
                    rows.Add(new VoucherRow { account = form("account" + i), debit = Money.parse(form("debit" + i)), credit = Money.parse(form("credit" + i)) });
                }
                var booked = Ledger.book(companyId, Ledger.Manual, form("date"), form("text"), rows, account.id);
                redirect(BasePath + "/voucher.html?id=" + u(booked.id) + "&ok=booked");
                return;
            }
            renderForm();
        }

        protected override void renderAfterError() {
            if (voucher != null) {
                renderVoucher();
            } else {
                renderForm();
            }
        }

        void renderForm() {
            var options = accountOptions().ToList();
            var sb = new StringBuilder("<section class='panel'><h1>Ny verifikation</h1><form method='post'>");
            sb.Append("<div class='grid'>");
            sb.Append(field("Datum", input("date", isPost ? form("date") : Dates.today(), "date", "required")));
            sb.Append(field("Text", input("text", form("text"), "text", "required")));
            sb.Append("</div>");
            sb.Append("<table class='rows'><thead><tr><th>Konto</th><th class='num'>Debet</th><th class='num'>Kredit</th></tr></thead><tbody>");
            for (int i = 0; i < FormRows; i++) {
                sb.Append("<tr><td>" + select("account" + i, options, form("account" + i)) + "</td>");
                sb.Append("<td>" + input("debit" + i, form("debit" + i), "text", "inputmode='decimal' class='num'") + "</td>");
                sb.Append("<td>" + input("credit" + i, form("credit" + i), "text", "inputmode='decimal' class='num'") + "</td></tr>");
            }
            sb.Append("</tbody><tfoot><tr><td>Differens</td><td class='num' id='diff' colspan='2'></td></tr></tfoot></table>");
            sb.Append("<div class='actions'><button type='submit'>Bokför</button><a href='" + BasePath + "/vouchers.html'>Avbryt</a></div>");
            sb.Append("<p class='hint'>En bokförd verifikation kan inte ändras. Fel rättas med en makulering som bokför det omvända.</p>");
            sb.Append("</form></section>");
            sb.Append(BalanceScript);
            render(sb.ToString());
        }

        void renderVoucher() {
            var sb = new StringBuilder("<section class='panel'><div class='heading'><h1>Verifikation " + h(Ledger.label(voucher)) + "</h1></div>");
            sb.Append("<dl class='facts'><dt>Datum</dt><dd>" + h(Db.str(voucher, "date")) + "</dd><dt>Text</dt><dd>" + h(Db.str(voucher, "text")) + "</dd>");
            string sourceType = Db.str(voucher, "sourceType");
            string sourceId = Db.str(voucher, "sourceId");
            if (sourceType == "invoice" && Db.getOwned("invoice", sourceId, companyId) is SwecialObject invoice) {
                sb.Append("<dt>Underlag</dt><dd><a href='" + BasePath + "/invoice.html?id=" + u(invoice.id) + "'>Faktura " + Db.num(invoice, "number") + "</a></dd>");
            } else if (sourceType == "expense" && Db.getOwned("expense", sourceId, companyId) is SwecialObject expense) {
                sb.Append("<dt>Underlag</dt><dd><a href='" + BasePath + "/expense.html?id=" + u(expense.id) + "'>Utgift</a>");
                if (Db.str(expense, "receiptFileId").Length > 0) sb.Append(" (<a href='" + BasePath + "/receipt.html?id=" + u(expense.id) + "&v=" + u(Db.str(expense, "receiptFileId")) + "' target='_blank'>kvitto</a>)");
                sb.Append("</dd>");
            } else if (sourceType == "bank_event") {
                sb.Append("<dt>Underlag</dt><dd><a href='" + BasePath + "/bank.html'>Bankhändelse</a></dd>");
            }
            sb.Append(linked("reversesId", "Rättar"));
            sb.Append(linked("reversedById", "Makulerad av"));
            sb.Append("</dl>");

            var rows = Ledger.rows(voucher);
            sb.Append("<table><thead><tr><th>Konto</th><th>Namn</th><th class='num'>Debet</th><th class='num'>Kredit</th></tr></thead><tbody>");
            foreach (var row in rows) {
                sb.Append("<tr><td>" + h(row.account) + "</td><td>" + h(accountName(row.account)) + "</td>" + amountCell(row.debit) + amountCell(row.credit) + "</tr>");
            }
            sb.Append("</tbody><tfoot><tr class='total'><td colspan='2'>Summa</td>" + amountCell(rows.Sum(r => r.debit)) + amountCell(rows.Sum(r => r.credit)) + "</tr></tfoot></table>");

            bool canReverse = sourceType.Length == 0 && Db.str(voucher, "reversedById").Length == 0 && Db.str(voucher, "reversesId").Length == 0;
            if (canReverse) {
                sb.Append("<form method='post' class='inline'>" + input("id", voucher.id, "hidden"));
                sb.Append(field("Rättelsedatum", input("date", isPost ? form("date") : Dates.today(), "date", "required")));
                sb.Append("<button type='submit' name='action' value='reverse' class='danger'>Makulera</button></form>");
            }
            sb.Append("</section>");
            render(sb.ToString());
        }

        string linked(string key, string label) {
            var other = Db.getOwned("voucher", Db.str(voucher, key), companyId);
            return other == null ? "" : "<dt>" + label + "</dt><dd><a href='" + BasePath + "/voucher.html?id=" + u(other.id) + "'>" + h(Ledger.label(other)) + "</a></dd>";
        }

        /// <summary>Shows debit minus credit while typing. Inline: the server doesn't serve .js files.</summary>
        const string BalanceScript = @"<script>
(function () {
  var f = document.querySelector('form'), out = document.getElementById('diff');
  function n(v) { v = (v || '').replace(/[\s ]/g, '').replace(',', '.'); return Math.round((parseFloat(v) || 0) * 100); }
  function update() {
    var d = 0;
    for (var i = 0; f['debit' + i]; i++) d += n(f['debit' + i].value) - n(f['credit' + i].value);
    out.textContent = d === 0 ? 'Balanserar' : (d / 100).toLocaleString('sv-SE', { minimumFractionDigits: 2 });
    out.className = 'num ' + (d === 0 ? 'ok' : 'off');
  }
  f.addEventListener('input', update); update();
})();
</script>";
    }
}
