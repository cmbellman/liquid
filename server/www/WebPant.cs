using com.swecial.code;
using com.swecial.server;
using System.Linq;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/pant.html : Pantpåsen. Throw receipts in (drop, pick or take a photo, several at once)
    /// without filling in anything; they wait here until they're sorted on the expense page, where
    /// they are booked. POST action=bag adds receipts, action=remove throws one away. Refreshes
    /// itself while receipts are being read by the receipt reader.
    /// </summary>
    public class WebPant : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            title(Tr("pant.title"));
            if (isPost) {
                if (form("action") == "remove") {
                    var item = Db.getOwned("expense", form("id"), companyId);
                    if (item == null || Db.str(item, "status") != Expenses.Inbox) throw new LiquidException("Kvittot finns inte i påsen.", "DOCUMENT NOT FOUND IN INTAKE HOPPER.");
                    Expenses.remove(item, account.id);
                    redirect(BasePath + "/pant.html?ok=deleted");
                    return;
                }
                bag(req, companyId, account.id);
                redirect(BasePath + "/pant.html?ok=bagged");
                return;
            }
            setValues();
        }

        protected override void renderAfterError() {
            setValues();
        }

        /// <summary>Puts every uploaded receipt in the bag. All are checked first, so it's all or nothing.</summary>
        public static int bag(HttpRequest req, string companyId, string userId) {
            var files = req.body.fileList.Where(f => f.data != null && f.data.Length > 0).ToList();
            if (files.Count == 0) throw new LiquidException("Välj eller dra in minst ett kvitto.", "SUBMIT AT LEAST ONE DOCUMENT.");
            foreach (var file in files) {
                try {
                    Expenses.checkReceipt(file);
                } catch (LiquidException ex) {
                    throw new LiquidException(file.fileName + ": " + ex.Message, file.fileName + ": " + ex.text(true));
                }
            }
            foreach (var file in files) Expenses.addToInbox(companyId, file, userId);
            return files.Count;
        }

        void setValues() {
            var items = Expenses.inbox(companyId);
            set("bagAction", BasePath + "/pant.html");
            set("toSort", Tn("pant.toSort", items.Count));
            set("firstId", items.Count > 0 ? u(items[0].id) : "");
            set("reading", items.Any(i => Db.str(i, "aiStatus") == ReceiptReader.Reading));
            rows("items", items.Select(item => {
                string fileId = Db.str(item, "receiptFileId");
                var file = Expenses.receipt(fileId, companyId);
                string type = file?.fileType ?? "";
                string amount = item.hasValue("aiTotal") ? Money.format(Db.num(item, "aiTotal")) + " " + Tr("common.currency") : "";
                string found = string.Join(" · ", new[] { Db.str(item, "aiSupplier"), amount, Db.str(item, "aiDate") }.Where(s => s.Length > 0));
                return row(("id", u(item.id)), ("src", BasePath + "/receipt.html?id=" + u(item.id) + "&v=" + u(fileId)),
                    ("isImage", type != "pdf" && type != "heic"), ("type", type.ToUpperInvariant()),
                    ("ai", Db.str(item, "aiStatus")), ("found", h(found)),
                    ("fileName", h(Db.str(file, "originalName"))), ("added", T("pant.added", item.createdAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))));
            }));
        }
    }
}
