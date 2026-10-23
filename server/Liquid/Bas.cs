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
        public const string ReverseChargeOutputVat = "2614";
        public const string ReverseChargeInputVat = "2645";
        public const string TaxAccount = "1630";
        public const string ShareCapital = "2081";
        public const string RetainedEarnings = "2091";
        public const string OwnerLoans = "2893";
        public const string BankFees = "6570";
        public const string InterestIncome = "8310";
        public const string VatSettlement = "2650";
        public const string Securities = "1350";
        public const string SecuritiesResult = "8220";

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

        /// <summary>Output VAT on purchases with reverse charge (bought from abroad), per rate: 2614, 2624, 2634.</summary>
        public static string reverseChargeVatAccount(int vatRate) {
            return vatRate == 12 ? "2624" : vatRate == 6 ? "2634" : "2614";
        }

        /// <summary>Output VAT on imported goods, per rate: 2615, 2625, 2635.</summary>
        public static string importVatAccount(int vatRate) {
            return vatRate == 12 ? "2625" : vatRate == 6 ? "2635" : "2615";
        }

        /// <summary>Sales without Swedish VAT: goods or services to a business in another EU country, or outside the EU.</summary>
        public static string foreignSalesAccount(string treatment, bool goods) {
            if (treatment == Invoicing.EuReverseCharge) return goods ? "3108" : "3308";
            return goods ? "3105" : "3305";
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
            ("1350", "Andelar och värdepapper i andra företag"),
            ("1510", "Kundfordringar"),
            ("1630", "Avräkning för skatter och avgifter (skattekonto)"),
            ("1910", "Kassa"),
            ("1930", "Företagskonto / affärskonto"),
            ("2010", "Eget kapital"),
            ("2081", "Aktiekapital"),
            ("2091", "Balanserad vinst eller förlust"),
            ("2099", "Årets resultat"),
            ("2440", "Leverantörsskulder"),
            ("2512", "Beräknad inkomstskatt"),
            ("2518", "Betald F-skatt"),
            ("2611", "Utgående moms på försäljning inom Sverige, 25 %"),
            ("2621", "Utgående moms på försäljning inom Sverige, 12 %"),
            ("2631", "Utgående moms på försäljning inom Sverige, 6 %"),
            ("2614", "Utgående moms omvänd skattskyldighet, 25 %"),
            ("2615", "Utgående moms import av varor, 25 %"),
            ("2624", "Utgående moms omvänd skattskyldighet, 12 %"),
            ("2625", "Utgående moms import av varor, 12 %"),
            ("2634", "Utgående moms omvänd skattskyldighet, 6 %"),
            ("2635", "Utgående moms import av varor, 6 %"),
            ("2641", "Debiterad ingående moms"),
            ("2645", "Beräknad ingående moms på förvärv från utlandet"),
            ("2650", "Redovisningskonto för moms"),
            ("2710", "Personalskatt"),
            ("2731", "Avräkning lagstadgade sociala avgifter"),
            ("2893", "Skulder till närstående personer"),
            ("3001", "Försäljning inom Sverige, 25 % moms"),
            ("3002", "Försäljning inom Sverige, 12 % moms"),
            ("3003", "Försäljning inom Sverige, 6 % moms"),
            ("3004", "Försäljning inom Sverige, momsfri"),
            ("3105", "Försäljning varor till land utanför EU"),
            ("3108", "Försäljning varor till annat EU-land, momsfri"),
            ("3305", "Försäljning tjänster till land utanför EU"),
            ("3308", "Försäljning tjänster till annat EU-land"),
            ("3740", "Öres- och kronutjämning"),
            ("4010", "Inköp material och varor"),
            ("5010", "Lokalhyra"),
            ("5410", "Förbrukningsinventarier"),
            ("5420", "Programvaror"),
            ("5800", "Resekostnader"),
            ("5831", "Kost och logi i Sverige"),
            ("5832", "Kost och logi i utlandet"),
            ("5910", "Annonsering"),
            ("6071", "Representation, avdragsgill"),
            ("6072", "Representation, ej avdragsgill"),
            ("6110", "Kontorsmateriel"),
            ("6212", "Mobiltelefon"),
            ("6230", "Datakommunikation"),
            ("6250", "Postbefordran"),
            ("6310", "Företagsförsäkringar"),
            ("6530", "Redovisningstjänster"),
            ("6550", "Konsultarvoden"),
            ("6570", "Bankkostnader"),
            ("6970", "Tidningar, tidskrifter och facklitteratur"),
            ("6981", "Föreningsavgifter, avdragsgilla"),
            ("6982", "Föreningsavgifter, ej avdragsgilla"),
            ("6990", "Övriga externa kostnader, avdragsgilla"),
            ("6992", "Övriga externa kostnader, ej avdragsgilla"),
            ("7610", "Utbildning"),
            ("7010", "Löner till kollektivanställda"),
            ("7210", "Löner till tjänstemän"),
            ("7510", "Arbetsgivaravgifter"),
            ("8220", "Resultat vid försäljning av värdepapper"),
            ("8310", "Ränteintäkter från omsättningstillgångar"),
            ("8314", "Skattefria ränteintäkter"),
            ("8410", "Räntekostnader för långfristiga skulder"),
            ("8423", "Räntekostnader för skatter och avgifter, ej avdragsgilla"),
            ("8811", "Avsättning till periodiseringsfond"),
            ("8819", "Återföring från periodiseringsfond"),
            ("8910", "Skatt som belastar årets resultat"),
            ("8999", "Årets resultat"),
        };

        /// <summary>
        /// Adds the starter accounts the company doesn't have yet. Runs for every company at startup
        /// (Cloud.onStartUp), so accounts added to this list reach existing companies too.
        /// </summary>
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

        /// <summary>Adds an account the books need (e.g. a tax allocation reserve's) when the company doesn't have it.</summary>
        public static void ensure(string companyId, string number, string name) {
            lock (Db.Sync) {
                if (find(companyId, number) != null) return;
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
