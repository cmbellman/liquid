using com.swecial.code;
using com.swecial.server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace com.swecial.web {
    /// <summary>
    /// Base class for the Liquid pages: session cookie, the signed-in user's company and theme, and
    /// the values every page template uses. Pages are templates (www/*.html, with the shell in
    /// partials/top.part and bottom.part) and a handler that only sets values:
    /// - &lt;%t.key%&gt; is a text from the user's theme (Themes), &lt;%icons.name%&gt; an icon;
    /// - lists are rows (row(), rows()) for &lt;swe:iterator&gt;, flags (bool) for &lt;swe:condition&gt;;
    /// - values are inserted raw: user text goes through val()/h(), never set() directly.
    /// A LiquidException from processPage is shown as the error, and renderAfterError() fills the
    /// form again with what was typed.
    /// </summary>
    public abstract class LiquidPage : WebPage {
        public const string BasePath = "/liquid";
        const string SessionCookie = "liquid_session";
        const string SessionField = "liquidWebToken";
        const int SessionDays = 30;

        protected SwecialUser account;
        protected bool isAdmin => account != null && account.isAdmin;
        protected SwecialObject company;
        protected Theme theme;
        /// <summary>The theme's texts for the templates (&lt;%t.key%&gt;), see Themes.tree.</summary>
        protected SwecialObject texts;
        bool redirected;

        public override void process() {
            account = getSessionUser();
            company = account != null ? Companies.forUser(account.id) : null;
            theme = Themes.get(Themes.of(account));
            texts = Themes.tree(theme);
            set("t", texts);
            set("theme", theme.id);
            string fonts = Themes.setting(theme, t => string.IsNullOrEmpty(t.fonts) ? null : t.fonts);
            set("themeFonts", fonts == null ? "" : "<link href='" + h(fonts) + "' rel='stylesheet'>");
            set("icons", iconSet());
            set("isAdmin", isAdmin);
            set("title", "Liquid");
            set("error", "");
            set("message", h(messageText(query("ok"))));
            val("userName", account?.fullName ?? account?.firstName ?? "");
            try {
                processPage();
            } catch (LiquidException ex) {
                set("error", h(ex.text(english)));
                if (!redirected) {
                    try {
                        renderAfterError();
                    } catch (LiquidException) {
                    }
                }
            }
            setNav();
        }

        public abstract void processPage();

        /// <summary>Sets the page's values again after a refused POST, so the form shows what was typed.</summary>
        protected virtual void renderAfterError() { }

        protected void title(string text) {
            set("title", h(text) + " · Liquid");
        }

        // --- Texts and values for the templates ---

        /// <summary>The theme's language is English (for messages from the books, which come in sv/en).</summary>
        protected bool english => Themes.setting(theme, t => t.language) == "en";

        /// <summary>
        /// The theme's text for a key, HTML-encoded. {0}, {1} … are filled with args, which are
        /// inserted as given: encode user text with h() first.
        /// </summary>
        public string T(string key, params object[] args) {
            string text = h(Themes.text(theme, key));
            return args.Length == 0 ? text : string.Format(text, args);
        }

        /// <summary>The theme's text as plain text (for title() and other places that encode).</summary>
        public string Tr(string key, params object[] args) {
            string text = Themes.text(theme, key);
            return args.Length == 0 ? text : string.Format(text, args);
        }

        /// <summary>A counted text: key.one for 1, key.many otherwise, with the count as {0} (and args after it).</summary>
        public string Tn(string key, long count, params object[] args) {
            var all = new object[args.Length + 1];
            all[0] = count;
            Array.Copy(args, 0, all, 1, args.Length);
            return T(key + (count == 1 ? ".one" : ".many"), all);
        }

        /// <summary>An icon for a place, as the theme wants it (e.g. "nav.bar").</summary>
        public string icon(string place, string fallback) {
            return Themes.icon(theme, place, fallback);
        }

        /// <summary>Icons the templates use, as &lt;%icons.name%&gt;.</summary>
        SwecialObject iconSet() {
            var o = new SwecialObject();
            o.set("drop", icon("pant.drop", "upload"));
            o.set("camera", icon("pant.camera", "camera"));
            o.set("ai", icon("ai", "sparkle"));
            var logo = Logo.of(company);
            o.set("company", logo != null ? Logo.svg(logo, "icon", Db.str(company, "name")) : Icons.Building);
            o.set("logout", icon("nav.logout", "logout"));
            o.set("menu", Icons.Menu);
            o.set("close", Icons.Cross);
            return o;
        }

        /// <summary>A user value for the template, HTML-encoded.</summary>
        protected void val(string key, string text) {
            set(key, h(text));
        }

        /// <summary>
        /// A row for a &lt;swe:iterator&gt;. Inside the loop the page's values are visible too
        /// (&lt;%t.key%&gt;), and the row's own as &lt;%current.key%&gt;. Values are inserted raw: h() user text.
        /// </summary>
        protected static SwecialObject row(params (string key, object value)[] values) {
            var o = new SwecialObject();
            foreach (var (key, value) in values) o.set(key, value ?? "");
            return o;
        }

        /// <summary>Rows for &lt;swe:iterator source="key"&gt;, and the flag keyAny for a condition around it.</summary>
        protected void rows(string key, IEnumerable<SwecialObject> items) {
            var list = items.ToList();
            set(key, list);
            set(key + "Any", list.Count > 0);
        }

        /// <summary>Options for a &lt;select&gt;: value, text, label (all encoded) and selected ("selected" or "").</summary>
        protected static List<SwecialObject> options(IEnumerable<(string value, string text)> items, string current) {
            return items.Select(o => row(("value", h(o.value)), ("text", h(o.text)), ("label", h(o.text)), ("selected", o.value == current ? "selected" : ""))).ToList();
        }

        protected static string selected(bool on) => on ? "selected" : "";
        protected static string isChecked(bool on) => on ? "checked" : "";

        /// <summary>The VAT rate options (25, 12, 6, 0 %).</summary>
        protected static List<SwecialObject> vatOptions(string current) {
            return options(Bas.VatRates.Select(r => (r.ToString(), r + " %")), current);
        }

        // --- Session ---

        SwecialUser getSessionUser() {
            string cookie = req.header.getCookie(SessionCookie);
            if (string.IsNullOrEmpty(cookie)) return null;
            int dot = cookie.IndexOf('.');
            if (dot <= 0) return null;
            string userId = cookie.Substring(0, dot);
            string token = cookie.Substring(dot + 1);
            var users = SwecialData.current?.users;
            if (users == null || token.Length == 0) return null;
            var user = users.getObject("_user", userId) as SwecialUser;
            if (user == null || user.get<string>(SessionField) != token || isAutoUser(user)) return null;
            return user;
        }

        protected void signIn(SwecialUser user) {
            string token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            user.set(SessionField, token);
            user.save();
            setSessionCookie(user.id + "." + token, DateTimeOffset.UtcNow.AddDays(SessionDays));
            account = user;
        }

        protected void signOut() {
            if (account != null) {
                account.remove(SessionField);
                account.save();
            }
            setSessionCookie("", DateTimeOffset.UtcNow.AddDays(-1));
            account = null;
        }

        void setSessionCookie(string value, DateTimeOffset expires) {
            string cookie = "Set-Cookie: " + SessionCookie + "=" + value + "; Path=" + BasePath
                + "; Expires=" + expires.ToUniversalTime().ToString("R") + "; HttpOnly; SameSite=Lax";
            if (req.isHttps) cookie += "; Secure";
            resp.header.add(cookie);
        }

        protected static bool isAutoUser(SwecialUser user) {
            return user.get<bool>("_autoUser");
        }

        protected static SwecialUser findUser(string email) {
            if (string.IsNullOrEmpty(email)) return null;
            var users = SwecialData.current?.users;
            var user = users?.getObject("_user", "_username", email) as SwecialUser;
            return user == null || isAutoUser(user) ? null : user;
        }

        protected static string normalizeEmail(string email) {
            return (email ?? "").Trim().ToLowerInvariant();
        }

        /// <summary>Signed in, or redirected to the login page. Returns false when the page should stop.</summary>
        protected bool requireAccount() {
            if (account != null) return true;
            string target = req.mappedPath + (string.IsNullOrEmpty(req.queryString) ? "" : "?" + req.queryString);
            redirect(BasePath + "/login.html?redirect=" + WebUtility.UrlEncode(target));
            return false;
        }

        /// <summary>Signed in with a company, or redirected to create one.</summary>
        protected bool requireCompany() {
            if (!requireAccount()) return false;
            if (company != null) return true;
            redirect(BasePath + "/company.html");
            return false;
        }

        protected string companyId => company?.id;

        // --- Requests and responses ---

        protected bool isPost => req.method == "POST";

        protected string form(string key) {
            if (!isPost || req.body?.form == null) return "";
            return req.body.form.get<string>(key) ?? "";
        }

        protected string query(string key) {
            return req.getQueryString(key) ?? "";
        }

        protected void redirect(string path) {
            redirected = true;
            resp.statusCode = 302;
            resp.header.add("Location: " + path);
            resp.binaryData = Encoding.UTF8.GetBytes("Redirecting...");
        }

        /// <summary>Only local Liquid paths, so ?redirect= can't send users to another site.</summary>
        protected static string safeRedirect(string path) {
            if (string.IsNullOrEmpty(path) || !path.StartsWith(BasePath + "/") || path.Contains("//") || path.Contains("\\")) {
                return BasePath + "/";
            }
            return path;
        }

        /// <summary>Messages shown after a redirect (?ok=key), so a reload doesn't repost a form.</summary>
        string messageText(string key) {
            switch (key) {
                case "saved": case "booked": case "reversed": case "sent": case "paid": case "unpaid": case "deleted": case "bagged": case "sorted": case "imported": case "registered": case "closed": case "reopened": case "renumbered":
                    return Tr("message." + key);
                default: return "";
            }
        }

        /// <summary>
        /// The receipt reader's last problem, if any (readerProblem and readerProblemText), for the
        /// pages where receipts come in. Cleared by the next reading that succeeds.
        /// </summary>
        protected void setReaderProblem() {
            string kind = Db.str(company, "readerProblem");
            set("readerProblem", kind.Length > 0 && ReceiptReader.enabled);
            set("readerProblemText", kind.Length > 0 ? T("reader.problem." + kind, h(Db.str(company, "readerProblemDetail"))) : "");
        }

        // --- Navigation (partials/top.part) ---

        void setNav() {
            val("brand", Themes.setting(theme, t => t.brand) ?? "liquid");
            set("logo", Icons.Logo);
            set("clock", Themes.setting(theme, t => t.clock) == true);
            set("signedIn", account != null);
            set("termsPending", account != null && !Terms.accepted(account) && !req.mappedPath.EndsWith("/terms.html") && !req.mappedPath.EndsWith("/dpa.html"));
            set("hasCompany", company != null);
            val("companyName", Db.str(company, "name"));
            var items = new List<SwecialObject>();
            int bag = 0;
            if (company != null) {
                bag = Expenses.inbox(companyId).Count;
                items.Add(navItem("/", "nav.bar", "bar", 0, "/index.html"));
                items.Add(navItem("/invoices.html", "nav.invoices", "invoice", 0, "/invoice.html", "/invoice-print.html", "/invoice-pdf.html"));
                items.Add(navItem("/expenses.html", "nav.expenses", "receipt", 0, "/expense.html"));
                items.Add(navItem("/pant.html", "nav.pant", "bag", bag));
                items.Add(navItem("/bank.html", "nav.bank", "wallet", 0));
                items.Add(navItem("/securities.html", "nav.securities", "barrel", 0));
                items.Add(navItem("/customers.html", "nav.customers", "people", 0));
                items.Add(navItem("/reports.html", "nav.reports", "chart", 0));
                items.Add(navItem("/closing.html", "nav.closing", "lock", 0));
                // Accounts and vouchers are there for those who want them, not needed day to day
                items.Add(navItem("/vouchers.html", "nav.advanced", "book", 0, "/voucher.html", "/accounts.html", "/rules.html", "/log.html", "/sie.html", "/archive.html"));
                items.Add(navItem("/company.html", "nav.settings", "gear", 0));
            }
            set("navBadgeAny", bag > 0);
            set("navBadge", bag.ToString());
            rows("navItems", items);
        }

        SwecialObject navItem(string path, string key, string iconName, int badge, params string[] related) {
            string href = BasePath + path;
            bool active = req.mappedPath == href || related.Any(r => req.mappedPath == BasePath + r);
            return row(("href", href), ("text", T(key)), ("icon", icon(key, iconName)), ("active", active ? "active" : ""),
                ("hasBadge", badge > 0), ("badge", badge.ToString()));
        }

        public static string h(string text) {
            return WebUtility.HtmlEncode(text ?? "");
        }

        /// <summary>For values inside URLs in markup.</summary>
        public static string u(string text) {
            return WebUtility.UrlEncode(text ?? "");
        }

        /// <summary>Account choices for voucher rows and reports, "1930 Företagskonto".</summary>
        protected IEnumerable<(string, string)> accountChoices() {
            yield return ("", "");
            foreach (var a in Db.forCompany("account", companyId).OrderBy(a => Db.str(a, "number"))) {
                yield return (Db.str(a, "number"), Db.str(a, "number") + " " + Db.str(a, "name"));
            }
        }

        protected string accountName(string number) {
            return Db.str(Bas.find(companyId, number), "name");
        }

    }
}
