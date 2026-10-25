using com.swecial.server;
using System;
using System.Collections.Generic;
using System.Linq;

namespace com.swecial.code {
    /// <summary>
    /// Everyday purchases paid from the company account (cash method: booked on the day the money
    /// left the account). An expense can start as a bare receipt in the bag (status inbox, nothing
    /// booked) and is booked when it's sorted. An expense owns its voucher: changing it reverses the old voucher and books
    /// a new one, removing it reverses it, so the books always show what happened.
    ///
    /// Booking (series U): cost (category account) D net, 2641 D VAT, 1930 K total. Bought from a
    /// business abroad without Swedish VAT (reverse charge: services from the EU or outside it, goods from
    /// the EU): cost D total, 1930 K total, and the Swedish VAT the company calculates itself, 2645 D /
    /// 2614, 2624 or 2634 K by rate, which cancel out in the VAT return. Goods imported from outside the
    /// EU: the same with import VAT on the customs value (2615, 2625, 2635). Foreign VAT on the receipt, or
    /// travel, hotels and meals abroad (taxed where they're used): the whole amount is cost, no VAT.
    /// Representation (food and drink): only the deductible part of the VAT goes to 2641 (Representation),
    /// the rest is part of the cost; the receipt's VAT is kept as receiptVat, and the participants and
    /// purpose with the expense.
    /// </summary>
    public static class Expenses {
        /// <summary>Services from a business in another EU country, reverse charge (VAT return box 21).</summary>
        public const string ForeignEu = "eu";
        /// <summary>Services from outside the EU, reverse charge (box 22).</summary>
        public const string ForeignNonEu = "noneu";
        /// <summary>Goods from a business in another EU country, reverse charge (box 20).</summary>
        public const string ForeignEuGoods = "eugoods";
        /// <summary>Goods imported from outside the EU: import VAT on the customs value (boxes 50 and 60–62).</summary>
        public const string Import = "import";
        /// <summary>Foreign VAT on the receipt, or bought and used abroad (hotel, taxi, meal): no Swedish VAT at all.</summary>
        public const string Abroad = "abroad";
        static readonly string[] AbroadKinds = { ForeignEu, ForeignNonEu, ForeignEuGoods, Import, Abroad };

        public const string HotelAbroadAccount = "5832";

        public static bool isReverseCharge(string foreign) {
            return foreign == ForeignEu || foreign == ForeignNonEu || foreign == ForeignEuGoods;
        }

        public const long MaxReceiptBytes = 10 * 1024 * 1024;

        static readonly Dictionary<string, string> ReceiptTypes = new Dictionary<string, string> {
            { "image/jpeg", "jpg" },
            { "image/png", "png" },
            { "image/webp", "webp" },
            { "image/heic", "heic" },
            { "application/pdf", "pdf" },
        };

        public class Input {
            public string date;
            public string supplier;
            public string category;
            public long total;
            /// <summary>The VAT on the receipt; null computes it from the rate.</summary>
            public long? vat;
            public int vatRate;
            /// <summary>"", "eu" or "noneu".</summary>
            public string foreign = "";
            /// <summary>Import: the taxable value from the customs decision; null uses the total.</summary>
            public long? importBase;
            public string note;
            // Representation (food and drink): the documentation and what decides the VAT deduction
            public string representationKind = "";
            public int participants;
            public string attendees = "";
            public string purpose = "";
            public bool alcohol;
        }

        public const string Inbox = "inbox";
        public const string Booked = "booked";
        public const string Removed = "removed";
        /// <summary>The document in the bag was our own sales invoice: it became an invoice (invoiceId).</summary>
        public const string Converted = "converted";

        /// <summary>Booked expenses, newest first (not the receipts still in the bag, not removed ones).</summary>
        public static List<SwecialObject> list(string companyId) {
            return Db.forCompany("expense", companyId)
                .Where(e => Db.str(e, "status") == Booked)
                .OrderByDescending(e => Db.str(e, "date"))
                .ThenByDescending(e => e.createdAt)
                .ToList();
        }

        /// <summary>Receipts thrown in the bag (Pantpåsen) that haven't been sorted yet, oldest first.</summary>
        public static List<SwecialObject> inbox(string companyId) {
            return Db.forCompany("expense", companyId)
                .Where(e => Db.str(e, "status") == Inbox)
                .OrderBy(e => e.createdAt)
                .ToList();
        }

        /// <summary>
        /// Puts a receipt in the bag: an expense without any details yet, booked only once it has been
        /// sorted (save). The receipt is kept right away, so nothing gets lost.
        /// </summary>
        public static SwecialObject addToInbox(string companyId, HttpRequest.UploadedFile upload, string userId) {
            checkReceipt(upload);
            lock (Db.Sync) {
                var expense = Db.create("expense", companyId);
                expense.set("status", Inbox);
                expense.set("createdBy", userId);
                Db.commit(expense);
                attachReceipt(expense, upload, userId);
                ReceiptReader.queue(expense);
                return expense;
            }
        }

        /// <summary>The VAT included in a total at a rate: total × rate / (100 + rate).</summary>
        public static long vatIncluded(long total, int rate) {
            return (long)Math.Round(total * (decimal)rate / (100m + rate), 0, MidpointRounding.AwayFromZero);
        }

        /// <summary>Creates the expense (expense == null) or changes it, and books it.</summary>
        public static SwecialObject save(string companyId, SwecialObject expense, Input input, string userId) {
            string date = Dates.clean(input.date);
            string supplier = (input.supplier ?? "").Trim();
            var category = Categories.find(input.category) ?? throw new LiquidException("Välj vad utgiften gäller.", "SELECT EXPENDITURE CLASS.");
            if (supplier.Length == 0) throw new LiquidException("Fyll i var du handlade (leverantör).", "SUPPLIER REQUIRED.");
            if (input.total <= 0) throw new LiquidException("Fyll i beloppet som drogs från kontot.", "ENTER THE AMOUNT DEBITED FROM THE ACCOUNT.");
            if (!Bas.VatRates.Contains(input.vatRate)) throw new LiquidException("Ogiltig momssats.", "INVALID VAT RATE.");
            string foreign = AbroadKinds.Contains(input.foreign) ? input.foreign : "";
            // Travel, hotels and meals abroad are taxed where they're used: never reverse charge
            if (foreign.Length > 0 && category.consumedAbroad) foreign = Abroad;
            bool reverseCharge = isReverseCharge(foreign);
            bool import = foreign == Import;
            long importBase = 0;
            if (foreign == Abroad) {
                // Foreign VAT, or bought and used abroad: no Swedish VAT, the whole amount is cost
                input.vatRate = 0;
                input.vat = 0;
            }
            if ((reverseCharge || import) && input.vatRate == 0) throw new LiquidException("Välj den svenska momssats som gäller för det som köptes (oftast 25 %).", "SELECT THE SWEDISH VAT RATE FOR THE PURCHASE (USUALLY 25 %).");
            if (import) {
                importBase = input.importBase ?? input.total;
                if (importBase <= 0) throw new LiquidException("Fyll i beskattningsunderlaget för importmomsen (tullvärdet).", "ENTER THE TAXABLE VALUE FOR IMPORT VAT (CUSTOMS VALUE).");
            }
            // Bought from abroad: the receipt has no Swedish VAT, the company calculates it itself
            long vat = reverseCharge ? Money.percent(input.total, input.vatRate)
                : import ? Money.percent(importBase, input.vatRate)
                : input.vat ?? vatIncluded(input.total, input.vatRate);
            if (vat < 0 || (!reverseCharge && !import && vat >= input.total)) throw new LiquidException("Momsen kan inte vara större än beloppet.", "VAT CANNOT EXCEED THE AMOUNT.");
            if (!reverseCharge && !import && input.vatRate == 0 && vat != 0) throw new LiquidException("Momsen ska vara 0 när momssatsen är 0 %.", "VAT MUST BE ZERO AT A 0 % RATE.");
            bool representation = category.key == Representation.CategoryKey;
            string costAccount = category.key == "hotel" && foreign == Abroad ? HotelAbroadAccount : category.account;

            string text = supplier + ", " + category.name;
            // Representation: only part of the VAT may be deducted, the rest is part of the cost
            long receiptVat = vat;
            string kind = "", attendees = "", purpose = "";
            int participants = 0;
            bool alcohol = false;
            if (representation) {
                kind = input.representationKind == Representation.External ? Representation.External : Representation.Internal;
                participants = input.participants;
                attendees = (input.attendees ?? "").Trim();
                purpose = (input.purpose ?? "").Trim();
                alcohol = input.alcohol;
                if (participants < 1 || participants > 1000) throw new LiquidException("Fyll i hur många som deltog.", "ENTER THE NUMBER OF PARTICIPANTS.");
                if (attendees.Length == 0) throw new LiquidException("Skriv vilka som deltog (namn och roll). Det krävs som underlag för representation.", "LIST THE PARTICIPANTS (NAME AND ROLE). REQUIRED FOR ENTERTAINMENT RECORDS.");
                if (purpose.Length == 0) throw new LiquidException("Skriv syftet med representationen. Det krävs som underlag.", "STATE THE PURPOSE. REQUIRED FOR ENTERTAINMENT RECORDS.");
                vat = Representation.deductibleVat(date, input.total, receiptVat, participants, alcohol);
                text += ", " + purpose + " (" + participants + " pers.)";
            }
            var rows = reverseCharge || import
                ? new List<VoucherRow> {
                    new VoucherRow { account = costAccount, debit = input.total },
                    new VoucherRow { account = Bas.ReverseChargeInputVat, debit = vat },
                    new VoucherRow { account = import ? Bas.importVatAccount(input.vatRate) : Bas.reverseChargeVatAccount(input.vatRate), credit = vat },
                    new VoucherRow { account = Bas.Bank, credit = input.total },
                }
                : new List<VoucherRow> {
                    new VoucherRow { account = costAccount, debit = input.total - vat },
                    new VoucherRow { account = Bas.InputVat, debit = vat },
                    new VoucherRow { account = Bas.Bank, credit = input.total },
                };
            Ledger.validate(companyId, date, text, rows);

            lock (Db.Sync) {
                if (expense != null && Db.str(expense, "status") == Removed) throw new LiquidException("Utgiften är borttagen.", "EXPENDITURE RECORD HAS BEEN PURGED.");
                if (expense == null) {
                    expense = Db.create("expense", companyId);
                    expense.set("createdBy", userId);
                } else if (Db.str(expense, "status") == Booked && Db.str(expense, "date") == date && Db.str(expense, "supplier") == supplier && Db.str(expense, "category") == category.key
                        && Db.num(expense, "total") == input.total && Db.num(expense, "vat") == vat
                        && Db.num(expense, "vatRate") == input.vatRate && Db.str(expense, "foreign") == foreign && Db.num(expense, "importBase") == importBase
                        && Db.num(expense, "receiptVat") == (representation ? receiptVat : 0) && Db.num(expense, "participants") == participants
                        && Db.str(expense, "purpose") == purpose) {
                    // Nothing that is booked changed (only the note, the receipt or who took part): keep the voucher
                    setRepresentation(expense, kind, attendees, alcohol);
                    expense.set("note", (input.note ?? "").Trim());
                    Db.commit(expense);
                    return expense;
                } else {
                    reverseVoucher(expense, userId);
                }
                expense.set("status", Booked);
                expense.set("date", date);
                expense.set("supplier", supplier);
                expense.set("category", category.key);
                expense.set("total", input.total);
                expense.set("vat", vat);
                expense.set("vatRate", input.vatRate);
                expense.set("foreign", foreign);
                expense.set("importBase", importBase);
                expense.set("note", (input.note ?? "").Trim());
                expense.set("receiptVat", representation ? receiptVat : 0);
                expense.set("participants", participants);
                expense.set("purpose", purpose);
                setRepresentation(expense, kind, attendees, alcohol);
                var voucher = Ledger.book(companyId, Ledger.Expenses, date, text, rows, userId, "expense", expense.id);
                expense.set("voucherId", voucher.id);
                Db.commit(expense);
                return expense;
            }
        }

        static void setRepresentation(SwecialObject expense, string kind, string attendees, bool alcohol) {
            expense.set("representationKind", kind);
            expense.set("attendees", attendees);
            expense.set("alcohol", alcohol);
        }

        /// <summary>Reverses the expense's voucher and hides it. The receipt is kept with the record.</summary>
        public static void remove(SwecialObject expense, string userId) {
            lock (Db.Sync) {
                if (Db.str(expense, "status") == Removed) return;
                reverseVoucher(expense, userId);
                expense.set("status", Removed);
                Db.commit(expense);
            }
        }

        static void reverseVoucher(SwecialObject expense, string userId) {
            var voucher = Db.getOwned("voucher", Db.str(expense, "voucherId"), Db.str(expense, "companyId"));
            if (voucher != null && Db.str(voucher, "reversedById").Length == 0) {
                // Dated like the original, so the period's figures come out right
                Ledger.reverse(voucher, Db.str(voucher, "date"), userId, fromSource: true);
            }
        }

        /// <summary>Checks an uploaded receipt (null when none was chosen) and returns its file extension.</summary>
        public static string checkReceipt(HttpRequest.UploadedFile upload) {
            if (upload == null || upload.data == null || upload.data.Length == 0) return null;
            if (upload.data.Length > MaxReceiptBytes) throw new LiquidException("Kvittot är för stort (högst 10 MB).", "DOCUMENT EXCEEDS 10 MB LIMIT.");
            if (!ReceiptTypes.TryGetValue(receiptType(upload), out string extension)) {
                throw new LiquidException("Kvittot ska vara en bild (JPEG, PNG, WebP, HEIC) eller en PDF.", "DOCUMENT FORMAT REJECTED. ACCEPTED: JPEG, PNG, WEBP, HEIC, PDF.");
            }
            return extension;
        }

        static string receiptType(HttpRequest.UploadedFile upload) {
            return (upload.contentType ?? "").Split(';')[0].Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Stores an uploaded receipt and attaches it to the expense. A receipt it replaces is kept with
        /// the expense (receiptHistoryJson: file, when and by whom) and the change is logged, so the
        /// original underlag stays readable for the seven years the law asks.
        /// </summary>
        public static void attachReceipt(SwecialObject expense, HttpRequest.UploadedFile upload, string userId) {
            string extension = checkReceipt(upload);
            if (extension == null) return;
            string type = receiptType(upload);
            var file = SwecialFile.create(Db.app);
            file.fileType = extension;
            file.set("companyId", Db.str(expense, "companyId"));
            file.set("contentType", type);
            file.set("originalName", upload.fileName ?? "");
            file.save(upload.data);
            file.saveNow();
            string previous = Db.str(expense, "receiptFileId");
            if (previous.Length > 0) {
                var history = receiptHistory(expense);
                history.Add(new ReplacedReceipt { fileId = previous, replacedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), replacedBy = userId ?? "" });
                expense.set("receiptHistoryJson", Newtonsoft.Json.JsonConvert.SerializeObject(history));
                Audit.log(Db.str(expense, "companyId"), userId, "receipt", "Underlaget byttes på utgiften " + Db.str(expense, "date") + " " + Db.str(expense, "supplier")
                    + (Db.str(expense, "voucherId").Length > 0 ? " (verifikation " + Ledger.label(Db.get("voucher", Db.str(expense, "voucherId"))) + ")" : "") + "; det tidigare finns kvar");
            }
            expense.set("receiptFileId", file.id);
            Db.commit(expense);
        }

        public class ReplacedReceipt {
            public string fileId;
            public string replacedAt;
            public string replacedBy;
        }

        /// <summary>The receipts an expense had before its current one, oldest first.</summary>
        public static List<ReplacedReceipt> receiptHistory(SwecialObject expense) {
            string json = Db.str(expense, "receiptHistoryJson");
            return json.Length == 0 ? new List<ReplacedReceipt>() : Newtonsoft.Json.JsonConvert.DeserializeObject<List<ReplacedReceipt>>(json);
        }

        public static SwecialFile receipt(string fileId, string companyId) {
            var stored = Db.getOwned("_file", fileId, companyId);
            if (stored == null) return null;
            // Objects loaded from disk may be plain SwecialObjects; copy so fileType and the path are right
            return stored as SwecialFile ?? new SwecialFile(Db.app, stored);
        }

        /// <summary>Purchases with reverse charge in the period, for VAT return boxes 20, 21 and 22.</summary>
        public static long foreignPurchases(string companyId, string foreign, string from, string to) {
            return inPeriod(companyId, from, to)
                .Where(e => Db.str(e, "foreign") == foreign && !(Categories.find(Db.str(e, "category"))?.consumedAbroad ?? false))
                .Sum(e => Db.num(e, "total"));
        }

        /// <summary>The taxable value of imported goods (VAT return box 50).</summary>
        public static long importBase(string companyId, string from, string to) {
            return inPeriod(companyId, from, to).Where(e => Db.str(e, "foreign") == Import).Sum(e => Db.num(e, "importBase"));
        }

        static IEnumerable<SwecialObject> inPeriod(string companyId, string from, string to) {
            return list(companyId).Where(e => string.CompareOrdinal(Db.str(e, "date"), from) >= 0 && string.CompareOrdinal(Db.str(e, "date"), to) <= 0);
        }
    }
}
