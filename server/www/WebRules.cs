using com.swecial.code;
using com.swecial.server;
using System.Linq;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/rules.html : the figures from laws and Skatteverket that Liquid uses (Rules), with
    /// every value's period, source and how far it has been checked. Read only: the table lives in code.
    /// </summary>
    public class WebRules : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            title(Tr("rules.title"));
            string today = Dates.today();
            rows("rules", Rules.All.Select(r => {
                bool link = r.source.StartsWith("http");
                return row(("name", T("rules." + r.key)), ("source", h(r.source)), ("link", link), ("ok", Rules.isChecked(r.key, today)),
                    ("values", r.values.Select(x => row(
                        ("period", h(x.from) + " – " + (x.to.Length > 0 ? h(x.to) : T("rules.open"))),
                        ("value", r.unit == Rules.Percent ? x.value + " %" : Money.whole(x.value) + " kr"),
                        ("checked", x.checkedUntil.Length > 0 ? T("rules.checkedUntil", h(x.checkedUntil)) : T("rules.notChecked")),
                        ("class", (Rules.at(r.key, today) == x ? "current " : "") + (x.checkedUntil.Length == 0 ? "off" : "")))).ToList()));
            }));
        }
    }
}
