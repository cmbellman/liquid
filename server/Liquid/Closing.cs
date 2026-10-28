using com.swecial.server;
using System;
using System.Collections.Generic;
using System.Linq;

namespace com.swecial.code {
    /// <summary>The company's income tax for a year, worked out like INK2S (amounts in öre).</summary>
    public class TaxCalculation {
        /// <summary>The income statement's result before tax (appropriations included), credit-positive.</summary>
        public long resultBeforeTax;
        /// <summary>4.3 c: booked costs that aren't deductible (representation, fees, tax interest …).</summary>
        public long nonDeductible;
        /// <summary>4.5 c: booked income that isn't taxed (interest on the tax account).</summary>
        public long taxFree;
        /// <summary>4.6 a: the standard income on tax allocation reserves held at the start of the year.</summary>
        public long reserveIncome;
        /// <summary>4.6 d: the uplift on reversed reserves from before 2021 (6 or 4 %).</summary>
        public long reserveUplift;
        /// <summary>4.6 b: the standard income on fund units held on 1 January.</summary>
        public long fundIncome;
        /// <summary>The funds were held on 1 January but their value then isn't entered (fundIncome unknown).</summary>
        public bool fundValueMissing;
        /// <summary>A net loss on funds this year: only deductible against gains on shares and funds, so it is added back and saved.</summary>
        public long capitalLossAddedBack;
        /// <summary>Saved losses on funds used against this year's gains.</summary>
        public long capitalLossUsed;
        public long capitalLossIn;
        public long capitalLossOut;
        /// <summary>4.14 a: the deficit from the year before.</summary>
        public long deficitIn;
        /// <summary>The surplus (positive) or deficit (negative) after everything above.</summary>
        public long surplus;
        /// <summary>This year's allocation to a tax allocation reserve, as booked (8811).</summary>
        public long allocationBooked;
        /// <summary>The most that may be allocated: a share of the surplus before the allocation.</summary>
        public long allocationMax;
        /// <summary>The taxable income: the surplus rounded down to whole hundreds of kronor.</summary>
        public long taxable;
        public long tax;
        public long deficitOut;
    }

    /// <summary>A step of the closing that books something: what is left to book, and whether it is done.</summary>
    public class ClosingStep {
        public string key;
        public long remaining;
        public bool done => remaining == 0;
    }

    /// <summary>
    /// The year-end closing (bokslut) for a fiscal year (the calendar year), step by step, each step
    /// booking what is left of it, dated the year's last day, in series S (vouchers that belong to the
    /// closing, sourceType closing, so they can't be reversed by hand). What is left is read from the
    /// books, so a step can be booked again when something earlier changes, until the year is locked.
    /// 1. Unpaid customer invoices (cash method): booked as receivables (Invoicing.bookAtYearEnd).
    /// 2. VAT: what is left on the VAT accounts (2610–2649) to 2650.
    /// 3. The tax account: preliminary tax (2518) and VAT (2650) Skatteverket took from it during the year.
    /// 4. Tax allocation reserves: reserves six years old are reversed (8819), and a new one may be
    ///    allocated (8811) up to a share of the surplus.
    /// 5. The year's income tax (8910 / 2512), from TaxCalculation.
    /// 6. The year's result to the balance sheet (8999 / 2099).
    /// 7. Locking: no voucher dated in a locked year can be booked (Ledger.validate). It can be
    ///    opened again, e.g. before the annual report is signed.
    /// The deficit and saved fund losses carry from the year before's locked closing, or are
    /// entered on the closing when the year before wasn't closed in Liquid.
    /// </summary>
    public static class Closing {
        public const string Open = "open";
        public const string Closed = "closed";

        public const string Receivables = "receivables";
        public const string Vat = "vat";
        public const string PreliminaryTax = "preliminaryTax";
        public const string VatPaid = "vatPaid";
        public const string ReserveReversal = "reserveReversal";
        public const string Allocation = "allocation";
        public const string Tax = "tax";
        public const string Result = "result";

        public const string TaxAccount = "1630";
        public const string VatSettlement = "2650";
        public const string IncomeTaxDebt = "2512";
        public const string PreliminaryTaxPaid = "2518";
        public const string IncomeTax = "8910";
        public const string AllocationAccount = "8811";
        public const string ReversalAccount = "8819";
        public const string YearResult = "8999";
        public const string YearResultEquity = "2099";
        public const string FundResult = "8220";

        /// <summary>Booked costs that aren't deductible for income tax (INK2S 4.3 c).</summary>
        public static readonly string[] NonDeductible = { "6072", "6982", "6992", "7622", "7632", "8423" };
        /// <summary>Booked income that isn't taxed (INK2S 4.5 c): interest on the tax account.</summary>
        public static readonly string[] TaxFree = { "8314" };

        public static string start(int year) => year + "-01-01";
        public static string end(int year) => year + "-12-31";

        public static SwecialObject find(string companyId, int year) {
            return Db.forCompany("closing", companyId).FirstOrDefault(c => Db.num(c, "year") == year);
        }

        static SwecialObject findOrCreate(string companyId, int year) {
            var closing = find(companyId, year);
            if (closing != null) return closing;
            closing = Db.create("closing", companyId);
            closing.set("year", year);
            closing.set("status", Open);
            Db.commit(closing);
            return closing;
        }

        public static bool isClosed(string companyId, int year) {
            return Db.str(find(companyId, year), "status") == Closed;
        }

        /// <summary>Throws when the date is in a locked year (called for every voucher).</summary>
        public static void requireOpen(string companyId, string date) {
            int year = Dates.parse(date).Year;
            if (isClosed(companyId, year)) {
                throw new LiquidException("Bokslutet för " + year + " är stängt, så inget kan bokföras med datum det året. Öppna året igen under Stängningsdags om något måste rättas.",
                    "THE " + year + " CLOSING IS LOCKED: NOTHING CAN BE BOOKED IN THAT YEAR. REOPEN THE YEAR TO CORRECT IT.");
            }
        }

        /// <summary>The year is over, so its closing can be booked (before that, the page is a preview).</summary>
        public static bool yearEnded(int year) => DateTime.Today.Year > year;

        /// <summary>The years the company has books for, oldest first, up to this year.</summary>
        public static List<int> years(string companyId) {
            var years = Ledger.vouchers(companyId).Select(v => Dates.parse(Db.str(v, "date")).Year).Distinct().ToList();
            years.Add(DateTime.Today.Year);
            return years.Distinct().Where(y => y <= DateTime.Today.Year).OrderBy(y => y).ToList();
        }

        /// <summary>The first ended year that isn't closed, or this year when all are.</summary>
        public static int defaultYear(string companyId) {
            var open = years(companyId).Where(y => yearEnded(y) && !isClosed(companyId, y)).ToList();
            return open.Count > 0 ? open.First() : DateTime.Today.Year;
        }

        // ---- 1. Unpaid customer invoices ----

        /// <summary>Invoices dated in the year, unpaid at its end and not booked yet (cash method years only).</summary>
        public static List<SwecialObject> unpaidInvoices(SwecialObject company, int year) {
            if (Settings.accountingMethod(company, year) != Settings.CashMethod) return new List<SwecialObject>();
            return Db.forCompany("invoice", company.id)
                .Where(i => Db.str(i, "status") == Invoicing.Sent && Db.str(i, "invoiceDate").StartsWith(year + "-") && !Invoicing.bookedAsReceivable(i))
                .OrderBy(i => Db.num(i, "number"))
                .ToList();
        }

        // ---- 2. VAT ----

        static bool isVatAccount(string account) {
            int n = number(account);
            return n >= 2610 && n <= 2649;
        }

        /// <summary>The VAT accounts' balances at the year's end (debit-positive), the ones not zero.</summary>
        public static List<(string account, long balance)> vatBalances(string companyId, int year) {
            return Statements.movements(companyId, "", end(year)).Where(m => isVatAccount(m.Key) && m.Value != 0)
                .OrderBy(m => m.Key).Select(m => (m.Key, m.Value)).ToList();
        }

        // ---- 4. Tax allocation reserves ----

        /// <summary>The reserve account for a year's allocation: 2110 + (year − 2010), as in BAS (2126 = 2026).</summary>
        public static string reserveAccount(int year) => (2110 + (year - 2010)).ToString();

        static int reserveYear(string account) {
            int n = number(account);
            return n >= 2110 && n <= 2149 ? 2010 + (n - 2110) : 0;
        }

        /// <summary>The reserves (credit-positive) on a day's end, by the year they were allocated.</summary>
        public static List<(int year, string account, long amount)> reserves(string companyId, string date) {
            return Statements.movements(companyId, "", date).Where(m => reserveYear(m.Key) > 0 && m.Value != 0)
                .Select(m => (reserveYear(m.Key), m.Key, -m.Value)).OrderBy(r => r.Item1).ToList();
        }

        /// <summary>Reserves that must be reversed this year: allocated six years ago or earlier.</summary>
        public static List<(int year, string account, long amount)> reservesToReverse(string companyId, int year) {
            return reserves(companyId, end(year)).Where(r => r.year <= year - 6 && r.amount > 0).ToList();
        }

        // ---- 5. Tax ----

        public static TaxCalculation tax(SwecialObject company, int year) {
            string companyId = company.id;
            var inYear = Statements.movements(companyId, start(year), end(year));
            long debit(string account) => inYear.TryGetValue(account, out long v) ? v : 0;
            long sumWhere(Func<int, bool> which) => inYear.Where(m => which(number(m.Key))).Sum(m => m.Value);

            var t = new TaxCalculation();
            t.resultBeforeTax = -sumWhere(n => n >= 3000 && n <= 8899);
            t.nonDeductible = NonDeductible.Sum(debit);
            t.taxFree = -TaxFree.Sum(debit);
            t.allocationBooked = debit(AllocationAccount);

            // Reserves held at the start of the year give a standard income; old ones reversed this year an uplift
            long reservesAtStart = Statements.movements(companyId, "", (year - 1) + "-12-31").Where(m => reserveYear(m.Key) > 0).Sum(m => -m.Value);
            long rate = Math.Max(Rules.value(Rules.GovernmentBondRate, start(year)), Rules.value(Rules.GovernmentBondRateFloor, start(year)));
            t.reserveIncome = reservesAtStart > 0 ? hundredths(reservesAtStart, rate) : 0;
            foreach (var m in inYear.Where(m => reserveYear(m.Key) > 0 && reserveYear(m.Key) <= 2020 && m.Value > 0)) {
                t.reserveUplift += (long)Math.Round(m.Value * (reserveYear(m.Key) <= 2018 ? 0.06m : 0.04m), 0, MidpointRounding.AwayFromZero);
            }

            // Funds: the standard income on 1 January's value, and losses only against gains on shares and funds
            if (Securities.holdingsBefore(companyId, start(year)).Count > 0) {
                if (Securities.valuesAtYearStart(company).TryGetValue(year, out long value)) t.fundIncome = Securities.standardIncome(year, value);
                else t.fundValueMissing = true;
            }
            var before = carriedIn(company, year);
            t.deficitIn = before.deficit;
            t.capitalLossIn = before.capitalLoss;
            long fundResult = -debit(FundResult);
            if (fundResult < 0) {
                t.capitalLossAddedBack = -fundResult;
            } else {
                t.capitalLossUsed = Math.Min(t.capitalLossIn, fundResult);
            }
            t.capitalLossOut = t.capitalLossIn - t.capitalLossUsed + t.capitalLossAddedBack;

            long beforeDeficit = t.resultBeforeTax + t.nonDeductible - t.taxFree + t.reserveIncome + t.reserveUplift + t.fundIncome
                + t.capitalLossAddedBack - t.capitalLossUsed;
            t.surplus = beforeDeficit - t.deficitIn;
            long allocationBase = t.surplus + t.allocationBooked;
            t.allocationMax = allocationBase > 0 ? wholeKronor(allocationBase * Rules.value(Rules.TaxAllocationShare, end(year)) / 100) : 0;
            t.taxable = t.surplus > 0 ? t.surplus / 100_00 * 100_00 : 0;
            t.tax = wholeKronor(t.taxable * Rules.value(Rules.CorporateTax, end(year)) / 1000);
            t.deficitOut = t.surplus < 0 ? -t.surplus : 0;
            return t;
        }

        static long hundredths(long amount, long rate) => (long)Math.Round(amount * (decimal)rate / 10000m, 0, MidpointRounding.AwayFromZero);

        static long wholeKronor(long ore) => ore / 100 * 100;

        /// <summary>The deficit and saved fund losses from the year before: its locked closing's, or entered on this one.</summary>
        public static (long deficit, long capitalLoss, bool fromClosing) carriedIn(SwecialObject company, int year) {
            var previous = find(company.id, year - 1);
            if (Db.str(previous, "status") == Closed) return (Db.num(previous, "deficitOut"), Db.num(previous, "capitalLossOut"), true);
            var closing = find(company.id, year);
            return (Db.num(closing, "deficitIn"), Db.num(closing, "capitalLossIn"), false);
        }

        public static void setCarriedIn(SwecialObject company, int year, long deficit, long capitalLoss, string userId) {
            if (deficit < 0 || capitalLoss < 0) throw new LiquidException("Beloppen kan inte vara negativa.", "AMOUNTS CANNOT BE NEGATIVE.");
            lock (Db.Sync) {
                var closing = findOrCreate(company.id, year);
                requireUnlocked(closing);
                string changed = Audit.changes(new[] {
                    ("Underskott från föregående år", Money.format(Db.num(closing, "deficitIn")), Money.format(deficit)),
                    ("Sparad förlust på fonder", Money.format(Db.num(closing, "capitalLossIn")), Money.format(capitalLoss)) });
                closing.set("deficitIn", deficit);
                closing.set("capitalLossIn", capitalLoss);
                Db.commit(closing);
                if (changed.Length > 0) Audit.log(company.id, userId, "closing", "Bokslut " + year + ": " + changed);
            }
        }

        // ---- The steps ----

        /// <summary>What is left to book in each step.</summary>
        public static List<ClosingStep> steps(SwecialObject company, int year) {
            string companyId = company.id;
            var closing = find(companyId, year);
            var inYear = Statements.movements(companyId, start(year), end(year));
            long debit(string account) => inYear.TryGetValue(account, out long v) ? v : 0;
            var t = tax(company, year);
            return new List<ClosingStep> {
                new ClosingStep { key = Receivables, remaining = unpaidInvoices(company, year).Count },
                new ClosingStep { key = Vat, remaining = vatBalances(companyId, year).Sum(b => Math.Abs(b.balance)) },
                new ClosingStep { key = PreliminaryTax, remaining = Db.num(closing, "preliminaryTax") - Db.num(closing, "preliminaryTaxBooked") },
                new ClosingStep { key = VatPaid, remaining = Db.num(closing, "vatPaid") - Db.num(closing, "vatPaidBooked") },
                new ClosingStep { key = ReserveReversal, remaining = reservesToReverse(companyId, year).Sum(r => r.amount) },
                new ClosingStep { key = Allocation, remaining = Db.num(closing, "allocation") - t.allocationBooked },
                new ClosingStep { key = Tax, remaining = t.tax - debit(IncomeTax) },
                new ClosingStep { key = Result, remaining = inYear.Where(m => number(m.Key) >= 3000).Sum(m => m.Value) },
            };
        }

        /// <summary>Books what is left of a step.</summary>
        public static void book(SwecialObject company, int year, string step, string userId) {
            string companyId = company.id;
            if (!yearEnded(year)) throw new LiquidException("Bokslutet kan bokföras när året är slut.", "THE CLOSING CAN BE BOOKED WHEN THE YEAR HAS ENDED.");
            lock (Db.Sync) {
                var closing = findOrCreate(companyId, year);
                requireUnlocked(closing);
                var left = steps(company, year).First(s => s.key == step);
                if (left.done) return;
                string date = end(year);
                Audit.log(companyId, userId, "closing", "Bokslut " + year + ": steget " + StepNames[step] + " bokfördes (" + Money.format(Math.Abs(left.remaining)) + (step == Receivables ? " fakturor" : " kr") + ")");
                switch (step) {
                    case Receivables:
                        foreach (var invoice in unpaidInvoices(company, year)) Invoicing.bookAtYearEnd(invoice, date, userId);
                        return;
                    case Vat: {
                        var rows = new List<VoucherRow>();
                        long net = 0;
                        foreach (var (account, balance) in vatBalances(companyId, year)) {
                            rows.Add(side(account, -balance));
                            net += balance;
                        }
                        rows.Add(side(VatSettlement, net));
                        voucher(closing, date, net <= 0 ? "Moms att betala för " + year : "Moms att få tillbaka för " + year, rows, userId);
                        return;
                    }
                    case PreliminaryTax:
                        voucher(closing, date, "Preliminärskatt dragen från skattekontot " + year,
                            new List<VoucherRow> { side(PreliminaryTaxPaid, left.remaining), side(TaxAccount, -left.remaining) }, userId);
                        closing.set("preliminaryTaxBooked", Db.num(closing, "preliminaryTaxBooked") + left.remaining);
                        Db.commit(closing);
                        return;
                    case VatPaid:
                        voucher(closing, date, "Moms dragen från skattekontot " + year,
                            new List<VoucherRow> { side(VatSettlement, left.remaining), side(TaxAccount, -left.remaining) }, userId);
                        closing.set("vatPaidBooked", Db.num(closing, "vatPaidBooked") + left.remaining);
                        Db.commit(closing);
                        return;
                    case ReserveReversal: {
                        var rows = reservesToReverse(companyId, year).Select(r => side(r.account, r.amount)).ToList();
                        rows.Add(side(ReversalAccount, -left.remaining));
                        voucher(closing, date, "Återföring av periodiseringsfond", rows, userId);
                        return;
                    }
                    case Allocation: {
                        string account = reserveAccount(year);
                        Bas.ensure(companyId, account, "Periodiseringsfond " + year);
                        voucher(closing, date, left.remaining > 0 ? "Avsättning till periodiseringsfond " + year : "Minskad avsättning till periodiseringsfond " + year,
                            new List<VoucherRow> { side(AllocationAccount, left.remaining), side(account, -left.remaining) }, userId);
                        return;
                    }
                    case Tax:
                        voucher(closing, date, "Skatt på årets resultat " + year,
                            new List<VoucherRow> { side(IncomeTax, left.remaining), side(IncomeTaxDebt, -left.remaining) }, userId);
                        return;
                    case Result:
                        voucher(closing, date, "Årets resultat " + year,
                            new List<VoucherRow> { side(YearResult, -left.remaining), side(YearResultEquity, left.remaining) }, userId);
                        return;
                }
            }
        }

        /// <summary>Sets what the user wants booked in an input step (the tax account, the allocation).</summary>
        static readonly Dictionary<string, string> StepNames = new Dictionary<string, string> {
            { Receivables, "obetalda kundfakturor" }, { Vat, "momsen" }, { PreliminaryTax, "preliminärskatt" }, { VatPaid, "moms från skattekontot" },
            { ReserveReversal, "återföring av periodiseringsfond" }, { Allocation, "avsättning till periodiseringsfond" }, { Tax, "skatten" }, { Result, "årets resultat" },
        };

        public static void setInput(SwecialObject company, int year, string step, long amount, string userId) {
            if (amount < 0) throw new LiquidException("Beloppet kan inte vara negativt.", "AMOUNT CANNOT BE NEGATIVE.");
            if (step == Allocation) {
                long max = tax(company, year).allocationMax;
                if (amount > max) throw new LiquidException("Högst " + Money.format(max) + " kr kan sättas av i år.", "AT MOST " + Money.format(max) + " SEK CAN BE ALLOCATED THIS YEAR.");
            } else if (step != PreliminaryTax && step != VatPaid) {
                return;
            }
            lock (Db.Sync) {
                var closing = findOrCreate(company.id, year);
                requireUnlocked(closing);
                long before = Db.num(closing, step);
                closing.set(step, amount);
                Db.commit(closing);
                if (before != amount) Audit.log(company.id, userId, "closing", "Bokslut " + year + ": " + StepNames[step] + " " + Money.format(before) + " → " + Money.format(amount) + " kr");
            }
        }

        /// <summary>Locks the year: everything booked, the funds' value known and the bank balance confirmed.</summary>
        public static void close(SwecialObject company, int year, bool bankConfirmed, string userId) {
            if (!yearEnded(year)) throw new LiquidException("Året är inte slut än.", "THE YEAR HAS NOT ENDED.");
            if (!bankConfirmed) throw new LiquidException("Bekräfta att saldot på företagskontot stämmer med banken.", "CONFIRM THE ACCOUNT BALANCE MATCHES THE BANK.");
            lock (Db.Sync) {
                var closing = findOrCreate(company.id, year);
                requireUnlocked(closing);
                var t = tax(company, year);
                if (t.fundValueMissing) throw new LiquidException("Fyll i fondernas värde den 1 januari " + year + " i Källaren först.", "ENTER THE FUNDS' VALUE ON 1 JANUARY " + year + " FIRST.");
                if (steps(company, year).Any(s => !s.done)) throw new LiquidException("Bokför alla steg först.", "BOOK EVERY STEP FIRST.");
                closing.set("status", Closed);
                closing.set("closedAt", Dates.today());
                Db.linkUser(closing, "closedBy", userId);
                closing.set("taxable", t.taxable);
                closing.set("tax", t.tax);
                closing.set("deficitOut", t.deficitOut);
                closing.set("capitalLossOut", t.capitalLossOut);
                Db.commit(closing);
                Audit.log(company.id, userId, "closing", "Bokslutet för " + year + " stängdes (skatt " + Money.format(t.tax) + " kr)");
            }
        }

        /// <summary>Opens a locked year again (to correct it); a later year's closing that carried from it must be opened first.</summary>
        public static void reopen(SwecialObject company, int year, string userId) {
            lock (Db.Sync) {
                var closing = find(company.id, year);
                if (Db.str(closing, "status") != Closed) return;
                if (isClosed(company.id, year + 1)) throw new LiquidException("Öppna " + (year + 1) + " först.", "REOPEN " + (year + 1) + " FIRST.");
                closing.set("status", Open);
                closing.set("reopenedAt", Dates.today());
                Db.commit(closing);
                Audit.log(company.id, userId, "closing", "Bokslutet för " + year + " öppnades igen");
            }
        }

        static void requireUnlocked(SwecialObject closing) {
            if (Db.str(closing, "status") == Closed) throw new LiquidException("Året är stängt. Öppna det igen för att ändra.", "THE YEAR IS LOCKED. REOPEN IT TO MAKE CHANGES.");
        }

        static void voucher(SwecialObject closing, string date, string text, List<VoucherRow> rows, string userId) {
            Ledger.book(Db.companyId(closing), Ledger.YearEnd, date, text, rows, userId, "closing", closing.id);
        }

        /// <summary>A row with a debit-positive amount: positive is debit, negative credit.</summary>
        static VoucherRow side(string account, long amount) {
            return amount >= 0 ? new VoucherRow { account = account, debit = amount } : new VoucherRow { account = account, credit = -amount };
        }

        static int number(string account) => account.Length == 4 && int.TryParse(account, out int n) ? n : 0;
    }
}
