using com.swecial.code;
using com.swecial.server;
using System.Collections.Generic;
using System.Linq;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/log.html : the change log (Audit), newest first: when, who and what changed outside
    /// the vouchers. Read only.
    /// </summary>
    public class WebLog : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            title(Tr("log.title"));
            string who(SwecialObject e) {
                var user = Db.linkedUser(e, "user");
                return user != null ? ((user.firstName ?? "").Length > 0 ? user.firstName : user.get<string>("_username") ?? "") : Db.linkedId(e, "user");
            }
            rows("entries", Audit.entries(companyId).Select(e => row(("at", h(Db.str(e, "at"))), ("who", h(who(e))),
                ("what", h(Db.str(e, "text"))), ("class", Db.str(e, "action") == "admin" ? "off" : ""))));
        }
    }
}
