using com.swecial.code;
using com.swecial.server;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/terms.html and /liquid/dpa.html : the terms of use and the data processing agreement
    /// (Terms), readable without signing in. POST action=accept accepts the current version.
    /// </summary>
    public class WebTerms : LiquidPage {
        public override void processPage() {
            bool dpa = req.mappedPath.EndsWith("/dpa.html");
            title(dpa ? "Personuppgiftsbiträdesavtal" : "Användarvillkor");
            if (isPost && form("action") == "accept") {
                if (!requireAccount()) return;
                Terms.accept(account, companyId);
                redirect(BasePath + (company != null ? "/" : "/company.html"));
                return;
            }
            set("version", Terms.Version);
            val("providerName", Terms.providerName);
            val("providerOrgNumber", Terms.providerOrgNumber);
            val("providerAddress", Terms.providerAddress);
            val("providerEmail", Terms.providerEmail);
            val("dataLocation", Terms.dataLocation);
            set("canAccept", account != null && !Terms.accepted(account));
            set("hasAccepted", account != null && Terms.accepted(account));
            set("acceptedAt", account != null ? h(account.get<string>("liquidTermsAt")) : "");
        }
    }
}
