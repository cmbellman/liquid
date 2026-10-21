using com.swecial.code;
using com.swecial.server;
using System.Linq;
using System.Text;

namespace com.swecial.web {
    /// <summary>/liquid/ : the company at a glance and what needs doing, in plain words.</summary>
    public class WebDashboard : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            title("Översikt");
            string today = Dates.today();
            string year = today.Substring(0, 4);
            string from = year + "-01-01", to = year + "-12-31";
            var balances = Reports.trialBalance(companyId, from, to).ToDictionary(b => b.account);
            long balance(string account) => balances.TryGetValue(account, out var b) ? b.closing : 0;
            long result = -balances.Values.Where(b => !Bas.isBalanceAccount(b.account)).Sum(b => b.closing);
            long vatToPay = Reports.vatReturn(companyId, from, to).Last().amount;

            var invoices = Db.forCompany("invoice", companyId);
            var unpaid = invoices.Where(i => Db.str(i, "status") == Invoicing.Sent).ToList();
            var overdue = unpaid.Where(i => string.CompareOrdinal(Db.str(i, "dueDate"), today) < 0).ToList();
            int drafts = invoices.Count(i => Db.str(i, "status") == Invoicing.Draft);
            var expenses = Expenses.list(companyId);
            int missingReceipts = expenses.Count(e => Db.str(e, "receiptFileId").Length == 0);

            var sb = new StringBuilder("<section class='panel'><h1>" + h(Db.str(company, "name")) + "</h1><div class='tiles'>");
            tile(sb, "På företagskontot", Money.format(balance(Bas.Bank)) + " kr", "enligt Liquid, <a href='" + BasePath + "/bank.html'>stäm av</a>");
            tile(sb, "Kunder är skyldiga", Money.format(unpaid.Sum(i => Db.num(i, "total"))) + " kr", unpaid.Count + " obetalda fakturor");
            tile(sb, "Resultat " + year, Money.format(result) + " kr", "intäkter minus kostnader, före skatt");
            tile(sb, "Moms " + year, Money.format(vatToPay) + " kr", vatToPay >= 0 ? "att betala hittills" : "att få tillbaka hittills");
            sb.Append("</div></section>");

            var todo = new StringBuilder();
            foreach (var i in overdue.OrderBy(i => Db.str(i, "dueDate"))) {
                todo.Append("<li><a href='" + BasePath + "/invoice.html?id=" + u(i.id) + "'>Faktura " + Db.num(i, "number") + " till " + h(Db.str(i, "customerName"))
                    + "</a> förföll " + h(Db.str(i, "dueDate")) + ". Har den betalats? Registrera betalningen, annars påminn kunden.</li>");
            }
            if (drafts > 0) todo.Append("<li><a href='" + BasePath + "/invoices.html'>" + drafts + " fakturautkast" + "</a> har inte skickats.</li>");
            if (missingReceipts > 0) todo.Append("<li><a href='" + BasePath + "/expenses.html'>" + missingReceipts + (missingReceipts == 1 ? " utgift saknar" : " utgifter saknar") + " kvitto.</a> Kvittot är bokföringens underlag och ska sparas i sju år.</li>");
            sb.Append("<section class='panel'><h2>Att göra</h2>");
            sb.Append(todo.Length > 0 ? "<ul class='todo'>" + todo + "</ul>" : "<p class='empty'>Inget just nu.</p>");
            sb.Append("<div class='actions'><a class='button' href='" + BasePath + "/expense.html'>Ny utgift</a>");
            sb.Append("<form method='post' action='" + BasePath + "/invoices.html'><button type='submit' class='secondary'>Ny faktura</button></form></div>");
            sb.Append("</section>");
            render(sb.ToString());
        }

        /// <summary>The note may hold markup (a link); label and value are escaped.</summary>
        static void tile(StringBuilder sb, string label, string value, string note) {
            sb.Append("<div class='tile'><small>" + h(label) + "</small><strong>" + h(value) + "</strong>" + (note.Length > 0 ? "<span>" + note + "</span>" : "") + "</div>");
        }
    }
}
