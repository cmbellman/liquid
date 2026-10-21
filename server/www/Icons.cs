namespace com.swecial.web {
    /// <summary>
    /// Line icons for the sidebar, inline SVG (the server doesn't serve image files from www/).
    /// 24×24, drawn with currentColor so they follow the link colour.
    /// </summary>
    public static class Icons {
        const string Open = "<svg class='icon' viewBox='0 0 24 24' aria-hidden='true' fill='none' stroke='currentColor' stroke-width='1.7' stroke-linecap='round' stroke-linejoin='round'>";
        const string Close = "</svg>";

        /// <summary>A beer mug.</summary>
        public const string Bar = Open + "<path d='M5 8h10v11a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2z'/><path d='M15 10h2.5a2 2 0 0 1 2 2v3a2 2 0 0 1-2 2H15'/><path d='M5 8a2.5 2.5 0 0 1 2.2-3.6A3 3 0 0 1 12.4 4a2.6 2.6 0 0 1 2.6 4'/><path d='M8.5 12v5M11.5 12v5'/>" + Close;
        public const string Invoice = Open + "<path d='M7 3h7l4 4v14H7a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1z'/><path d='M14 3v4h4'/><path d='M9 12h6M9 16h4'/>" + Close;
        /// <summary>A receipt with a torn edge.</summary>
        public const string Receipt = Open + "<path d='M6 3h12v18l-2-1.4-2 1.4-2-1.4-2 1.4-2-1.4L6 21z'/><path d='M9 8h6M9 12h6M9 16h3'/>" + Close;
        /// <summary>A bag for the receipts (Pantpåsen).</summary>
        public const string Bag = Open + "<path d='M5 8h14l-1.2 12a1 1 0 0 1-1 .9H7.2a1 1 0 0 1-1-.9z'/><path d='M9 8V6a3 3 0 0 1 6 0v2'/><path d='M10 13l1.5 1.5L14.5 11'/>" + Close;
        public const string Wallet = Open + "<path d='M4 7a2 2 0 0 1 2-2h11v4'/><path d='M4 7v11a2 2 0 0 0 2 2h13V9H6a2 2 0 0 1-2-2z'/><circle cx='16' cy='14.5' r='1.2'/>" + Close;
        public const string People = Open + "<circle cx='9' cy='8' r='3.2'/><path d='M3.5 20a5.5 5.5 0 0 1 11 0'/><circle cx='17' cy='9' r='2.4'/><path d='M15.5 14.2A4.5 4.5 0 0 1 21 18.5'/>" + Close;
        public const string Chart = Open + "<path d='M4 20h16'/><path d='M4 16c3-6 5 2 8-4s4-6 8-7'/>" + Close;
        public const string Gear = Open + "<circle cx='12' cy='12' r='3'/><path d='M12 2.8v2.4M12 18.8v2.4M2.8 12h2.4M18.8 12h2.4M5.5 5.5l1.7 1.7M16.8 16.8l1.7 1.7M5.5 18.5l1.7-1.7M16.8 7.2l1.7-1.7'/>" + Close;
        /// <summary>An open ledger book (Avancerat: vouchers and accounts).</summary>
        public const string Book = Open + "<path d='M12 6c-2-1.5-5-2-8-1.5V19c3-.5 6 0 8 1.5 2-1.5 5-2 8-1.5V4.5c-3-.5-6 0-8 1.5z'/><path d='M12 6v14.5'/>" + Close;
        public const string Building = Open + "<path d='M5 21V5a1 1 0 0 1 1-1h8a1 1 0 0 1 1 1v16'/><path d='M15 10h3a1 1 0 0 1 1 1v10'/><path d='M3 21h18M8.5 8h3M8.5 12h3M8.5 16h3'/>" + Close;
        public const string Logout = Open + "<path d='M14 4h4a1 1 0 0 1 1 1v14a1 1 0 0 1-1 1h-4'/><path d='M10 8l-4 4 4 4M6 12h9'/>" + Close;
        public const string Upload = Open + "<path d='M12 16V4M7 9l5-5 5 5'/><path d='M4 16v3a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1v-3'/>" + Close;
        public const string Camera = Open + "<path d='M4 8a1 1 0 0 1 1-1h3l1.5-2h5L16 7h3a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1z'/><circle cx='12' cy='13' r='3.5'/>" + Close;
    }
}
