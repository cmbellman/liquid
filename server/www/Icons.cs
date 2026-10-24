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
        /// The logo's mark: two arms of liquid swirling around each other in a ball, traced from the
        /// logo artwork. Each arm has its own gradient, coloured by the theme: --logo-1 to --logo-2 for
        /// the left arm, --logo-4 through --logo-3 to --logo-2 for the right one. favicon.ico is a
        /// simpler version.
        /// </summary>
        public const string Logo = "<svg class='logo' viewBox='0 0 64 64' aria-hidden='true'><defs>"
            + "<linearGradient id='logo-a' x1='6' y1='44' x2='40' y2='12' gradientUnits='userSpaceOnUse'><stop offset='0' style='stop-color:var(--logo-1)'/><stop offset='1' style='stop-color:var(--logo-2)'/></linearGradient>"
            + "<linearGradient id='logo-b' x1='44' y1='6' x2='26' y2='56' gradientUnits='userSpaceOnUse'><stop offset='0' style='stop-color:var(--logo-4)'/><stop offset='0.55' style='stop-color:var(--logo-3)'/><stop offset='1' style='stop-color:var(--logo-2)'/></linearGradient></defs>"
            + "<path fill='url(#logo-a)' d='M31.9 62.0C28.7 61.8 26.6 61.4 23.4 60.2C19.6 58.7 15.7 55.8 13.3 52.7C13.0 52.4 12.7 52.0 12.7 51.9C12.4 51.6 11.1 49.2 10.7 48.4C10.3 47.5 9.5 45.6 9.4 45.1C9.4 44.9 9.3 44.7 9.3 44.5C9.2 44.1 9.0 43.6 8.7 42.1C8.5 41.0 8.5 36.3 8.7 35.5C8.7 35.3 8.9 34.7 9.0 34.2C9.5 31.7 10.6 29.1 12.2 26.7C12.6 26.1 12.9 25.7 14.0 24.4C14.5 23.8 16.1 22.2 16.8 21.7C17.1 21.5 17.3 21.3 17.4 21.2C17.8 20.7 19.6 19.4 21.5 18.1C22.7 17.3 23.6 16.5 23.6 16.1C23.6 15.7 23.1 15.5 22.2 15.5C21.7 15.5 19.8 15.7 19.7 15.8C19.7 15.8 19.2 16.0 18.7 16.1C15.6 17.1 12.7 18.8 10.6 21.0C8.0 23.7 6.6 26.1 5.4 29.4C5.2 30.1 5.0 31.0 4.9 31.3C4.8 31.6 4.7 32.1 4.7 32.2C4.4 33.2 4.3 35.1 4.3 37.4C4.3 38.6 4.3 39.6 4.2 39.6C4.1 39.7 3.7 38.5 3.4 36.7C3.2 35.6 3.0 33.0 3.1 32.0C3.1 31.6 3.2 31.0 3.2 30.7C3.3 28.8 3.9 26.1 4.6 24.4C4.7 24.1 4.9 23.7 4.9 23.5C5.8 21.1 7.3 18.5 9.1 16.2C11.0 13.9 13.2 11.9 15.9 10.3C17.7 9.2 19.0 8.6 21.5 7.9C24.5 6.9 28.6 6.6 31.0 7.2C32.6 7.6 32.8 7.7 33.4 8.0C34.3 8.3 36.0 9.2 36.5 9.6C37.4 10.3 38.1 11.1 39.1 12.5C39.4 12.9 40.0 14.5 40.2 15.1C40.8 17.7 40.9 18.7 40.3 20.7C40.2 21.0 40.2 21.3 40.2 21.4C40.2 21.7 39.2 23.4 38.7 24.1C37.2 26.1 34.6 27.9 31.9 28.9C31.5 29.1 31.1 29.2 30.9 29.3C30.7 29.4 30.3 29.6 30.0 29.7C26.3 31.1 23.0 33.0 21.4 34.8C20.3 36.0 19.2 37.8 18.7 39.2C18.0 41.3 17.9 41.9 17.9 44.4C17.9 46.7 18.0 47.3 18.5 48.9C19.4 51.6 20.6 53.5 22.6 55.5C25.0 57.9 26.5 58.8 29.7 59.9C30.5 60.2 32.1 60.7 32.2 60.7C32.3 60.7 32.6 60.7 32.9 60.8C33.7 61.0 36.0 61.1 37.6 61.1C38.3 61.0 38.9 61.0 39.0 61.1C39.2 61.1 39.2 61.1 39.0 61.3C38.5 61.6 34.6 62.1 33.3 62.1C33.2 62.1 32.6 62.0 31.9 62.0Z'/>"
            + "<path fill='url(#logo-b)' d='M34.4 57.1C34.1 57.1 33.5 57.0 33.3 56.9C32.8 56.9 32.5 56.8 30.7 56.1C28.6 55.4 26.6 53.9 25.3 52.2C24.8 51.4 24.0 49.8 23.8 49.3C23.8 49.0 23.7 48.7 23.6 48.5C23.1 46.9 23.1 45.3 23.6 43.4C23.9 42.2 23.9 42.1 24.5 41.0C25.7 38.9 27.4 37.4 30.7 35.8C31.2 35.5 32.9 34.8 33.2 34.7C33.3 34.6 33.6 34.5 33.8 34.4C34.0 34.3 34.4 34.1 34.7 34.0C36.0 33.5 38.6 32.3 39.6 31.6C41.1 30.7 43.0 28.9 43.6 27.9C43.6 27.8 43.8 27.6 44.0 27.4C44.7 26.4 45.6 23.9 45.9 22.2C46.1 21.3 46.1 18.4 46.0 17.6C45.8 16.4 45.7 15.8 45.5 15.3C45.4 14.9 45.2 14.6 45.2 14.4C45.1 14.1 44.2 12.2 43.9 11.6C43.7 11.4 43.4 11.0 43.3 10.7C43.0 10.3 42.5 9.8 41.8 9.0C40.1 7.3 38.6 6.1 36.7 5.2C36.3 4.9 34.5 4.2 34.3 4.2C34.3 4.2 33.9 4.1 33.4 3.9C31.7 3.4 31.2 3.3 28.7 3.2C27.5 3.2 24.9 3.3 24.1 3.4C23.3 3.6 23.5 3.3 24.4 3.0C25.2 2.8 26.0 2.6 26.5 2.5C26.7 2.5 27.2 2.4 27.6 2.4C30.1 1.9 33.2 1.9 35.6 2.4C35.9 2.4 36.4 2.5 36.6 2.5C37.4 2.6 40.1 3.4 40.9 3.9C41.1 4.0 41.4 4.1 41.7 4.2C43.0 4.7 45.6 6.3 46.9 7.4C50.1 10.0 51.7 12.1 53.4 15.7C54.2 17.2 55.1 20.3 55.1 21.1C55.1 21.2 55.2 21.6 55.3 21.9C55.5 22.8 55.6 25.2 55.5 26.6C55.4 28.3 54.9 30.7 54.6 31.6C54.4 31.9 54.2 32.4 54.1 32.7C52.6 36.6 50.1 40.0 46.2 43.2C45.8 43.5 45.0 44.1 43.1 45.4C39.9 47.6 39.5 48.5 41.7 48.5C42.8 48.5 44.8 48.1 46.1 47.5C47.3 47.0 47.5 46.9 47.8 46.8C47.9 46.7 48.3 46.5 48.6 46.4C51.6 44.8 55.1 41.3 56.9 37.8C57.3 37.1 58.2 35.0 58.2 34.9C58.2 34.8 58.4 34.2 58.6 33.6C58.8 32.9 59.1 32.1 59.1 31.7C59.2 31.4 59.2 30.9 59.3 30.7C59.5 29.1 59.6 26.8 59.5 25.4C59.4 24.6 59.3 23.3 59.2 22.7C59.2 22.3 59.2 22.2 59.3 22.2C59.4 22.2 59.8 23.0 59.9 23.5C59.9 23.7 60.1 24.3 60.2 24.7C60.5 25.7 60.7 26.7 60.8 27.7C60.8 28.2 60.9 28.8 60.9 29.1C61.1 29.9 61.1 32.4 60.9 33.2C60.9 33.5 60.8 34.2 60.8 34.7C60.7 35.8 60.6 36.2 60.1 38.1C60.0 38.4 59.9 38.7 59.9 38.7C59.9 38.9 59.5 40.0 59.3 40.6C59.2 40.8 59.1 41.1 59.0 41.2C58.9 41.8 57.6 44.3 56.7 45.6C55.2 48.0 53.1 50.3 50.8 52.1C49.1 53.5 45.5 55.4 43.6 56.0C43.4 56.1 42.8 56.3 42.4 56.4C41.5 56.7 40.8 56.9 39.5 57.0C39.0 57.1 38.4 57.2 38.2 57.2C37.4 57.3 35.2 57.3 34.4 57.1Z'/></svg>";

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
