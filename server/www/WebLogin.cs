using com.swecial.server;
using System;
using System.Collections.Concurrent;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/login.html : sign in with a Swecial account (email + password). Same lockout as Big
    /// Ring and the server admin: 5 failures per IP or per email lock it for 15 minutes.
    /// ?logout=true signs out. ?redirect= returns to a Liquid page.
    /// </summary>
    public class WebLogin : LiquidPage {
        const int MaxAttempts = 5;
        static readonly TimeSpan LockTime = TimeSpan.FromMinutes(15);
        static readonly ConcurrentDictionary<string, (int attempts, DateTime lockUntil)> attempts =
            new ConcurrentDictionary<string, (int attempts, DateTime lockUntil)>();

        string email = "";

        public override void processPage() {
            title(Tr("login.title"));
            string target = safeRedirect(query("redirect"));

            if (query("logout").Length > 0) {
                signOut();
                redirect(BasePath + "/login.html");
                return;
            }
            if (!isPost) {
                if (account != null) {
                    redirect(target);
                    return;
                }
                renderForm();
                return;
            }

            email = normalizeEmail(form("email"));
            string password = form("password").Trim();
            renderForm();

            string ipKey = "ip:" + (req.ip ?? "unknown");
            string emailKey = "email:" + email;
            if (isLocked(ipKey) || isLocked(emailKey)) {
                set("error", T("login.locked"));
                return;
            }

            var user = findUser(email);
            if (user == null || !user.checkPassword(password)) {
                fail(ipKey);
                fail(emailKey);
                set("error", T("login.wrong"));
                return;
            }

            attempts.TryRemove(ipKey, out _);
            attempts.TryRemove(emailKey, out _);
            signIn(user);
            redirect(target);
        }

        void renderForm() {
            val("email", email);
        }

        static bool isLocked(string key) {
            return attempts.TryGetValue(key, out var info) && DateTime.UtcNow < info.lockUntil;
        }

        static void fail(string key) {
            attempts.AddOrUpdate(key,
                _ => (1, DateTime.MinValue),
                (_, old) => {
                    int count = DateTime.UtcNow >= old.lockUntil && old.lockUntil != DateTime.MinValue ? 1 : old.attempts + 1;
                    return (count, count >= MaxAttempts ? DateTime.UtcNow + LockTime : DateTime.MinValue);
                });
        }
    }
}
