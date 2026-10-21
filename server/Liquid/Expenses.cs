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
    /// Booking (series U): cost (category account) D net, 2641 D VAT, 1930 K total. Bought abroad
    /// without Swedish VAT (reverse charge): cost D total, 1930 K total, and the Swedish VAT the company
    /// calculates itself, 2645 D / 2614 K, which cancel out in the VAT return.
    /// </summary>
    public static class Expenses {
        public const string ForeignEu = "eu";
        public const string ForeignNonEu = "noneu";

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
            public string note;
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
                attachReceipt(expense, upload);
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
            string foreign = input.foreign == ForeignEu || input.foreign == ForeignNonEu ? input.foreign : "";
            // 2614 is the 25 % account; services bought abroad are almost always 25 % in Sweden
            if (foreign.Length > 0 && input.vatRate != 25) throw new LiquidException("Köp från utlandet räknas med 25 % moms i Liquid.", "FOREIGN PURCHASES ARE ASSESSED AT 25 % VAT.");
            // Bought abroad: the receipt has no Swedish VAT, the company calculates it on the total
            long vat = foreign.Length > 0 ? Money.percent(input.total, input.vatRate) : input.vat ?? vatIncluded(input.total, input.vatRate);
            if (vat < 0 || (foreign.Length == 0 && vat >= input.total)) throw new LiquidException("Momsen kan inte vara större än beloppet.", "VAT CANNOT EXCEED THE AMOUNT.");
            if (foreign.Length == 0 && input.vatRate == 0 && vat != 0) throw new LiquidException("Momsen ska vara 0 när momssatsen är 0 %.", "VAT MUST BE ZERO AT A 0 % RATE.");

            string text = supplier + ", " + category.name;
            var rows = foreign.Length > 0
                ? new List<VoucherRow> {
                    new VoucherRow { account = category.account, debit = input.total },
                    new VoucherRow { account = Bas.ReverseChargeInputVat, debit = vat },
                    new VoucherRow { account = Bas.ReverseChargeOutputVat, credit = vat },
                    new VoucherRow { account = Bas.Bank, credit = input.total },
                }
                : new List<VoucherRow> {
                    new VoucherRow { account = category.account, debit = input.total - vat },
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
                        && Db.num(expense, "vatRate") == input.vatRate && Db.str(expense, "foreign") == foreign) {
                    // Nothing that is booked changed (only the note or the receipt): keep the voucher
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
                expense.set("note", (input.note ?? "").Trim());
                var voucher = Ledger.book(companyId, Ledger.Expenses, date, text, rows, userId, "expense", expense.id);
                expense.set("voucherId", voucher.id);
                Db.commit(expense);
                return expense;
            }
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

        /// <summary>Stores an uploaded receipt and attaches it to the expense (replacing an earlier one).</summary>
        public static void attachReceipt(SwecialObject expense, HttpRequest.UploadedFile upload) {
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
            expense.set("receiptFileId", file.id);
            Db.commit(expense);
        }

        public static SwecialFile receipt(string fileId, string companyId) {
            var stored = Db.getOwned("_file", fileId, companyId);
            if (stored == null) return null;
            // Objects loaded from disk may be plain SwecialObjects; copy so fileType and the path are right
            return stored as SwecialFile ?? new SwecialFile(Db.app, stored);
        }

        /// <summary>Net purchases of services from abroad in the period, for VAT return boxes 21 and 22.</summary>
        public static long foreignPurchases(string companyId, string foreign, string from, string to) {
            return list(companyId)
                .Where(e => Db.str(e, "foreign") == foreign
                    && string.CompareOrdinal(Db.str(e, "date"), from) >= 0
                    && string.CompareOrdinal(Db.str(e, "date"), to) <= 0)
                .Sum(e => Db.num(e, "total"));
        }
    }
}
