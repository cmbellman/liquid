using com.swecial.code;
using com.swecial.server;
using System;
using System.Collections.Generic;
using System.Linq;

namespace com.swecial.web {
    /// <summary>
    /// /liquid/archive.html?year= : the year's räkenskapsinformation to read, print or save as PDF from
    /// the browser: every voucher with its rows, who booked it and when, corrections both ways, and its
    /// underlag (receipts inline, earlier replaced receipts, invoices). Always Swedish and light, like the
    /// printable invoice: it is a document, not a themed page. With the SIE file it is what the
    /// company keeps for seven years.
    /// </summary>
    public class WebArchive : LiquidPage {
        public override void processPage() {
            if (!requireCompany()) return;
            var years = Closing.years(companyId);
            int year = int.TryParse(query("year"), out int y) && years.Contains(y) ? y : Closing.defaultYear(companyId);
            title("Räkenskapsinformation " + year);
            set("year", year.ToString());
            val("companyName", Db.str(company, "name"));
            val("orgNumber", Db.str(company, "orgNumber"));
            set("generated", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            set("yearOptions", options(Enumerable.Reverse(years).Select(x => (x.ToString(), x.ToString())), year.ToString()));
            set("sieHref", BasePath + "/sie.html?download=" + year);
            var closing = Closing.find(companyId, year);
            set("closed", Closing.isClosed(companyId, year));
            set("closedText", "Bokslutet stängdes " + h(Db.str(closing, "closedAt")) + ". Skatt " + Money.format(Db.num(closing, "tax")) + " kr.");

            var names = Db.forCompany("account", companyId).ToDictionary(a => Db.str(a, "number"), a => Db.str(a, "name"));
            string who(SwecialUser user) => user == null ? "" : (user.firstName ?? "").Length > 0 ? user.firstName : user.get<string>("_username") ?? "";
            var vouchers = Ledger.vouchers(companyId).Where(v => Db.str(v, "date").StartsWith(year + "-")).ToList();
            set("count", vouchers.Count.ToString());
            rows("vouchers", vouchers.Select(v => {
                var lines = Ledger.rows(v);
                var receipts = underlag(v);
                string by = who(Db.linkedUser(v, "createdBy"));
                return row(("label", h(Ledger.label(v))), ("date", h(Db.str(v, "date"))), ("text", h(Db.str(v, "text"))),
                    ("booked", h(v.createdAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm") + (by.Length > 0 ? ", " + by : ""))),
                    ("correction", h(correction(v))),
                    ("lines", lines.Select(l => row(("account", h(l.account)), ("name", h(names.TryGetValue(l.account, out var n) ? n : "")),
                        ("debit", Money.formatOrBlank(l.debit)), ("credit", Money.formatOrBlank(l.credit)))).ToList()),
                    ("sumDebit", Money.format(lines.Sum(l => l.debit))), ("sumCredit", Money.format(lines.Sum(l => l.credit))),
                    ("source", h(sourceText(v))),
                    ("images", receipts.Where(r => r.image).Select(r => row(("src", r.src), ("caption", h(r.caption)))).ToList()),
                    ("files", receipts.Where(r => !r.image).Select(r => row(("href", r.src), ("caption", h(r.caption)))).ToList()));
            }));
        }

        string correction(SwecialObject v) {
            var reverses = Db.linked(v, "reverses", "voucher");
            var reversedBy = Db.linked(v, "reversedBy", "voucher");
            if (reverses != null) return "Rättelse av " + Ledger.label(reverses) + ".";
            if (reversedBy != null) return "Makulerad genom " + Ledger.label(reversedBy) + ".";
            return "";
        }

        string sourceText(SwecialObject v) {
            switch (Db.str(v, "sourceType")) {
                case "invoice":
                    var invoice = Db.linked(v, "source", "invoice");
                    return invoice == null ? "" : "Kundfaktura " + Db.num(invoice, "number") + ", " + Db.str(invoice, "customerName") + ", fakturadatum " + Db.str(invoice, "invoiceDate") + ", " + Money.format(Db.num(invoice, "total")) + " kr";
                case "expense":
                    var expense = Db.linked(v, "source", "expense");
                    return expense == null ? "" : "Utgift: " + Db.str(expense, "supplier") + (Db.str(expense, "note").Length > 0 ? ", " + Db.str(expense, "note") : "")
                        + (Db.str(expense, "attendees").Length > 0 ? ". Deltagare: " + Db.str(expense, "attendees") + ". Syfte: " + Db.str(expense, "purpose") : "");
                case "bank_event": return "Bankhändelse";
                case "security_event": return "Fondhändelse";
                case "closing": return "Bokslut";
                case "sie_import": return "Importerad från SIE-fil";
                default: return "";
            }
        }

        class Underlag { public string src, caption; public bool image; }

        /// <summary>The voucher's underlag: the expense's receipt (and earlier ones it replaced), the invoice and its original document.</summary>
        List<Underlag> underlag(SwecialObject v) {
            var list = new List<Underlag>();
            void add(string fileId, string src, string caption) {
                var file = Expenses.receipt(fileId, companyId);
                if (file == null) return;
                string type = file.fileType ?? "";
                list.Add(new Underlag { src = src, caption = caption, image = type.Length > 0 && type != "pdf" && type != "heic" });
            }
            switch (Db.str(v, "sourceType")) {
                case "expense":
                    var expense = Db.linked(v, "source", "expense");
                    if (expense == null) break;
                    string current = Db.linkedId(expense, "receiptFile");
                    if (current.Length > 0) add(current, BasePath + "/receipt.html?id=" + u(expense.id) + "&amp;v=" + u(current), "Kvitto");
                    foreach (var old in Expenses.receiptHistory(expense)) {
                        add(old.fileId, BasePath + "/receipt.html?id=" + u(expense.id) + "&amp;file=" + u(old.fileId), "Tidigare kvitto, ersatt " + old.replacedAt);
                    }
                    break;
                case "invoice":
                    var invoice = Db.linked(v, "source", "invoice");
                    if (invoice == null) break;
                    list.Add(new Underlag { src = BasePath + "/invoice-print.html?id=" + u(invoice.id), caption = "Fakturan" });
                    string doc = Db.linkedId(invoice, "documentFile");
                    if (doc.Length > 0) add(doc, BasePath + "/receipt.html?invoice=" + u(invoice.id), "Originalfakturan");
                    break;
            }
            return list;
        }
    }
}
