using com.swecial.server;
using System.Linq;

namespace com.swecial.code {
    /// <summary>
    /// The chart of accounts: a small part of BAS that a new company gets, plus the accounts that
    /// invoicing books to. Companies can add more accounts on the accounts page.
    /// </summary>
    public static class Bas {
        public const string Receivables = "1510";
        public const string Bank = "1930";
        public const string InputVat = "2641";
        public const string VatSettlement = "2650";

        /// <summary>The VAT rates an invoice row can have.</summary>
        public static readonly int[] VatRates = { 25, 12, 6, 0 };

        /// <summary>Sales account per VAT rate (3001–3004).</summary>
        public static string salesAccount(int vatRate) {
            switch (vatRate) {
                case 25: return "3001";
                case 12: return "3002";
                case 6: return "3003";
                default: return "3004";
            }
        }

        /// <summary>Output VAT account per rate (2611, 2621, 2631), null for 0 %.</summary>
        public static string outputVatAccount(int vatRate) {
            switch (vatRate) {
                case 25: return "2611";
                case 12: return "2621";
                case 6: return "2631";
                default: return null;
            }
        }

        static readonly (string number, string name)[] Starter = {
            ("1510", "Kundfordringar"),
            ("1630", "Avräkning för skatter och avgifter (skattekonto)"),
            ("1910", "Kassa"),
            ("1930", "Företagskonto / affärskonto"),
            ("2010", "Eget kapital"),
            ("2081", "Aktiekapital"),
            ("2091", "Balanserad vinst eller förlust"),
            ("2099", "Årets resultat"),
            ("2440", "Leverantörsskulder"),
            ("2611", "Utgående moms på försäljning inom Sverige, 25 %"),
            ("2621", "Utgående moms på försäljning inom Sverige, 12 %"),
            ("2631", "Utgående moms på försäljning inom Sverige, 6 %"),
            ("2641", "Debiterad ingående moms"),
            ("2650", "Redovisningskonto för moms"),
            ("2710", "Personalskatt"),
            ("2731", "Avräkning lagstadgade sociala avgifter"),
            ("2893", "Skulder till närstående personer"),
            ("3001", "Försäljning inom Sverige, 25 % moms"),
            ("3002", "Försäljning inom Sverige, 12 % moms"),
            ("3003", "Försäljning inom Sverige, 6 % moms"),
            ("3004", "Försäljning inom Sverige, momsfri"),
            ("3740", "Öres- och kronutjämning"),
            ("4010", "Inköp material och varor"),
            ("5010", "Lokalhyra"),
            ("5410", "Förbrukningsinventarier"),
            ("5420", "Programvaror"),
            ("5800", "Resekostnader"),
            ("6071", "Representation, avdragsgill"),
            ("6110", "Kontorsmateriel"),
            ("6212", "Mobiltelefon"),
            ("6230", "Datakommunikation"),
            ("6570", "Bankkostnader"),
            ("7010", "Löner till kollektivanställda"),
            ("7210", "Löner till tjänstemän"),
            ("7510", "Arbetsgivaravgifter"),
            ("8310", "Ränteintäkter från omsättningstillgångar"),
            ("8410", "Räntekostnader för långfristiga skulder"),
            ("8910", "Skatt som belastar årets resultat"),
        };

        /// <summary>Adds the starter accounts the company doesn't have yet.</summary>
        public static void seed(string companyId) {
            var existing = Db.forCompany("account", companyId).Select(a => Db.str(a, "number")).ToHashSet();
            foreach (var (number, name) in Starter) {
                if (existing.Contains(number)) continue;
                var account = Db.create("account", companyId);
                account.set("number", number);
                account.set("name", name);
                Db.commit(account);
            }
        }

        public static SwecialObject find(string companyId, string number) {
            return Db.forCompany("account", companyId).FirstOrDefault(a => Db.str(a, "number") == number);
        }

        /// <summary>Balance sheet accounts start with 1 (assets) or 2 (equity and liabilities); 3–8 are the income statement.</summary>
        public static bool isBalanceAccount(string number) {
            return number.Length > 0 && (number[0] == '1' || number[0] == '2');
        }
    }
}
