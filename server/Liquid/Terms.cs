using com.swecial.server;
using System;

namespace com.swecial.code {
    /// <summary>
    /// The terms of use and the data processing agreement (personuppgiftsbiträdesavtal), which is part
    /// of them. A user accepts the current version when signing up, or later from the banner every page
    /// shows until they have (stored on the user: liquidTermsVersion, liquidTermsAt; logged per company).
    /// Raise Version when the texts change in substance. The provider's details and where the data is
    /// kept come from the app's settings.json on the server (providerName, providerOrgNumber,
    /// providerAddress, providerEmail, dataLocation), so they aren't in the code.
    /// </summary>
    public static class Terms {
        public const string Version = "2026-10-25";

        static SwecialApplication.Settings settings => Db.app?.settings;

        public static string setting(string key, string fallback) {
            string value = (settings?.get<string>(key) ?? "").Trim();
            return value.Length > 0 ? value : fallback;
        }

        public static string providerName => setting("providerName", "[leverantörens namn]");
        public static string providerOrgNumber => setting("providerOrgNumber", "[organisationsnummer]");
        public static string providerAddress => setting("providerAddress", "[adress]");
        public static string providerEmail => setting("providerEmail", "[e-postadress]");
        public static string dataLocation => setting("dataLocation", "Sverige");

        public static bool accepted(SwecialUser user) {
            return user != null && (user.get<string>("liquidTermsVersion") ?? "") == Version;
        }

        public static void accept(SwecialUser user, string companyId) {
            user.set("liquidTermsVersion", Version);
            user.set("liquidTermsAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            user.save();
            if (companyId != null) Audit.log(companyId, user.id, "terms", "Användarvillkoren och personuppgiftsbiträdesavtalet (version " + Version + ") godkändes");
        }
    }
}
