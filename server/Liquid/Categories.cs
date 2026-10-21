using System.Collections.Generic;
using System.Linq;

namespace com.swecial.code {
    public class Category {
        public string key;
        public string name;
        /// <summary>BAS account the cost is booked on.</summary>
        public string account;
        /// <summary>The usual VAT rate, suggested on the form; the user can change it.</summary>
        public int vatRate;
        /// <summary>Help shown under the category, empty when there's nothing to say.</summary>
        public string hint = "";
    }

    /// <summary>
    /// What an expense is for, in plain words. The category decides the account, so users never
    /// have to pick one. name and hint here are the Swedish defaults (used in voucher texts); the
    /// pages show the theme's texts, category.&lt;key&gt;.name and .hint. Keys are stored on expenses: never change a key, add a new category instead.
    /// </summary>
    public static class Categories {
        public static readonly List<Category> All = new List<Category> {
            new Category { key = "software", name = "Programvara och prenumerationer", account = "5420", vatRate = 25,
                hint = "Står det ingen svensk moms på kvittot (t.ex. Google, Microsoft, Adobe)? Markera att det är köpt från utlandet." },
            new Category { key = "equipment", name = "Datorer och utrustning", account = "5410", vatRate = 25,
                hint = "För saker som kostar mindre än ett halvt prisbasbelopp (knappt 30 000 kr) och inte är en del av en större anskaffning." },
            new Category { key = "phone", name = "Telefon och mobilabonnemang", account = "6212", vatRate = 25 },
            new Category { key = "internet", name = "Internet, webbhotell och domäner", account = "6230", vatRate = 25 },
            new Category { key = "office", name = "Kontorsmaterial", account = "6110", vatRate = 25 },
            new Category { key = "rent", name = "Lokalhyra", account = "5010", vatRate = 25,
                hint = "Hyresvärden tar bara moms om lokalen är frivilligt momsregistrerad. Står ingen moms på avin, välj 0 %." },
            new Category { key = "travel", name = "Resor (tåg, flyg, taxi)", account = "5800", vatRate = 6 },
            new Category { key = "hotel", name = "Hotell och logi", account = "5831", vatRate = 12 },
            new Category { key = "representation", name = "Representation (mat och dryck med kunder)", account = "6072", vatRate = 12,
                hint = "Kostnaden är inte avdragsgill. Momsen får dras av på högst 300 kr exkl. moms per person och tillfälle, alltså högst 36 kr per person. Ändra momsbeloppet om det behövs." },
            new Category { key = "marketing", name = "Reklam och marknadsföring", account = "5910", vatRate = 25 },
            new Category { key = "consulting", name = "Konsulter och köpta tjänster", account = "6550", vatRate = 25 },
            new Category { key = "accounting", name = "Redovisning och revision", account = "6530", vatRate = 25 },
            new Category { key = "insurance", name = "Försäkringar", account = "6310", vatRate = 0 },
            new Category { key = "postage", name = "Frakt och porto", account = "6250", vatRate = 25 },
            new Category { key = "literature", name = "Böcker och facklitteratur", account = "6970", vatRate = 6 },
            new Category { key = "education", name = "Kurser och utbildning", account = "7610", vatRate = 25 },
            new Category { key = "membership", name = "Medlemsavgifter", account = "6981", vatRate = 0 },
            new Category { key = "goods", name = "Varor för vidareförsäljning", account = "4010", vatRate = 25 },
            new Category { key = "bankfees", name = "Bankavgifter", account = "6570", vatRate = 0 },
            new Category { key = "other", name = "Övrigt", account = "6990", vatRate = 25 },
        };

        public static Category find(string key) {
            return All.FirstOrDefault(c => c.key == key);
        }
    }
}
