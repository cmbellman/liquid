using com.swecial.code;
using com.swecial.server;
using System.Linq;

namespace com.swecial.web {
    /// <summary>/liquid/expenses.html : expenses, newest first, with this year's sum and missing receipts.</summary>
    public class WebExpenses : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            title(Tr("expenses.title"));
            var expenses = Expenses.list(companyId);
            string year = Dates.today().Substring(0, 4);
            var thisYear = expenses.Where(e => Db.str(e, "date").StartsWith(year)).ToList();
            int missing = expenses.Count(e => Db.linkedId(e, "receiptFile").Length == 0);

            set("summary", T("expenses.summary", Money.format(thisYear.Sum(e => Db.num(e, "total"))),
                Money.format(thisYear.Where(e => Db.str(e, "foreign").Length == 0).Sum(e => Db.num(e, "vat")))));
            set("missingAny", missing > 0);
            set("missing", T("expenses.missing", missing));
            rows("expenses", expenses.Select(e => {
                string category = Db.str(e, "category");
                string receiptId = Db.linkedId(e, "receiptFile");
                return row(("id", u(e.id)), ("date", h(Db.str(e, "date"))), ("supplier", h(Db.str(e, "supplier"))),
                    ("category", category.Length > 0 ? T("category." + category + ".name") : ""), ("foreign", Db.str(e, "foreign").Length > 0),
                    ("total", Money.formatOrBlank(Db.num(e, "total"))), ("vat", Money.formatOrBlank(Db.num(e, "vat"))),
                    ("hasReceipt", receiptId.Length > 0), ("receipt", BasePath + "/receipt.html?id=" + u(e.id) + "&v=" + u(receiptId)));
            }));
        }
    }
}
