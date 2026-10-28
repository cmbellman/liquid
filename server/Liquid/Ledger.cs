using com.swecial.server;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace com.swecial.code {
    public class VoucherRow {
        public string account;
        public long debit;
        public long credit;
    }

    /// <summary>
    /// Vouchers (verifikationer). A voucher is never changed or deleted once booked: a mistake is
    /// corrected with a reversing voucher (reverse), as the Swedish Bookkeeping Act requires. The
    /// rows live inside the voucher as JSON, so a voucher is always written as a whole.
    ///
    /// Series: A manual vouchers, F customer invoices booked when sent (accrual method), I customer
    /// payments, U expenses, B other bank events, V funds and other securities, S the year-end closing. Numbers run per series and calendar year (the
    /// fiscal year is the calendar year for now).
    /// </summary>
    public static class Ledger {
        public const string Manual = "A";
        public const string Invoices = "F";
        public const string Payments = "I";
        public const string Expenses = "U";
        public const string BankEvents = "B";
        public const string Securities = "V";
        public const string YearEnd = "S";

        public static List<VoucherRow> rows(SwecialObject voucher) {
            string json = Db.str(voucher, "rowsJson");
            return json.Length == 0 ? new List<VoucherRow>() : JsonConvert.DeserializeObject<List<VoucherRow>>(json);
        }

        public static List<SwecialObject> vouchers(string companyId) {
            return Db.forCompany("voucher", companyId)
                .OrderBy(v => Db.str(v, "date"))
                .ThenBy(v => Db.str(v, "series"))
                .ThenBy(v => Db.num(v, "number"))
                .ToList();
        }

        public static string label(SwecialObject voucher) {
            return Db.str(voucher, "series") + Db.num(voucher, "number");
        }

        /// <summary>
        /// Checks a voucher before booking and returns the rows to book: rows with neither debit nor
        /// credit are dropped; the rest must balance and use accounts in the company's chart.
        /// </summary>
        public static List<VoucherRow> validate(string companyId, string date, string text, List<VoucherRow> input) {
            Dates.parse(date);
            Closing.requireOpen(companyId, date);
            if ((text ?? "").Trim().Length == 0) throw new LiquidException("Verifikationen behöver en text.", "VOUCHER DESCRIPTION REQUIRED.");

            var rows = input.Where(r => r.debit != 0 || r.credit != 0).ToList();
            if (rows.Count < 2) throw new LiquidException("En verifikation behöver minst två rader.", "A VOUCHER REQUIRES AT LEAST TWO LINES.");

            var accounts = Db.forCompany("account", companyId).Select(a => Db.str(a, "number")).ToHashSet();
            foreach (var row in rows) {
                row.account = (row.account ?? "").Trim();
                if (!accounts.Contains(row.account)) throw new LiquidException("Kontot finns inte i kontoplanen: " + row.account, "ACCOUNT NOT IN CHART OF ACCOUNTS: " + row.account);
                if (row.debit < 0 || row.credit < 0) throw new LiquidException("Debet och kredit kan inte vara negativa (konto " + row.account + ").", "DEBIT AND CREDIT CANNOT BE NEGATIVE (ACCOUNT " + row.account + ").");
                if (row.debit != 0 && row.credit != 0) throw new LiquidException("En rad kan inte ha både debet och kredit (konto " + row.account + ").", "A LINE CANNOT CARRY BOTH DEBIT AND CREDIT (ACCOUNT " + row.account + ").");
            }
            long debit = rows.Sum(r => r.debit);
            long credit = rows.Sum(r => r.credit);
            if (debit != credit) {
                throw new LiquidException("Verifikationen balanserar inte: debet " + Money.format(debit) + ", kredit " + Money.format(credit) + ".", "VOUCHER OUT OF BALANCE: DEBIT " + Money.format(debit) + ", CREDIT " + Money.format(credit) + ".");
            }
            return rows;
        }

        /// <summary>Validates, numbers and books a voucher.</summary>
        public static SwecialObject book(string companyId, string series, string date, string text, List<VoucherRow> input,
                string userId, string sourceType = null, string sourceId = null) {
            var rows = validate(companyId, date, text, input);
            date = Dates.clean(date);
            text = text.Trim();
            long debit = rows.Sum(r => r.debit);

            lock (Db.Sync) {
                int year = Dates.parse(date).Year;
                var voucher = Db.create("voucher", companyId);
                voucher.set("series", series);
                voucher.set("year", year);
                voucher.set("number", Db.nextNumber(companyId, "voucher-" + series + "-" + year));
                voucher.set("date", date);
                voucher.set("text", text);
                voucher.set("rowsJson", JsonConvert.SerializeObject(rows));
                voucher.set("total", debit);
                Db.linkUser(voucher, "createdBy", userId);
                if (sourceType != null) {
                    voucher.set("sourceType", sourceType);
                    if (!string.IsNullOrEmpty(sourceId)) {
                        Db.link(voucher, "source", Db.getOwned(sourceType, sourceId, companyId));
                    }
                }
                Db.commit(voucher);
                return voucher;
            }
        }

        /// <summary>
        /// Books the opposite of a voucher and links the two. A voucher can be reversed once. Vouchers
        /// booked from an expense or bank event are reversed by that object (fromSource), so the two
        /// stay in step; from the voucher page they can't be.
        /// </summary>
        public static SwecialObject reverse(SwecialObject original, string date, string userId, bool fromSource = false) {
            lock (Db.Sync) {
                if (Db.linkedId(original, "reversedBy").Length > 0) throw new LiquidException("Verifikationen är redan makulerad.", "VOUCHER ALREADY VOIDED.");
                if (Db.linkedId(original, "reverses").Length > 0) throw new LiquidException("En rättelse kan inte makuleras.", "A REVERSAL ENTRY CANNOT BE VOIDED.");
                // The invoice's status would no longer match its vouchers (credit notes come later)
                if (!fromSource && Db.str(original, "sourceType").Length > 0) throw new LiquidException("Verifikationen är bokförd från en faktura, utgift, bankhändelse eller fondhändelse. Ändra eller ta bort den där i stället.", "VOUCHER ORIGINATES FROM AN INVOICE, EXPENDITURE, TREASURY OR SECURITIES EVENT. AMEND IT AT THE SOURCE.");
                var swapped = rows(original).Select(r => new VoucherRow { account = r.account, debit = r.credit, credit = r.debit }).ToList();
                var reversal = book(Db.companyId(original), Db.str(original, "series"), date,
                    "Rättelse av " + label(original) + ": " + Db.str(original, "text"), swapped, userId);
                Db.link(reversal, "reverses", original);
                Db.commit(reversal);
                Db.link(original, "reversedBy", reversal);
                Db.commit(original);
                return reversal;
            }
        }

        /// <summary>
        /// Year of a voucher: from the 'year' field or fallback to the date string prefix.
        /// </summary>
        public static int voucherYear(SwecialObject voucher) {
            long y = Db.num(voucher, "year");
            if (y > 0) return (int)y;
            string d = Db.str(voucher, "date");
            if (d.Length >= 4 && int.TryParse(d.Substring(0, 4), out int yearFromDate)) return yearFromDate;
            return 0;
        }

        /// <summary>
        /// Admin only (for test data): deletes a voucher (and optionally its linked reversal/original),
        /// cleans up source references, and renumbers the remaining vouchers in the series and year
        /// so the series remains gap-free (1, 2, 3...).
        /// </summary>
        public static void deleteAndRenumber(SwecialObject voucher, string companyId, bool deleteLinked = false) {
            if (voucher == null) return;
            lock (Db.Sync) {
                string date = Db.str(voucher, "date");
                Closing.requireOpen(companyId, date);

                var toDelete = new List<SwecialObject> { voucher };
                if (deleteLinked) {
                    var linkedRev = Db.linked(voucher, "reversedBy", "voucher");
                    if (linkedRev != null) {
                        Closing.requireOpen(companyId, Db.str(linkedRev, "date"));
                        toDelete.Add(linkedRev);
                    }
                    var linkedOrig = Db.linked(voucher, "reverses", "voucher");
                    if (linkedOrig != null && !toDelete.Any(v => v.id == linkedOrig.id)) {
                        Closing.requireOpen(companyId, Db.str(linkedOrig, "date"));
                        toDelete.Add(linkedOrig);
                    }
                }

                var affectedSeries = new HashSet<(string series, int year)>();

                foreach (var v in toDelete) {
                    string series = Db.str(v, "series");
                    int year = voucherYear(v);
                    if (series.Length > 0 && year > 0) {
                        affectedSeries.Add((series, year));
                    }

                    // Clear links from any other voucher pointing to this one
                    foreach (var other in Db.forCompany("voucher", companyId)) {
                        if (toDelete.Any(d => d.id == other.id)) continue;
                        bool changed = false;
                        if (Db.linkedId(other, "reversedBy") == v.id) {
                            Db.unlink(other, "reversedBy");
                            changed = true;
                        }
                        if (Db.linkedId(other, "reverses") == v.id) {
                            Db.unlink(other, "reverses");
                            changed = true;
                        }
                        if (changed) Db.commit(other);
                    }

                    // Clean up source references
                    cleanSourceReferences(v, companyId);

                    // Delete voucher object
                    Db.delete(v);
                }

                // Renumber affected series
                foreach (var (series, year) in affectedSeries) {
                    renumberSeries(companyId, series, year);
                }
            }
        }

        static void cleanSourceReferences(SwecialObject v, string companyId) {
            string vId = v.id;
            string sourceId = Db.linkedId(v, "source");

            // Expenses: if an expense was removed or points to this voucher, delete it if removed or clear voucher
            foreach (var expense in Db.forCompany("expense", companyId)) {
                if (expense.id == sourceId || Db.linkedId(expense, "voucher") == vId) {
                    if (Db.str(expense, "status") == "removed") {
                        Db.delete(expense);
                    } else {
                        Db.unlink(expense, "voucher");
                        Db.commit(expense);
                    }
                }
            }

            // Invoices: clear any matching voucher pointers
            foreach (var invoice in Db.forCompany("invoice", companyId)) {
                bool changed = false;
                if (Db.linkedId(invoice, "voucher") == vId) { Db.unlink(invoice, "voucher"); changed = true; }
                if (Db.linkedId(invoice, "paymentVoucher") == vId) { Db.unlink(invoice, "paymentVoucher"); changed = true; }
                if (Db.linkedId(invoice, "closingVoucher") == vId) { Db.unlink(invoice, "closingVoucher"); changed = true; }
                if (changed) Db.commit(invoice);
            }

            // Bank events: clear voucher or delete if matching sourceId
            foreach (var be in Db.forCompany("bank_event", companyId)) {
                if (be.id == sourceId || Db.linkedId(be, "voucher") == vId) {
                    Db.unlink(be, "voucher");
                    Db.commit(be);
                }
            }

            // Security events: clear voucher
            foreach (var se in Db.forCompany("security_event", companyId)) {
                if (se.id == sourceId || Db.linkedId(se, "voucher") == vId) {
                    Db.unlink(se, "voucher");
                    Db.commit(se);
                }
            }
        }

        /// <summary>
        /// Renumbers all remaining vouchers in the given series and year sequentially (1, 2, 3...)
        /// ordered by date, existing number, and creation time, and updates the sequence counter.
        /// </summary>
        public static void renumberSeries(string companyId, string series, int year) {
            lock (Db.Sync) {
                var list = Db.forCompany("voucher", companyId)
                    .Where(v => Db.str(v, "series") == series && voucherYear(v) == year)
                    .OrderBy(v => Db.str(v, "date"))
                    .ThenBy(v => Db.num(v, "number"))
                    .ThenBy(v => v.createdAt)
                    .ToList();

                for (int i = 0; i < list.Count; i++) {
                    long newNumber = i + 1;
                    var v = list[i];
                    bool changed = false;
                    if (Db.num(v, "number") != newNumber) {
                        v.set("number", newNumber);
                        changed = true;
                    }
                    if (Db.num(v, "year") != year) {
                        v.set("year", year);
                        changed = true;
                    }
                    // Update text if it references an original that might have changed number
                    var orig = Db.linked(v, "reverses", "voucher");
                    if (orig != null) {
                        string text = Db.str(v, "text");
                        if (text.StartsWith("Rättelse av ")) {
                            int colon = text.IndexOf(": ");
                            if (colon >= 0) {
                                string newText = "Rättelse av " + label(orig) + ": " + text.Substring(colon + 2);
                                if (text != newText) {
                                    v.set("text", newText);
                                    changed = true;
                                }
                            }
                        }
                    }
                    if (changed) Db.commit(v);
                }

                Db.setCounter(companyId, "voucher-" + series + "-" + year, list.Count);
            }
        }

        /// <summary>
        /// Renumbers every series across all years for the company.
        /// </summary>
        public static void renumberAllSeries(string companyId) {
            lock (Db.Sync) {
                var seriesAndYears = Db.forCompany("voucher", companyId)
                    .Select(v => (series: Db.str(v, "series"), year: voucherYear(v)))
                    .Where(sy => sy.series.Length > 0 && sy.year > 0)
                    .Distinct()
                    .ToList();

                foreach (var (series, year) in seriesAndYears) {
                    renumberSeries(companyId, series, year);
                }
            }
        }
    }
}
