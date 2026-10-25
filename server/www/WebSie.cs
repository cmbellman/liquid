using com.swecial.code;
using com.swecial.server;
using System.Linq;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/sie.html : SIE 4. ?download=&lt;year&gt; gives the year's file (Sie.export, PC8); POST with
    /// a file imports one (Sie.import, mode=vouchers or closing) and shows its summary (?imported=&lt;id&gt;).
    /// </summary>
    public class WebSie : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            title(Tr("sie.title"));
            if (int.TryParse(query("download"), out int year) && Closing.years(companyId).Contains(year)) {
                string slug = new string(Db.str(company, "name").Where(char.IsLetterOrDigit).ToArray());
                resp.header.add("Content-Type: application/octet-stream");
                resp.header.add("Content-Disposition: attachment; filename=\"" + (slug.Length > 0 ? slug : "liquid") + "-" + year + ".se\"");
                resp.header.add("Cache-Control: no-store");
                resp.binaryData = Sie.encode(Sie.export(company, year));
                return;
            }
            if (isPost) {
                var file = req.body.fileList.FirstOrDefault(f => f.data != null && f.data.Length > 0)
                    ?? throw new LiquidException("Välj en SIE-fil.", "SELECT AN SIE FILE.");
                if (file.data.Length > 20_000_000) throw new LiquidException("Filen är för stor (högst 20 MB).", "FILE TOO LARGE (MAX 20 MB).");
                string mode = form("mode") == Sie.ClosingAsOpening ? Sie.ClosingAsOpening : Sie.WithVouchers;
                Sie.import(company, file.data, mode, account.id);
                var latest = Db.forCompany("sie_import", companyId).OrderByDescending(i => i.createdAt).First();
                redirect(BasePath + "/sie.html?imported=" + u(latest.id));
                return;
            }
            setValues();
        }

        protected override void renderAfterError() {
            setValues();
        }

        void setValues() {
            rows("years", Enumerable.Reverse(Closing.years(companyId)).Select(y => row(("year", y.ToString()),
                ("href", BasePath + "/sie.html?download=" + y), ("closed", Closing.isClosed(companyId, y) ? Tr("closing.closedShort") : ""))));
            var imported = Db.getOwned("sie_import", query("imported"), companyId);
            set("importedAny", imported != null);
            val("importedText", Db.str(imported, "summary"));
        }
    }
}
