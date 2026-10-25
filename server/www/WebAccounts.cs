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
            title(Tr("accounts.title"));
            if (isPost) {
                string number = form("number").Trim();
                string name = form("name").Trim();
                if (!Regex.IsMatch(number, "^[1-8][0-9]{3}$")) throw new LiquidException("Kontonumret ska vara fyra siffror, 1000–8999.", "ACCOUNT NUMBER MUST BE FOUR DIGITS, 1000–8999.");
                if (name.Length == 0) throw new LiquidException("Fyll i kontots namn.", "ACCOUNT NAME REQUIRED.");
                lock (Db.Sync) {
                    var existing = Bas.find(companyId, number);
                    string before = Db.str(existing, "name");
                    var account = existing ?? Db.create("account", companyId);
                    account.set("number", number);
                    account.set("name", name);
                    Db.commit(account);
                    if (existing == null) Audit.log(companyId, this.account.id, "account", "Konto " + number + " lades till: " + name);
                    else if (before != name) Audit.log(companyId, this.account.id, "account", "Konto " + number + " bytte namn: " + before + " → " + name);
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
            set("balanceTitle", T("accounts.balance", year));
            rows("accounts", Db.forCompany("account", companyId).OrderBy(a => Db.str(a, "number")).Select(a => {
                string number = Db.str(a, "number");
                return row(("number", h(number)), ("name", h(Db.str(a, "name"))), ("balance", Money.formatOrBlank(balances.TryGetValue(number, out var b) ? b.closing : 0)));
            }));
            val("number", form("number"));
            val("name", form("name"));
        }
    }
}
