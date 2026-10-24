using com.swecial.code;
using com.swecial.server;
using System.Linq;
using System.Text;

namespace com.swecial.web {
    /// <summary>/liquid/vouchers.html : the voucher journal (verifikationslista), newest first.</summary>
    public class WebVouchers : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            if (isPost && form("action") == "renumberAll") {
                if (!isAdmin) throw new LiquidException("Bara administratörer får omnumrera verifikationer.", "ONLY ADMINISTRATORS MAY RENUMBER VOUCHERS.");
                Ledger.renumberAllSeries(companyId);
                redirect(BasePath + "/vouchers.html?ok=renumbered");
                return;
            }
            title(Tr("vouchers.title"));
            var vouchers = Ledger.vouchers(companyId);
            vouchers.Reverse();
            rows("vouchers", vouchers.Select(v => {
                bool reversed = Db.str(v, "reversedById").Length > 0;
                return row(("id", u(v.id)), ("label", h(Ledger.label(v))), ("date", h(Db.str(v, "date"))), ("text", h(Db.str(v, "text"))),
                    ("reversed", reversed), ("class", reversed ? "reversed" : ""), ("total", Money.formatOrBlank(Db.num(v, "total"))));
            }));
        }
    }
}
