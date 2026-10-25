using com.swecial.server;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace com.swecial.code {
    /// <summary>
    /// SIE 4 (typ 4, the Swedish standard for moving books between programs and to an accountant or
    /// Skatteverket). Export: one fiscal year with the chart of accounts, opening and closing balances
    /// for this year and the one before, the result per account and every voucher. Import: accounts,
    /// and either the vouchers with the opening balances of the file's year, or only its closing
    /// balances as the opening balances of the year after (when the company moves to Liquid at a
    /// year end). Files are in PC8 (code page 437), as the format says.
    /// </summary>
    public static class Sie {
        /// <summary>Imported vouchers go in their own series; the original series and number are kept in the text.</summary>
        public const string Series = "E";

        // ---- PC8 (code page 437) ----

        const string Cp437High = "ÇüéâäàåçêëèïîìÄÅÉæÆôöòûùÿÖÜ¢£¥₧ƒáíóúñÑªº¿⌐¬½¼¡«»░▒▓│┤╡╢╖╕╣║╗╝╜╛┐└┴┬├─┼╞╟╚╔╩╦╠═╬╧╨╤╥╙╘╒╓╫╪┘┌█▄▌▐▀αßΓπΣσµτΦΘΩδ∞φε∩≡±≥≤⌠⌡÷≈°∙·√ⁿ²■ ";

        public static byte[] encode(string text) {
            var bytes = new List<byte>(text.Length);
            foreach (char c in text) {
                if (c < 128) { bytes.Add((byte)c); continue; }
                int i = Cp437High.IndexOf(c);
                bytes.Add(i >= 0 ? (byte)(128 + i) : (byte)'?');
            }
            return bytes.ToArray();
        }

        public static string decode(byte[] data) {
            // Some programs write UTF-8 although the file says PC8: take it as UTF-8 when it is valid UTF-8 with non-ASCII in it
            try {
                string utf8 = new UTF8Encoding(false, true).GetString(data);
                if (utf8.Any(c => c > 127)) return utf8.TrimStart('﻿');
            } catch (ArgumentException) { }
            var sb = new StringBuilder(data.Length);
            foreach (byte b in data) sb.Append(b < 128 ? (char)b : Cp437High[b - 128]);
            return sb.ToString();
        }

        // ---- Export ----

        static string q(string text) => "\"" + (text ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        static string amount(long ore) => (ore / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        static string day(string date) => date.Replace("-", "");

        /// <summary>The SIE 4 file for a fiscal year, as text (encode it with encode()).</summary>
        public static string export(SwecialObject company, int year) {
            string companyId = company.id;
            string start = Closing.start(year), end = Closing.end(year);
            string prevStart = Closing.start(year - 1), prevEnd = Closing.end(year - 1);
            var all = Ledger.vouchers(companyId);
            bool hasPrevious = all.Any(v => string.CompareOrdinal(Db.str(v, "date"), start) < 0);
            var sb = new StringBuilder();
            void line(string s) => sb.Append(s).Append("\r\n");

            line("#FLAGGA 0");
            line("#FORMAT PC8");
            line("#SIETYP 4");
            line("#PROGRAM " + q("Liquid") + " 1.0");
            line("#GEN " + DateTime.Today.ToString("yyyyMMdd") + " " + q("Liquid"));
            line("#FNAMN " + q(Db.str(company, "name")));
            if (Db.str(company, "orgNumber").Length > 0) line("#ORGNR " + Db.str(company, "orgNumber").Trim());
            if (Db.str(company, "address").Length > 0 || Db.str(company, "postalAddress").Length > 0) {
                line("#ADRESS " + q("") + " " + q(Db.str(company, "address")) + " " + q(Db.str(company, "postalAddress")) + " " + q(Db.str(company, "phone")));
            }
            line("#FTYP AB");
            line("#RAR 0 " + day(start) + " " + day(end));
            if (hasPrevious) line("#RAR -1 " + day(prevStart) + " " + day(prevEnd));
            line("#KPTYP BAS2014");
            line("#VALUTA SEK");

            foreach (var a in Db.forCompany("account", companyId).OrderBy(a => Db.str(a, "number"))) {
                string number = Db.str(a, "number");
                line("#KONTO " + number + " " + q(Db.str(a, "name")));
                line("#KTYP " + number + " " + accountType(number));
            }

            var balances = new List<(int index, string from, string to)> { (0, start, end) };
            if (hasPrevious) balances.Add((-1, prevStart, prevEnd));
            foreach (var (index, from, to) in balances) {
                var opening = Statements.movements(companyId, "", Dates.format(Dates.parse(from).AddDays(-1)));
                var closing = Statements.movements(companyId, "", to);
                var result = Statements.movements(companyId, from, to);
                foreach (var a in opening.Where(m => Bas.isBalanceAccount(m.Key) && m.Value != 0).OrderBy(m => m.Key)) line("#IB " + index + " " + a.Key + " " + amount(a.Value));
                foreach (var a in closing.Where(m => Bas.isBalanceAccount(m.Key) && m.Value != 0).OrderBy(m => m.Key)) line("#UB " + index + " " + a.Key + " " + amount(a.Value));
                foreach (var a in result.Where(m => !Bas.isBalanceAccount(m.Key) && m.Value != 0).OrderBy(m => m.Key)) line("#RES " + index + " " + a.Key + " " + amount(a.Value));
            }

            foreach (var v in all.Where(v => Db.str(v, "date").StartsWith(year + "-"))) {
                string registered = v.createdAt > DateTimeOffset.MinValue ? v.createdAt.LocalDateTime.ToString("yyyyMMdd") : day(Db.str(v, "date"));
                line("#VER " + q(Db.str(v, "series")) + " " + q(Db.num(v, "number").ToString()) + " " + day(Db.str(v, "date")) + " " + q(Db.str(v, "text")) + " " + registered);
                line("{");
                foreach (var r in Ledger.rows(v)) line("   #TRANS " + r.account + " {} " + amount(r.debit - r.credit));
                line("}");
            }
            return sb.ToString();
        }

        static string accountType(string number) {
            switch (number[0]) {
                case '1': return "T";
                case '2': return "S";
                case '3': return "I";
                default: return "K";
            }
        }

        // ---- Import ----

        public class ParsedVoucher {
            public string series, number, date, text;
            public List<VoucherRow> rows = new List<VoucherRow>();
        }

        public class Parsed {
            public string companyName = "", orgNumber = "";
            public string yearStart = "", yearEnd = "";
            public Dictionary<string, string> accounts = new Dictionary<string, string>();
            public Dictionary<string, long> opening = new Dictionary<string, long>();
            public Dictionary<string, long> closing = new Dictionary<string, long>();
            public List<ParsedVoucher> vouchers = new List<ParsedVoucher>();
        }

        /// <summary>Splits a SIE line into its fields: words, "quoted strings" and {object lists}.</summary>
        static List<string> fields(string line) {
            var result = new List<string>();
            int i = 0;
            while (i < line.Length) {
                char c = line[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                var sb = new StringBuilder();
                if (c == '"') {
                    i++;
                    while (i < line.Length && line[i] != '"') {
                        if (line[i] == '\\' && i + 1 < line.Length) i++;
                        sb.Append(line[i++]);
                    }
                    i++;
                } else if (c == '{') {
                    int depth = 0;
                    while (i < line.Length) {
                        if (line[i] == '{') depth++;
                        if (line[i] == '}') depth--;
                        sb.Append(line[i++]);
                        if (depth == 0) break;
                    }
                } else {
                    while (i < line.Length && !char.IsWhiteSpace(line[i])) sb.Append(line[i++]);
                }
                result.Add(sb.ToString());
            }
            return result;
        }

        static long parseAmount(string text, int lineNumber) {
            if (!decimal.TryParse(text.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)) {
                throw new LiquidException("Rad " + lineNumber + ": beloppet går inte att läsa: " + text, "LINE " + lineNumber + ": UNREADABLE AMOUNT: " + text);
            }
            decimal ore = value * 100;
            if (ore != Math.Round(ore)) throw new LiquidException("Rad " + lineNumber + ": belopp med fler än två decimaler: " + text, "LINE " + lineNumber + ": AMOUNT WITH MORE THAN TWO DECIMALS: " + text);
            return (long)ore;
        }

        static string parseDate(string text, int lineNumber) {
            if (!DateTime.TryParseExact(text, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d)) {
                throw new LiquidException("Rad " + lineNumber + ": datumet går inte att läsa: " + text, "LINE " + lineNumber + ": UNREADABLE DATE: " + text);
            }
            return Dates.format(d);
        }

        public static Parsed parse(byte[] data) {
            var p = new Parsed();
            string[] lines = decode(data).Replace("\r\n", "\n").Split('\n');
            ParsedVoucher current = null;
            bool sie4 = false;
            for (int n = 0; n < lines.Length; n++) {
                string line = lines[n].Trim();
                if (line.Length == 0) continue;
                if (line == "{") continue;
                if (line == "}") { current = null; continue; }
                var f = fields(line);
                string tag = f[0].ToUpperInvariant();
                int lineNumber = n + 1;
                switch (tag) {
                    case "#SIETYP": sie4 = f.Count > 1 && f[1] == "4"; break;
                    case "#FNAMN": if (f.Count > 1) p.companyName = f[1]; break;
                    case "#ORGNR": if (f.Count > 1) p.orgNumber = f[1]; break;
                    case "#RAR":
                        if (f.Count > 3 && f[1] == "0") { p.yearStart = parseDate(f[2], lineNumber); p.yearEnd = parseDate(f[3], lineNumber); }
                        break;
                    case "#KONTO": if (f.Count > 2) p.accounts[f[1]] = f[2]; break;
                    case "#IB": if (f.Count > 3 && f[1] == "0") p.opening[f[2]] = parseAmount(f[3], lineNumber); break;
                    case "#UB": if (f.Count > 3 && f[1] == "0") p.closing[f[2]] = parseAmount(f[3], lineNumber); break;
                    case "#VER":
                        if (f.Count < 4) throw new LiquidException("Rad " + lineNumber + ": ofullständig verifikation.", "LINE " + lineNumber + ": INCOMPLETE VOUCHER.");
                        current = new ParsedVoucher { series = f[1], number = f[2], date = parseDate(f[3], lineNumber), text = f.Count > 4 ? f[4] : "" };
                        p.vouchers.Add(current);
                        break;
                    case "#TRANS":
                        if (current == null) throw new LiquidException("Rad " + lineNumber + ": #TRANS utanför en verifikation.", "LINE " + lineNumber + ": #TRANS OUTSIDE A VOUCHER.");
                        if (f.Count < 4) throw new LiquidException("Rad " + lineNumber + ": ofullständig transaktion.", "LINE " + lineNumber + ": INCOMPLETE TRANSACTION.");
                        long a = parseAmount(f[3], lineNumber);
                        current.rows.Add(a >= 0 ? new VoucherRow { account = f[1], debit = a } : new VoucherRow { account = f[1], credit = -a });
                        break;
                    // #RTRANS/#BTRANS (added and removed rows) are history in the other program; the #TRANS rows are the voucher
                }
            }
            if (!sie4) throw new LiquidException("Filen är inte SIE typ 4 (#SIETYP 4). Exportera SIE 4 från det andra programmet.", "NOT AN SIE TYPE 4 FILE (#SIETYP 4).");
            if (p.yearStart.Length == 0) throw new LiquidException("Filen saknar räkenskapsår (#RAR 0).", "FILE HAS NO FISCAL YEAR (#RAR 0).");
            return p;
        }

        public const string WithVouchers = "vouchers";
        public const string ClosingAsOpening = "closing";

        /// <summary>
        /// Imports a parsed file. With the vouchers: the accounts, the file year's opening balances (when
        /// Liquid has nothing earlier) and every voucher, in series E in date order with the original
        /// series and number in the text. Only the closing balances: they become the opening balances on
        /// the first day of the year after. Everything is checked before anything is booked, and the same
        /// file can't be imported twice. Returns a summary for the change log.
        /// </summary>
        public static string import(SwecialObject company, byte[] data, string mode, string userId) {
            string companyId = company.id;
            var p = parse(data);
            string hash = Convert.ToBase64String(SHA256.Create().ComputeHash(data));
            if (Db.forCompany("sie_import", companyId).Any(i => Db.str(i, "hash") == hash)) {
                throw new LiquidException("Den här filen är redan importerad.", "THIS FILE HAS ALREADY BEEN IMPORTED.");
            }
            string org(string s) => new string((s ?? "").Where(char.IsDigit).ToArray());
            if (org(p.orgNumber).Length > 0 && org(Db.str(company, "orgNumber")).Length > 0 && org(p.orgNumber) != org(Db.str(company, "orgNumber"))) {
                throw new LiquidException("Filen gäller ett annat bolag (organisationsnummer " + p.orgNumber + ").", "THE FILE BELONGS TO ANOTHER COMPANY (" + p.orgNumber + ").");
            }

            var used = new HashSet<string>(p.accounts.Keys);
            foreach (var v in p.vouchers) foreach (var r in v.rows) used.Add(r.account);
            foreach (var a in p.opening.Keys.Concat(p.closing.Keys)) used.Add(a);
            var bad = used.Where(a => !System.Text.RegularExpressions.Regex.IsMatch(a, "^[1-8][0-9]{3}$")).ToList();
            if (bad.Count > 0) throw new LiquidException("Liquid använder fyrsiffriga BAS-konton. Filen har: " + string.Join(", ", bad.Take(10)), "LIQUID USES FOUR-DIGIT BAS ACCOUNTS. THE FILE HAS: " + string.Join(", ", bad.Take(10)));

            lock (Db.Sync) {
                // What will be booked, checked first
                var toBook = new List<(string date, string text, List<VoucherRow> rows)>();
                string openingNote = "";
                if (mode == ClosingAsOpening) {
                    string next = Dates.format(Dates.parse(p.yearEnd).AddDays(1));
                    toBook.Add(openingVoucher(companyId, p.closing, next, "Ingående balanser enligt SIE-fil (utgående balanser " + p.yearEnd + ")", ref openingNote));
                } else {
                    toBook.Add(openingVoucher(companyId, p.opening, p.yearStart, "Ingående balanser enligt SIE-fil", ref openingNote));
                    foreach (var v in p.vouchers.OrderBy(v => v.date)) {
                        toBook.Add((v.date, "SIE " + v.series + v.number + (v.text.Length > 0 ? ": " + v.text : ""), v.rows));
                    }
                }
                toBook.RemoveAll(b => b.rows == null);
                if (toBook.Count == 0) throw new LiquidException("Det finns inget att importera i filen.", "NOTHING TO IMPORT IN THE FILE.");

                // New accounts first, so the vouchers can be checked against the chart
                int added = 0;
                foreach (var a in used.OrderBy(a => a)) {
                    if (Bas.find(companyId, a) != null) continue;
                    Bas.ensure(companyId, a, p.accounts.TryGetValue(a, out var name) && name.Trim().Length > 0 ? name.Trim() : "Konto " + a);
                    added++;
                }
                foreach (var b in toBook) {
                    try {
                        Ledger.validate(companyId, b.date, b.text, b.rows.Select(r => new VoucherRow { account = r.account, debit = r.debit, credit = r.credit }).ToList());
                    } catch (LiquidException e) {
                        throw new LiquidException(b.date + " " + b.text + ": " + e.Message, e.english);
                    }
                }

                var import = Db.create("sie_import", companyId);
                import.set("hash", hash);
                import.set("mode", mode);
                import.set("yearStart", p.yearStart);
                import.set("yearEnd", p.yearEnd);
                import.set("importedBy", userId);
                Db.commit(import);
                foreach (var b in toBook) Ledger.book(companyId, Series, b.date, b.text, b.rows, userId, "sie_import", import.id);

                int vouchers = toBook.Count(b => b.text.StartsWith("SIE "));
                string summary = "SIE-fil importerad (" + (p.companyName.Length > 0 ? p.companyName + ", " : "") + p.yearStart + "–" + p.yearEnd + "): "
                    + (mode == ClosingAsOpening ? "utgående balanser som ingående balanser" : vouchers + " verifikationer")
                    + (added > 0 ? ", " + added + " nya konton" : "") + openingNote + ".";
                import.set("summary", summary);
                Db.commit(import);
                Audit.log(companyId, userId, "import", summary);
                return summary;
            }
        }

        /// <summary>
        /// The opening balances as a voucher on the year's first day (series E), when Liquid has nothing
        /// before that day (otherwise its own balances carry over and the file's are left out).
        /// </summary>
        static (string, string, List<VoucherRow>) openingVoucher(string companyId, Dictionary<string, long> balances, string date, string text, ref string note) {
            var rows = balances.Where(b => b.Value != 0 && Bas.isBalanceAccount(b.Key)).OrderBy(b => b.Key)
                .Select(b => b.Value > 0 ? new VoucherRow { account = b.Key, debit = b.Value } : new VoucherRow { account = b.Key, credit = -b.Value }).ToList();
            if (rows.Count == 0) return (date, text, null);
            if (Ledger.vouchers(companyId).Any(v => string.CompareOrdinal(Db.str(v, "date"), date) < 0)) {
                note = "; ingående balanser hoppades över, bokföringen i Liquid har redan saldon före " + date;
                return (date, text, null);
            }
            long diff = rows.Sum(r => r.debit - r.credit);
            if (diff != 0) {
                throw new LiquidException("De ingående balanserna i filen går inte jämnt ut (skillnad " + Money.format(diff) + " kr). Har föregående års resultat förts över till eget kapital i det andra programmet?",
                    "THE OPENING BALANCES IN THE FILE DO NOT BALANCE (DIFFERENCE " + Money.format(diff) + " SEK).");
            }
            return (date, text, rows);
        }
    }
}
