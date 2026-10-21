using com.swecial.code;
using com.swecial.server;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/voucher.html : without ?id= a form for a new manual voucher (series A); with ?id= the
    /// booked voucher, read-only, with a button that reverses it.
    /// </summary>
    public class WebVoucher : LiquidPage {
        const int FormRows = 8;

        SwecialObject voucher;

        public override void processPage() {
            if (!requireCompany()) return;
            string id = isPost ? form("id") : query("id");
            if (id.Length > 0) {
                voucher = Db.getOwned("voucher", id, companyId);
                if (voucher == null) {
                    redirect(BasePath + "/vouchers.html");
                    return;
                }
            }

            if (voucher != null) {
                title(Tr("voucher.title", Ledger.label(voucher)));
                if (isPost && form("action") == "reverse") {
                    var reversal = Ledger.reverse(voucher, form("date"), account.id);
                    redirect(BasePath + "/voucher.html?id=" + u(reversal.id) + "&ok=reversed");
                    return;
                }
                setValues();
                return;
            }

            title(Tr("voucher.new"));
            if (isPost) {
                var rows = new List<VoucherRow>();
                for (int i = 0; i < FormRows; i++) {
                    rows.Add(new VoucherRow { account = form("account" + i), debit = Money.parse(form("debit" + i)), credit = Money.parse(form("credit" + i)) });
                }
                var booked = Ledger.book(companyId, Ledger.Manual, form("date"), form("text"), rows, account.id);
                redirect(BasePath + "/voucher.html?id=" + u(booked.id) + "&ok=booked");
                return;
            }
            setValues();
        }

        protected override void renderAfterError() {
            setValues();
        }

        void setValues() {
            set("viewing", voucher != null);
            if (voucher == null) {
                val("date", isPost ? form("date") : Dates.today());
                val("text", form("text"));
                var accounts = accountChoices().ToList();
                rows("formLines", Enumerable.Range(0, FormRows).Select(i => row(("i", i.ToString()),
                    ("accounts", options(accounts, form("account" + i))), ("debit", h(form("debit" + i))), ("credit", h(form("credit" + i))))));
                return;
            }
            set("heading", T("voucher.title", h(Ledger.label(voucher))));
            set("id", h(voucher.id));
            val("date", Db.str(voucher, "date"));
            val("text", Db.str(voucher, "text"));
            set("source", sourceLink());
            set("hasSource", ((string)get("source")).Length > 0);
            set("reverses", linked("reversesId"));
            set("hasReverses", ((string)get("reverses")).Length > 0);
            set("reversedBy", linked("reversedById"));
            set("hasReversedBy", ((string)get("reversedBy")).Length > 0);
            var lines = Ledger.rows(voucher);
            rows("lines", lines.Select(l => row(("account", h(l.account)), ("name", h(accountName(l.account))),
                ("debit", Money.formatOrBlank(l.debit)), ("credit", Money.formatOrBlank(l.credit)))));
            set("sumDebit", Money.formatOrBlank(lines.Sum(l => l.debit)));
            set("sumCredit", Money.formatOrBlank(lines.Sum(l => l.credit)));
            set("canReverse", Db.str(voucher, "sourceType").Length == 0 && Db.str(voucher, "reversedById").Length == 0 && Db.str(voucher, "reversesId").Length == 0);
            val("reverseDate", isPost ? form("date") : Dates.today());
        }

        /// <summary>The invoice, expense or bank event the voucher was booked from, as a link.</summary>
        string sourceLink() {
            string sourceId = Db.str(voucher, "sourceId");
            switch (Db.str(voucher, "sourceType")) {
                case "invoice":
                    var invoice = Db.getOwned("invoice", sourceId, companyId);
                    return invoice == null ? "" : "<a href='" + BasePath + "/invoice.html?id=" + u(invoice.id) + "'>" + T("invoice.title", Db.num(invoice, "number")) + "</a>";
                case "expense":
                    var expense = Db.getOwned("expense", sourceId, companyId);
                    if (expense == null) return "";
                    string link = "<a href='" + BasePath + "/expense.html?id=" + u(expense.id) + "'>" + T("expense.title") + "</a>";
                    string receipt = Db.str(expense, "receiptFileId");
                    return receipt.Length == 0 ? link : link + " (<a href='" + BasePath + "/receipt.html?id=" + u(expense.id) + "&amp;v=" + u(receipt) + "' target='_blank'>" + T("expense.receipt") + "</a>)";
                case "bank_event":
                    return "<a href='" + BasePath + "/bank.html'>" + T("voucher.bankEvent") + "</a>";
                default:
                    return "";
            }
        }

        string linked(string key) {
            var other = Db.getOwned("voucher", Db.str(voucher, key), companyId);
            return other == null ? "" : "<a href='" + BasePath + "/voucher.html?id=" + u(other.id) + "'>" + h(Ledger.label(other)) + "</a>";
        }
    }
}
