using com.swecial.server;

namespace com.swecial.web {
    /// <summary>
    /// The look, chosen per user under Inställningar (stored on the user as liquidTheme) and set as
    /// a class on &lt;body&gt;: theme-neon (default, the pub look) or theme-nostromo (a green phosphor
    /// terminal). Themes only change style.css tokens and typography, never the markup.
    /// </summary>
    public static class Themes {
        public const string Neon = "neon";
        public const string Nostromo = "nostromo";
        const string Field = "liquidTheme";

        public static string of(SwecialUser user) {
            return user?.get<string>(Field) == Nostromo ? Nostromo : Neon;
        }

        public static void set(SwecialUser user, string theme) {
            string value = theme == Nostromo ? Nostromo : Neon;
            if (user.get<string>(Field) == value) return;
            user.set(Field, value);
            user.save();
        }
    }
}
