using com.swecial.code;
using com.swecial.server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace com.swecial.web {
    /// <summary>
    /// Base class for the Liquid pages: session cookie, the signed-in user's company, the shared
    /// header and small HTML helpers. Everything set() into a template is inserted raw and re-scanned
    /// for template tags, so all user text must go through h().
    ///
    /// A page's template is a shell with &lt;%title%&gt;, &lt;%nav%&gt;, &lt;%error%&gt;, &lt;%message%&gt; and
    /// &lt;%content%&gt;; the page builds the content. A LiquidException from processPage is shown as the error.
    /// </summary>
    public abstract class LiquidPage : WebPage {
        public const string BasePath = "/liquid";
        const string SessionCookie = "liquid_session";
        const string SessionField = "liquidWebToken";
        const int SessionDays = 30;

        protected SwecialUser account;
        protected SwecialObject company;
        string content = "";
        bool redirected;

        public override void process() {
            account = getSessionUser();
            company = account != null ? Companies.forUser(account.id) : null;
            set("title", "Liquid");
            set("error", "");
            set("message", h(messageText(query("ok"))));
            try {
                processPage();
            } catch (LiquidException ex) {
                set("error", h(ex.Message));
                if (!redirected) {
                    try {
                        renderAfterError();
                    } catch (LiquidException) {
                    }
                }
            }
            set("content", content);
            set("nav", nav());
        }

        public abstract void processPage();

        /// <summary>Renders the page again after a refused POST, so the form shows what was typed.</summary>
        protected virtual void renderAfterError() { }

        protected void title(string text) {
            set("title", h(text) + " · Liquid");
        }

        protected void render(string html) {
            content = html;
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
        static string messageText(string key) {
            switch (key) {
                case "saved": return "Sparat.";
                case "booked": return "Verifikationen är bokförd.";
                case "reversed": return "Verifikationen är makulerad med en rättelse.";
                case "sent": return "Fakturan har fått sitt nummer. Den bokförs när betalningen kommer in.";
                case "paid": return "Betalningen är bokförd.";
                case "deleted": return "Borttaget.";
                case "bagged": return "I påsen. Sortera kvittona när det passar.";
                case "sorted": return "Sorterat och bokfört.";
                default: return "";
            }
        }

        // --- Markup ---

        string nav() {
            var sb = new StringBuilder();
            sb.Append("<a class='brand' href='" + BasePath + "/'>liquid</a>");
            if (account == null) return sb.ToString();
            if (company != null) {
                int bag = Expenses.inbox(companyId).Count;
                sb.Append("<nav>");
                sb.Append(link("/", "Baren", Icons.Bar, "", "/index.html"));
                sb.Append(link("/invoices.html", "Fakturor", Icons.Invoice, "", "/invoice.html", "/invoice-print.html"));
                sb.Append(link("/expenses.html", "Utgifter", Icons.Receipt, "", "/expense.html"));
                sb.Append(link("/pant.html", "Pantpåsen", Icons.Bag, bag > 0 ? bag.ToString() : ""));
                sb.Append(link("/bank.html", "Kassan", Icons.Wallet, ""));
                sb.Append(link("/customers.html", "Stamgästerna", Icons.People, ""));
                sb.Append(link("/reports.html", "Rapporter", Icons.Chart, ""));
                // Accounts and vouchers are there for those who want them, not needed day to day
                sb.Append(link("/vouchers.html", "Avancerat", Icons.Gear, "", "/voucher.html", "/accounts.html"));
                sb.Append("</nav>");
            }
            sb.Append("<div class='who'>");
            if (company != null) sb.Append("<a href='" + BasePath + "/company.html'>" + Icons.Building + "<span>" + h(Db.str(company, "name")) + "</span></a>");
            sb.Append("<a href='" + BasePath + "/login.html?logout=true'>" + Icons.Logout + "<span>Logga ut</span></a></div>");
            return sb.ToString();
        }

        /// <summary>A nav link with icon and an optional badge, active on its own page and the related pages.</summary>
        string link(string path, string text, string icon, string badge, params string[] related) {
            string href = BasePath + path;
            bool active = req.mappedPath == href || related.Any(r => req.mappedPath == BasePath + r);
            return "<a href='" + href + "'" + (active ? " class='active'" : "") + ">" + icon + "<span>" + text + "</span>"
                + (badge.Length > 0 ? "<b class='badge'>" + badge + "</b>" : "") + "</a>";
        }

        /// <summary>A page heading with the plain-words explanation under it.</summary>
        protected static string heading(string title, string subtitle, string actions = "") {
            return "<div class='heading'><div><h1>" + h(title) + "</h1>" + (subtitle.Length > 0 ? "<p class='sub'>" + h(subtitle) + "</p>" : "") + "</div>"
                + (actions.Length > 0 ? "<div class='heading-actions'>" + actions + "</div>" : "") + "</div>";
        }

        public static string h(string text) {
            return WebUtility.HtmlEncode(text ?? "");
        }

        /// <summary>For values inside URLs in markup.</summary>
        public static string u(string text) {
            return WebUtility.UrlEncode(text ?? "");
        }

        protected static string input(string name, string value, string type = "text", string extra = "") {
            return "<input type='" + type + "' name='" + name + "' value='" + h(value) + "'" + (extra.Length > 0 ? " " + extra : "") + ">";
        }

        protected static string field(string label, string control) {
            return "<label>" + h(label) + control + "</label>";
        }

        protected static string select(string name, IEnumerable<(string value, string text)> options, string selected, string extra = "") {
            var sb = new StringBuilder("<select name='" + name + "'" + (extra.Length > 0 ? " " + extra : "") + ">");
            foreach (var (value, text) in options) {
                sb.Append("<option value='" + h(value) + "'" + (value == selected ? " selected" : "") + ">" + h(text) + "</option>");
            }
            sb.Append("</select>");
            return sb.ToString();
        }

        /// <summary>Account picker for voucher rows, "1930 Företagskonto".</summary>
        protected IEnumerable<(string, string)> accountOptions() {
            yield return ("", "");
            foreach (var a in Db.forCompany("account", companyId).OrderBy(a => Db.str(a, "number"))) {
                yield return (Db.str(a, "number"), Db.str(a, "number") + " " + Db.str(a, "name"));
            }
        }

        protected string accountName(string number) {
            return Db.str(Bas.find(companyId, number), "name");
        }

        protected static string amountCell(long ore) {
            return "<td class='num'>" + Money.formatOrBlank(ore) + "</td>";
        }
    }
}
