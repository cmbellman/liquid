namespace com.swecial.web {
    /// <summary>
    /// Line icons for the sidebar, inline SVG (the server doesn't serve image files from www/).
    /// 24×24, drawn with currentColor so they follow the link colour.
    /// </summary>
    public static class Icons {
        /// <summary>An icon by the name themes use (Theme.icons), null if unknown.</summary>
        public static string byName(string name) {
            switch (name) {
                case "bar": return Bar;
                case "console": return Console;
                case "invoice": return Invoice;
                case "receipt": return Receipt;
                case "bag": return Bag;
                case "crate": return Crate;
                case "wallet": return Wallet;
                case "barrel": return Barrel;
                case "vault": return Vault;
                case "people": return People;
                case "chart": return Chart;
                case "book": return Book;
                case "gear": return Gear;
                case "building": return Building;
                case "logout": return Logout;
                case "upload": return Upload;
                case "camera": return Camera;
                case "sparkle": return Sparkle;
                case "reticle": return Reticle;
                case "crosshair": return Crosshair;
                case "shredder": return Shredder;
                case "lock": return Lock;
                default: return null;
            }
        }

        /// <summary>
        /// The logo's mark: two arms of liquid swirling into the centre, each coloured from its outer
        /// end to its tip by a radial gradient (the radius falls along an arm). The colours come from
        /// the theme (--logo-1 violet arm, --logo-2 both tips, --logo-3 and --logo-4 the other arm), so
        /// each theme recolours it. Generated as an Archimedean spiral; favicon.ico is a simpler version.
        /// </summary>
        public const string Logo = "<svg class='logo' viewBox='0 0 64 64' aria-hidden='true'><defs>"
            + "<radialGradient id='logo-a' cx='32' cy='32' r='27' gradientUnits='userSpaceOnUse'><stop offset='0.1' style='stop-color:var(--logo-2)'/><stop offset='1' style='stop-color:var(--logo-1)'/></radialGradient>"
            + "<radialGradient id='logo-b' cx='32' cy='32' r='27' gradientUnits='userSpaceOnUse'><stop offset='0.1' style='stop-color:var(--logo-2)'/><stop offset='0.5' style='stop-color:var(--logo-3)'/><stop offset='1' style='stop-color:var(--logo-4)'/></radialGradient></defs>"
            + "<path fill='url(#logo-a)' d='M13.7 12.1L11.7 15.0L10.0 18.0L8.8 21.2L8.0 24.5L7.7 27.7L7.8 31.0L8.4 34.1L9.3 37.0L10.6 39.8L12.2 42.3L14.1 44.5L16.2 46.5L18.6 48.0L21.0 49.2L23.6 50.1L26.1 50.6L28.7 50.8L31.2 50.6L33.6 50.0L35.8 49.2L37.9 48.1L39.7 46.8L41.3 45.3L42.7 43.6L43.7 41.8L44.5 40.0L45.0 38.1L45.2 36.2L45.2 34.3L44.9 32.6L44.4 30.9L43.7 29.4L42.8 28.0L41.8 26.9L40.7 25.9L39.5 25.1L38.2 24.6L36.9 24.2L35.7 24.1L34.5 24.1L33.3 24.3L32.3 24.7L31.4 25.2L30.6 25.8L30.0 26.4L29.5 27.2L29.1 27.9L28.9 28.7A4.5 4.5 0 0 1 35.1 35.3L34.3 35.3L33.7 35.2L33.1 34.9L32.6 34.6L32.2 34.2L31.9 33.7L31.8 33.2L31.8 32.7L31.9 32.3L32.1 31.9L32.4 31.5L32.8 31.3L33.3 31.1L33.8 31.1L34.4 31.1L34.9 31.4L35.4 31.7L35.9 32.2L36.3 32.8L36.6 33.5L36.8 34.2L36.9 35.1L36.8 36.0L36.5 37.0L36.1 37.9L35.5 38.8L34.8 39.6L33.9 40.4L32.8 41.0L31.6 41.5L30.3 41.8L28.9 42.0L27.4 41.9L25.9 41.6L24.2 41.3L22.4 40.8L20.5 40.1L18.4 39.1L16.4 37.7L14.4 35.9L12.7 33.7L11.2 31.1L10.2 28.2L9.7 25.0L9.7 21.6L10.4 18.3L11.8 15.0L13.7 12.1Z'/>"
            + "<path fill='url(#logo-b)' d='M50.3 51.9L52.3 49.0L54.0 46.0L55.2 42.8L56.0 39.5L56.3 36.3L56.2 33.0L55.6 29.9L54.7 27.0L53.4 24.2L51.8 21.7L49.9 19.5L47.8 17.5L45.4 16.0L43.0 14.8L40.4 13.9L37.9 13.4L35.3 13.2L32.8 13.4L30.4 14.0L28.2 14.8L26.1 15.9L24.3 17.2L22.7 18.7L21.3 20.4L20.3 22.2L19.5 24.0L19.0 25.9L18.8 27.8L18.8 29.7L19.1 31.4L19.6 33.1L20.3 34.6L21.2 36.0L22.2 37.1L23.3 38.1L24.5 38.9L25.8 39.4L27.1 39.8L28.3 39.9L29.5 39.9L30.7 39.7L31.7 39.3L32.6 38.8L33.4 38.2L34.0 37.6L34.5 36.8L34.9 36.1L35.1 35.3A4.5 4.5 0 0 1 28.9 28.7L29.7 28.7L30.3 28.8L30.9 29.1L31.4 29.4L31.8 29.8L32.1 30.3L32.2 30.8L32.2 31.3L32.1 31.7L31.9 32.1L31.6 32.5L31.2 32.7L30.7 32.9L30.2 32.9L29.6 32.9L29.1 32.6L28.6 32.3L28.1 31.8L27.7 31.2L27.4 30.5L27.2 29.8L27.1 28.9L27.2 28.0L27.5 27.0L27.9 26.1L28.5 25.2L29.2 24.4L30.1 23.6L31.2 23.0L32.4 22.5L33.7 22.2L35.1 22.0L36.6 22.1L38.1 22.4L39.8 22.7L41.6 23.2L43.5 23.9L45.6 24.9L47.6 26.3L49.6 28.1L51.3 30.3L52.8 32.9L53.8 35.8L54.3 39.0L54.3 42.4L53.6 45.7L52.2 49.0L50.3 51.9Z'/></svg>";

        const string Open = "<svg class='icon' viewBox='0 0 24 24' aria-hidden='true' fill='none' stroke='currentColor' stroke-width='1.7' stroke-linecap='round' stroke-linejoin='round'>";
        const string Close = "</svg>";

        /// <summary>A beer mug.</summary>
        public const string Bar = Open + "<path d='M5 8h10v11a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2z'/><path d='M15 10h2.5a2 2 0 0 1 2 2v3a2 2 0 0 1-2 2H15'/><path d='M5 8a2.5 2.5 0 0 1 2.2-3.6A3 3 0 0 1 12.4 4a2.6 2.6 0 0 1 2.6 4'/><path d='M8.5 12v5M11.5 12v5'/>" + Close;
        /// <summary>A console screen with a readout (a ship's bridge).</summary>
        public const string Console = Open + "<rect x='3' y='4' width='18' height='12' rx='1'/><path d='M7 12l3-3 2 2 4-4'/><path d='M8 20h8M12 16v4'/>" + Close;
        /// <summary>A cargo crate.</summary>
        public const string Crate = Open + "<path d='M3 7.5L12 3l9 4.5v9L12 21l-9-4.5z'/><path d='M3 7.5l9 4.5 9-4.5M12 12v9'/><path d='M7.5 5.2l9 4.6'/>" + Close;
        public const string Invoice = Open + "<path d='M7 3h7l4 4v14H7a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1z'/><path d='M14 3v4h4'/><path d='M9 12h6M9 16h4'/>" + Close;
        /// <summary>A receipt with a torn edge.</summary>
        public const string Receipt = Open + "<path d='M6 3h12v18l-2-1.4-2 1.4-2-1.4-2 1.4-2-1.4L6 21z'/><path d='M9 8h6M9 12h6M9 16h3'/>" + Close;
        /// <summary>A bag for the receipts (Pantpåsen).</summary>
        public const string Bag = Open + "<path d='M5 8h14l-1.2 12a1 1 0 0 1-1 .9H7.2a1 1 0 0 1-1-.9z'/><path d='M9 8V6a3 3 0 0 1 6 0v2'/><path d='M10 13l1.5 1.5L14.5 11'/>" + Close;
        /// <summary>A barrel in the cellar (Källaren: the funds).</summary>
        public const string Barrel = Open + "<path d='M7 3h10c1.5 3 2 6 2 9s-.5 6-2 9H7c-1.5-3-2-6-2-9s.5-6 2-9z'/><path d='M5.4 8h13.2M5.4 16h13.2'/><path d='M12 11v2'/>" + Close;
        /// <summary>A safe (funds, in the terminal themes).</summary>
        public const string Vault = Open + "<rect x='3' y='4' width='18' height='15' rx='1.5'/><circle cx='12' cy='11.5' r='3.5'/><path d='M12 8v1.5M12 13.5V15M8.5 11.5H10M14 11.5h1.5M6 19v2M18 19v2'/>" + Close;
        public const string Wallet = Open + "<path d='M4 7a2 2 0 0 1 2-2h11v4'/><path d='M4 7v11a2 2 0 0 0 2 2h13V9H6a2 2 0 0 1-2-2z'/><circle cx='16' cy='14.5' r='1.2'/>" + Close;
        public const string People = Open + "<circle cx='9' cy='8' r='3.2'/><path d='M3.5 20a5.5 5.5 0 0 1 11 0'/><circle cx='17' cy='9' r='2.4'/><path d='M15.5 14.2A4.5 4.5 0 0 1 21 18.5'/>" + Close;
        public const string Chart = Open + "<path d='M4 20h16'/><path d='M4 16c3-6 5 2 8-4s4-6 8-7'/>" + Close;
        public const string Gear = Open + "<circle cx='12' cy='12' r='3'/><path d='M12 2.8v2.4M12 18.8v2.4M2.8 12h2.4M18.8 12h2.4M5.5 5.5l1.7 1.7M16.8 16.8l1.7 1.7M5.5 18.5l1.7-1.7M16.8 7.2l1.7-1.7'/>" + Close;
        /// <summary>An open ledger book (Avancerat: vouchers and accounts).</summary>
        public const string Book = Open + "<path d='M12 6c-2-1.5-5-2-8-1.5V19c3-.5 6 0 8 1.5 2-1.5 5-2 8-1.5V4.5c-3-.5-6 0-8 1.5z'/><path d='M12 6v14.5'/>" + Close;
        public const string Building = Open + "<path d='M5 21V5a1 1 0 0 1 1-1h8a1 1 0 0 1 1 1v16'/><path d='M15 10h3a1 1 0 0 1 1 1v10'/><path d='M3 21h18M8.5 8h3M8.5 12h3M8.5 16h3'/>" + Close;
        /// <summary>A padlock (the year-end closing locks the year).</summary>
        public const string Lock = Open + "<rect x='5' y='11' width='14' height='10' rx='1.5'/><path d='M8 11V8a4 4 0 0 1 8 0v3'/><path d='M12 15v2.5'/>" + Close;
        public const string Logout = Open + "<path d='M14 4h4a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1h-4'/><path d='M10 8l-4 4 4 4M6 12h9'/>" + Close;
        /// <summary>AI: a small sparkle.</summary>
        public const string Sparkle = Open + "<path d='M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8z'/><path d='M19 16l.7 1.8 1.8.7-1.8.7L19 21l-.7-1.8-1.8-.7 1.8-.7z'/>" + Close;
        public const string Upload = Open + "<path d='M12 16V4M7 9l5-5 5 5'/><path d='M4 16v3a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1v-3'/>" + Close;
        public const string Camera = Open + "<path d='M4 8a1 1 0 0 1 1-1h3l1.5-2h5L16 7h3a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1z'/><circle cx='12' cy='13' r='3.5'/>" + Close;
        /// <summary>An AI sensor / targeting reticle.</summary>
        public const string Reticle = Open + "<circle cx='12' cy='12' r='8'/><circle cx='12' cy='12' r='2.5'/><path d='M12 2v3M12 19v3M2 12h3M19 12h3'/>" + Close;
        /// <summary>Target acquisition crosshairs.</summary>
        public const string Crosshair = Open + "<circle cx='12' cy='12' r='7'/><path d='M12 2v4M12 18v4M2 12h4M18 12h4M10 12h4M12 10v4'/>" + Close;
        /// <summary>Document shredder / assimilation port.</summary>
        public const string Shredder = Open + "<path d='M7 3h7l3 3v4H7z'/><rect x='3' y='10' width='18' height='5' rx='1'/><path d='M6 15v5M9 15v6M12 15v4M15 15v6M18 15v5'/>" + Close;
    }
}
