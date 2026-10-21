using com.swecial.code;
using com.swecial.server;
using System.Linq;
using System.Text;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/bank.html : the company account (1930) in plain words: what came in and went out,
    /// a check against the bank's balance (?date=&amp;bank=), and a form for money that isn't an
    /// invoice or an expense (bank events).
    /// </summary>
    public class WebBank : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            title("Kassan");
            if (isPost) {
                if (form("action") == "remove") {
                    var bankEvent = Db.getOwned("bank_event", form("id"), companyId) ?? throw new LiquidException("Händelsen finns inte.");
                    BankEvents.remove(bankEvent, account.id);
                    redirect(BasePath + "/bank.html?ok=deleted");
                    return;
                }
                BankEvents.book(companyId, form("kind"), form("date"), Money.parse(form("amount")), form("note"), account.id);
                redirect(BasePath + "/bank.html?ok=saved");
                return;
            }
            renderPage();
        }

        protected override void renderAfterError() {
            renderPage();
        }

        void renderPage() {
            string today = Dates.today();
            var lines = Reports.generalLedger(companyId, Bas.Bank, "0000-01-01", "9999-12-31", out _);
            long balance = lines.Count > 0 ? lines.Last().balance : 0;

            var sb = new StringBuilder(heading("Kassan", "Företagskontot: allt som gått in och ut"));
            sb.Append("<section class='panel'>");
            sb.Append("<div class='tiles'><div class='tile'><small>Saldo enligt Liquid</small><strong>" + Money.format(balance) + " kr</strong></div></div>");
            sb.Append(reconcile(lines, today));
            sb.Append("</section>");

            sb.Append(eventForm(today));

            sb.Append("<section class='panel'><h2>In och ut</h2>");
            if (lines.Count == 0) {
                sb.Append("<p class='empty'>Inget har gått in eller ut än.</p>");
            } else {
                sb.Append("<table><thead><tr><th>Datum</th><th>Vad</th><th class='num'>In</th><th class='num'>Ut</th><th class='num'>Saldo</th><th></th></tr></thead><tbody>");
                // A changed or removed expense/event leaves a voucher and its reversal on the same date;
                // they cancel out, so only what still counts is listed (the balances include everything)
                foreach (var line in Enumerable.Reverse(lines)) {
                    if (Db.str(line.voucher, "reversedById").Length > 0 || Db.str(line.voucher, "reversesId").Length > 0) continue;
                    sb.Append("<tr><td>" + h(Db.str(line.voucher, "date")) + "</td><td>" + source(line.voucher) + "</td>");
                    sb.Append(amountCell(line.debit) + amountCell(line.credit) + "<td class='num'>" + Money.format(line.balance) + "</td>");
                    sb.Append("<td>" + removeButton(line.voucher) + "</td></tr>");
                }
                sb.Append("</tbody></table>");
            }
            sb.Append("</section>");
            render(sb.ToString());
        }

        /// <summary>Compares Liquid's balance on a date with the balance the bank shows.</summary>
        string reconcile(System.Collections.Generic.List<LedgerLine> lines, string today) {
            string date = query("date").Length > 0 ? query("date") : today;
            var sb = new StringBuilder("<h2>Stäm av mot banken</h2><form method='get' class='inline'>");
            sb.Append(field("Datum", input("date", date, "date", "required")));
            sb.Append(field("Saldo enligt banken (kr)", input("bank", query("bank"), "text", "inputmode='decimal' class='num' required")));
            sb.Append("<button type='submit' class='secondary'>Jämför</button></form>");
            if (query("bank").Length > 0) {
                string cleanDate = Dates.clean(date);
                long bank = Money.parse(query("bank"));
                var upTo = lines.Where(l => string.CompareOrdinal(Db.str(l.voucher, "date"), cleanDate) <= 0).ToList();
                long liquid = upTo.Count > 0 ? upTo.Last().balance : 0;
                long diff = bank - liquid;
                if (diff == 0) {
                    sb.Append("<p class='message'>Det stämmer: " + Money.format(liquid) + " kr den " + h(cleanDate) + ".</p>");
                } else {
                    sb.Append("<p class='error'>Banken visar " + Money.format(bank) + " kr men Liquid " + Money.format(liquid) + " kr den " + h(cleanDate)
                        + ". Skillnad " + Money.format(diff) + " kr: " + (diff > 0 ? "något har kommit in som inte är registrerat (en betald faktura eller en insättning?)." : "något har dragits som inte är registrerat (en utgift eller en avgift?).") + "</p>");
                }
            }
            return sb.ToString();
        }

        string eventForm(string today) {
            var sb = new StringBuilder("<section class='panel'><h2>Annat som gått in eller ut</h2>");
            sb.Append("<p class='lead'>Fakturor som betalas registreras på fakturan och inköp under <a href='" + BasePath + "/expenses.html'>Utgifter</a>. Här lägger du till resten.</p>");
            sb.Append("<form method='post' id='bankEvent'><label>Vad gäller det?<select name='kind' required><option value=''>Välj</option>");
            foreach (var kind in BankEvents.Kinds) {
                sb.Append("<option value='" + kind.key + "' data-hint='" + h(kind.hint) + "'" + (form("kind") == kind.key ? " selected" : "") + ">"
                    + h(kind.name) + (kind.incoming ? " (in)" : " (ut)") + "</option>");
            }
            sb.Append("</select></label><p class='hint' id='kindHint'></p><div class='grid'>");
            sb.Append(field("Datum", input("date", isPost ? form("date") : today, "date", "required")));
            sb.Append(field("Belopp (kr)", input("amount", form("amount"), "text", "inputmode='decimal' class='num' required")));
            sb.Append(field("Anteckning (valfritt)", input("note", form("note"))));
            sb.Append("</div><div class='actions'><button type='submit'>Lägg till</button></div></form></section>");
            sb.Append(@"<script>
(function () {
  var k = document.getElementById('bankEvent').kind, h = document.getElementById('kindHint');
  function show() { var o = k.options[k.selectedIndex]; h.textContent = o ? (o.getAttribute('data-hint') || '') : ''; h.style.display = h.textContent ? '' : 'none'; }
  k.addEventListener('change', show); show();
})();
</script>");
            return sb.ToString();
        }

        /// <summary>What a bank movement was, linked to the invoice, expense or event it came from.</summary>
        string source(SwecialObject voucher) {
            string text = h(Db.str(voucher, "text"));
            string sourceId = Db.str(voucher, "sourceId");
            switch (Db.str(voucher, "sourceType")) {
                case "invoice": return "<a href='" + BasePath + "/invoice.html?id=" + u(sourceId) + "'>" + text + "</a>";
                case "expense": return "<a href='" + BasePath + "/expense.html?id=" + u(sourceId) + "'>" + text + "</a>";
                default: return "<a href='" + BasePath + "/voucher.html?id=" + u(voucher.id) + "'>" + text + "</a>";
            }
        }

        string removeButton(SwecialObject voucher) {
            if (Db.str(voucher, "sourceType") != "bank_event" || Db.str(voucher, "reversedById").Length > 0) return "";
            var bankEvent = Db.getOwned("bank_event", Db.str(voucher, "sourceId"), companyId);
            if (bankEvent == null || Db.str(bankEvent, "status") == "removed") return "";
            return "<form method='post' class='row-action'>" + input("id", bankEvent.id, "hidden")
                + "<button type='submit' name='action' value='remove' class='link danger'>Ta bort</button></form>";
        }
    }
}
