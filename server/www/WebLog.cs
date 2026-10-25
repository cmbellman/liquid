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
            var names = new Dictionary<string, string>();
            string who(string id) {
                if (id.Length == 0) return "";
                if (!names.TryGetValue(id, out var name)) {
                    var user = SwecialData.current.getUsers()?.getObject("_user", id) as SwecialUser;
                    name = user == null ? id : (user.firstName ?? "").Length > 0 ? user.firstName : user.get<string>("_username");
                    names[id] = name;
                }
                return name;
            }
            rows("entries", Audit.entries(companyId).Select(e => row(("at", h(Db.str(e, "at"))), ("who", h(who(Db.str(e, "userId")))),
                ("what", h(Db.str(e, "text"))), ("class", Db.str(e, "action") == "admin" ? "off" : ""))));
        }
    }
}
