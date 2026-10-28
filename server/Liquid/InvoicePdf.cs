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
            var totals = Invoicing.totals(sampleLines);
            return generate(company, sampleInvoice, sampleCustomer, sampleLines, totals, styleOverride, accentOverride, themeId);
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
            string numberText = isDraft ? "Utkast" : Db.num(invoice, "number").ToString();
            string compName = Db.str(company, "name");
            string compAddr = Db.str(company, "address");
            string compPostal = Db.str(company, "postalAddress");
            string bankgiro = Db.str(company, "bankgiro");
            string orgNr = Db.str(company, "orgNumber");

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

            var meta = new List<(string label, string val)> {
                ("Fakturanummer", numberText),
                ("Fakturadatum", invDate),
                ("Förfallodatum", dueDate)
            };
            if (ourRef.Length > 0) meta.Add(("Vår referens", ourRef));
            if (yourRef.Length > 0) meta.Add(("Er referens", yourRef));
            if (poNumber.Length > 0) meta.Add((poLabel, poNumber));

            var pages = new List<(PdfPage page, XGraphics gfx)>();
            PdfPage currentPage = doc.AddPage();
            currentPage.Size = PageSize.A4;
            XGraphics gfx = XGraphics.FromPdfPage(currentPage);
            pages.Add((currentPage, gfx));

            double y = 0;

            if (style == Settings.StyleStudio) {
                // === STUDIO LAYOUT: Full-bleed dark/accent masthead + docked metrics bar ===
                double bannerH = 112;
                gfx.DrawRectangle(brushDark, 0, 0, pageW, bannerH);
                gfx.DrawRectangle(brushPrimary, 0, bannerH - 4, pageW, 4);
                if (isPuben) {
                    gfx.DrawLine(new XPen(XColor.FromArgb(140, 232, 168, 56), 0.7), left, bannerH - 7, right, bannerH - 7);
                }

                double badgeSize = 38;
                double badgeX = left;
                double badgeY = 30;
                drawMonogramBadge(gfx, style, themeId, pal, badgeX, badgeY, badgeSize, monogram(compName), fontMonogram, brushPrimary, brushWhite, penPrimary, penPrimaryThin);

                double compX = badgeX + badgeSize + 13;
                gfx.DrawString(compName, fontCompany, brushWhite, new XPoint(compX, 45));
                string subLine = string.Join("  ·  ", new[] { compAddr, compPostal, orgNr.Length > 0 ? "Org.nr " + orgNr : "" }.Where(s => s.Length > 0));
                if (subLine.Length > 0) {
                    gfx.DrawString(subLine, fontSmall, new XSolidBrush(XColor.FromArgb(200, 255, 255, 255)), new XPoint(compX, 61));
                }

                string docLabel = isDraft ? "FAKTURAUTKAST" : "FAKTURA";
                drawRight(gfx, docLabel, fontHero, new XSolidBrush(pal.glow), right, 47);
                drawRight(gfx, (isTech ? "NR // " : "Nr ") + numberText, fontNumBold, new XSolidBrush(XColor.FromArgb(215, 255, 255, 255)), right, 63);

                double stripY = bannerH;
                double stripH = 44;
                gfx.DrawRectangle(brushSoft, left, stripY, width, stripH);
                gfx.DrawLine(penBorder, left, stripY + stripH, right, stripY + stripH);

                double cellW = width / 4.0;
                var stripItems = new[] {
                    (prefix + "FAKTURADATUM", invDate, false),
                    (prefix + "FÖRFALLODATUM", dueDate, false),
                    (prefix + "BANKGIRO", bankgiro.Length > 0 ? bankgiro : "—", false),
                    (prefix + "ATT BETALA (SEK)", Money.format(totals.total) + " kr", true)
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
                double leftBoxY = drawRecipientBlock(gfx, style, themeId, pal, left, y, 245, custName, custAddr, custPostal, custOrg, custVat,
                    fontSection, fontBold, fontRegular, fontSmall, brushText, brushSub, brushMuted, brushSoft, penPrimary, penBorder);

                double refY = y;
                gfx.DrawString(prefix + "REFERENSER & UPPGIFTER", fontSection, brushPrimary, new XPoint(325, refY + 10));
                refY += 24;
                foreach (var m in meta) {
                    gfx.DrawString(m.label, fontSmall, brushMuted, new XPoint(325, refY));
                    drawRight(gfx, m.val, fontNumBold, brushText, right, refY);
                    refY += 14.5;
                }
                y = Math.Max(leftBoxY, refY) + 18;
            } else if (style == Settings.StyleExecutive) {
                // === EXECUTIVE LAYOUT: Classic serif crest, double ornamental rules ===
                if (isPuben || isTech) {
                    drawTopRibbon(gfx, themeId, pageW, brushPrimary, penPrimaryThin);
                }
                y = 44;
                double crestSize = 38;
                drawMonogramBadge(gfx, style, themeId, pal, left, y, crestSize, monogram(compName), fontMonogram, brushPrimary, brushWhite, penPrimary, penPrimaryThin);

                double compX = left + crestSize + 14;
                gfx.DrawString(compName, fontCompany, brushText, new XPoint(compX, y + 16));
                string addrLine = string.Join("  ·  ", new[] { compAddr, compPostal }.Where(s => s.Length > 0));
                if (addrLine.Length > 0) {
                    gfx.DrawString(addrLine, fontItalic, brushSub, new XPoint(compX, y + 31));
                }

                string docTitle = isDraft ? "F A K T U R A U T K A S T" : "F A K T U R A";
                drawRight(gfx, docTitle, fontHero, brushPrimary, right, y + 17);
                drawRight(gfx, (isTech ? "NR // " : "Fakturanummer ") + numberText, isTech ? fontNumBold : fontItalic, brushSub, right, y + 32);

                y += crestSize + 14;
                gfx.DrawLine(penPrimary, left, y, right, y);
                gfx.DrawLine(penPrimaryThin, left, y + 2.5, right, y + 2.5);
                y += 18;

                double leftBoxY = drawRecipientBlock(gfx, style, themeId, pal, left, y, 245, custName, custAddr, custPostal, custOrg, custVat,
                    fontSection, fontBold, fontRegular, fontSmall, brushText, brushSub, brushMuted, brushSoft, penPrimary, penBorder);

                double metaX = 320;
                double metaW = right - metaX;
                double metaH = meta.Count * 15 + 16;
                gfx.DrawRectangle(penBorder, brushSoft, metaX, y, metaW, metaH);
                if (isTech) drawCornerTicks(gfx, penPrimary, metaX, y, metaW, metaH, 5);
                double my = y + 16;
                foreach (var m in meta) {
                    gfx.DrawString(m.label, fontSmall, brushMuted, new XPoint(metaX + 12, my));
                    drawRight(gfx, m.val, fontNumBold, brushText, right - 12, my);
                    my += 15;
                }
                y = Math.Max(leftBoxY, y + metaH) + 20;
            } else {
                // === NORDIC ATELIER LAYOUT (Default): Top accent ribbon, monogram seal, key-metric pill ===
                drawTopRibbon(gfx, themeId, pageW, brushPrimary, penPrimaryThin);
                y = 40;

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

                string docTitle = isDraft ? "FAKTURAUTKAST" : "FAKTURA";
                drawRight(gfx, docTitle, fontHero, brushPrimary, right, y + 17);
                drawRight(gfx, (isTech ? "NR // " : "Nr ") + numberText, fontNumBold, brushSub, right, y + 31);

                y += badgeSize + 16;
                if (isPuben) {
                    gfx.DrawLine(penPrimary, left, y - 4, right, y - 4);
                    gfx.DrawLine(penPrimaryThin, left, y - 1.5, right, y - 1.5);
                    y += 6;
                }

                double leftBoxY = drawRecipientBlock(gfx, style, themeId, pal, left, y, 245, custName, custAddr, custPostal, custOrg, custVat,
                    fontSection, fontBold, fontRegular, fontSmall, brushText, brushSub, brushMuted, brushSoft, penPrimary, penBorder);

                double rightColX = 315;
                double rightColW = right - rightColX;
                double pillH = 40;
                drawBox(gfx, penBorder, brushSoft, rightColX, y, rightColW, pillH, cornerRadius);
                if (isTech) drawCornerTicks(gfx, penPrimary, rightColX, y, rightColW, pillH, 4.5);
                gfx.DrawLine(penBorder, rightColX + rightColW * 0.54, y + 6, rightColX + rightColW * 0.54, y + pillH - 6);

                gfx.DrawString(prefix + "ATT BETALA (SEK)", fontSection, brushPrimary, new XPoint(rightColX + 11, y + 14));
                gfx.DrawString(Money.format(totals.total) + " kr", fontNumBold, brushText, new XPoint(rightColX + 11, y + 29));

                double dueColX = rightColX + rightColW * 0.54 + 11;
                gfx.DrawString(prefix + "FÖRFALLODATUM", fontSection, brushMuted, new XPoint(dueColX, y + 14));
                gfx.DrawString(dueDate, fontNumBold, brushText, new XPoint(dueColX, y + 29));

                double my = y + pillH + 15;
                foreach (var m in meta.Where(x => x.label != "Förfallodatum")) {
                    gfx.DrawString(m.label, fontSmall, brushMuted, new XPoint(rightColX + 4, my));
                    drawRight(gfx, m.val, fontNumBold, brushText, right, my);
                    my += 14.5;
                }

                y = Math.Max(leftBoxY, my) + 16;
            }

            // === 3. SPECIFICATION TABLE ===
            bool isHourly = Db.str(invoice, "isHourly") == "1";
            string qtyHeader = isHourly ? "TIMMAR" : "ANTAL";
            string priceHeader = isHourly ? "TIMARVODE" : "À-PRIS";

            double colDescX = left + 8;
            double colDescW = 240;
            double colQtyR = left + 305;
            double colPriceR = left + 382;
            double colVatR = left + 430;
            double colNetR = right - 8;

            y = drawTableHeader(gfx, style, themeId, pal, left, right, width, y, qtyHeader, priceHeader,
                colDescX, colQtyR, colPriceR, colVatR, colNetR,
                fontHeader, brushPrimary, brushMuted, brushWhite, brushSoft, penPrimary, penPrimaryThin, penLine);

            int rowIndex = 0;
            foreach (var line in lines) {
                var wrappedDesc = wrapLines(gfx, line.text ?? "", fontRegular, colDescW);
                double rowH = Math.Max(22, wrappedDesc.Count * 13 + 9);

                if (y + rowH > 650) {
                    drawFooter(gfx, company, left, right, width, pal, prefix, fontSection, fontSmall, fontNumBold, brushText, brushMuted, brushPrimary, penLine, pages.Count, 0);
                    currentPage = doc.AddPage();
                    currentPage.Size = PageSize.A4;
                    gfx = XGraphics.FromPdfPage(currentPage);
                    pages.Add((currentPage, gfx));

                    if (style == Settings.StyleNordic) {
                        drawTopRibbon(gfx, themeId, pageW, brushPrimary, penPrimaryThin);
                    }
                    y = 42;
                    gfx.DrawString(compName + "  —  " + (isDraft ? "Fakturautkast" : "Faktura " + numberText) + " (forts.)", fontSmall, brushMuted, new XPoint(left, y));
                    y += 14;
                    y = drawTableHeader(gfx, style, themeId, pal, left, right, width, y, qtyHeader, priceHeader,
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

            if (y > 555) {
                drawFooter(gfx, company, left, right, width, pal, prefix, fontSection, fontSmall, fontNumBold, brushText, brushMuted, brushPrimary, penLine, pages.Count, 0);
                currentPage = doc.AddPage();
                currentPage.Size = PageSize.A4;
                gfx = XGraphics.FromPdfPage(currentPage);
                pages.Add((currentPage, gfx));
                if (style == Settings.StyleNordic) drawTopRibbon(gfx, themeId, pageW, brushPrimary, penPrimaryThin);
                y = 50;
            }

            // === 4. TOTALS SUMMARY & NOTES ===
            y += 12;
            double notesStartY = y;

            double noteW = 255;
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
                gfx.DrawString(prefix + "MEDDELANDE", fontSection, brushPrimary, new XPoint(left, noteY));
                noteY += 13;
                noteY = drawTextWrap(gfx, note, fontRegular, brushSub, left, noteY, noteW, 13.5) + 8;
            }

            double totX = 325;
            double totW = right - totX;
            double totY = notesStartY;

            gfx.DrawString("Summa exkl. moms", fontSmall, brushMuted, new XPoint(totX + 8, totY + 10));
            drawRight(gfx, Money.formatOrBlank(totals.net) + " kr", fontNum, brushText, colNetR, totY + 10);
            totY += 17;

            foreach (var r in totals.byRate.Where(r => r.Key > 0).Reverse()) {
                gfx.DrawString("Moms " + r.Key + " % (på " + Money.format(r.Value.net) + " kr)", fontSmall, brushMuted, new XPoint(totX + 8, totY + 10));
                drawRight(gfx, Money.formatOrBlank(r.Value.vat) + " kr", fontNum, brushText, colNetR, totY + 10);
                totY += 17;
            }

            totY += 4;
            if (style == Settings.StyleExecutive) {
                gfx.DrawLine(penPrimary, totX, totY, right, totY);
                totY += 6;
                gfx.DrawString("Att betala (SEK)", fontTotalLabel, brushText, new XPoint(totX + 8, totY + 13));
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
                gfx.DrawString(prefix + "ATT BETALA (SEK)", fontTotalLabel, brushWhite, new XPoint(totX + 12, totY + 19));
                drawRight(gfx, Money.format(totals.total) + " kr", fontTotalVal, brushWhite, right - 10, totY + 19.5);
                totY += barH + 10;
            }

            y = Math.Max(noteY, totY) + 14;

            // === 5. PROMINENT PAYMENT SLIP / BETALNINGSKORT ===
            drawPaymentCard(gfx, style, themeId, pal, left, right, width, y,
                bankgiro, numberText, dueDate, Money.format(totals.total) + " kr",
                isPaid, Db.str(invoice, "paidDate"),
                fontSection, fontSmall, fontNumBold, fontCardBig,
                brushText, brushMuted, brushWhite, brushPrimary, brushSoft, brushPaid, brushPaidSoft,
                penPrimary, penPrimaryThin, penBorder);

            // === 6. PINNED 4-COLUMN FOOTER ON ALL PAGES ===
            int totalPages = pages.Count;
            for (int i = 0; i < totalPages; i++) {
                drawFooter(pages[i].gfx, company, left, right, width, pal, prefix, fontSection, fontSmall, fontNumBold, brushText, brushMuted, brushPrimary, penLine, i + 1, totalPages);
            }
        }

        static void drawTopRibbon(XGraphics gfx, string themeId, double pageW, XBrush brushPrimary, XPen penPrimaryThin) {
            if (themeId == "skynet") {
                gfx.DrawRectangle(brushPrimary, 0, 0, pageW, 4.5);
                gfx.DrawLine(penPrimaryThin, 0, 7, pageW, 7);
            } else if (themeId == "puben") {
                gfx.DrawRectangle(brushPrimary, 0, 0, pageW, 5);
                gfx.DrawLine(penPrimaryThin, 0, 7.5, pageW, 7.5);
            } else {
                gfx.DrawRectangle(brushPrimary, 0, 0, pageW, 6);
            }
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
            XGraphics gfx, string style, string themeId, Palette pal, double x, double y, double w,
            string custName, string custAddr, string custPostal, string custOrg, string custVat,
            XFont fontSection, XFont fontBold, XFont fontRegular, XFont fontSmall,
            XBrush brushText, XBrush brushSub, XBrush brushMuted, XBrush brushSoft,
            XPen penPrimary, XPen penBorder) {

            bool isTech = themeId == "nostromo" || themeId == "skynet";
            string prefix = isTech ? "// " : "";

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
            gfx.DrawString(prefix + "FAKTURAMOTTAGARE", fontSection, new XSolidBrush(pal.primary), new XPoint(padX, y + 15));
            double curY = y + 31;
            foreach (var l in lines) {
                gfx.DrawString(l.text, l.font, l.brush, new XPoint(padX, curY));
                curY += 14.5;
            }
            return y + boxH;
        }

        static double drawTableHeader(
            XGraphics gfx, string style, string themeId, Palette pal, double left, double right, double width, double y,
            string qtyHeader, string priceHeader,
            double colDescX, double colQtyR, double colPriceR, double colVatR, double colNetR,
            XFont fontHeader, XBrush brushPrimary, XBrush brushMuted, XBrush brushWhite, XBrush brushSoft,
            XPen penPrimary, XPen penPrimaryThin, XPen penLine) {

            bool isTech = themeId == "nostromo" || themeId == "skynet";
            double h = 24;
            if (style == Settings.StyleStudio) {
                gfx.DrawRectangle(brushPrimary, left, y, width, h);
                gfx.DrawString("BESKRIVNING", fontHeader, brushWhite, new XPoint(colDescX, y + 15));
                drawRight(gfx, qtyHeader, fontHeader, brushWhite, colQtyR, y + 15);
                drawRight(gfx, priceHeader, fontHeader, brushWhite, colPriceR, y + 15);
                drawRight(gfx, "MOMS", fontHeader, brushWhite, colVatR, y + 15);
                drawRight(gfx, "BELOPP", fontHeader, brushWhite, colNetR, y + 15);
                return y + h;
            }
            if (style == Settings.StyleExecutive) {
                gfx.DrawLine(penPrimary, left, y, right, y);
                gfx.DrawString("BESKRIVNING", fontHeader, brushPrimary, new XPoint(colDescX, y + 15));
                drawRight(gfx, qtyHeader, fontHeader, brushPrimary, colQtyR, y + 15);
                drawRight(gfx, priceHeader, fontHeader, brushPrimary, colPriceR, y + 15);
                drawRight(gfx, "MOMS", fontHeader, brushPrimary, colVatR, y + 15);
                drawRight(gfx, "BELOPP", fontHeader, brushPrimary, colNetR, y + 15);
                gfx.DrawLine(penPrimaryThin, left, y + h, right, y + h);
                return y + h;
            }
            drawBox(gfx, null, brushSoft, left, y, width, h, isTech ? 0 : 4);
            gfx.DrawLine(penPrimary, left, y, right, y);
            gfx.DrawString("BESKRIVNING", fontHeader, brushPrimary, new XPoint(colDescX, y + 15));
            drawRight(gfx, qtyHeader, fontHeader, brushMuted, colQtyR, y + 15);
            drawRight(gfx, priceHeader, fontHeader, brushMuted, colPriceR, y + 15);
            drawRight(gfx, "MOMS", fontHeader, brushMuted, colVatR, y + 15);
            drawRight(gfx, "BELOPP", fontHeader, brushPrimary, colNetR, y + 15);
            gfx.DrawLine(penLine, left, y + h, right, y + h);
            return y + h;
        }

        static void drawPaymentCard(
            XGraphics gfx, string style, string themeId, Palette pal, double left, double right, double width, double y,
            string bankgiro, string numberText, string dueDate, string totalFormatted,
            bool isPaid, string paidDate,
            XFont fontSection, XFont fontSmall, XFont fontNumBold, XFont fontCardBig,
            XBrush brushText, XBrush brushMuted, XBrush brushWhite, XBrush brushPrimary, XBrush brushSoft,
            XBrush brushPaid, XBrush brushPaidSoft,
            XPen penPrimary, XPen penPrimaryThin, XPen penBorder) {

            bool isTech = themeId == "nostromo" || themeId == "skynet";
            string prefix = isTech ? "// " : "";

            double targetY = Math.Max(y, 676);
            if (targetY > 704) targetY = y;

            double cardH = 58;
            if (style == Settings.StyleExecutive || themeId == "puben") {
                gfx.DrawRectangle(penBorder, brushSoft, left, targetY, width, cardH);
                gfx.DrawLine(penPrimary, left, targetY, right, targetY);
                gfx.DrawLine(penPrimaryThin, left, targetY + 2.5, right, targetY + 2.5);
            } else {
                drawBox(gfx, penBorder, brushSoft, left, targetY, width, cardH, isTech ? 0 : 7);
                gfx.DrawRectangle(brushPrimary, left, targetY + (isTech ? 0 : 7), 4, cardH - (isTech ? 0 : 14));
                if (isTech) drawCornerTicks(gfx, penPrimary, left, targetY, width, cardH, 5.5);
            }

            gfx.DrawString(prefix + "BETALNINGSUPPGIFTER  —  ANGE FAKTURANUMMER VID BETALNING", fontSection, brushPrimary, new XPoint(left + 14, targetY + 15));

            double c1 = left + 14;
            double c2 = left + 145;
            double c3 = left + 275;
            double c4 = left + 390;

            gfx.DrawString("Bankgiro", fontSmall, brushMuted, new XPoint(c1, targetY + 31));
            gfx.DrawString(bankgiro.Length > 0 ? bankgiro : "—", fontCardBig, brushText, new XPoint(c1, targetY + 46));

            gfx.DrawString("Fakturanr / Referens", fontSmall, brushMuted, new XPoint(c2, targetY + 31));
            gfx.DrawString(numberText, fontNumBold, brushText, new XPoint(c2, targetY + 45));

            gfx.DrawString("Förfallodatum", fontSmall, brushMuted, new XPoint(c3, targetY + 31));
            gfx.DrawString(dueDate, fontNumBold, brushText, new XPoint(c3, targetY + 45));

            if (isPaid) {
                string pText = paidDate.Length > 0 ? "BETALD " + paidDate : "BETALD";
                drawBox(gfx, new XPen(ColorPaid, 1.0), brushPaidSoft, c4, targetY + 22, right - c4 - 12, 28, isTech ? 0 : 5);
                drawCentered(gfx, pText, fontNumBold, brushPaid, c4 + (right - c4 - 12) / 2, targetY + 40);
            } else {
                gfx.DrawString("Att betala (SEK)", fontSmall, brushMuted, new XPoint(c4, targetY + 31));
                gfx.DrawString(totalFormatted, fontCardBig, brushPrimary, new XPoint(c4, targetY + 46));
            }
        }

        static void drawFooter(
            XGraphics gfx, SwecialObject company, double left, double right, double width, Palette pal, string prefix,
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
                (prefix + "ADRESS", col1Lines, false),
                (prefix + "KONTAKT", col2Lines, false),
                (prefix + "FÖRETAGSUPPGIFTER", col3Lines, false),
                (prefix + "BANKGIRO", col4Lines, true)
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
