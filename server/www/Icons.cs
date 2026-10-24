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
        /// The logo's mark: two arms of liquid swirling around each other in a ball, each arm two bands,
        /// traced from the logo artwork. Every band has its own gradient, with stops measured along the
        /// band in the artwork and written as mixes of the theme's four logo colours (--logo-1 violet,
        /// --logo-2 magenta, --logo-3 orange, --logo-4 yellow in Boring), so the soft blends survive a
        /// recolouring: the left arm's outer band violet through magenta to pink and its inner band
        /// violet to magenta, the right arm's inner band yellow to orange and its outer band amber
        /// through orange and magenta to violet. Each arm is drawn whole with its
        /// outer band's gradient and the inner band on top, so the bands never leave a gap.
        /// favicon.svg, favicon.ico and apple-touch-icon.png are this mark in its own colours.
        /// </summary>
        public const string Logo = "<svg class='logo' viewBox='0 0 64 64' aria-hidden='true'>"
            + "<defs><linearGradient id='logo-p' x1='5' y1='26' x2='38.5' y2='15.5' gradientUnits='userSpaceOnUse'><stop offset='0' style='stop-color:color-mix(in srgb,var(--logo-1) 80%,var(--logo-2))'/><stop offset='0.17' style='stop-color:color-mix(in srgb,var(--logo-1) 30%,var(--logo-2))'/><stop offset='0.33' style='stop-color:color-mix(in srgb,var(--logo-1) 5%,var(--logo-2))'/><stop offset='0.5' style='stop-color:color-mix(in srgb,var(--logo-2) 85%,var(--logo-3))'/><stop offset='0.67' style='stop-color:color-mix(in srgb,var(--logo-2) 80%,var(--logo-3))'/><stop offset='0.83' style='stop-color:color-mix(in srgb,var(--logo-2) 70%,var(--logo-3))'/><stop offset='1' style='stop-color:color-mix(in srgb,var(--logo-2) 60%,var(--logo-3))'/></linearGradient>"
            + "<linearGradient id='logo-v' x1='10' y1='45' x2='31' y2='15' gradientUnits='userSpaceOnUse'><stop offset='0' style='stop-color:color-mix(in srgb,var(--logo-1) 95%,var(--logo-2))'/><stop offset='0.17' style='stop-color:var(--logo-1)'/><stop offset='0.33' style='stop-color:color-mix(in srgb,var(--logo-1) 95%,var(--logo-2))'/><stop offset='0.5' style='stop-color:color-mix(in srgb,var(--logo-1) 80%,var(--logo-2))'/><stop offset='0.67' style='stop-color:color-mix(in srgb,var(--logo-1) 65%,var(--logo-2))'/><stop offset='0.83' style='stop-color:color-mix(in srgb,var(--logo-1) 40%,var(--logo-2))'/><stop offset='1' style='stop-color:color-mix(in srgb,var(--logo-1) 20%,var(--logo-2))'/></linearGradient>"
            + "<linearGradient id='logo-o' x1='48.5' y1='10.6' x2='33.7' y2='50.2' gradientUnits='userSpaceOnUse'><stop offset='0' style='stop-color:color-mix(in srgb,var(--logo-3) 50%,var(--logo-4))'/><stop offset='0.17' style='stop-color:color-mix(in srgb,var(--logo-3) 65%,var(--logo-4))'/><stop offset='0.33' style='stop-color:color-mix(in srgb,var(--logo-3) 95%,var(--logo-4))'/><stop offset='0.5' style='stop-color:color-mix(in srgb,var(--logo-2) 30%,var(--logo-3))'/><stop offset='0.67' style='stop-color:color-mix(in srgb,var(--logo-2) 70%,var(--logo-3))'/><stop offset='0.83' style='stop-color:color-mix(in srgb,var(--logo-1) 20%,var(--logo-2))'/><stop offset='1' style='stop-color:color-mix(in srgb,var(--logo-1) 50%,var(--logo-2))'/></linearGradient>"
            + "<linearGradient id='logo-y' x1='41.7' y1='7.3' x2='29.1' y2='53.5' gradientUnits='userSpaceOnUse'><stop offset='0' style='stop-color:color-mix(in srgb,var(--logo-3) 20%,var(--logo-4))'/><stop offset='0.17' style='stop-color:var(--logo-4)'/><stop offset='0.33' style='stop-color:color-mix(in srgb,var(--logo-3) 10%,var(--logo-4))'/><stop offset='0.5' style='stop-color:color-mix(in srgb,var(--logo-3) 30%,var(--logo-4))'/><stop offset='0.67' style='stop-color:color-mix(in srgb,var(--logo-3) 60%,var(--logo-4))'/><stop offset='0.83' style='stop-color:color-mix(in srgb,var(--logo-3) 90%,var(--logo-4))'/><stop offset='1' style='stop-color:var(--logo-3)'/></linearGradient></defs>"
            + "<path fill='url(#logo-p)' d='M31.9 62.0C28.8 61.9 26.3 61.4 23.2 60.2C21.5 59.5 18.9 58.1 18.0 57.3C17.8 57.1 17.4 56.8 17.1 56.5C13.4 53.6 10.6 49.5 9.5 45.3C9.4 44.9 9.2 44.5 9.2 44.4C9.2 44.3 9.0 43.6 8.8 42.8C8.5 41.2 8.4 39.0 8.6 36.9C8.8 33.0 10.8 28.2 13.4 25.1C13.7 24.8 14.0 24.4 14.1 24.3C14.3 24.0 16.5 22.0 17.4 21.2C18.4 20.3 19.7 19.3 21.7 18.0C22.6 17.4 23.5 16.6 23.5 16.2C23.7 15.3 21.8 15.2 19.2 15.9C15.5 17.0 11.7 19.4 9.5 22.2C9.2 22.5 8.8 23.0 8.6 23.3C5.8 26.7 4.2 32.0 4.2 37.2C4.2 38.4 4.2 38.8 4.1 38.9C3.8 39.2 3.2 36.9 3.1 34.4C2.6 25.4 7.3 16.0 14.8 11.0C17.9 9.0 21.8 7.5 25.1 7.1C28.2 6.7 31.3 7.0 33.8 8.1C36.2 9.2 37.1 9.9 38.4 11.6C40.9 14.9 41.4 19.2 39.6 22.6C38.2 25.3 35.2 27.8 31.9 28.9C28.9 30.0 25.3 31.7 23.6 33.0C21.1 34.7 19.7 36.6 18.5 39.6C17.1 43.4 17.7 48.6 20.0 52.4C21.6 55.0 24.7 57.8 27.3 59.0C30.2 60.4 32.3 60.9 35.8 61.2C37.1 61.2 38.2 61.3 38.2 61.3C38.4 61.5 37.0 61.8 35.6 61.9C34.3 62.1 33.5 62.1 31.9 62.0Z'/>"
            + "<path fill='url(#logo-v)' d='M31.4 62.0C28.5 61.8 26.3 61.3 23.3 60.2C18.7 58.4 14.5 54.9 12.0 51.0C10.4 48.3 9.5 45.9 8.8 42.6C8.3 40.3 8.4 37.0 8.9 34.5C9.6 31.6 10.5 29.3 12.3 26.7C14.1 24.0 17.2 21.1 21.1 18.4C22.4 17.5 23.4 16.7 23.5 16.3C23.8 14.9 19.5 15.4 15.8 17.2C13.0 18.7 11.2 20.1 9.0 22.8C7.0 25.2 5.3 28.8 4.7 31.7C4.6 32.1 4.5 32.5 4.5 32.6C4.4 32.7 4.4 33.1 4.3 33.4C4.1 34.8 4.0 34.6 4.0 33.2C4.0 32.6 4.1 32.0 4.2 31.8C4.2 31.5 4.4 30.9 4.5 30.4C4.7 29.2 5.0 28.4 5.3 27.8C5.6 27.2 5.6 27.0 5.4 26.2C5.2 25.4 5.2 24.9 5.6 24.1C5.7 23.8 5.9 23.4 6.0 23.2C6.2 22.5 7.0 21.1 7.5 20.6C7.8 20.4 8.0 20.0 8.2 19.7C8.5 19.1 9.8 17.5 10.5 16.9C10.8 16.7 11.3 16.2 11.6 15.9C12.4 15.1 13.8 14.1 14.5 13.8C14.8 13.6 15.3 13.3 15.6 13.1C15.9 12.9 16.3 12.7 16.7 12.6C17.0 12.5 17.6 12.2 18.1 12.0C18.7 11.7 19.2 11.5 19.5 11.5C19.8 11.4 20.3 11.3 20.7 11.1C21.3 10.8 21.5 10.8 23.3 10.5C24.5 10.3 27.1 10.4 28.2 10.7C29.3 10.9 31.4 12.1 32.1 13.0C33.2 14.1 33.7 16.1 33.5 17.6C33.4 18.1 32.9 19.3 32.5 19.7C31.8 20.6 30.2 21.8 28.8 22.5C28.5 22.7 28.2 22.9 28.0 23.0C27.9 23.0 27.6 23.2 27.3 23.4C27.0 23.5 26.4 23.8 26.0 24.1C25.6 24.3 25.0 24.6 24.7 24.8C24.0 25.1 21.5 26.6 20.6 27.2C19.9 27.7 18.0 29.5 17.1 30.6C14.9 33.1 13.4 36.4 13.1 39.4C12.9 41.2 13.0 44.3 13.2 45.2C13.3 45.4 13.4 46.0 13.5 46.4C13.8 48.0 15.6 52.2 16.3 53.0C16.4 53.1 16.7 53.6 17.0 54.0C18.7 56.0 21.2 58.2 22.4 58.6C22.6 58.7 23.0 58.9 23.2 59.0C24.0 59.5 24.9 59.9 26.7 60.5C28.7 61.2 29.3 61.3 30.4 61.0C31.4 60.6 31.6 60.6 32.5 60.8C33.1 60.9 34.0 61.1 34.8 61.1C36.4 61.2 37.3 61.3 37.4 61.4C37.7 61.8 34.1 62.2 31.4 62.0Z'/>"
            + "<path fill='url(#logo-o)' d='M34.4 57.2C32.9 56.9 32.2 56.8 31.6 56.5C31.3 56.4 30.9 56.2 30.7 56.2C30.2 56.0 28.4 55.1 28.0 54.8C27.8 54.7 27.5 54.5 27.3 54.3C25.8 53.3 24.1 50.7 23.5 48.4C23.2 47.3 23.2 47.3 23.2 46.0L23.2 44.7L23.5 43.5C24.3 40.5 26.4 38.0 29.7 36.3C30.1 36.1 30.5 35.9 30.5 35.8C30.6 35.8 32.4 35.0 33.1 34.7C33.4 34.6 33.8 34.5 33.9 34.4C34.0 34.3 34.4 34.2 34.9 34.0C40.7 31.7 43.8 28.9 45.3 24.5C46.1 22.3 46.3 19.5 45.9 17.0C45.3 13.3 43.1 9.7 39.9 7.3C38.0 5.8 35.6 4.5 34.3 4.2C34.1 4.2 33.7 4.0 33.4 3.9C31.7 3.3 29.2 3.1 26.5 3.3C25.2 3.3 24.8 3.3 24.7 3.1C24.6 3.0 25.6 2.6 26.4 2.5C26.7 2.5 27.2 2.4 27.6 2.4C30.2 1.9 33.1 1.9 35.6 2.4C36.0 2.4 36.4 2.5 36.5 2.5C37.0 2.5 40.1 3.5 40.9 3.9C41.1 4.0 41.5 4.1 41.8 4.2C44.7 5.3 48.7 8.5 50.9 11.4C52.8 13.8 54.5 17.7 55.0 20.3C55.1 20.8 55.2 21.5 55.3 21.9C55.6 23.5 55.6 27.2 55.3 28.7C55.2 29.0 55.1 29.6 55.0 30.1C54.9 30.5 54.8 31.1 54.6 31.4C54.5 31.7 54.3 32.3 54.2 32.6C53.1 35.7 50.5 39.4 47.6 42.1C46.3 43.2 45.7 43.6 43.2 45.4C40.4 47.3 39.7 48.2 41.0 48.6C41.6 48.7 43.2 48.5 44.7 48.1C50.3 46.5 55.0 42.1 57.7 36.3C58.5 34.5 59.2 31.9 59.4 30.1C59.4 29.8 59.5 29.3 59.5 29.1C59.6 28.4 59.6 26.3 59.5 25.2C59.4 24.0 59.3 22.9 59.4 22.8C59.5 22.6 59.7 22.9 59.9 23.4C59.9 23.6 60.1 24.2 60.2 24.6C60.5 25.5 60.7 26.7 60.8 27.8C60.8 28.2 60.9 28.8 61.0 29.2C61.1 30.1 61.1 32.3 60.9 33.5C60.9 34.0 60.8 34.6 60.8 34.8C60.8 35.3 60.6 36.5 60.3 37.5C60.1 38.0 60.0 38.6 59.9 38.9C59.9 39.1 59.6 39.8 59.4 40.4C59.2 40.9 59.0 41.4 59.0 41.4C59.0 41.5 58.1 43.5 57.6 44.3C54.9 49.0 51.1 52.6 46.3 54.9C44.9 55.6 44.4 55.8 42.9 56.3C40.2 57.2 36.6 57.6 34.4 57.2Z'/>"
            + "<path fill='url(#logo-y)' d='M34.4 57.2C31.8 56.7 29.7 56.0 27.8 54.7C25.0 52.8 23.1 49.1 23.2 45.7C23.4 40.9 26.8 37.0 33.0 34.8C33.3 34.7 33.7 34.5 33.9 34.4C34.1 34.3 34.7 34.1 35.1 33.9C41.0 31.5 43.8 28.9 45.4 24.4C46.0 22.7 46.1 22.0 46.1 19.6C46.1 17.3 46.0 16.7 45.4 14.9C44.4 12.3 43.4 10.6 41.7 8.9C39.3 6.5 36.6 4.9 34.0 4.1C32.8 3.8 32.5 3.6 32.8 3.2C33.1 2.8 34.1 2.7 35.7 3.1C36.1 3.2 36.8 3.4 37.1 3.5C38.1 3.7 40.2 4.6 41.0 5.2C41.2 5.3 41.6 5.6 41.8 5.7C43.4 6.4 46.3 9.3 47.7 11.5C49.1 13.6 50.5 17.7 50.6 19.8C50.7 20.3 50.7 20.7 50.8 20.9C50.9 21.6 50.8 24.4 50.4 26.0C50.1 27.7 49.5 29.3 49.0 30.1C48.9 30.3 48.7 30.7 48.6 30.9C47.7 33.2 43.9 37.1 41.7 38.0C41.5 38.1 41.1 38.3 40.9 38.5C40.3 38.9 39.8 39.2 37.6 40.4C34.1 42.2 33.5 42.7 31.7 44.4C30.8 45.2 30.5 46.0 30.4 47.2C30.3 49.3 31.3 50.9 33.4 52.4L34.3 53.1L35.4 53.4C37.5 53.9 40.0 54.0 41.9 53.5C42.2 53.4 42.7 53.3 43.2 53.2C43.7 53.1 44.2 53.0 44.8 52.7C45.3 52.5 45.8 52.3 46.1 52.1C47.0 51.8 48.8 50.8 49.9 50.0C50.6 49.6 51.3 49.1 51.5 48.9C52.6 48.3 55.4 45.2 56.6 43.5C56.8 43.0 57.2 42.6 57.4 42.5C57.7 42.2 57.8 42.0 57.9 41.5C57.9 41.3 58.2 40.6 58.5 40.0C58.7 39.4 59.0 38.7 59.1 38.4C59.4 37.7 59.7 37.4 60.0 37.6C60.4 38.0 59.2 41.5 57.6 44.2C54.3 50.1 49.3 54.2 42.9 56.3C40.2 57.2 36.7 57.6 34.4 57.2Z'/></svg>";

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
