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
        /// outer band's gradient and the inner band on top, clipped to the arm, so the bands never
        /// leave a gap or stick out. The outlines are smoothed and fitted with as few Bézier curves as
        /// a small tolerance allows.
        /// favicon.svg, favicon.ico and apple-touch-icon.png are this mark in its own colours.
        /// </summary>
        public const string Logo = "<svg class='logo' viewBox='0 0 64 64' aria-hidden='true'>"
            + "<defs><linearGradient id='logo-p' x1='5' y1='26' x2='38.5' y2='15.5' gradientUnits='userSpaceOnUse'><stop offset='0' style='stop-color:color-mix(in srgb,var(--logo-1) 80%,var(--logo-2))'/><stop offset='0.17' style='stop-color:color-mix(in srgb,var(--logo-1) 30%,var(--logo-2))'/><stop offset='0.33' style='stop-color:color-mix(in srgb,var(--logo-1) 5%,var(--logo-2))'/><stop offset='0.5' style='stop-color:color-mix(in srgb,var(--logo-2) 85%,var(--logo-3))'/><stop offset='0.67' style='stop-color:color-mix(in srgb,var(--logo-2) 80%,var(--logo-3))'/><stop offset='0.83' style='stop-color:color-mix(in srgb,var(--logo-2) 70%,var(--logo-3))'/><stop offset='1' style='stop-color:color-mix(in srgb,var(--logo-2) 60%,var(--logo-3))'/></linearGradient>"
            + "<linearGradient id='logo-v' x1='10' y1='45' x2='31' y2='15' gradientUnits='userSpaceOnUse'><stop offset='0' style='stop-color:color-mix(in srgb,var(--logo-1) 95%,var(--logo-2))'/><stop offset='0.17' style='stop-color:var(--logo-1)'/><stop offset='0.33' style='stop-color:color-mix(in srgb,var(--logo-1) 95%,var(--logo-2))'/><stop offset='0.5' style='stop-color:color-mix(in srgb,var(--logo-1) 80%,var(--logo-2))'/><stop offset='0.67' style='stop-color:color-mix(in srgb,var(--logo-1) 65%,var(--logo-2))'/><stop offset='0.83' style='stop-color:color-mix(in srgb,var(--logo-1) 40%,var(--logo-2))'/><stop offset='1' style='stop-color:color-mix(in srgb,var(--logo-1) 20%,var(--logo-2))'/></linearGradient>"
            + "<linearGradient id='logo-o' x1='48.5' y1='10.6' x2='33.7' y2='50.2' gradientUnits='userSpaceOnUse'><stop offset='0' style='stop-color:color-mix(in srgb,var(--logo-3) 50%,var(--logo-4))'/><stop offset='0.17' style='stop-color:color-mix(in srgb,var(--logo-3) 65%,var(--logo-4))'/><stop offset='0.33' style='stop-color:color-mix(in srgb,var(--logo-3) 95%,var(--logo-4))'/><stop offset='0.5' style='stop-color:color-mix(in srgb,var(--logo-2) 30%,var(--logo-3))'/><stop offset='0.67' style='stop-color:color-mix(in srgb,var(--logo-2) 70%,var(--logo-3))'/><stop offset='0.83' style='stop-color:color-mix(in srgb,var(--logo-1) 20%,var(--logo-2))'/><stop offset='1' style='stop-color:color-mix(in srgb,var(--logo-1) 50%,var(--logo-2))'/></linearGradient>"
            + "<linearGradient id='logo-y' x1='41.7' y1='7.3' x2='29.1' y2='53.5' gradientUnits='userSpaceOnUse'><stop offset='0' style='stop-color:color-mix(in srgb,var(--logo-3) 20%,var(--logo-4))'/><stop offset='0.17' style='stop-color:var(--logo-4)'/><stop offset='0.33' style='stop-color:color-mix(in srgb,var(--logo-3) 10%,var(--logo-4))'/><stop offset='0.5' style='stop-color:color-mix(in srgb,var(--logo-3) 30%,var(--logo-4))'/><stop offset='0.67' style='stop-color:color-mix(in srgb,var(--logo-3) 60%,var(--logo-4))'/><stop offset='0.83' style='stop-color:color-mix(in srgb,var(--logo-3) 90%,var(--logo-4))'/><stop offset='1' style='stop-color:var(--logo-3)'/></linearGradient>"
            + "<path id='logo-l' d='M4.05 39.29C3.58 38.88 3.36 36.95 3.25 36.28C2.06 28.48 5.17 20.10 10.58 14.48C14.37 10.55 19.57 7.76 25.00 7.05C28.45 6.60 31.81 7.08 34.91 8.67C37.97 10.25 39.88 13.17 40.49 16.52C40.83 18.43 40.54 20.52 39.72 22.28C35.71 30.88 23.50 28.60 19.06 38.26C17.94 40.70 17.59 43.29 17.82 45.95C18.23 50.57 20.97 54.55 24.56 57.34C27.17 59.37 31.03 60.79 34.33 61.04C35.27 61.11 36.21 61.17 37.14 61.17C37.58 61.18 38.02 61.10 38.46 61.13C38.59 61.14 38.61 61.19 38.71 61.26C38.44 61.68 36.23 61.83 35.63 61.89C23.77 63.06 12.41 56.29 9.23 44.53C8.02 40.05 8.25 35.32 9.92 30.98C13.32 22.14 22.23 18.36 23.17 16.55C23.23 16.43 23.28 16.29 23.27 16.14C23.13 14.39 17.49 16.43 16.63 16.81C10.17 19.70 5.66 25.81 4.51 32.76C4.17 34.80 4.20 36.79 4.21 38.85C4.21 39.07 4.19 39.14 4.05 39.29Z'/>"
            + "<path id='logo-r' d='M23.73 3.32C24.03 2.87 26.33 2.51 26.96 2.41C30.50 1.85 34.02 1.81 37.52 2.68C46.54 4.92 53.53 12.09 55.15 21.27C55.56 23.61 55.65 26.07 55.26 28.42C54.31 34.24 50.92 39.34 46.37 43.02C44.97 44.15 41.65 46.03 40.76 47.32C40.57 47.60 40.45 47.97 40.72 48.24C41.10 48.62 41.82 48.56 42.31 48.52C43.92 48.36 45.55 47.83 47.03 47.16C53.14 44.39 57.84 38.36 59.10 31.76C59.64 28.88 59.74 25.83 59.25 22.94C59.20 22.66 59.14 22.51 59.33 22.30C59.88 22.68 60.39 25.30 60.53 26.06C62.47 36.32 58.26 47.19 49.48 53.03C45.43 55.72 39.95 57.83 35.01 57.22C30.61 56.67 26.57 54.82 24.43 50.74C23.25 48.50 22.89 45.98 23.50 43.52C26.12 33.00 40.90 36.00 45.13 24.91C46.06 22.47 46.31 19.79 45.92 17.21C45.20 12.48 42.13 8.59 38.18 6.04C31.53 1.77 24.38 3.67 23.73 3.32Z'/>"
            + "<clipPath id='logo-lc'><use href='#logo-l'/></clipPath><clipPath id='logo-rc'><use href='#logo-r'/></clipPath></defs>"
            + "<use href='#logo-l' fill='url(#logo-p)'/>"
            + "<path clip-path='url(#logo-lc)' fill='url(#logo-v)' d='M4.18 39.24C3.62 38.89 3.67 37.96 3.62 37.36C3.40 35.00 3.59 32.58 4.16 30.28C4.38 29.37 4.69 28.47 4.87 27.55C5.18 25.97 5.14 24.46 5.81 22.95C8.84 16.25 16.81 10.62 24.17 10.17C26.91 10.01 29.69 10.52 31.76 12.45C32.92 13.53 33.63 15.04 33.60 16.64C33.52 21.74 24.94 24.47 21.07 27.17C15.74 30.90 12.91 36.67 13.28 43.15C13.53 47.53 15.40 52.00 18.44 55.17C20.92 57.76 24.95 60.18 28.57 60.59C29.73 60.72 30.90 60.51 32.07 60.55C32.95 60.58 37.45 60.81 37.97 61.17C38.08 61.24 38.13 61.35 38.05 61.47C37.61 62.09 35.36 62.18 34.65 62.23C22.41 62.99 11.04 55.14 8.60 42.92C6.70 33.47 11.16 25.80 18.37 20.07C18.97 19.60 23.19 16.94 22.32 16.15C21.77 15.64 20.24 15.98 19.59 16.13C15.17 17.16 11.26 20.01 8.60 23.63C5.81 27.42 4.56 32.12 4.45 36.78C4.44 37.39 4.69 38.93 4.18 39.24Z'/>"
            + "<use href='#logo-r' fill='url(#logo-o)'/>"
            + "<path clip-path='url(#logo-rc)' fill='url(#logo-y)' d='M31.87 3.55C31.99 3.27 32.17 3.18 32.45 3.04C33.58 2.45 34.45 2.64 35.67 2.86C41.07 3.84 46.09 7.84 48.59 12.69C50.52 16.43 51.45 20.77 50.83 24.96C48.54 40.23 31.52 40.07 30.86 47.08C30.74 48.31 31.13 49.54 31.86 50.53C33.27 52.46 35.58 53.25 37.88 53.43C45.37 53.99 52.85 48.66 56.76 42.63C57.47 41.53 58.60 38.50 59.42 37.88C59.52 37.80 59.65 37.73 59.78 37.76C60.94 37.97 58.61 42.79 58.26 43.45C54.53 50.50 47.45 56.18 39.43 57.31C35.38 57.88 31.09 57.22 27.71 54.80C25.16 52.97 23.37 50.03 23.03 46.90C22.82 44.92 23.26 42.86 24.23 41.12C29.27 32.11 43.06 35.14 45.50 22.51C46.10 19.39 45.68 16.13 44.36 13.25C42.46 9.10 38.83 6.35 34.71 4.62C34.08 4.36 32.55 4.01 32.13 3.82C31.99 3.75 31.95 3.66 31.87 3.55Z'/></svg>";

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
