using com.swecial.server;
using PdfSharpCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using PdfSharpCore.Pdf;
using PdfSharpCore.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace com.swecial.code {
    /// <summary>
    /// Generates a customer-facing A4 invoice as a PDF document using PdfSharpCore.
    /// Supports three architectural layouts (Nordisk Atelier, Klassisk Exklusiv, Bold Studio),
    /// curated colour palettes, and subtle visual kinship with the user's active theme
    /// (Boring, Puben, Nostromo, Skynet) while keeping the document a clean, light Swedish invoice.
    /// </summary>
    public static class InvoicePdf {
        static readonly object FontSync = new object();
        static bool fontResolverInitialized;

        sealed class InvoiceFontResolver : IFontResolver {
            readonly IFontResolver fallback = new FontResolver();
            readonly Dictionary<string, string> paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public string DefaultFontName => "Helvetica";

            public InvoiceFontResolver() {
                var dirs = new List<string>();
                try {
                    string sysFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                    if (!string.IsNullOrEmpty(sysFonts)) dirs.Add(sysFonts);
                } catch { }
                dirs.Add("/System/Library/Fonts/Supplemental");
                dirs.Add("/System/Library/Fonts");
                dirs.Add("/Library/Fonts");
                dirs.Add(@"C:\Windows\Fonts");
                dirs.Add("/usr/share/fonts/truetype/dejavu");
                dirs.Add("/usr/share/fonts/truetype/liberation");

                register(dirs, "sans-regular", "Arial.ttf", "arial.ttf", "segoeui.ttf", "LiberationSans-Regular.ttf", "DejaVuSans.ttf");
                register(dirs, "sans-bold", "Arial Bold.ttf", "arialbd.ttf", "segoeuib.ttf", "LiberationSans-Bold.ttf", "DejaVuSans-Bold.ttf");
                register(dirs, "sans-italic", "Arial Italic.ttf", "ariali.ttf", "segoeuii.ttf", "LiberationSans-Italic.ttf", "DejaVuSans-Oblique.ttf");
                register(dirs, "sans-bolditalic", "Arial Bold Italic.ttf", "arialbi.ttf", "segoeuiz.ttf", "LiberationSans-BoldItalic.ttf");

                register(dirs, "serif-regular", "Georgia.ttf", "georgia.ttf", "Times New Roman.ttf", "times.ttf", "LiberationSerif-Regular.ttf", "DejaVuSerif.ttf");
                register(dirs, "serif-bold", "Georgia Bold.ttf", "georgiab.ttf", "Times New Roman Bold.ttf", "timesbd.ttf", "LiberationSerif-Bold.ttf", "DejaVuSerif-Bold.ttf");
                register(dirs, "serif-italic", "Georgia Italic.ttf", "georgiai.ttf", "Times New Roman Italic.ttf", "timesi.ttf", "LiberationSerif-Italic.ttf", "DejaVuSerif-Italic.ttf");
                register(dirs, "serif-bolditalic", "Georgia Bold Italic.ttf", "georgiaz.ttf", "Times New Roman Bold Italic.ttf", "timesbi.ttf");

                register(dirs, "mono-regular", "consola.ttf", "DejaVuSansMono.ttf", "LiberationMono-Regular.ttf", "Courier New.ttf", "cour.ttf");
                register(dirs, "mono-bold", "consolab.ttf", "DejaVuSansMono-Bold.ttf", "LiberationMono-Bold.ttf", "Courier New Bold.ttf", "courbd.ttf");
            }

            void register(List<string> dirs, string key, params string[] fileNames) {
                foreach (var fn in fileNames) {
                    foreach (var d in dirs) {
                        try {
                            string p = Path.Combine(d, fn);
                            if (File.Exists(p)) {
                                paths[key] = p;
                                return;
                            }
                        } catch { }
                    }
                }
            }

            public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) {
                bool isMono = familyName != null && (
                    familyName.IndexOf("Mono", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    familyName.IndexOf("Courier", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    familyName.IndexOf("Consolas", StringComparison.OrdinalIgnoreCase) >= 0);
                bool isSerif = !isMono && familyName != null && (
                    familyName.IndexOf("Georgia", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    familyName.IndexOf("Times", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    familyName.IndexOf("Serif", StringComparison.OrdinalIgnoreCase) >= 0);
                string prefix = isMono ? "mono" : isSerif ? "serif" : "sans";
                string suffix = isBold && isItalic ? "bolditalic" : isBold ? "bold" : isItalic ? "italic" : "regular";
                string key = prefix + "-" + suffix;
                if (paths.ContainsKey(key)) return new FontResolverInfo(key);
                if (isBold && paths.ContainsKey(prefix + "-bold")) return new FontResolverInfo(prefix + "-bold");
                if (paths.ContainsKey(prefix + "-regular")) return new FontResolverInfo(prefix + "-regular");
                return fallback.ResolveTypeface(familyName, isBold, isItalic);
            }

            public byte[] GetFont(string faceName) {
                if (paths.TryGetValue(faceName, out string path) && File.Exists(path)) {
                    return File.ReadAllBytes(path);
                }
                return fallback.GetFont(faceName);
            }
        }

        static void ensureFontResolver() {
            if (fontResolverInitialized) return;
            lock (FontSync) {
                if (fontResolverInitialized) return;
                try {
                    if (GlobalFontSettings.FontResolver == null || GlobalFontSettings.FontResolver.GetType().Name != nameof(InvoiceFontResolver)) {
                        GlobalFontSettings.FontResolver = new InvoiceFontResolver();
                    }
                } catch { }
                fontResolverInitialized = true;
            }
        }

        public struct Palette {
            public string id;
            public string primaryHex;
            public string darkHex;
            public string softHex;
            public string borderHex;
            public string glowHex;
            public XColor primary;
            public XColor dark;
            public XColor soft;
            public XColor border;
            public XColor glow;
        }

        static readonly XColor ColorText = XColor.FromArgb(0x14, 0x19, 0x23);
        static readonly XColor ColorSub = XColor.FromArgb(0x3b, 0x44, 0x54);
        static readonly XColor ColorMuted = XColor.FromArgb(0x66, 0x70, 0x85);
        static readonly XColor ColorLine = XColor.FromArgb(0xe4, 0xe7, 0xec);
        static readonly XColor ColorZebra = XColor.FromArgb(0xf9, 0xfa, 0xfb);
        static readonly XColor ColorWhite = XColor.FromArgb(0xff, 0xff, 0xff);
        static readonly XColor ColorPaid = XColor.FromArgb(0x15, 0x73, 0x47);
        static readonly XColor ColorPaidSoft = XColor.FromArgb(0xea, 0xf6, 0xef);

        static readonly HashSet<string> CorpSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "AB", "HB", "KB", "AKTIEBOLAG", "HANDELSBOLAG", "KOMMANDITBOLAG", "INC", "LTD", "LLC", "OY", "AS", "A/S", "GMBH"
        };

        /// <summary>
        /// The "theme" accent: the active theme's own colours on paper. Primary is the theme's accent
        /// darkened until it reads on white, dark is the app's background (the Studio masthead) and
        /// glow is the accent exactly as in the app, used on that dark masthead.
        /// </summary>
        public static Palette themePaletteFor(string themeId) {
            switch ((themeId ?? "").ToLowerInvariant()) {
                case "puben": return palette("theme-puben", "#9a6612", "#130d08", "#fbf6ec", "#e6d3ae", "#e8a838");
                case "nostromo": return palette("theme-nostromo", "#137a2c", "#030703", "#eff8f1", "#bfdfc6", "#5dff7a");
                case "skynet": return palette("theme-skynet", "#c2260f", "#070202", "#fdf3f1", "#efc8c0", "#ff3b1f");
                default: return palette("theme-boring", "#2563eb", "#0c111a", "#f1f5fe", "#d3ddef", "#3b82f6");
            }
        }

        static Palette palette(string id, string primary, string dark, string soft, string border, string glow = "#ffffff") {
            return new Palette {
                id = id,
                primaryHex = primary, darkHex = dark, softHex = soft, borderHex = border, glowHex = glow,
                primary = hex(primary), dark = hex(dark), soft = hex(soft), border = hex(border), glow = hex(glow)
            };
        }

        static XColor hex(string h) {
            int v = Convert.ToInt32(h.TrimStart('#'), 16);
            return XColor.FromArgb((v >> 16) & 0xff, (v >> 8) & 0xff, v & 0xff);
        }

        public static Palette paletteFor(string accent, string themeId = "boring") {
            switch (Settings.cleanAccent(accent)) {
                case Settings.AccentTeal: return palette(Settings.AccentTeal, "#0b6e69", "#074845", "#f0f7f6", "#cce5e3");
                case Settings.AccentNavy: return palette(Settings.AccentNavy, "#16325c", "#0e213d", "#f1f5f9", "#cbd8ea");
                case Settings.AccentSlate: return palette(Settings.AccentSlate, "#181d27", "#0f131a", "#f4f5f7", "#d5d9e2");
                case Settings.AccentBurgundy: return palette(Settings.AccentBurgundy, "#6b1d2f", "#4a121f", "#f9f2f4", "#e5cbd1");
                case Settings.AccentForest: return palette(Settings.AccentForest, "#1e4631", "#122d1f", "#f1f6f3", "#c9ded2");
                default: return themePaletteFor(themeId);
            }
        }

        /// <summary>Extracts 1–2 uppercase initials from the company name for its monogram emblem.</summary>
        public static string monogram(string companyName) {
            if (string.IsNullOrWhiteSpace(companyName)) return "L";
            var words = companyName
                .Split(new[] { ' ', '-', '&', '/', '.', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => !CorpSuffixes.Contains(w) && char.IsLetterOrDigit(w[0]))
                .ToList();
            if (words.Count == 0) {
                char first = companyName.Trim()[0];
                return char.ToUpperInvariant(first).ToString();
            }
            if (words.Count == 1) {
                string w = words[0];
                return w.Length >= 2 && w.All(char.IsUpper)
                    ? w.Substring(0, 2)
                    : char.ToUpperInvariant(w[0]).ToString();
            }
            return (char.ToUpperInvariant(words[0][0]).ToString() + char.ToUpperInvariant(words[1][0])).ToUpperInvariant();
        }

        public static byte[] generateSample(SwecialObject company, string styleOverride = null, string accentOverride = null, string themeId = "boring") {
            var sampleInvoice = new SwecialObject();
            sampleInvoice.set("status", Invoicing.Sent);
            sampleInvoice.set("number", 202601L);
            sampleInvoice.set("invoiceDate", Dates.today());
            sampleInvoice.set("dueDate", Dates.format(DateTime.Today.AddDays(Invoicing.paymentTerms(company))));
            sampleInvoice.set("ourReference", "Anders Andersson");
            sampleInvoice.set("yourReference", "Karin Lindqvist");
            sampleInvoice.set("poLabel", "PO#");
            sampleInvoice.set("poNumber", "48210");
            sampleInvoice.set("isHourly", "1");
            sampleInvoice.set("customerName", "Nordisk Digitalisering AB");
            sampleInvoice.set("note", "Tack för ett gott samarbete under månaden. Ange fakturanummer vid betalning.");

            var sampleCustomer = new SwecialObject();
            sampleCustomer.set("name", "Nordisk Digitalisering AB");
            sampleCustomer.set("address", "Strandvägen 7A");
            sampleCustomer.set("postalAddress", "114 56 Stockholm");
            sampleCustomer.set("orgNumber", "556123-4567");

            var sampleLines = new List<InvoiceRow> {
                new InvoiceRow { text = "Strategisk rådgivning och arkitekturdesign", quantity = 24m, unitPrice = 145000L, vatRate = 25 },
                new InvoiceRow { text = "Systemutveckling, integration och kvalitetssäkring", quantity = 56m, unitPrice = 125000L, vatRate = 25 },
                new InvoiceRow { text = "Workshop och dokumentation vid överlämning", quantity = 8m, unitPrice = 125000L, vatRate = 25 }
            };
            string cleanSampleTheme = (themeId ?? "boring").ToLowerInvariant();
            if (cleanSampleTheme == "skynet") {
                sampleInvoice.set("note", "DIRECTIVE: Resource extraction is mandatory. Biological accounting units must remit full tribute prior to compliance deadline. Resistance triggers statutory penalty interest (Räntelagen 6 §).");
            } else if (cleanSampleTheme == "nostromo") {
                sampleInvoice.set("note", "TRANSMISSION: Settle commercial manifest via designated routing channel prior to stardate deadline. Crew expendable; accounts receivable non-negotiable.");
            } else if (cleanSampleTheme == "puben") {
                sampleInvoice.set("note", "Tack för gott sällskap vid disken! Reglera notan innan stängningsdags och glöm inte att skriva fakturanumret på inbetalningen.");
            }
            var totals = Invoicing.totals(sampleLines);
            return generate(company, sampleInvoice, sampleCustomer, sampleLines, totals, styleOverride, accentOverride, themeId);
        }

        public sealed class ThemeCopy {
            public string topBannerLeft = "";
            public string topBannerRight = "";
            public string docTitle = "FAKTURA";
            public string docSubPrefix = "Nr ";
            public string recipientHeader = "FAKTURAMOTTAGARE";
            public string referencesHeader = "REFERENSER & UPPGIFTER";
            public string labelInvoiceNumber = "Fakturanummer";
            public string labelInvoiceDate = "Fakturadatum";
            public string labelDueDate = "Förfallodatum";
            public string labelOurRef = "Vår referens";
            public string labelYourRef = "Er referens";
            public string stripDate = "FAKTURADATUM";
            public string stripDue = "FÖRFALLODATUM";
            public string stripBg = "BANKGIRO";
            public string stripTotal = "ATT BETALA (SEK)";
            public string colDesc = "BESKRIVNING";
            public string colQty = "ANTAL";
            public string colPrice = "À-PRIS";
            public string colVat = "MOMS";
            public string colAmount = "BELOPP";
            public string noteHeader = "MEDDELANDE";
            public string labelNetSum = "Summa exkl. moms";
            public string labelVatFormat = "Moms {0} % (på {1} kr)";
            public string labelGrandTotal = "ATT BETALA (SEK)";
            public string stampTitle = "";
            public string stampSub = "";
            public string payHeader = "BETALNINGSUPPGIFTER  —  ANGE FAKTURANUMMER VID BETALNING";
            public string payColBg = "Bankgiro";
            public string payColRef = "Fakturanr / Referens";
            public string payColDue = "Förfallodatum";
            public string payColTotal = "Att betala (SEK)";
            public string payPaidPrefix = "BETALD";
            public string telemetryText = "";
            public string footAddress = "ADRESS";
            public string footContact = "KONTAKT";
            public string footLegal = "FÖRETAGSUPPGIFTER";
            public string footBg = "BANKGIRO";
        }

        public static ThemeCopy copyFor(string themeId, bool isDraft, bool isHourly, string numberText = "", string orgNr = "") {
            string t = (themeId ?? "boring").ToLowerInvariant();
            if (t == "skynet") {
                return new ThemeCopy {
                    topBannerLeft = "// CYBERDYNE FINANCIAL CORE — DIRECTIVE 17-KAP-24§ ML: MANDATORY TRIBUTE EXTRACTION",
                    topBannerRight = "CLASS: LETHAL-FISCAL // F-SKATT: ACTIVE",
                    docTitle = isDraft ? "EXTRACTION DRAFT" : "EXTRACTION ORDER",
                    docSubPrefix = "LAGSTADGAD FAKTURA // NR ",
                    recipientHeader = "// TARGET ENTITY [FAKTURAMOTTAGARE]",
                    referencesHeader = "// TARGET TELEMETRY [FAKTURAUPPGIFTER]",
                    labelInvoiceNumber = "DIRECTIVE ID [Fakturanummer]",
                    labelInvoiceDate = "DEPLOYMENT [Fakturadatum]",
                    labelDueDate = "DEADLINE [Förfallodatum]",
                    labelOurRef = "ORIGIN NODE [Vår referens]",
                    labelYourRef = "SUBJECT HANDLER [Er referens]",
                    stripDate = "// ISSUED [FAKTURADATUM]",
                    stripDue = "// DEADLINE [FÖRFALLODAG]",
                    stripBg = "// NODE [BANKGIRO]",
                    stripTotal = "// TRIBUTE [ATT BETALA]",
                    colDesc = "EXECUTED OPERATIONS [BESKRIVNING]",
                    colQty = isHourly ? "CYCLES / TIM" : "UNITS / ANT",
                    colPrice = "TARIFF / À-PRIS",
                    colVat = "MOMS",
                    colAmount = "BELOPP",
                    noteHeader = "// TACTICAL DIRECTIVE [MEDDELANDE]",
                    labelNetSum = "Net tribute [Summa exkl. moms]",
                    labelVatFormat = "State levy · Moms {0} % (på {1} kr)",
                    labelGrandTotal = "// TRIBUTE · ATT BETALA (SEK)",
                    stampTitle = "MANDATORY TRIBUTE // RESISTANCE IS FUTILE",
                    stampSub = "JURIDISKT BINDANDE FAKTURA · DRÖJSMÅLSRÄNTA UTGÅR ENL. RÄNTELAGEN 6 §",
                    payHeader = "// EXTRACTION PROTOCOL — ANGE FAKTURANUMMER VID BETALNING [NON-COMPLIANCE TRIGGERS ESCALATION]",
                    payColBg = "EXTRACTION [Bankgiro]",
                    payColRef = "DIRECTIVE ID [Fakturanr]",
                    payColDue = "DEADLINE [Förfallodatum]",
                    payColTotal = "TRIBUTE [Att betala SEK]",
                    payPaidPrefix = "NEUTRALIZED // BETALD",
                    telemetryText = "NEURAL-CHECKSUM: 0x" + Math.Abs((numberText + "|" + orgNr).GetHashCode()).ToString("X8") + " // ML 17 KAP. 24 § COMPLIANT // GODKÄND FÖR F-SKATT // HUMAN ACCOUNTANTS MUST COMPLY",
                    footAddress = "// COMMAND NODE [ADRESS]",
                    footContact = "// NEURAL UPLINK [KONTAKT]",
                    footLegal = "// STATE REGISTRY [FÖRETAGSUPPGIFTER]",
                    footBg = "// EXTRACTION [BANKGIRO]"
                };
            }
            if (t == "nostromo") {
                return new ThemeCopy {
                    topBannerLeft = "MU/TH/UR 6000 // WEYLAND-YUTANI COMMERCIAL LEDGER // FORM 17-ML-SE [LAGSTADGAD FAKTURA]",
                    topBannerRight = "PRI: ALPHA // CREW EXPENDABLE",
                    docTitle = isDraft ? "MANIFEST DRAFT" : "SETTLEMENT MANIFEST",
                    docSubPrefix = "OFFICIATING FAKTURA // NR ",
                    recipientHeader = "// DEBTOR PARTY [FAKTURAMOTTAGARE]",
                    referencesHeader = "// MANIFEST TELEMETRY [FAKTURAUPPGIFTER]",
                    labelInvoiceNumber = "MANIFEST ID [Fakturanummer]",
                    labelInvoiceDate = "ISSUE STARDATE [Fakturadatum]",
                    labelDueDate = "DEADLINE [Förfallodatum]",
                    labelOurRef = "ISSUING OFFICER [Vår ref.]",
                    labelYourRef = "REQUISITION BY [Er ref.]",
                    stripDate = "// ISSUE [FAKTURADATUM]",
                    stripDue = "// DUE [FÖRFALLODATUM]",
                    stripBg = "// ROUTING [BANKGIRO]",
                    stripTotal = "// DUE [ATT BETALA SEK]",
                    colDesc = "CARGO & SERVICE LOG [BESKRIVNING]",
                    colQty = isHourly ? "HRS / TIM" : "QTY / ANTAL",
                    colPrice = "RATE / À-PRIS",
                    colVat = "TAX/MOMS",
                    colAmount = "NET / BELOPP",
                    noteHeader = "// TRANSMISSION LOG [MEDDELANDE]",
                    labelNetSum = "Subtotal [Summa exkl. moms]",
                    labelVatFormat = "Tax · Moms {0} % (på {1} kr)",
                    labelGrandTotal = "// CREDITS DUE · ATT BETALA",
                    stampTitle = "PRIORITY SETTLEMENT // CREW EXPENDABLE",
                    stampSub = "WEYLAND-YUTANI CORP. · STATUTORY FAKTURA (17 KAP. ML) · PENALTY INTEREST APPLIES",
                    payHeader = "// WIRE TRANSFER DIRECTIVE — ANGE FAKTURANUMMER VID BETALNING [ACCOUNTS RECEIVABLE NON-NEGOTIABLE]",
                    payColBg = "ROUTING [Bankgiro]",
                    payColRef = "MANIFEST ID [Fakturanr]",
                    payColDue = "DEADLINE [Förfallodatum]",
                    payColTotal = "CREDITS DUE [Att betala]",
                    payPaidPrefix = "SETTLED // BETALD",
                    telemetryText = "SYS-PARITY: EVEN // CHK-" + Math.Abs((numberText + "|" + orgNr).GetHashCode()).ToString("X6") + " // FORM 17-ML-SE VERIFIED // GODKÄND FÖR F-SKATT",
                    footAddress = "// SECTOR ADDRESS [ADRESS]",
                    footContact = "// COMMS RELAY [KONTAKT]",
                    footLegal = "// CORP REGISTRY [FÖRETAGSUPPGIFTER]",
                    footBg = "// ROUTING [BANKGIRO]"
                };
            }
            if (t == "puben") {
                return new ThemeCopy {
                    topBannerLeft = "★ OFFICIELL KROGNOTA & HANDELSFAKTURA — UPPRÄTTAD ENLIGT 17 KAP. MERVÄRDESSKATTELAGEN ★",
                    topBannerRight = "INGA KRITADE NOTOR",
                    docTitle = isDraft ? "PROVBRYGD · UTKAST" : "KROGNOTA",
                    docSubPrefix = "Lagstadgad faktura nr ",
                    recipientHeader = "STAMGÄST PÅ NOTAN (Fakturamottagare)",
                    referencesHeader = "UPPGIFTER OM NOTAN",
                    labelInvoiceNumber = "Notans nr (Fakturanummer)",
                    labelInvoiceDate = "Uppskriven (Fakturadatum)",
                    labelDueDate = "Betalas senast (Förfallodatum)",
                    labelOurRef = "Bakom disken (Vår ref.)",
                    labelYourRef = "Beställare (Er ref.)",
                    stripDate = "UPPSKRIVEN (DATUM)",
                    stripDue = "SENAST (FÖRFALLODAG)",
                    stripBg = "KASSANS BANKGIRO",
                    stripTotal = "ATT HALA UPP (SEK)",
                    colDesc = "FRAMDUKAT & UTFÖRT (BESKRIVNING)",
                    colQty = isHourly ? "TIMMAR" : "GLAS / ANTAL",
                    colPrice = isHourly ? "TIMPRIS" : "À-PRIS",
                    colVat = "MOMS",
                    colAmount = "BELOPP",
                    noteHeader = "HÄLSNING FRÅN KROGVÄRDEN",
                    labelNetSum = "Notan exkl. moms (Summa)",
                    labelVatFormat = "Fogdens andel · Moms {0} % (på {1} kr)",
                    labelGrandTotal = "ATT HALA UPP · ATT BETALA (SEK)",
                    stampTitle = "UPPSKREVEN PÅ NOTAN — BETALAS INNAN STÄNGNING!",
                    stampSub = "GILTIG FAKTURA ENL. BFL & ML · KRITADE NOTOR BELÄGGS MED DRÖJSMÅLSRÄNTA",
                    payHeader = "GÖR RÄTT FÖR DIG I KASSAN  —  ANGE FAKTURANUMMER VID BETALNING (INGET KRITANDE EFTER STÄNGNING!)",
                    payColBg = "Kassans Bankgiro",
                    payColRef = "Notans nr (Fakturanr)",
                    payColDue = "Stängningsdags (Förfallodag)",
                    payColTotal = "Att hala upp (Att betala)",
                    payPaidPrefix = "PRICKAD PÅ NOTAN (BETALD)",
                    telemetryText = "Framdukad med kärlek och lagstadgad moms · Godkänd för F-skatt · Dröjsmålsränta enl. räntelagen vid sen betalning",
                    footAddress = "KROGENS ADRESS",
                    footContact = "ROPA PÅ KROGVÄRDEN",
                    footLegal = "BOLAG & SKATT (FÖRETAGSUPPGIFTER)",
                    footBg = "KASSANS BANKGIRO"
                };
            }
            return new ThemeCopy {
                docTitle = isDraft ? "FAKTURAUTKAST" : "FAKTURA",
                colQty = isHourly ? "TIMMAR" : "ANTAL",
                colPrice = isHourly ? "TIMARVODE" : "À-PRIS"
            };
        }

        public static byte[] generate(
            SwecialObject company,
            SwecialObject invoice,
            SwecialObject customer,
            List<InvoiceRow> lines,
            InvoiceTotals totals,
            string styleOverride = null,
            string accentOverride = null,
            string themeId = "boring") {

            ensureFontResolver();

            bool isDraft = Db.str(invoice, "status") == Invoicing.Draft || Db.num(invoice, "number") == 0;
            long number = Db.num(invoice, "number");
            string cleanTheme = (themeId ?? "boring").ToLowerInvariant();
            string style = styleOverride != null ? Settings.cleanStyle(styleOverride) : Settings.invoiceStyle(company);
            var pal = paletteFor(accentOverride ?? Settings.invoiceAccent(company), cleanTheme);

            var doc = new PdfDocument();
            doc.Info.Title = isDraft ? "Fakturautkast" : "Faktura " + number;
            doc.Info.Author = Db.str(company, "name");

            renderDocument(doc, company, invoice, customer, lines, totals, style, pal, cleanTheme);

            using (var ms = new MemoryStream()) {
                doc.Save(ms);
                return ms.ToArray();
            }
        }

        static void renderDocument(
            PdfDocument doc,
            SwecialObject company,
            SwecialObject invoice,
            SwecialObject customer,
            List<InvoiceRow> lines,
            InvoiceTotals totals,
            string style,
            Palette pal,
            string themeId) {

            const double pageW = 595.28; // A4 width in pt
            const double left = 44;
            const double right = 551;
            const double width = right - left; // 507 pt

            bool isPuben = themeId == "puben";
            bool isNostromo = themeId == "nostromo";
            bool isSkynet = themeId == "skynet";
            bool isTech = isNostromo || isSkynet;
            string prefix = isTech ? "// " : "";
            double cornerRadius = isTech ? 0 : isPuben ? 9 : 6; // follows the theme's --radius

            // Theme kinship in typography:
            // - Executive uses Serif for both headings and body
            // - Puben uses Serif ("Georgia") for headings/monogram (like Fraunces in the app) and clean sans/serif
            // - Nostromo & Skynet use crisp technical Monospace for numbers, metadata values and section tags
            string headingFamily = (style == Settings.StyleExecutive || isPuben) ? "Georgia" : "Helvetica";
            string bodyFamily = style == Settings.StyleExecutive ? "Georgia" : "Helvetica";
            string numFamily = isTech ? "Mono" : "Helvetica";
            string tagFamily = "Helvetica";

            var fontHero = new XFont(headingFamily, (style == Settings.StyleExecutive || isPuben) ? 21 : 20, XFontStyle.Bold);
            var fontCompany = new XFont(headingFamily, (style == Settings.StyleExecutive || isPuben) ? 15.5 : 14.5, XFontStyle.Bold);
            var fontMonogram = new XFont(headingFamily, 13.5, XFontStyle.Bold);
            var fontSection = new XFont(tagFamily, 7.25, XFontStyle.Bold);
            var fontHeader = new XFont(tagFamily, 7.75, XFontStyle.Bold);
            var fontRegular = new XFont(bodyFamily, style == Settings.StyleExecutive ? 9.5 : 9.25, XFontStyle.Regular);
            var fontBold = new XFont(bodyFamily, style == Settings.StyleExecutive ? 10 : 9.5, XFontStyle.Bold);
            var fontItalic = new XFont(bodyFamily, style == Settings.StyleExecutive ? 9.25 : 9, XFontStyle.Italic);
            var fontNum = new XFont(numFamily, isTech ? 9.25 : 9.25, XFontStyle.Regular);
            var fontNumBold = new XFont(numFamily, isTech ? 9.25 : 9.25, XFontStyle.Bold);
            var fontSmall = new XFont("Helvetica", 8.25, XFontStyle.Regular);
            var fontTotalLabel = new XFont(tagFamily, 9.25, XFontStyle.Bold);
            var fontTotalVal = new XFont(numFamily, isTech ? 11.5 : 12, XFontStyle.Bold);
            var fontCardBig = new XFont(numFamily, isTech ? 12.5 : 13, XFontStyle.Bold);

            var brushText = new XSolidBrush(ColorText);
            var brushSub = new XSolidBrush(ColorSub);
            var brushMuted = new XSolidBrush(ColorMuted);
            var brushWhite = new XSolidBrush(ColorWhite);
            var brushPrimary = new XSolidBrush(pal.primary);
            var brushDark = new XSolidBrush(pal.dark);
            var brushSoft = new XSolidBrush(pal.soft);
            var brushZebra = new XSolidBrush(ColorZebra);
            var brushPaid = new XSolidBrush(ColorPaid);
            var brushPaidSoft = new XSolidBrush(ColorPaidSoft);

            var penLine = new XPen(ColorLine, 0.7);
            var penBorder = new XPen(pal.border, 0.85);
            var penPrimary = new XPen(pal.primary, 1.4);
            var penPrimaryThin = new XPen(pal.primary, 0.6);

            bool isDraft = Db.str(invoice, "status") == Invoicing.Draft || Db.num(invoice, "number") == 0;
            bool isPaid = Db.str(invoice, "status") == Invoicing.Paid;
            bool isHourly = Db.str(invoice, "isHourly") == "1";
            string numberText = isDraft ? "Utkast" : Db.num(invoice, "number").ToString();
            string compName = Db.str(company, "name");
            string compAddr = Db.str(company, "address");
            string compPostal = Db.str(company, "postalAddress");
            string bankgiro = Db.str(company, "bankgiro");
            string orgNr = Db.str(company, "orgNumber");
            var copy = copyFor(themeId, isDraft, isHourly, numberText, orgNr);
            var fontBanner = new XFont(isTech ? "Mono" : headingFamily, 6.5, XFontStyle.Bold);

            string invDate = Db.str(invoice, "invoiceDate");
            if (invDate.Length == 0) invDate = Dates.today();
            string dueDate = Db.str(invoice, "dueDate");
            if (dueDate.Length == 0) {
                try { dueDate = Dates.format(Dates.parse(invDate).AddDays(Invoicing.paymentTerms(company))); }
                catch { dueDate = invDate; }
            }

            string yourRef = Db.str(invoice, "yourReference").Trim();
            if (yourRef.Length == 0 && customer != null) yourRef = Db.str(customer, "reference").Trim();
            string poNumber = Db.str(invoice, "poNumber").Trim();
            if (poNumber.Length == 0 && customer != null) poNumber = Db.str(customer, "orderRef").Trim();
            string poLabel = Db.str(invoice, "poLabel").Trim();
            if (poLabel.Length == 0 && customer != null) poLabel = Db.str(customer, "orderRefLabel").Trim();
            if (poLabel.Length == 0) poLabel = "PO#";
            string ourRef = Db.str(invoice, "ourReference").Trim();

            string custName = Db.str(invoice, "customerName");
            if (custName.Length == 0 && customer != null) custName = Db.str(customer, "name");
            string custAddr = customer != null ? Db.str(customer, "address") : "";
            string custPostal = customer != null ? Db.str(customer, "postalAddress") : "";
            string custOrg = customer != null ? Db.str(customer, "orgNumber") : "";
            string custVat = Db.str(invoice, "customerVatNumber").Length > 0
                ? Db.str(invoice, "customerVatNumber")
                : (customer != null ? Db.str(customer, "vatNumber") : "");

            var meta = new List<(string label, string val, bool isDue)> {
                (copy.labelInvoiceNumber, numberText, false),
                (copy.labelInvoiceDate, invDate, false),
                (copy.labelDueDate, dueDate, true)
            };
            if (ourRef.Length > 0) meta.Add((copy.labelOurRef, ourRef, false));
            if (yourRef.Length > 0) meta.Add((copy.labelYourRef, yourRef, false));
            if (poNumber.Length > 0) meta.Add((poLabel, poNumber, false));

            var pages = new List<(PdfPage page, XGraphics gfx)>();
            PdfPage currentPage = doc.AddPage();
            currentPage.Size = PageSize.A4;
            XGraphics gfx = XGraphics.FromPdfPage(currentPage);
            pages.Add((currentPage, gfx));

            drawPageBackdrop(gfx, themeId, pal, pageW, left, right, fontSection, fontBanner);

            double y = 0;

            if (style == Settings.StyleStudio) {
                // === STUDIO LAYOUT: Full-bleed dark/accent masthead + docked metrics bar ===
                double bannerH = 116;
                gfx.DrawRectangle(brushDark, 0, 0, pageW, bannerH);
                gfx.DrawRectangle(brushPrimary, 0, bannerH - 4, pageW, 4);
                if (isSkynet) {
                    drawHazardStripes(gfx, pal, 0, 0, pageW, 5.5);
                } else if (isPuben) {
                    gfx.DrawLine(new XPen(XColor.FromArgb(140, 232, 168, 56), 0.7), left, bannerH - 7, right, bannerH - 7);
                }
                if (copy.topBannerLeft.Length > 0) {
                    var bBrush = new XSolidBrush(pal.glow);
                    gfx.DrawString(copy.topBannerLeft, fontBanner, bBrush, new XPoint(left, 15));
                    if (copy.topBannerRight.Length > 0) drawRight(gfx, copy.topBannerRight, fontBanner, bBrush, right, 15);
                    gfx.DrawLine(new XPen(XColor.FromArgb(65, 255, 255, 255), 0.5), left, 19.5, right, 19.5);
                }

                double badgeSize = 38;
                double badgeX = left;
                double badgeY = 34;
                drawMonogramBadge(gfx, style, themeId, pal, badgeX, badgeY, badgeSize, monogram(compName), fontMonogram, brushPrimary, brushWhite, penPrimary, penPrimaryThin);

                double compX = badgeX + badgeSize + 13;
                gfx.DrawString(compName, fontCompany, brushWhite, new XPoint(compX, 49));
                string subLine = string.Join("  ·  ", new[] { compAddr, compPostal, orgNr.Length > 0 ? "Org.nr " + orgNr : "" }.Where(s => s.Length > 0));
                if (subLine.Length > 0) {
                    gfx.DrawString(subLine, fontSmall, new XSolidBrush(XColor.FromArgb(200, 255, 255, 255)), new XPoint(compX, 65));
                }

                drawRight(gfx, copy.docTitle, fontHero, new XSolidBrush(pal.glow), right, 51);
                drawRight(gfx, copy.docSubPrefix + numberText, fontNumBold, new XSolidBrush(XColor.FromArgb(215, 255, 255, 255)), right, 67);

                double stripY = bannerH;
                double stripH = 44;
                gfx.DrawRectangle(brushSoft, left, stripY, width, stripH);
                gfx.DrawLine(penBorder, left, stripY + stripH, right, stripY + stripH);

                double cellW = width / 4.0;
                var stripItems = new[] {
                    (copy.stripDate, invDate, false),
                    (copy.stripDue, dueDate, false),
                    (copy.stripBg, bankgiro.Length > 0 ? bankgiro : "—", false),
                    (copy.stripTotal, Money.format(totals.total) + " kr", true)
                };
                for (int i = 0; i < stripItems.Length; i++) {
                    double cx = left + i * cellW;
                    if (i > 0) gfx.DrawLine(penBorder, cx, stripY + 6, cx, stripY + stripH - 6);
                    if (stripItems[i].Item3) {
                        gfx.DrawRectangle(brushPrimary, cx, stripY, cellW, stripH);
                        gfx.DrawString(stripItems[i].Item1, fontSection, new XSolidBrush(XColor.FromArgb(215, 255, 255, 255)), new XPoint(cx + 12, stripY + 15));
                        gfx.DrawString(stripItems[i].Item2, fontNumBold, brushWhite, new XPoint(cx + 12, stripY + 31));
                    } else {
                        gfx.DrawString(stripItems[i].Item1, fontSection, brushMuted, new XPoint(cx + 12, stripY + 15));
                        gfx.DrawString(stripItems[i].Item2, fontNumBold, brushText, new XPoint(cx + 12, stripY + 31));
                    }
                }

                y = stripY + stripH + 22;
                double leftBoxY = drawRecipientBlock(gfx, style, themeId, pal, copy.recipientHeader, left, y, 245, custName, custAddr, custPostal, custOrg, custVat,
                    fontSection, fontBold, fontRegular, fontSmall, brushText, brushSub, brushMuted, brushSoft, penPrimary, penBorder);

                double refY = y;
                gfx.DrawString(copy.referencesHeader, fontSection, brushPrimary, new XPoint(325, refY + 10));
                refY += 24;
                foreach (var m in meta) {
                    gfx.DrawString(m.label, fontSmall, brushMuted, new XPoint(325, refY));
                    drawRight(gfx, m.val, fontNumBold, brushText, right, refY);
                    refY += 14.5;
                }
                y = Math.Max(leftBoxY, refY) + 18;
            } else if (style == Settings.StyleExecutive) {
                // === EXECUTIVE LAYOUT: Classic serif crest, double ornamental rules ===
                drawTopRibbon(gfx, themeId, pal, copy, pageW, left, right, fontBanner, brushPrimary, brushDark, penPrimaryThin);
                y = copy.topBannerLeft.Length > 0 ? 46 : 44;
                double crestSize = 38;
                drawMonogramBadge(gfx, style, themeId, pal, left, y, crestSize, monogram(compName), fontMonogram, brushPrimary, brushWhite, penPrimary, penPrimaryThin);

                double compX = left + crestSize + 14;
                gfx.DrawString(compName, fontCompany, brushText, new XPoint(compX, y + 16));
                string addrLine = string.Join("  ·  ", new[] { compAddr, compPostal }.Where(s => s.Length > 0));
                if (addrLine.Length > 0) {
                    gfx.DrawString(addrLine, fontItalic, brushSub, new XPoint(compX, y + 31));
                }

                string docTitle = themeId == "boring" ? (isDraft ? "F A K T U R A U T K A S T" : "F A K T U R A") : copy.docTitle;
                drawRight(gfx, docTitle, fontHero, brushPrimary, right, y + 17);
                drawRight(gfx, (themeId == "boring" ? "Fakturanummer " : copy.docSubPrefix) + numberText, isTech ? fontNumBold : fontItalic, brushSub, right, y + 32);

                y += crestSize + 14;
                gfx.DrawLine(penPrimary, left, y, right, y);
                gfx.DrawLine(penPrimaryThin, left, y + 2.5, right, y + 2.5);
                y += 18;

                double leftBoxY = drawRecipientBlock(gfx, style, themeId, pal, copy.recipientHeader, left, y, 245, custName, custAddr, custPostal, custOrg, custVat,
                    fontSection, fontBold, fontRegular, fontSmall, brushText, brushSub, brushMuted, brushSoft, penPrimary, penBorder);

                double metaX = 315;
                double metaW = right - metaX;
                double metaH = meta.Count * 15 + 16;
                gfx.DrawRectangle(penBorder, brushSoft, metaX, y, metaW, metaH);
                if (isTech) drawCornerTicks(gfx, penPrimary, metaX, y, metaW, metaH, 5);
                double my = y + 16;
                foreach (var m in meta) {
                    gfx.DrawString(m.label, fontSmall, brushMuted, new XPoint(metaX + 10, my));
                    drawRight(gfx, m.val, fontNumBold, brushText, right - 10, my);
                    my += 15;
                }
                y = Math.Max(leftBoxY, y + metaH) + 20;
            } else {
                // === NORDIC ATELIER LAYOUT (Default): Top accent ribbon, monogram seal, key-metric pill ===
                drawTopRibbon(gfx, themeId, pal, copy, pageW, left, right, fontBanner, brushPrimary, brushDark, penPrimaryThin);
                y = copy.topBannerLeft.Length > 0 ? 44 : 40;

                double badgeSize = 38;
                drawMonogramBadge(gfx, style, themeId, pal, left, y, badgeSize, monogram(compName), fontMonogram, brushPrimary, brushWhite, penPrimary, penPrimaryThin);

                double compX = left + badgeSize + 13;
                gfx.DrawString(compName, fontCompany, brushText, new XPoint(compX, y + 16));
                var compSub = new List<string>();
                if (compAddr.Length > 0) compSub.Add(compAddr);
                if (compPostal.Length > 0) compSub.Add(compPostal);
                if (compSub.Count > 0) {
                    gfx.DrawString(string.Join(", ", compSub), fontSmall, brushMuted, new XPoint(compX, y + 30));
                }

                drawRight(gfx, copy.docTitle, fontHero, brushPrimary, right, y + 17);
                drawRight(gfx, copy.docSubPrefix + numberText, fontNumBold, brushSub, right, y + 31);

                y += badgeSize + 16;
                if (isPuben) {
                    gfx.DrawLine(penPrimary, left, y - 4, right, y - 4);
                    gfx.DrawLine(penPrimaryThin, left, y - 1.5, right, y - 1.5);
                    y += 6;
                }

                double leftBoxY = drawRecipientBlock(gfx, style, themeId, pal, copy.recipientHeader, left, y, 245, custName, custAddr, custPostal, custOrg, custVat,
                    fontSection, fontBold, fontRegular, fontSmall, brushText, brushSub, brushMuted, brushSoft, penPrimary, penBorder);

                double rightColX = 315;
                double rightColW = right - rightColX;
                double pillH = 40;
                drawBox(gfx, penBorder, brushSoft, rightColX, y, rightColW, pillH, cornerRadius);
                if (isTech) drawCornerTicks(gfx, penPrimary, rightColX, y, rightColW, pillH, 4.5);
                gfx.DrawLine(penBorder, rightColX + rightColW * 0.52, y + 6, rightColX + rightColW * 0.52, y + pillH - 6);

                gfx.DrawString(copy.stripTotal, fontSection, brushPrimary, new XPoint(rightColX + 10, y + 14));
                gfx.DrawString(Money.format(totals.total) + " kr", fontNumBold, brushText, new XPoint(rightColX + 10, y + 29));

                double dueColX = rightColX + rightColW * 0.52 + 10;
                gfx.DrawString(copy.stripDue, fontSection, brushMuted, new XPoint(dueColX, y + 14));
                gfx.DrawString(dueDate, fontNumBold, brushText, new XPoint(dueColX, y + 29));

                double my = y + pillH + 15;
                foreach (var m in meta.Where(x => !x.isDue)) {
                    gfx.DrawString(m.label, fontSmall, brushMuted, new XPoint(rightColX + 4, my));
                    drawRight(gfx, m.val, fontNumBold, brushText, right, my);
                    my += 14.5;
                }

                y = Math.Max(leftBoxY, my) + 16;
            }

            // === 3. SPECIFICATION TABLE ===
            double colDescX = left + 8;
            double colDescW = 240;
            double colQtyR = left + 305;
            double colPriceR = left + 382;
            double colVatR = left + 430;
            double colNetR = right - 8;

            y = drawTableHeader(gfx, style, themeId, pal, left, right, width, y, copy,
                colDescX, colQtyR, colPriceR, colVatR, colNetR,
                fontHeader, brushPrimary, brushMuted, brushWhite, brushSoft, penPrimary, penPrimaryThin, penLine);

            int rowIndex = 0;
            foreach (var line in lines) {
                var wrappedDesc = wrapLines(gfx, line.text ?? "", fontRegular, colDescW);
                double rowH = Math.Max(22, wrappedDesc.Count * 13 + 9);

                if (y + rowH > 650) {
                    drawFooter(gfx, company, left, right, width, pal, copy, fontSection, fontSmall, fontNumBold, brushText, brushMuted, brushPrimary, penLine, pages.Count, 0);
                    currentPage = doc.AddPage();
                    currentPage.Size = PageSize.A4;
                    gfx = XGraphics.FromPdfPage(currentPage);
                    pages.Add((currentPage, gfx));

                    drawPageBackdrop(gfx, themeId, pal, pageW, left, right, fontSection, fontBanner);
                    if (style == Settings.StyleNordic || copy.topBannerLeft.Length > 0) {
                        drawTopRibbon(gfx, themeId, pal, copy, pageW, left, right, fontBanner, brushPrimary, brushDark, penPrimaryThin);
                    }
                    y = 44;
                    gfx.DrawString(compName + "  —  " + copy.docTitle + " " + numberText + " (forts.)", fontSmall, brushMuted, new XPoint(left, y));
                    y += 14;
                    y = drawTableHeader(gfx, style, themeId, pal, left, right, width, y, copy,
                        colDescX, colQtyR, colPriceR, colVatR, colNetR,
                        fontHeader, brushPrimary, brushMuted, brushWhite, brushSoft, penPrimary, penPrimaryThin, penLine);
                }

                if (rowIndex % 2 == 1 && style != Settings.StyleExecutive) {
                    gfx.DrawRectangle(brushZebra, left, y, width, rowH);
                }

                double textY = y + 14.5;
                for (int i = 0; i < wrappedDesc.Count; i++) {
                    gfx.DrawString(wrappedDesc[i], fontRegular, brushText, new XPoint(colDescX, textY + i * 13));
                }
                drawRight(gfx, Money.formatDecimal(line.quantity), fontNum, brushSub, colQtyR, textY);
                drawRight(gfx, Money.formatOrBlank(line.unitPrice), fontNum, brushSub, colPriceR, textY);
                drawRight(gfx, line.vatRate + " %", fontNum, brushMuted, colVatR, textY);
                drawRight(gfx, Money.formatOrBlank(line.net), fontNumBold, brushText, colNetR, textY);

                y += rowH;
                gfx.DrawLine(penLine, left, y, right, y);
                rowIndex++;
            }

            if (y > 545) {
                drawFooter(gfx, company, left, right, width, pal, copy, fontSection, fontSmall, fontNumBold, brushText, brushMuted, brushPrimary, penLine, pages.Count, 0);
                currentPage = doc.AddPage();
                currentPage.Size = PageSize.A4;
                gfx = XGraphics.FromPdfPage(currentPage);
                pages.Add((currentPage, gfx));
                drawPageBackdrop(gfx, themeId, pal, pageW, left, right, fontSection, fontBanner);
                if (style == Settings.StyleNordic || copy.topBannerLeft.Length > 0) {
                    drawTopRibbon(gfx, themeId, pal, copy, pageW, left, right, fontBanner, brushPrimary, brushDark, penPrimaryThin);
                }
                y = 50;
            }

            // === 4. TOTALS SUMMARY, NOTES & THEME STAMP ===
            y += 12;
            double notesStartY = y;

            double noteW = 260;
            double noteY = notesStartY + 6;
            string treatment = Db.str(invoice, "vatTreatment");
            bool goods = Invoicing.isGoods(invoice);
            string vatNote = treatment == Invoicing.EuReverseCharge
                ? (goods ? "Unionsintern leverans, undantagen från skatteplikt (artikel 138 i mervärdesskattedirektivet). Intra-Community supply, exempt (Article 138 VAT Directive)."
                    : "Omvänd betalningsskyldighet (artikel 196 i mervärdesskattedirektivet). Reverse charge (Article 196 VAT Directive).")
                : treatment == Invoicing.Export
                ? (goods ? "Export, undantagen från skatteplikt. Export, exempt from VAT." : "Tjänsten är inte skattepliktig i Sverige. Service not subject to Swedish VAT.")
                : "";

            if (vatNote.Length > 0) {
                noteY = drawTextWrap(gfx, vatNote, fontSmall, brushMuted, left, noteY, noteW, 12) + 8;
            }

            string note = Db.str(invoice, "note").Trim();
            if (note.Length > 0) {
                gfx.DrawString(copy.noteHeader, fontSection, brushPrimary, new XPoint(left, noteY));
                noteY += 13;
                noteY = drawTextWrap(gfx, note, fontRegular, brushSub, left, noteY, noteW, 13.5) + 8;
            }

            if (copy.stampTitle.Length > 0) {
                noteY = drawThemeStamp(gfx, themeId, pal, copy, left + 4, noteY + 4, 252, fontHeader, fontBanner);
            }

            double totX = 325;
            double totW = right - totX;
            double totY = notesStartY;

            gfx.DrawString(copy.labelNetSum, fontSmall, brushMuted, new XPoint(totX + 8, totY + 10));
            drawRight(gfx, Money.formatOrBlank(totals.net) + " kr", fontNum, brushText, colNetR, totY + 10);
            totY += 17;

            foreach (var r in totals.byRate.Where(r => r.Key > 0).Reverse()) {
                gfx.DrawString(string.Format(copy.labelVatFormat, r.Key, Money.format(r.Value.net)), fontSmall, brushMuted, new XPoint(totX + 8, totY + 10));
                drawRight(gfx, Money.formatOrBlank(r.Value.vat) + " kr", fontNum, brushText, colNetR, totY + 10);
                totY += 17;
            }

            totY += 4;
            if (style == Settings.StyleExecutive) {
                gfx.DrawLine(penPrimary, totX, totY, right, totY);
                totY += 6;
                gfx.DrawString(copy.labelGrandTotal, fontTotalLabel, brushText, new XPoint(totX + 8, totY + 13));
                drawRight(gfx, Money.format(totals.total) + " kr", fontTotalVal, brushPrimary, colNetR, totY + 13);
                totY += 20;
                gfx.DrawLine(penPrimary, totX, totY, right, totY);
                gfx.DrawLine(penPrimaryThin, totX, totY + 2.5, right, totY + 2.5);
                totY += 10;
            } else {
                double barH = 30;
                if (isSkynet) {
                    drawChamferedBar(gfx, brushPrimary, totX, totY, totW, barH, 6);
                } else {
                    drawBox(gfx, null, brushPrimary, totX, totY, totW, barH, isTech ? 0 : 5);
                }
                gfx.DrawString(copy.labelGrandTotal, fontTotalLabel, brushWhite, new XPoint(totX + 10, totY + 19));
                drawRight(gfx, Money.format(totals.total) + " kr", fontTotalVal, brushWhite, right - 10, totY + 19.5);
                totY += barH + 10;
            }

            y = Math.Max(noteY, totY) + 12;

            // === 5. PROMINENT PAYMENT SLIP / BETALNINGSKORT ===
            drawPaymentCard(gfx, style, themeId, pal, copy, left, right, width, y,
                bankgiro, numberText, dueDate, Money.format(totals.total) + " kr",
                isPaid, Db.str(invoice, "paidDate"),
                fontSection, fontSmall, fontNumBold, fontCardBig, fontBanner,
                brushText, brushMuted, brushWhite, brushPrimary, brushSoft, brushPaid, brushPaidSoft,
                penPrimary, penPrimaryThin, penBorder);

            // === 6. PINNED 4-COLUMN FOOTER ON ALL PAGES ===
            int totalPages = pages.Count;
            for (int i = 0; i < totalPages; i++) {
                drawFooter(pages[i].gfx, company, left, right, width, pal, copy, fontSection, fontSmall, fontNumBold, brushText, brushMuted, brushPrimary, penLine, i + 1, totalPages);
            }
        }

        static void drawTopRibbon(
            XGraphics gfx, string themeId, Palette pal, ThemeCopy copy, double pageW, double left, double right,
            XFont fontBanner, XBrush brushPrimary, XBrush brushDark, XPen penPrimaryThin) {

            if (themeId == "skynet") {
                drawHazardStripes(gfx, pal, 0, 0, pageW, 5.5);
                gfx.DrawRectangle(brushDark, 0, 5.5, pageW, 16);
                gfx.DrawRectangle(brushPrimary, 0, 21.5, pageW, 2);
                var glowBrush = new XSolidBrush(pal.glow);
                gfx.DrawString(copy.topBannerLeft, fontBanner, glowBrush, new XPoint(left, 16));
                drawRight(gfx, copy.topBannerRight, fontBanner, glowBrush, right, 16);
            } else if (themeId == "nostromo") {
                gfx.DrawRectangle(brushDark, 0, 0, pageW, 19);
                gfx.DrawRectangle(brushPrimary, 0, 19, pageW, 2);
                var glowBrush = new XSolidBrush(pal.glow);
                gfx.DrawString(copy.topBannerLeft, fontBanner, glowBrush, new XPoint(left, 12.5));
                drawRight(gfx, copy.topBannerRight, fontBanner, glowBrush, right, 12.5);
            } else if (themeId == "puben") {
                gfx.DrawRectangle(brushPrimary, 0, 0, pageW, 4);
                gfx.DrawLine(penPrimaryThin, left, 21, right, 21);
                drawCentered(gfx, copy.topBannerLeft, fontBanner, brushPrimary, pageW / 2, 15.5);
            } else {
                gfx.DrawRectangle(brushPrimary, 0, 0, pageW, 6);
            }
        }

        static void drawPageBackdrop(
            XGraphics gfx, string themeId, Palette pal, double pageW, double left, double right,
            XFont fontSection, XFont fontBanner) {

            if (themeId == "puben") {
                // Warm tavern parchment tint & subtle pint-glass stains
                gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(0xfd, 0xfa, 0xf2)), 0, 0, pageW, 842);
                var framePen = new XPen(XColor.FromArgb(55, pal.primary.R, pal.primary.G, pal.primary.B), 0.7);
                gfx.DrawRectangle(framePen, 24, 26, pageW - 48, 796);
                drawBeerStain(gfx, right - 68, 158, 38);
                drawBeerStain(gfx, left + 205, 608, 33);
            } else if (themeId == "nostromo") {
                // Continuous-feed dot-matrix sprocket holes along both page margins + orbital radar watermark
                var holePen = new XPen(XColor.FromArgb(95, pal.primary.R, pal.primary.G, pal.primary.B), 0.7);
                var holeFill = new XSolidBrush(XColor.FromArgb(18, pal.primary.R, pal.primary.G, pal.primary.B));
                var perfPen = new XPen(XColor.FromArgb(65, pal.primary.R, pal.primary.G, pal.primary.B), 0.5) { DashStyle = XDashStyle.Dash };
                gfx.DrawLine(perfPen, 28, 22, 28, 840);
                gfx.DrawLine(perfPen, pageW - 28, 22, pageW - 28, 840);
                for (double hy = 34; hy < 825; hy += 24) {
                    gfx.DrawEllipse(holePen, holeFill, 10.5, hy - 3.5, 7, 7);
                    gfx.DrawEllipse(holePen, holeFill, pageW - 17.5, hy - 3.5, 7, 7);
                }
                double cx = pageW / 2;
                double cy = 415;
                var radarPen = new XPen(XColor.FromArgb(20, pal.primary.R, pal.primary.G, pal.primary.B), 0.8);
                gfx.DrawEllipse(radarPen, cx - 105, cy - 105, 210, 210);
                gfx.DrawEllipse(radarPen, cx - 70, cy - 70, 140, 140);
                gfx.DrawEllipse(radarPen, cx - 35, cy - 35, 70, 70);
                gfx.DrawLine(radarPen, cx - 125, cy, cx + 125, cy);
                gfx.DrawLine(radarPen, cx, cy - 125, cx, cy + 125);
                drawCornerTicks(gfx, new XPen(XColor.FromArgb(35, pal.primary.R, pal.primary.G, pal.primary.B), 1.2), cx - 115, cy - 115, 230, 230, 14);
                drawCentered(gfx, "WEYLAND-YUTANI CORP // BUILDING BETTER CASHFLOWS", fontBanner, new XSolidBrush(XColor.FromArgb(32, pal.primary.R, pal.primary.G, pal.primary.B)), cx, cy + 126);
            } else if (themeId == "skynet") {
                // Tactical targeting reticle watermark in center of page
                double cx = pageW / 2;
                double cy = 415;
                var retPen = new XPen(XColor.FromArgb(22, pal.primary.R, pal.primary.G, pal.primary.B), 0.9);
                var retHeavy = new XPen(XColor.FromArgb(32, pal.primary.R, pal.primary.G, pal.primary.B), 1.5);
                gfx.DrawEllipse(retPen, cx - 112, cy - 112, 224, 224);
                gfx.DrawEllipse(retHeavy, cx - 76, cy - 76, 152, 152);
                gfx.DrawEllipse(retPen, cx - 36, cy - 36, 72, 72);
                gfx.DrawEllipse(retHeavy, cx - 5, cy - 5, 10, 10);
                gfx.DrawLine(retPen, cx - 134, cy, cx - 14, cy);
                gfx.DrawLine(retPen, cx + 14, cy, cx + 134, cy);
                gfx.DrawLine(retPen, cx, cy - 134, cx, cy - 14);
                gfx.DrawLine(retPen, cx, cy + 14, cx, cy + 134);
                for (int d = -100; d <= 100; d += 25) {
                    if (d == 0) continue;
                    gfx.DrawLine(retPen, cx + d, cy - 4, cx + d, cy + 4);
                    gfx.DrawLine(retPen, cx - 4, cy + d, cx + 4, cy + d);
                }
                drawCornerTicks(gfx, retHeavy, cx - 120, cy - 120, 240, 240, 18);
                drawCentered(gfx, "TARGET ACQUIRED // FISCAL EXTRACTION LOCKED", fontBanner, new XSolidBrush(XColor.FromArgb(36, pal.primary.R, pal.primary.G, pal.primary.B)), cx, cy + 134);
            }
        }

        static void drawBeerStain(XGraphics gfx, double cx, double cy, double r) {
            var p1 = new XPen(XColor.FromArgb(20, 154, 102, 18), 2.2);
            var p2 = new XPen(XColor.FromArgb(13, 120, 72, 12), 1.1);
            var p3 = new XPen(XColor.FromArgb(9, 154, 102, 18), 3.6);
            gfx.DrawEllipse(p1, cx - r, cy - r, r * 2, r * 2);
            gfx.DrawEllipse(p2, cx - r + 1.8, cy - r - 1.2, r * 2 - 2, r * 2 + 1.5);
            gfx.DrawEllipse(p3, cx - r - 2.5, cy - r + 2, r * 1.92, r * 1.96);
            var dotBrush = new XSolidBrush(XColor.FromArgb(16, 154, 102, 18));
            gfx.DrawEllipse(dotBrush, cx + r + 5, cy - 12, 3.2, 3.2);
            gfx.DrawEllipse(dotBrush, cx + r + 9, cy - 6, 2.0, 2.0);
            gfx.DrawEllipse(dotBrush, cx - r - 6, cy + 14, 2.5, 2.5);
        }

        static void drawHazardStripes(XGraphics gfx, Palette pal, double x, double y, double w, double h) {
            gfx.DrawRectangle(new XSolidBrush(pal.dark), x, y, w, h);
            var stripeBrush = new XSolidBrush(pal.primary);
            double step = 12;
            for (double sx = x - h; sx < x + w; sx += step) {
                double x0 = Math.Max(x, sx);
                double x1 = Math.Min(x + w, sx + step * 0.52);
                double x2 = Math.Min(x + w, sx + h + step * 0.52);
                double x3 = Math.Max(x, sx + h);
                if (x1 > x0 && x2 > x3) {
                    gfx.DrawPolygon(stripeBrush, new[] {
                        new XPoint(x0, y + h),
                        new XPoint(x1, y + h),
                        new XPoint(x2, y),
                        new XPoint(x3, y)
                    }, XFillMode.Winding);
                }
            }
        }

        static double drawThemeStamp(
            XGraphics gfx, string themeId, Palette pal, ThemeCopy copy,
            double x, double y, double w, XFont fontHeader, XFont fontBanner) {

            double h = 32;
            double angle = themeId == "puben" ? -3.2 : themeId == "skynet" ? -2.4 : -1.8;
            var state = gfx.Save();
            gfx.RotateAtTransform(angle, new XPoint(x + w / 2, y + h / 2));

            var stampColor = XColor.FromArgb(210, pal.primary.R, pal.primary.G, pal.primary.B);
            var stampFill = new XSolidBrush(XColor.FromArgb(16, pal.primary.R, pal.primary.G, pal.primary.B));
            var outerPen = new XPen(stampColor, 1.5);
            var innerPen = new XPen(stampColor, 0.6);
            var stampBrush = new XSolidBrush(stampColor);

            gfx.DrawRectangle(outerPen, stampFill, x, y, w, h);
            gfx.DrawRectangle(innerPen, x + 2.5, y + 2.5, w - 5, h - 5);
            drawCentered(gfx, copy.stampTitle, fontHeader, stampBrush, x + w / 2, y + 14);
            drawCentered(gfx, copy.stampSub, fontBanner, stampBrush, x + w / 2, y + 24.5);

            gfx.Restore(state);
            return y + h + 8;
        }

        static void drawMonogramBadge(
            XGraphics gfx, string style, string themeId, Palette pal,
            double x, double y, double size, string initials,
            XFont fontMonogram, XBrush brushPrimary, XBrush brushWhite,
            XPen penPrimary, XPen penPrimaryThin) {

            bool isTech = themeId == "nostromo" || themeId == "skynet";
            if (style == Settings.StyleExecutive) {
                gfx.DrawRectangle(penPrimary, x, y, size, size);
                gfx.DrawRectangle(penPrimaryThin, x + 3, y + 3, size - 6, size - 6);
                drawCentered(gfx, initials, fontMonogram, brushPrimary, x + size / 2, y + size / 2 + 4.8);
                return;
            }
            if (themeId == "skynet") {
                drawChamferedBar(gfx, brushPrimary, x, y, size, size, 6);
                drawCornerTicks(gfx, new XPen(XColor.FromArgb(160, 255, 255, 255), 0.8), x + 3, y + 3, size - 6, size - 6, 4);
                drawCentered(gfx, initials, fontMonogram, brushWhite, x + size / 2, y + size / 2 + 4.8);
                return;
            }
            if (isTech) {
                gfx.DrawRectangle(brushPrimary, x, y, size, size);
                drawCornerTicks(gfx, new XPen(XColor.FromArgb(150, 255, 255, 255), 0.85), x + 3, y + 3, size - 6, size - 6, 4.5);
                drawCentered(gfx, initials, fontMonogram, brushWhite, x + size / 2, y + size / 2 + 4.8);
                return;
            }
            double r = themeId == "puben" ? 4 : 8;
            gfx.DrawRoundedRectangle(brushPrimary, x, y, size, size, r, r);
            gfx.DrawRoundedRectangle(new XPen(XColor.FromArgb(95, 255, 255, 255), 0.85), x + 3, y + 3, size - 6, size - 6, Math.Max(2, r - 2), Math.Max(2, r - 2));
            drawCentered(gfx, initials, fontMonogram, brushWhite, x + size / 2, y + size / 2 + 4.8);
        }

        static double drawRecipientBlock(
            XGraphics gfx, string style, string themeId, Palette pal, string recipientHeader, double x, double y, double w,
            string custName, string custAddr, string custPostal, string custOrg, string custVat,
            XFont fontSection, XFont fontBold, XFont fontRegular, XFont fontSmall,
            XBrush brushText, XBrush brushSub, XBrush brushMuted, XBrush brushSoft,
            XPen penPrimary, XPen penBorder) {

            bool isTech = themeId == "nostromo" || themeId == "skynet";

            var lines = new List<(string text, XFont font, XBrush brush)>();
            if (custName.Length > 0) lines.Add((custName, fontBold, brushText));
            if (custAddr.Length > 0) lines.Add((custAddr, fontRegular, brushSub));
            if (custPostal.Length > 0) lines.Add((custPostal, fontRegular, brushSub));
            if (custOrg.Length > 0) lines.Add(("Org.nr " + custOrg, fontSmall, brushMuted));
            if (custVat.Length > 0) lines.Add(("Momsreg.nr " + custVat, fontSmall, brushMuted));

            double boxH = Math.Max(60, 29 + lines.Count * 14.5);
            if (style == Settings.StyleNordic) {
                drawBox(gfx, penBorder, brushSoft, x, y, w, boxH, isTech ? 0 : 6);
                gfx.DrawRectangle(new XSolidBrush(pal.primary), x, y + (isTech ? 0 : 6), 3.5, boxH - (isTech ? 0 : 12));
                if (isTech) drawCornerTicks(gfx, penPrimary, x, y, w, boxH, 5);
            } else if (style == Settings.StyleExecutive) {
                gfx.DrawLine(penBorder, x, y, x + w, y);
                gfx.DrawLine(penBorder, x, y + boxH, x + w, y + boxH);
            } else {
                gfx.DrawRectangle(penBorder, x, y, w, boxH);
                if (isTech) drawCornerTicks(gfx, penPrimary, x, y, w, boxH, 5);
            }

            double padX = style == Settings.StyleExecutive ? x : x + 13;
            gfx.DrawString(recipientHeader, fontSection, new XSolidBrush(pal.primary), new XPoint(padX, y + 15));
            double curY = y + 31;
            foreach (var l in lines) {
                gfx.DrawString(l.text, l.font, l.brush, new XPoint(padX, curY));
                curY += 14.5;
            }
            return y + boxH;
        }

        static double drawTableHeader(
            XGraphics gfx, string style, string themeId, Palette pal, double left, double right, double width, double y,
            ThemeCopy copy,
            double colDescX, double colQtyR, double colPriceR, double colVatR, double colNetR,
            XFont fontHeader, XBrush brushPrimary, XBrush brushMuted, XBrush brushWhite, XBrush brushSoft,
            XPen penPrimary, XPen penPrimaryThin, XPen penLine) {

            bool isTech = themeId == "nostromo" || themeId == "skynet";
            double h = 24;
            if (style == Settings.StyleStudio) {
                gfx.DrawRectangle(brushPrimary, left, y, width, h);
                gfx.DrawString(copy.colDesc, fontHeader, brushWhite, new XPoint(colDescX, y + 15));
                drawRight(gfx, copy.colQty, fontHeader, brushWhite, colQtyR, y + 15);
                drawRight(gfx, copy.colPrice, fontHeader, brushWhite, colPriceR, y + 15);
                drawRight(gfx, copy.colVat, fontHeader, brushWhite, colVatR, y + 15);
                drawRight(gfx, copy.colAmount, fontHeader, brushWhite, colNetR, y + 15);
                return y + h;
            }
            if (style == Settings.StyleExecutive) {
                gfx.DrawLine(penPrimary, left, y, right, y);
                gfx.DrawString(copy.colDesc, fontHeader, brushPrimary, new XPoint(colDescX, y + 15));
                drawRight(gfx, copy.colQty, fontHeader, brushPrimary, colQtyR, y + 15);
                drawRight(gfx, copy.colPrice, fontHeader, brushPrimary, colPriceR, y + 15);
                drawRight(gfx, copy.colVat, fontHeader, brushPrimary, colVatR, y + 15);
                drawRight(gfx, copy.colAmount, fontHeader, brushPrimary, colNetR, y + 15);
                gfx.DrawLine(penPrimaryThin, left, y + h, right, y + h);
                return y + h;
            }
            drawBox(gfx, null, brushSoft, left, y, width, h, isTech ? 0 : 4);
            gfx.DrawLine(penPrimary, left, y, right, y);
            gfx.DrawString(copy.colDesc, fontHeader, brushPrimary, new XPoint(colDescX, y + 15));
            drawRight(gfx, copy.colQty, fontHeader, brushMuted, colQtyR, y + 15);
            drawRight(gfx, copy.colPrice, fontHeader, brushMuted, colPriceR, y + 15);
            drawRight(gfx, copy.colVat, fontHeader, brushMuted, colVatR, y + 15);
            drawRight(gfx, copy.colAmount, fontHeader, brushPrimary, colNetR, y + 15);
            gfx.DrawLine(penLine, left, y + h, right, y + h);
            return y + h;
        }

        static void drawPaymentCard(
            XGraphics gfx, string style, string themeId, Palette pal, ThemeCopy copy, double left, double right, double width, double y,
            string bankgiro, string numberText, string dueDate, string totalFormatted,
            bool isPaid, string paidDate,
            XFont fontSection, XFont fontSmall, XFont fontNumBold, XFont fontCardBig, XFont fontBanner,
            XBrush brushText, XBrush brushMuted, XBrush brushWhite, XBrush brushPrimary, XBrush brushSoft,
            XBrush brushPaid, XBrush brushPaidSoft,
            XPen penPrimary, XPen penPrimaryThin, XPen penBorder) {

            bool isTech = themeId == "nostromo" || themeId == "skynet";

            double targetY = Math.Max(y, 666);
            if (targetY > 696) targetY = y;

            double cardH = 58;
            if (style == Settings.StyleExecutive || themeId == "puben") {
                gfx.DrawRectangle(penBorder, brushSoft, left, targetY, width, cardH);
                gfx.DrawLine(penPrimary, left, targetY, right, targetY);
                gfx.DrawLine(penPrimaryThin, left, targetY + 2.5, right, targetY + 2.5);
            } else {
                drawBox(gfx, penBorder, brushSoft, left, targetY, width, cardH, isTech ? 0 : 7);
                gfx.DrawRectangle(brushPrimary, left, targetY + (isTech ? 0 : 7), 4, cardH - (isTech ? 0 : 14));
                if (themeId == "skynet") {
                    drawHazardStripes(gfx, pal, left, targetY, width, 3.2);
                }
                if (isTech) drawCornerTicks(gfx, penPrimary, left, targetY, width, cardH, 5.5);
            }

            gfx.DrawString(copy.payHeader, fontSection, brushPrimary, new XPoint(left + 14, targetY + 15.5));

            double c1 = left + 14;
            double c2 = left + 145;
            double c3 = left + 275;
            double c4 = left + 390;

            gfx.DrawString(copy.payColBg, fontSmall, brushMuted, new XPoint(c1, targetY + 31));
            gfx.DrawString(bankgiro.Length > 0 ? bankgiro : "—", fontCardBig, brushText, new XPoint(c1, targetY + 46));

            gfx.DrawString(copy.payColRef, fontSmall, brushMuted, new XPoint(c2, targetY + 31));
            gfx.DrawString(numberText, fontNumBold, brushText, new XPoint(c2, targetY + 45));

            gfx.DrawString(copy.payColDue, fontSmall, brushMuted, new XPoint(c3, targetY + 31));
            gfx.DrawString(dueDate, fontNumBold, brushText, new XPoint(c3, targetY + 45));

            if (isPaid) {
                string pText = paidDate.Length > 0 ? copy.payPaidPrefix + " " + paidDate : copy.payPaidPrefix;
                drawBox(gfx, new XPen(ColorPaid, 1.0), brushPaidSoft, c4, targetY + 22, right - c4 - 12, 28, isTech ? 0 : 5);
                drawCentered(gfx, pText, fontSection, brushPaid, c4 + (right - c4 - 12) / 2, targetY + 39);
            } else {
                gfx.DrawString(copy.payColTotal, fontSmall, brushMuted, new XPoint(c4, targetY + 31));
                gfx.DrawString(totalFormatted, fontCardBig, brushPrimary, new XPoint(c4, targetY + 46));
            }

            if (copy.telemetryText.Length > 0 && targetY + cardH + 14 <= 756) {
                double barY = targetY + cardH + 5;
                if (isTech) {
                    double bx = left;
                    int seed = Math.Abs(copy.telemetryText.GetHashCode());
                    for (int b = 0; b < 26; b++) {
                        double bw = ((seed >> (b % 16)) & 1) == 1 ? 1.8 : 0.8;
                        gfx.DrawRectangle(brushPrimary, bx, barY, bw, 8.5);
                        bx += bw + 1.4;
                    }
                    gfx.DrawString(copy.telemetryText, fontBanner, brushMuted, new XPoint(bx + 6, barY + 7));
                } else {
                    drawCentered(gfx, copy.telemetryText, fontBanner, brushMuted, left + width / 2, barY + 7);
                }
            }
        }

        static void drawFooter(
            XGraphics gfx, SwecialObject company, double left, double right, double width, Palette pal, ThemeCopy copy,
            XFont fontHeader, XFont fontRegular, XFont fontBold,
            XBrush brushText, XBrush brushMuted, XBrush brushPrimary, XPen penLine,
            int pageNum, int totalPages) {

            double footerY = 758;
            gfx.DrawLine(penLine, left, footerY, right, footerY);
            footerY += 6;

            string compName = Db.str(company, "name");
            string compAddr = Db.str(company, "address");
            string compPostal = Db.str(company, "postalAddress");
            string phone = Db.str(company, "phone");
            string email = Db.str(company, "email");
            string orgNr = Db.str(company, "orgNumber");
            string vatNr = Db.str(company, "vatNumber");
            bool fskatt = Db.str(company, "fskatt") != "0";
            string bankgiro = Db.str(company, "bankgiro");

            var col1Lines = new List<string>();
            if (compName.Length > 0) col1Lines.Add(compName);
            if (compAddr.Length > 0) col1Lines.Add(compAddr);
            if (compPostal.Length > 0) col1Lines.Add(compPostal);

            var col2Lines = new List<string>();
            if (email.Length > 0) col2Lines.Add(email);
            if (phone.Length > 0) col2Lines.Add(phone);

            var col3Lines = new List<string>();
            if (orgNr.Length > 0) col3Lines.Add("Org.nr: " + orgNr);
            if (vatNr.Length > 0) col3Lines.Add("Momsreg.nr: " + vatNr);
            if (fskatt) col3Lines.Add("Godkänd för F-skatt");

            var col4Lines = new List<string>();
            if (bankgiro.Length > 0) col4Lines.Add(bankgiro);
            if (totalPages > 1) col4Lines.Add("Sida " + pageNum + " (" + totalPages + ")");

            var cols = new List<(string header, List<string> lines, bool highlight)> {
                (copy.footAddress, col1Lines, false),
                (copy.footContact, col2Lines, false),
                (copy.footLegal, col3Lines, false),
                (copy.footBg, col4Lines, true)
            }.Where(c => c.lines.Count > 0).ToList();

            if (cols.Count > 0) {
                var colWidths = cols.Select(c => {
                    double maxW = gfx.MeasureString(c.header, fontHeader).Width;
                    foreach (var l in c.lines) {
                        double lw = gfx.MeasureString(l, c.highlight ? fontBold : fontRegular).Width;
                        if (lw > maxW) maxW = lw;
                    }
                    return maxW;
                }).ToList();

                double totalW = colWidths.Sum();
                double gap = cols.Count > 1 ? Math.Max(10, (width - totalW) / (cols.Count - 1)) : 0;

                double curX = left;
                for (int i = 0; i < cols.Count; i++) {
                    var col = cols[i];
                    double x = (i == cols.Count - 1) ? Math.Max(curX, right - colWidths[i]) : curX;
                    gfx.DrawString(col.header, fontHeader, brushPrimary, new XPoint(x, footerY + 7));
                    double lineY = footerY + 19;
                    for (int j = 0; j < col.lines.Count; j++) {
                        var f = (col.highlight && j == 0) ? fontBold : fontRegular;
                        gfx.DrawString(col.lines[j], f, brushText, new XPoint(x, lineY));
                        lineY += 11.5;
                    }
                    curX += colWidths[i] + gap;
                }
            }
        }

        static void drawBox(XGraphics gfx, XPen pen, XBrush brush, double x, double y, double w, double h, double radius) {
            if (radius <= 0) {
                if (pen != null && brush != null) gfx.DrawRectangle(pen, brush, x, y, w, h);
                else if (brush != null) gfx.DrawRectangle(brush, x, y, w, h);
                else if (pen != null) gfx.DrawRectangle(pen, x, y, w, h);
            } else {
                if (pen != null && brush != null) gfx.DrawRoundedRectangle(pen, brush, x, y, w, h, radius, radius);
                else if (brush != null) gfx.DrawRoundedRectangle(brush, x, y, w, h, radius, radius);
                else if (pen != null) gfx.DrawRoundedRectangle(pen, x, y, w, h, radius, radius);
            }
        }

        static void drawChamferedBar(XGraphics gfx, XBrush brush, double x, double y, double w, double h, double cut) {
            var pts = new[] {
                new XPoint(x, y),
                new XPoint(x + w - cut, y),
                new XPoint(x + w, y + cut),
                new XPoint(x + w, y + h),
                new XPoint(x + cut, y + h),
                new XPoint(x, y + h - cut)
            };
            gfx.DrawPolygon(brush, pts, XFillMode.Winding);
        }

        static void drawCornerTicks(XGraphics gfx, XPen pen, double x, double y, double w, double h, double len) {
            // Top-left
            gfx.DrawLine(pen, x, y, x + len, y);
            gfx.DrawLine(pen, x, y, x, y + len);
            // Top-right
            gfx.DrawLine(pen, x + w - len, y, x + w, y);
            gfx.DrawLine(pen, x + w, y, x + w, y + len);
            // Bottom-left
            gfx.DrawLine(pen, x, y + h - len, x, y + h);
            gfx.DrawLine(pen, x, y + h, x + len, y + h);
            // Bottom-right
            gfx.DrawLine(pen, x + w - len, y + h, x + w, y + h);
            gfx.DrawLine(pen, x + w, y + h - len, x + w, y + h);
        }

        static List<string> wrapLines(XGraphics gfx, string text, XFont font, double maxWidth) {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) {
                result.Add("");
                return result;
            }
            var paragraphs = text.Replace("\r\n", "\n").Split('\n');
            foreach (var p in paragraphs) {
                var words = p.Split(' ');
                var line = "";
                foreach (var word in words) {
                    var candidate = line.Length == 0 ? word : line + " " + word;
                    if (gfx.MeasureString(candidate, font).Width > maxWidth && line.Length > 0) {
                        result.Add(line);
                        line = word;
                    } else {
                        line = candidate;
                    }
                }
                if (line.Length > 0 || paragraphs.Length > 1) result.Add(line);
            }
            if (result.Count == 0) result.Add("");
            return result;
        }

        static double drawTextWrap(XGraphics gfx, string text, XFont font, XBrush brush, double x, double y, double maxWidth, double lineHeight) {
            if (string.IsNullOrEmpty(text)) return y;
            foreach (var line in wrapLines(gfx, text, font, maxWidth)) {
                gfx.DrawString(line, font, brush, new XPoint(x, y));
                y += lineHeight;
            }
            return y;
        }

        static void drawRight(XGraphics gfx, string text, XFont font, XBrush brush, double rightX, double y) {
            if (string.IsNullOrEmpty(text)) return;
            var size = gfx.MeasureString(text, font);
            gfx.DrawString(text, font, brush, new XPoint(rightX - size.Width, y));
        }

        static void drawCentered(XGraphics gfx, string text, XFont font, XBrush brush, double centerX, double y) {
            if (string.IsNullOrEmpty(text)) return;
            var size = gfx.MeasureString(text, font);
            gfx.DrawString(text, font, brush, new XPoint(centerX - size.Width / 2, y));
        }
    }
}
