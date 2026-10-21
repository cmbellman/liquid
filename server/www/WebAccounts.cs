using com.swecial.code;
using com.swecial.server;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/accounts.html : the chart of accounts with this year's balances, and a form to add
    /// an account or rename one (same number). Accounts are never deleted, vouchers point at them.
    /// </summary>
    public class WebAccounts : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            title("Kontoplan");
            if (isPost) {
                string number = form("number").Trim();
                string name = form("name").Trim();
                if (!Regex.IsMatch(number, "^[1-8][0-9]{3}$")) throw new LiquidException("Kontonumret ska vara fyra siffror, 1000–8999.");
                if (name.Length == 0) throw new LiquidException("Fyll i kontots namn.");
                lock (Db.Sync) {
                    var account = Bas.find(companyId, number) ?? Db.create("account", companyId);
                    account.set("number", number);
                    account.set("name", name);
                    Db.commit(account);
                }
                redirect(BasePath + "/accounts.html?ok=saved");
                return;
            }
            renderPage();
        }

        protected override void renderAfterError() {
            renderPage();
        }

        void renderPage() {
            string year = Dates.today().Substring(0, 4);
            var balances = Reports.trialBalance(companyId, year + "-01-01", year + "-12-31").ToDictionary(b => b.account);

            var sb = new StringBuilder();
            sb.Append("<section class='panel'><h1>Kontoplan</h1>");
            sb.Append("<table><thead><tr><th>Konto</th><th>Namn</th><th class='num'>Saldo " + year + "</th></tr></thead><tbody>");
            foreach (var a in Db.forCompany("account", companyId).OrderBy(a => Db.str(a, "number"))) {
                string number = Db.str(a, "number");
                long balance = balances.TryGetValue(number, out var b) ? b.closing : 0;
                sb.Append("<tr><td><a href='" + BasePath + "/reports.html?report=ledger&account=" + u(number) + "'>" + h(number) + "</a></td>");
                sb.Append("<td>" + h(Db.str(a, "name")) + "</td>" + amountCell(balance) + "</tr>");
            }
            sb.Append("</tbody></table></section>");

            sb.Append("<section class='panel'><h2>Lägg till eller byt namn på konto</h2><form method='post' class='grid'>");
            sb.Append(field("Kontonummer", input("number", form("number"), "text", "inputmode='numeric' pattern='[1-8][0-9]{3}' required")));
            sb.Append(field("Namn", input("name", form("name"), "text", "required")));
            sb.Append("<div class='actions'><button type='submit'>Spara</button></div></form></section>");
            render(sb.ToString());
        }
    }
}
