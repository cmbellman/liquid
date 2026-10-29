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
                case "menu": return Menu;
                case "close": return Cross;
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
        /// outer band's gradient and the inner band on top. The seam between the bands runs from one
        /// tip of the arm to the other, so both bands taper all the way into the tips, and the inner
        /// band's outer edge is the arm's own curves. The outlines are smoothed and fitted with as few
        /// Bézier curves as a small tolerance allows.
        /// favicon.svg, favicon.ico and apple-touch-icon.png are this mark in its own colours.
        /// </summary>
        public const string Logo = "<svg class='logo' viewBox='0 0 64 64' aria-hidden='true'>"
            + "<defs><linearGradient id='logo-p' x1='5' y1='26' x2='38.5' y2='15.5' gradientUnits='userSpaceOnUse'><stop offset='0' style='stop-color:color-mix(in srgb,var(--logo-1) 80%,var(--logo-2))'/><stop offset='0.17' style='stop-color:color-mix(in srgb,var(--logo-1) 30%,var(--logo-2))'/><stop offset='0.33' style='stop-color:color-mix(in srgb,var(--logo-1) 5%,var(--logo-2))'/><stop offset='0.5' style='stop-color:color-mix(in srgb,var(--logo-2) 85%,var(--logo-3))'/><stop offset='0.67' style='stop-color:color-mix(in srgb,var(--logo-2) 80%,var(--logo-3))'/><stop offset='0.83' style='stop-color:color-mix(in srgb,var(--logo-2) 70%,var(--logo-3))'/><stop offset='1' style='stop-color:color-mix(in srgb,var(--logo-2) 60%,var(--logo-3))'/></linearGradient>"
            + "<linearGradient id='logo-v' x1='10' y1='45' x2='31' y2='15' gradientUnits='userSpaceOnUse'><stop offset='0' style='stop-color:color-mix(in srgb,var(--logo-1) 95%,var(--logo-2))'/><stop offset='0.17' style='stop-color:var(--logo-1)'/><stop offset='0.33' style='stop-color:color-mix(in srgb,var(--logo-1) 95%,var(--logo-2))'/><stop offset='0.5' style='stop-color:color-mix(in srgb,var(--logo-1) 80%,var(--logo-2))'/><stop offset='0.67' style='stop-color:color-mix(in srgb,var(--logo-1) 65%,var(--logo-2))'/><stop offset='0.83' style='stop-color:color-mix(in srgb,var(--logo-1) 40%,var(--logo-2))'/><stop offset='1' style='stop-color:color-mix(in srgb,var(--logo-1) 20%,var(--logo-2))'/></linearGradient>"
            + "<linearGradient id='logo-o' x1='48.5' y1='10.6' x2='33.7' y2='50.2' gradientUnits='userSpaceOnUse'><stop offset='0' style='stop-color:color-mix(in srgb,var(--logo-3) 50%,var(--logo-4))'/><stop offset='0.17' style='stop-color:color-mix(in srgb,var(--logo-3) 65%,var(--logo-4))'/><stop offset='0.33' style='stop-color:color-mix(in srgb,var(--logo-3) 95%,var(--logo-4))'/><stop offset='0.5' style='stop-color:color-mix(in srgb,var(--logo-2) 30%,var(--logo-3))'/><stop offset='0.67' style='stop-color:color-mix(in srgb,var(--logo-2) 70%,var(--logo-3))'/><stop offset='0.83' style='stop-color:color-mix(in srgb,var(--logo-1) 20%,var(--logo-2))'/><stop offset='1' style='stop-color:color-mix(in srgb,var(--logo-1) 50%,var(--logo-2))'/></linearGradient>"
            + "<linearGradient id='logo-y' x1='41.7' y1='7.3' x2='29.1' y2='53.5' gradientUnits='userSpaceOnUse'><stop offset='0' style='stop-color:color-mix(in srgb,var(--logo-3) 20%,var(--logo-4))'/><stop offset='0.17' style='stop-color:var(--logo-4)'/><stop offset='0.33' style='stop-color:color-mix(in srgb,var(--logo-3) 10%,var(--logo-4))'/><stop offset='0.5' style='stop-color:color-mix(in srgb,var(--logo-3) 30%,var(--logo-4))'/><stop offset='0.67' style='stop-color:color-mix(in srgb,var(--logo-3) 60%,var(--logo-4))'/><stop offset='0.83' style='stop-color:color-mix(in srgb,var(--logo-3) 90%,var(--logo-4))'/><stop offset='1' style='stop-color:var(--logo-3)'/></linearGradient>"
            + "<path id='logo-l' d='M4.09 39.28C3.68 39.06 3.47 37.52 3.38 37.01C1.13 24.96 8.92 12.27 20.33 8.23C22.31 7.53 24.35 7.04 26.45 6.93C29.92 6.74 33.47 7.48 36.34 9.52C38.55 11.09 39.92 13.68 40.44 16.29C40.84 18.25 40.58 20.37 39.75 22.20C35.83 30.85 23.56 28.60 19.10 38.19C18.02 40.49 17.63 42.92 17.79 45.45C18.07 49.96 20.47 53.87 23.84 56.76C26.56 59.08 30.55 60.72 34.12 61.02C35.10 61.11 36.09 61.17 37.07 61.17C37.53 61.18 38.00 61.10 38.46 61.13C38.59 61.14 38.61 61.19 38.71 61.26C38.40 61.74 35.38 61.92 34.67 61.97C19.25 63.17 5.49 49.75 8.98 34.07C9.86 30.13 11.94 26.36 14.81 23.52C16.29 22.07 17.84 20.69 19.51 19.47C20.45 18.78 21.48 18.17 22.38 17.42C22.69 17.16 23.03 16.86 23.20 16.49C23.52 15.76 22.66 15.50 22.11 15.47C20.92 15.41 19.72 15.72 18.59 16.08C14.14 17.48 10.35 20.50 7.75 24.36C5.51 27.69 4.36 31.81 4.18 35.80C4.13 36.85 4.24 37.90 4.22 38.95C4.21 39.11 4.17 39.16 4.09 39.28Z'/>"
            + "<path id='logo-r' d='M23.73 3.30C24.16 2.77 27.09 2.39 27.89 2.26C41.53 0.16 54.60 9.83 55.44 23.83C55.74 28.78 54.35 33.59 51.45 37.62C50.18 39.38 48.72 40.98 47.09 42.41C45.79 43.55 41.19 46.39 40.65 47.51C40.24 48.37 41.33 48.56 41.93 48.54C43.38 48.49 44.89 48.03 46.23 47.50C52.70 44.95 57.71 38.71 59.07 31.91C59.57 29.37 59.60 26.90 59.38 24.33C59.34 23.89 59.10 22.58 59.31 22.30C59.86 22.64 60.37 25.25 60.51 25.97C62.96 38.54 56.15 51.14 44.19 55.81C40.45 57.27 36.01 57.88 32.11 56.66C28.91 55.66 26.16 53.93 24.52 50.92C23.28 48.63 22.87 46.05 23.50 43.51C26.02 33.42 39.96 35.68 44.60 26.12C45.69 23.89 46.08 21.63 46.06 19.16C46.02 14.83 43.92 10.85 40.74 7.99C33.08 1.12 24.35 3.77 23.73 3.30Z'/>"
            + "<clipPath id='logo-lc'><use href='#logo-l'/></clipPath><clipPath id='logo-rc'><use href='#logo-r'/></clipPath></defs>"
            + "<use href='#logo-l' fill='url(#logo-p)'/>"
            + "<path clip-path='url(#logo-lc)' fill='url(#logo-v)' d='M4.09 39.28C4.00 37.83 3.74 36.40 3.77 34.94C3.80 32.66 4.25 30.44 4.76 28.22C5.22 26.16 5.70 24.06 6.60 22.14C9.12 16.75 14.41 13.03 19.92 11.18C21.13 10.77 22.46 10.30 23.75 10.25C24.52 10.23 26.96 10.40 27.68 10.58C31.01 11.43 34.09 13.99 32.24 17.72C29.32 23.59 19.12 27.31 15.17 33.56C10.89 40.31 12.90 49.83 18.23 55.38C20.22 57.45 22.71 58.95 25.37 59.98C29.03 61.40 34.83 61.51 38.71 61.26C38.40 61.74 35.38 61.92 34.67 61.97C19.25 63.17 5.49 49.75 8.98 34.07C9.86 30.13 11.94 26.36 14.81 23.52C16.29 22.07 17.84 20.69 19.51 19.47C20.45 18.78 21.48 18.17 22.38 17.42C22.69 17.16 23.03 16.86 23.20 16.49C23.52 15.76 22.66 15.50 22.11 15.47C20.92 15.41 19.72 15.72 18.59 16.08C14.14 17.48 10.35 20.50 7.75 24.36C5.51 27.69 4.36 31.81 4.18 35.80C4.13 36.85 4.24 37.90 4.22 38.95C4.21 39.11 4.17 39.16 4.09 39.28Z'/>"
            + "<use href='#logo-r' fill='url(#logo-o)'/>"
            + "<path clip-path='url(#logo-rc)' fill='url(#logo-y)' d='M23.73 3.30C28.06 2.78 33.35 2.44 37.61 3.65C39.20 4.10 40.69 4.91 42.07 5.82C48.26 9.91 51.45 17.32 50.57 24.64C49.33 34.78 38.72 37.62 32.98 44.57C32.04 45.71 31.18 47.06 31.17 48.59C31.16 49.97 31.91 51.16 32.98 51.99C34.71 53.33 36.89 53.76 39.03 53.76C39.57 53.76 40.10 53.89 40.64 53.85C42.14 53.72 43.67 53.11 45.06 52.56C51.00 50.19 55.72 45.73 58.59 40.03C59.33 38.57 60.02 37.04 60.42 35.45C61.00 33.09 61.04 30.69 60.77 28.29C60.64 27.20 60.07 23.24 59.51 22.46C59.46 22.39 59.36 22.37 59.31 22.30C59.86 22.64 60.37 25.25 60.51 25.97C62.96 38.54 56.15 51.14 44.19 55.81C40.45 57.27 36.01 57.88 32.11 56.66C28.91 55.66 26.16 53.93 24.52 50.92C23.28 48.63 22.87 46.05 23.50 43.51C26.02 33.42 39.96 35.68 44.60 26.12C45.69 23.89 46.08 21.63 46.06 19.16C46.02 14.83 43.92 10.85 40.74 7.99C33.08 1.12 24.35 3.77 23.73 3.30Z'/></svg>";

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
        public const string Menu = Open + "<path d='M4 7h16M4 12h16M4 17h16'/>" + Close;
        public const string Cross = Open + "<path d='M6 6l12 12M18 6L6 18'/>" + Close;
    }
}
