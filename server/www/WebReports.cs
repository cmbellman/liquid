using com.swecial.code;
using com.swecial.server;
using System.Linq;
using System.Text;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/reports.html?report=balance|ledger|vat&amp;from=&amp;to=&amp;account= : trial balance,
    /// general ledger for one account and the VAT return boxes. The period defaults to this year.
    /// </summary>
    public class WebReports : LiquidPage {
        string report, from, to, accountNumber;

        public override void processPage() {
            if (!requireCompany()) return;
            title("Rapporter");
            string year = Dates.today().Substring(0, 4);
            report = query("report");
            if (report != "ledger" && report != "vat") report = "balance";
            from = query("from").Length > 0 ? query("from") : year + "-01-01";
            to = query("to").Length > 0 ? query("to") : year + "-12-31";
            accountNumber = query("account");

            renderFilter();
            from = Dates.clean(from);
            to = Dates.clean(to);
            if (string.CompareOrdinal(from, to) > 0) throw new LiquidException("Från-datum är efter till-datum.");

            var sb = new StringBuilder(filter());
            if (report == "balance") sb.Append(balance());
            else if (report == "ledger") sb.Append(ledger());
            else sb.Append(vat());
            render(sb.ToString());
        }

        void renderFilter() {
            render(filter());
        }

        string filter() {
            var sb = new StringBuilder("<section class='panel'><h1>Rapporter</h1><form method='get' class='grid'>");
            sb.Append(field("Rapport", select("report", new[] { ("balance", "Saldobalans"), ("ledger", "Huvudbok"), ("vat", "Momsrapport") }, report)));
            sb.Append(field("Från", input("from", from, "date", "required")));
            sb.Append(field("Till", input("to", to, "date", "required")));
            if (report == "ledger") sb.Append(field("Konto", select("account", accountOptions(), accountNumber)));
            sb.Append("<div class='actions'><button type='submit'>Visa</button></div></form></section>");
            return sb.ToString();
        }

        string balance() {
            var rows = Reports.trialBalance(companyId, from, to);
            var sb = new StringBuilder("<section class='panel'><h2>Saldobalans " + h(from) + " – " + h(to) + "</h2>");
            sb.Append("<table><thead><tr><th>Konto</th><th>Namn</th><th class='num'>Ingående</th><th class='num'>Debet</th><th class='num'>Kredit</th><th class='num'>Utgående</th></tr></thead><tbody>");
            foreach (var b in rows) {
                sb.Append("<tr><td><a href='" + ledgerLink(b.account) + "'>" + h(b.account) + "</a></td><td>" + h(accountName(b.account)) + "</td>");
                sb.Append(amountCell(b.opening) + amountCell(b.debit) + amountCell(b.credit) + "<td class='num'>" + Money.format(b.closing) + "</td></tr>");
            }
            long result = -rows.Where(b => !Bas.isBalanceAccount(b.account)).Sum(b => b.closing);
            sb.Append("</tbody><tfoot>");
            sb.Append("<tr class='total'><td colspan='3'>Summa</td>" + amountCell(rows.Sum(b => b.debit)) + amountCell(rows.Sum(b => b.credit)) + "<td class='num'>" + Money.format(rows.Sum(b => b.closing)) + "</td></tr>");
            sb.Append("<tr><td colspan='5'>Periodens resultat (intäkter − kostnader)</td><td class='num'>" + Money.format(result) + "</td></tr>");
            sb.Append("</tfoot></table></section>");
            return sb.ToString();
        }

        string ledger() {
            if (accountNumber.Length == 0) return "<section class='panel'><p class='empty'>Välj ett konto.</p></section>";
            var lines = Reports.generalLedger(companyId, accountNumber, from, to, out long opening);
            var sb = new StringBuilder("<section class='panel'><h2>Huvudbok " + h(accountNumber) + " " + h(accountName(accountNumber)) + "</h2>");
            sb.Append("<table><thead><tr><th>Ver</th><th>Datum</th><th>Text</th><th class='num'>Debet</th><th class='num'>Kredit</th><th class='num'>Saldo</th></tr></thead><tbody>");
            sb.Append("<tr><td></td><td>" + h(from) + "</td><td>Ingående saldo</td><td></td><td></td><td class='num'>" + Money.format(opening) + "</td></tr>");
            foreach (var line in lines) {
                sb.Append("<tr><td><a href='" + BasePath + "/voucher.html?id=" + u(line.voucher.id) + "'>" + h(Ledger.label(line.voucher)) + "</a></td>");
                sb.Append("<td>" + h(Db.str(line.voucher, "date")) + "</td><td>" + h(Db.str(line.voucher, "text")) + "</td>");
                sb.Append(amountCell(line.debit) + amountCell(line.credit) + "<td class='num'>" + Money.format(line.balance) + "</td></tr>");
            }
            long closing = lines.Count > 0 ? lines.Last().balance : opening;
            sb.Append("</tbody><tfoot><tr class='total'><td colspan='3'>Utgående saldo</td>" + amountCell(lines.Sum(l => l.debit)) + amountCell(lines.Sum(l => l.credit)));
            sb.Append("<td class='num'>" + Money.format(closing) + "</td></tr></tfoot></table></section>");
            return sb.ToString();
        }

        string vat() {
            var sb = new StringBuilder("<section class='panel'><h2>Momsrapport " + h(from) + " – " + h(to) + "</h2>");
            sb.Append("<table><thead><tr><th>Ruta</th><th>Beskrivning</th><th class='num'>Belopp</th></tr></thead><tbody>");
            foreach (var (box, label, amount) in Reports.vatReturn(companyId, from, to)) {
                sb.Append("<tr" + (box == "49" ? " class='total'" : "") + "><td>" + box + "</td><td>" + h(label) + "</td><td class='num'>" + Money.format(amount) + "</td></tr>");
            }
            sb.Append("</tbody></table>");
            sb.Append("<p class='hint'>Underlag för momsdeklarationen, beräknat från bokförda konton. Rutorna till Skatteverket avrundas till hela kronor.</p></section>");
            return sb.ToString();
        }

        string ledgerLink(string account) {
            return BasePath + "/reports.html?report=ledger&account=" + u(account) + "&from=" + u(from) + "&to=" + u(to);
        }
    }
}
