using com.swecial.code;
using com.swecial.server;
using System.Linq;
using System.Text;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/expense.html : a new expense, or ?id= to change or remove one. The form is
    /// multipart so the receipt (photo or PDF) comes with it. The category picks the account and
    /// suggests the VAT rate; the VAT amount is computed while typing and can be changed.
    /// A receipt from Pantpåsen is sorted here: the receipt is shown beside the form, and saving
    /// books it and moves on to the next receipt in the bag.
    /// </summary>
    public class WebExpense : LiquidPage {
        SwecialObject expense;

        bool sorting => expense != null && Db.str(expense, "status") == Expenses.Inbox;

        public override void processPage() {
            if (!requireCompany()) return;
            string id = isPost ? form("id") : query("id");
            if (id.Length > 0) {
                expense = Db.getOwned("expense", id, companyId);
                if (expense == null || Db.str(expense, "status") == Expenses.Removed) {
                    redirect(BasePath + "/expenses.html");
                    return;
                }
            }
            title(Tr(expense == null ? "expense.new" : sorting ? "expense.sortTitle" : "expense.title"));

            if (isPost) {
                if (sorting && form("action") == "toInvoice") {
                    // Our own sales invoice, not a purchase: it becomes an invoice draft to confirm
                    var invoice = Invoicing.draftFromDocument(company, expense, account.id);
                    redirect(BasePath + "/invoice.html?id=" + u(invoice.id) + "&ok=imported");
                    return;
                }
                if (sorting && form("action") == "reread") {
                    ReceiptReader.queue(expense);
                    redirect(BasePath + "/expense.html?id=" + u(expense.id));
                    return;
                }
                if (form("action") == "remove") {
                    bool removedFromBag = sorting;
                    Expenses.remove(expense, account.id);
                    redirect(BasePath + (removedFromBag ? "/pant.html" : "/expenses.html") + "?ok=deleted");
                    return;
                }
                req.body.files.TryGetValue("receipt", out var upload);
                Expenses.checkReceipt(upload);
                var input = new Expenses.Input {
                    date = form("date"),
                    supplier = form("supplier"),
                    category = form("category"),
                    total = Money.parse(form("total")),
                    vat = form("vat").Trim().Length > 0 ? Money.parse(form("vat")) : (long?)null,
                    vatRate = int.TryParse(form("vatRate"), out int rate) ? rate : -1,
                    foreign = form("foreign"),
                    importBase = form("importBase").Trim().Length > 0 ? Money.parse(form("importBase")) : (long?)null,
                    note = form("note"),
                    representationKind = form("representationKind"),
                    participants = int.TryParse(form("participants").Trim(), out int people) ? people : 0,
                    attendees = form("attendees"),
                    purpose = form("purpose"),
                    alcohol = form("alcohol") == "1",
                };
                bool isNew = expense == null;
                bool fromBag = sorting;
                expense = Expenses.save(companyId, expense, input, account.id);
                Expenses.attachReceipt(expense, upload, account.id);
                if (fromBag) {
                    var next = Expenses.inbox(companyId).FirstOrDefault();
                    redirect(next != null ? BasePath + "/expense.html?id=" + u(next.id) + "&ok=sorted" : BasePath + "/pant.html?ok=sorted");
                } else {
                    redirect(isNew && form("action") == "another" ? BasePath + "/expense.html?ok=saved" : BasePath + "/expenses.html?ok=saved");
                }
                return;
            }
            setValues();
        }

        protected override void renderAfterError() {
            setValues();
        }

        string value(string key) {
            if (isPost) return form(key);
            if (expense == null) return "";
            if (sorting && Db.str(expense, "aiStatus") == ReceiptReader.Done) return suggestion(key);
            if (key == "vat" && Db.str(expense, "category") == Representation.CategoryKey) return Money.input(Db.num(expense, "receiptVat"));
            if (key == "total" || key == "vat") return Money.input(Db.num(expense, key));
            if (key == "importBase") return Db.num(expense, key) > 0 ? Money.input(Db.num(expense, key)) : "";
            if (key == "participants") return Db.num(expense, key) > 0 ? Db.num(expense, key).ToString() : "";
            if (key == "alcohol") return expense.get<bool>("alcohol") ? "1" : "";
            return Db.str(expense, key);
        }

        /// <summary>What the receipt reader found, as form values ("" where it found nothing usable).</summary>
        string suggestion(string key) {
            switch (key) {
                case "supplier": return Db.str(expense, "aiSupplier");
                case "date": return Db.str(expense, "aiDate");
                case "category": return Db.str(expense, "aiCategory");
                case "total": return expense.hasValue("aiTotal") ? Money.input(Db.num(expense, "aiTotal")) : "";
                case "vat": return expense.hasValue("aiVat") ? Money.input(Db.num(expense, "aiVat")) : "";
                case "vatRate": return expense.hasValue("aiVatRate") ? Db.num(expense, "aiVatRate").ToString() : "";
                case "foreign": return Db.str(expense, "aiForeign");
                case "alcohol": return expense.get<bool>("aiAlcohol") ? "1" : "";
                default: return "";
            }
        }

        void setValues() {
            string receiptId = Db.str(expense, "receiptFileId");
            var file = receiptId.Length > 0 ? Expenses.receipt(receiptId, companyId) : null;
            string type = file?.fileType ?? "";
            bool exists = expense != null;

            set("sorting", sorting);
            set("exists", exists);
            set("id", exists ? h(expense.id) : "");
            set("heading", T(exists ? "expense.title" : "expense.new"));
            set("receiptSrc", BasePath + "/receipt.html?id=" + u(expense?.id) + "&v=" + u(receiptId));
            rows("oldReceipts", exists ? Expenses.receiptHistory(expense).Select(r => row(("href", BasePath + "/receipt.html?id=" + u(expense.id) + "&amp;file=" + u(r.fileId)),
                ("when", T("expense.replacedAt", h(r.replacedAt))))) : Enumerable.Empty<SwecialObject>());
            set("preview", type == "pdf" ? "pdf" : type.Length > 0 && type != "heic" ? "image" : "");
            set("hasReceipt", receiptId.Length > 0);
            set("showReceipt", receiptId.Length > 0 && !sorting);
            set("receiptIsImage", type.Length > 0 && type != "pdf" && type != "heic");
            set("showChangeHint", exists && !sorting);
            set("cancelHref", BasePath + (sorting ? "/pant.html" : "/expenses.html"));
            if (sorting) {
                int left = Expenses.inbox(companyId).Count;
                set("sortLeft", Tn("expense.sortLeft", left));
                set("ai", Db.str(expense, "aiStatus"));
                set("isSale", Db.str(expense, "aiKind") == "sales");
                set("canReread", ReceiptReader.enabled && Db.str(expense, "aiStatus") != ReceiptReader.Reading);
                string currency = Db.str(expense, "aiCurrency");
                set("aiCurrency", currency.Length > 0 && currency != "SEK" && currency != "KR" ? T("ai.currency", h(currency)) : "");
                set("aiForeignKind", expense.get<bool>("aiForeignNoKind") ? T("ai.foreignKind") : "");
            }

            string date = value("date");
            if (date.Length == 0 && !isPost) date = Dates.today();
            val("date", date);
            val("supplier", value("supplier"));
            val("total", value("total"));
            val("vat", value("vat"));
            val("note", value("note"));
            string category = value("category");
            rows("categories", Categories.All.Select(c => {
                string hint = Tr("category." + c.key + ".hint", Money.whole(Rules.value(Rules.PriceBaseAmount, date.Length > 0 ? date : Dates.today()) / 2));
                // Food VAT depends on the date (6 % from April 2026)
                int suggested = c.key == Representation.CategoryKey ? Representation.foodRate(date) : c.vatRate;
                return row(("value", c.key), ("vat", suggested.ToString()), ("hint", h(hint)), ("abroad", c.consumedAbroad ? "1" : ""),
                    ("selected", selected(c.key == category)), ("text", T("category." + c.key + ".name")));
            }));
            string rate = value("vatRate");
            if (rate.Length == 0) rate = (Categories.find(category)?.vatRate ?? 25).ToString();
            set("vatRates", vatOptions(rate));
            // The representation rules by date, for the page script (same table as the server)
            val("rulesFood", Rules.json(Rules.FoodVat));
            val("rulesBase", Rules.json(Rules.RepresentationBase));
            val("rulesSchablon", Rules.json(Rules.AlcoholSchablon));
            val("participants", value("participants"));
            val("attendees", value("attendees"));
            val("purpose", value("purpose"));
            set("alcoholChecked", isChecked(value("alcohol") == "1"));
            set("representationKinds", options(new[] { (Representation.Internal, Tr("expense.representation.internal")), (Representation.External, Tr("expense.representation.external")) },
                value("representationKind") == Representation.External ? Representation.External : Representation.Internal));
            string foreign = value("foreign");
            rows("foreignOptions", new[] { "", Expenses.ForeignEu, Expenses.ForeignNonEu, Expenses.ForeignEuGoods, Expenses.Import, Expenses.Abroad }.Select(k =>
                row(("value", k), ("text", T("expense.foreign." + (k.Length == 0 ? "no" : k))), ("hint", k.Length == 0 ? "" : T("expense.foreignHint." + k)),
                    ("selected", selected(k == foreign)))));
            val("importBase", value("importBase"));
        }
    }
}
