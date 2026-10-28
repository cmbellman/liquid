using com.swecial.code;
using com.swecial.server;
using System.Linq;
using System.Text;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/receipt.html?id=&lt;expense id&gt; : the expense's receipt file. The server doesn't serve
    /// images or PDFs from www/, so the page returns the bytes itself, only to the company's users.
    /// &amp;file= gives one of the expense's earlier receipts, kept when it was replaced.
    /// </summary>
    public class WebReceipt : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            // An expense's receipt (?id=), or the original document of an invoice issued outside Liquid (?invoice=)
            var expense = Db.getOwned("expense", query("id"), companyId);
            var invoice = Db.getOwned("invoice", query("invoice"), companyId);
            string fileId = expense != null ? Db.linkedId(expense, "receiptFile") : Db.linkedId(invoice, "documentFile");
            // A receipt the expense had before (?file=), kept when it was replaced
            if (expense != null && query("file").Length > 0) {
                fileId = Expenses.receiptHistory(expense).Any(r => r.fileId == query("file")) ? query("file") : "";
            }
            var file = fileId.Length > 0 ? Expenses.receipt(fileId, companyId) : null;
            byte[] data = file?.data;
            if (data == null) {
                resp.statusCode = 404;
                resp.header.add("Content-Type: text/plain; charset=utf-8");
                resp.binaryData = Encoding.UTF8.GetBytes("Not found");
                return;
            }
            string type = Db.str(file, "contentType");
            resp.header.add("Content-Type: " + (type.Length > 0 ? type : "application/octet-stream"));
            resp.header.add("Content-Disposition: inline; filename=\"" + (expense != null ? "kvitto-" + Db.str(expense, "date") : "faktura-" + Db.num(invoice, "number")) + "." + file.fileType + "\"");
            resp.header.add("Cache-Control: private, max-age=86400");
            resp.header.add("X-Content-Type-Options: nosniff");
            resp.binaryData = data;
        }
    }
}
