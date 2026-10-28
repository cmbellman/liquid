using com.swecial.server;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace com.swecial.code {
    /// <summary>A fund (or other security) the company holds, worked out from its events.</summary>
    public class Holding {
        public string name;
        /// <summary>Millionths of a unit (Securities.UnitScale).</summary>
        public long units;
        /// <summary>What the units cost (omkostnadsbelopp), in öre: the average cost method.</summary>
        public long cost;
        /// <summary>The value per unit at the latest event, in öre per whole unit, and its date.</summary>
        public decimal lastPrice;
        public string lastDate = "";
    }

    /// <summary>
    /// Funds and other securities (Källaren): bought with money from the account, sold for money, or
    /// switched from one fund to another. A switch is a sale, so the gain or loss is booked on the
    /// day of the switch. The cost of sold units is the average cost of the holding (genomsnittsmetoden),
    /// as tax law requires. Booked in series V on 1350 (the holdings) and 8220 (gain or loss).
    ///
    /// An event's cost depends on every earlier event of the fund, so events are registered in date
    /// order and only the latest one can be removed. Holdings the company had before it used Liquid
    /// are registered as "owned": units and cost, nothing booked.
    ///
    /// Fund units held at the start of a year give a standard income for tax (schablonintäkt, a
    /// share of their market value then, Rules.FundStandardIncome). Liquid doesn't know market
    /// values, so the user enters the value on 1 January per year (fundValuesJson on the company).
    /// </summary>
    public static class Securities {
        public const string Buy = "buy";
        public const string Sell = "sell";
        public const string Switch = "switch";
        public const string Owned = "owned";
        public static readonly string[] Kinds = { Switch, Sell, Buy, Owned };

        public const long UnitScale = 1_000_000;

        public static long parseUnits(string text) {
            decimal value = Money.parseDecimal(text, "antal andelar");
            decimal scaled = value * UnitScale;
            if (scaled != Math.Round(scaled)) throw new LiquidException("Antal andelar kan ha högst sex decimaler: " + text, "UNITS ALLOW SIX DECIMALS: " + text);
            return (long)scaled;
        }

        public static string formatUnits(long units) {
            return ((decimal)units / UnitScale).ToString("#,##0.######", CultureInfo.GetCultureInfo("sv-SE"));
        }

        /// <summary>For an input field: no thousand separators, a point so page scripts can read it.</summary>
        public static string inputUnits(long units) {
            return ((decimal)units / UnitScale).ToString("0.######", CultureInfo.InvariantCulture);
        }

        /// <summary>The booked events, oldest first.</summary>
        public static List<SwecialObject> events(string companyId) {
            return Db.forCompany("security_event", companyId)
                .Where(e => Db.str(e, "status") == "booked")
                .OrderBy(e => Db.str(e, "date"))
                .ThenBy(e => Db.num(e, "seq"))
                .ToList();
        }

        /// <summary>The holdings after all events, the ones with units left, by name.</summary>
        public static List<Holding> holdings(string companyId) {
            return holdingsBefore(companyId, "9999-12-31");
        }

        /// <summary>The holdings before a date (the events dated earlier).</summary>
        public static List<Holding> holdingsBefore(string companyId, string date) {
            return replay(events(companyId).Where(e => string.CompareOrdinal(Db.str(e, "date"), date) < 0)).Values.Where(h => h.units > 0).OrderBy(h => h.name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        static string key(string name) => (name ?? "").Trim().ToLowerInvariant();

        static Dictionary<string, Holding> replay(IEnumerable<SwecialObject> list) {
            var all = new Dictionary<string, Holding>();
            Holding get(string name) {
                if (!all.TryGetValue(key(name), out var h)) all[key(name)] = h = new Holding { name = name.Trim() };
                return h;
            }
            foreach (var e in list) {
                string date = Db.str(e, "date");
                long amount = Db.num(e, "amount");
                if (Db.str(e, "fromFund").Length > 0) {
                    var from = get(Db.str(e, "fromFund"));
                    long sold = Db.num(e, "fromUnits");
                    from.units -= sold;
                    from.cost -= Db.num(e, "cost");
                    from.lastPrice = price(amount, sold);
                    from.lastDate = date;
                }
                if (Db.str(e, "toFund").Length > 0) {
                    var to = get(Db.str(e, "toFund"));
                    long bought = Db.num(e, "toUnits");
                    to.units += bought;
                    to.cost += amount;
                    to.lastPrice = price(amount, bought);
                    to.lastDate = date;
                }
            }
            return all;
        }

        static decimal price(long amount, long units) => units == 0 ? 0 : amount * (decimal)UnitScale / units;

        /// <summary>The cost of some of a holding's units: their share of its cost, all of it for all units.</summary>
        public static long costOf(Holding h, long units) {
            if (units >= h.units) return h.cost;
            return (long)Math.Round(h.cost * (decimal)units / h.units, 0, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Registers and books an event. amount is what was paid (buy), received (sell), what the sold
        /// units were worth (switch) or what the holding cost (owned).
        /// </summary>
        public static SwecialObject book(string companyId, string kind, string date, string fromFund, long fromUnits,
                string toFund, long toUnits, long amount, string note, string userId) {
            if (!Kinds.Contains(kind)) throw new LiquidException("Välj vad som har hänt.", "SELECT WHAT HAPPENED.");
            date = Dates.clean(date);
            fromFund = (fromFund ?? "").Trim();
            toFund = (toFund ?? "").Trim();
            note = (note ?? "").Trim();
            bool sells = kind == Sell || kind == Switch;
            bool buys = kind != Sell;
            if (!sells) { fromFund = ""; fromUnits = 0; }
            if (!buys) { toFund = ""; toUnits = 0; }
            if (amount <= 0) throw new LiquidException("Fyll i beloppet.", "AMOUNT REQUIRED.");
            if (sells && fromUnits <= 0) throw new LiquidException("Fyll i hur många andelar som såldes.", "UNITS SOLD REQUIRED.");
            if (buys && toFund.Length == 0) throw new LiquidException("Fyll i vilken fond det gäller.", "FUND NAME REQUIRED.");
            if (buys && toUnits <= 0) throw new LiquidException("Fyll i hur många andelar det blev.", "UNITS REQUIRED.");
            if (kind == Switch && key(fromFund) == key(toFund)) throw new LiquidException("Ett byte går från en fond till en annan.", "A SWITCH MOVES FROM ONE FUND TO ANOTHER.");

            lock (Db.Sync) {
                var list = events(companyId);
                string last = list.Count > 0 ? Db.str(list.Last(), "date") : "";
                if (string.CompareOrdinal(date, last) < 0) {
                    throw new LiquidException("Registrera händelserna i datumordning: den senaste är från " + last + ".", "REGISTER EVENTS IN DATE ORDER: THE LATEST IS FROM " + last + ".");
                }
                long cost = 0;
                if (sells) {
                    var all = replay(list);
                    if (!all.TryGetValue(key(fromFund), out var from) || from.units <= 0) throw new LiquidException("Bolaget har inga andelar i " + fromFund + ".", "NO UNITS HELD IN " + fromFund + ".");
                    if (fromUnits > from.units) throw new LiquidException("Bolaget har bara " + formatUnits(from.units) + " andelar i " + from.name + ".", "ONLY " + formatUnits(from.units) + " UNITS HELD IN " + from.name + ".");
                    fromFund = from.name;
                    cost = costOf(from, fromUnits);
                }
                if (buys) {
                    // The same spelling as a fund already held, so it stays one holding
                    var held = replay(list).Values.FirstOrDefault(h => key(h.name) == key(toFund));
                    if (held != null) toFund = held.name;
                }
                long gain = sells ? amount - cost : 0;

                string text;
                List<VoucherRow> rows = null;
                switch (kind) {
                    case Buy:
                        text = "Köp av " + toFund;
                        rows = new List<VoucherRow> { new VoucherRow { account = Bas.Securities, debit = amount }, new VoucherRow { account = Bas.Bank, credit = amount } };
                        break;
                    case Sell:
                        text = "Försäljning av " + fromFund;
                        rows = new List<VoucherRow> { new VoucherRow { account = Bas.Bank, debit = amount }, new VoucherRow { account = Bas.Securities, credit = cost } };
                        break;
                    case Switch:
                        text = "Byte från " + fromFund + " till " + toFund;
                        rows = new List<VoucherRow> { new VoucherRow { account = Bas.Securities, debit = amount }, new VoucherRow { account = Bas.Securities, credit = cost } };
                        break;
                    default:
                        text = "Innehav " + toFund;
                        break;
                }
                if (rows != null) {
                    rows.Add(gain >= 0 ? new VoucherRow { account = Bas.SecuritiesResult, credit = gain } : new VoucherRow { account = Bas.SecuritiesResult, debit = -gain });
                    if (note.Length > 0) text += ": " + note;
                    Ledger.validate(companyId, date, text, rows);
                }

                var e = Db.create("security_event", companyId);
                e.set("kind", kind);
                e.set("date", date);
                e.set("seq", Db.nextNumber(companyId, "security_event"));
                e.set("fromFund", fromFund);
                e.set("fromUnits", fromUnits);
                e.set("toFund", toFund);
                e.set("toUnits", toUnits);
                e.set("amount", amount);
                e.set("cost", cost);
                e.set("gain", gain);
                e.set("note", note);
                e.set("status", "booked");
                Db.linkUser(e, "createdBy", userId);
                if (rows != null) {
                    var voucher = Ledger.book(companyId, Ledger.Securities, date, text, rows, userId, "security_event", e.id);
                    Db.link(e, "voucher", voucher);
                }
                Db.commit(e);
                return e;
            }
        }

        /// <summary>Removes the latest event and reverses its voucher. Earlier events can't go: later costs build on them.</summary>
        public static void remove(SwecialObject e, string userId) {
            lock (Db.Sync) {
                if (Db.str(e, "status") != "booked") return;
                var list = events(Db.companyId(e));
                if (list.Last().id != e.id) throw new LiquidException("Bara den senaste händelsen kan tas bort. Ta bort de senare först.", "ONLY THE LATEST EVENT CAN BE REMOVED. REMOVE LATER EVENTS FIRST.");
                var voucher = Db.linked(e, "voucher", "voucher");
                if (voucher != null && Db.linkedId(voucher, "reversedBy").Length == 0) {
                    Ledger.reverse(voucher, Db.str(voucher, "date"), userId, fromSource: true);
                }
                e.set("status", "removed");
                Db.commit(e);
            }
        }

        /// <summary>The years whose 1 January found the company with funds, oldest first, up to this year.</summary>
        public static List<int> yearsWithFunds(string companyId) {
            var list = events(companyId);
            if (list.Count == 0) return new List<int>();
            int first = Dates.parse(Db.str(list.First(), "date")).Year + 1;
            return Enumerable.Range(first, Math.Max(0, DateTime.Today.Year - first + 1))
                .Where(y => holdingsBefore(companyId, y + "-01-01").Count > 0).ToList();
        }

        public static Dictionary<int, long> valuesAtYearStart(SwecialObject company) {
            string json = Db.str(company, "fundValuesJson");
            return json.Length == 0 ? new Dictionary<int, long>() : JsonConvert.DeserializeObject<Dictionary<int, long>>(json);
        }

        /// <summary>Sets the market value of the funds on 1 January of a year (0 removes it).</summary>
        public static void setValueAtYearStart(SwecialObject company, int year, long value) {
            if (value < 0) throw new LiquidException("Värdet kan inte vara negativt.", "VALUE CANNOT BE NEGATIVE.");
            if (year < 2000 || year > DateTime.Today.Year) throw new LiquidException("Välj ett år.", "SELECT A YEAR.");
            lock (Db.Sync) {
                var all = valuesAtYearStart(company);
                if (value == 0) all.Remove(year); else all[year] = value;
                company.set("fundValuesJson", JsonConvert.SerializeObject(all));
                Db.commit(company);
            }
        }

        /// <summary>The standard income for a year on the value of the funds on 1 January, in öre.</summary>
        public static long standardIncome(int year, long valueAtYearStart) {
            long permille = Rules.value(Rules.FundStandardIncome, year + "-01-01");
            return (long)Math.Round(valueAtYearStart * (decimal)permille / 1000m, 0, MidpointRounding.AwayFromZero);
        }

        /// <summary>A holding at the price of its latest event (Liquid doesn't know today's price).</summary>
        public static long lastKnownValue(Holding h) {
            return (long)Math.Round(h.units * h.lastPrice / UnitScale, 0, MidpointRounding.AwayFromZero);
        }

        /// <summary>The funds at the latest prices known before a date (an aid for entering the market value).</summary>
        public static long lastKnownValueBefore(string companyId, string date) {
            return holdingsBefore(companyId, date).Sum(lastKnownValue);
        }

        /// <summary>This year began with funds but their value on 1 January isn't entered (Baren asks for it).</summary>
        public static bool missingValueThisYear(SwecialObject company) {
            int year = DateTime.Today.Year;
            return holdingsBefore(company.id, year + "-01-01").Count > 0 && !valuesAtYearStart(company).ContainsKey(year);
        }

        /// <summary>Gain and loss realised in a year, for the tax return (a loss only offsets gains on shares and funds).</summary>
        public static (long gain, long loss) realised(string companyId, int year) {
            var inYear = events(companyId).Where(e => Db.str(e, "date").StartsWith(year + "-")).Select(e => Db.num(e, "gain")).ToList();
            return (inYear.Where(g => g > 0).Sum(), -inYear.Where(g => g < 0).Sum());
        }
    }
}
