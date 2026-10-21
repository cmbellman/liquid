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
            title("Logga in");
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
                set("error", "För många misslyckade försök. Försök igen om 15 minuter.");
                return;
            }

            var user = findUser(email);
            if (user == null || !user.checkPassword(password)) {
                fail(ipKey);
                fail(emailKey);
                set("error", "Fel e-post eller lösenord.");
                return;
            }

            attempts.TryRemove(ipKey, out _);
            attempts.TryRemove(emailKey, out _);
            signIn(user);
            redirect(target);
        }

        void renderForm() {
            render(@"<section class='panel narrow'>
<h1>Logga in</h1>
<p class='lead'>Använd e-post och lösenord för ditt Swecial-konto.</p>
<form method='post'>"
+ field("E-post", input("email", email, "email", "autocomplete='username' required autofocus"))
+ field("Lösenord", input("password", "", "password", "autocomplete='current-password' required"))
+ @"<button type='submit'>Logga in</button>
</form>
<p class='links'><a href='" + BasePath + @"/signup.html'>Skapa konto</a></p>
</section>");
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
