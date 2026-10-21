using com.swecial.code;
using com.swecial.server;
using System.Text.RegularExpressions;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/signup.html : creates a Swecial account (the global _user, same as the games'
    /// accounts) and signs in. The server's unique "name" is a generated handle, since Liquid users
    /// are known by their email; the whole typed name goes in firstName (SwecialUser.fullName).
    /// </summary>
    public class WebSignup : LiquidPage {
        const int MinPasswordLength = 8;

        string fullName = "";
        string email = "";

        public override void processPage() {
            title(Tr("signup.title"));
            if (account != null) {
                redirect(BasePath + "/");
                return;
            }
            if (isPost) {
                fullName = form("fullName").Trim();
                email = normalizeEmail(form("email"));
                string password = form("password");
                renderForm();
                if (fullName.Length == 0) throw new LiquidException("Fyll i ditt namn.");
                if (!isEmail(email)) throw new LiquidException("E-postadressen är inte giltig.");
                if (password.Length < MinPasswordLength) throw new LiquidException("Lösenordet måste vara minst " + MinPasswordLength + " tecken.");

                var users = SwecialData.current.getUsers();
                SwecialUser user;
                lock (Db.Sync) {
                    if (users.getObject("_user", "_username", email) != null) {
                        throw new LiquidException("Det finns redan ett konto med den e-postadressen. Logga in i stället.");
                    }
                    user = new SwecialUser();
                    user.set("_username", email);
                    user.set("_password", BCrypt.HashPassword(password, BCrypt.GenerateSalt()));
                    user.set("_emailVerified", false);
                    user.firstName = fullName;
                    user.spawn();
                    user.set("name", "liquid_" + user.id);
                    user.save();
                }
                signIn(user);
                redirect(BasePath + "/company.html");
                return;
            }
            renderForm();
        }

        void renderForm() {
            val("fullName", fullName);
            val("email", email);
            set("passwordLabel", T("signup.password", MinPasswordLength));
        }

        // System.Net.Mail isn't referenced by the server's cloud code compiler, so a plain pattern
        static bool isEmail(string email) {
            return email.Length <= 254 && Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        }
    }
}
