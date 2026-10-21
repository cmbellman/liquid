using com.swecial.server;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;

namespace com.swecial.code {
    public class InvoiceRow {
        public string text;
        public decimal quantity;
        public long unitPrice;
        public int vatRate;

        [JsonIgnore]
        public long net => (long)Math.Round(quantity * unitPrice, 0, MidpointRounding.AwayFromZero);
    }

    public class InvoiceTotals {
        public long net;
        public long vat;
        public long total;
        /// <summary>Net and VAT per VAT rate, for the invoice footer and the voucher.</summary>
        public SortedDictionary<int, (long net, long vat)> byRate = new SortedDictionary<int, (long net, long vat)>();
    }

    /// <summary>
    /// Customer invoices: draft → sent (numbered) → paid. Only drafts can be edited or deleted.
    /// How an invoice is booked depends on the company's method when it is sent, and is remembered
    /// on the invoice (voucherId), so changing the method later never books an invoice twice:
    /// - cash method (kontantmetoden): nothing at sending; paid: 1930 D / 300x K / 26x1 K.
    /// - accrual method (faktureringsmetoden): sent: 1510 D / 300x K / 26x1 K (series F);
    ///   paid: 1930 D / 1510 K.
    /// </summary>
    public static class Invoicing {
        public const string Draft = "draft";
        public const string Sent = "sent";
        public const string Paid = "paid";

        public static List<InvoiceRow> rows(SwecialObject invoice) {
            string json = Db.str(invoice, "rowsJson");
            return json.Length == 0 ? new List<InvoiceRow>() : JsonConvert.DeserializeObject<List<InvoiceRow>>(json);
        }

        public static void setRows(SwecialObject invoice, List<InvoiceRow> rows) {
            invoice.set("rowsJson", JsonConvert.SerializeObject(rows));
            invoice.set("total", totals(rows).total);
        }

        /// <summary>VAT is computed per rate on the summed net amounts, as on the invoice footer.</summary>
        public static InvoiceTotals totals(List<InvoiceRow> rows) {
            var t = new InvoiceTotals();
            foreach (var group in rows.GroupBy(r => r.vatRate)) {
                long net = group.Sum(r => r.net);
                long vat = Money.percent(net, group.Key);
                t.byRate[group.Key] = (net, vat);
                t.net += net;
                t.vat += vat;
            }
            t.total = t.net + t.vat;
            return t;
        }

        public static SwecialObject createDraft(SwecialObject company) {
            var invoice = Db.create("invoice", company.id);
            invoice.set("status", Draft);
            invoice.set("invoiceDate", Dates.today());
            invoice.set("dueDate", Dates.format(DateTime.Today.AddDays(paymentTerms(company))));
            invoice.set("rowsJson", "[]");
            invoice.set("total", 0L);
            Db.commit(invoice);
            return invoice;
        }

        public static int paymentTerms(SwecialObject company) {
            long days = Db.num(company, "paymentTerms");
            return days > 0 ? (int)days : 30;
        }

        public static void requireDraft(SwecialObject invoice) {
            if (Db.str(invoice, "status") != Draft) throw new LiquidException("Fakturan är redan skickad och kan inte ändras.", "INVOICE ALREADY ISSUED. RECORD IS LOCKED.");
        }

        /// <summary>
        /// A draft from a sales invoice that was issued outside Liquid and put in Pantpåsen: the
        /// customer is found (organisation number, then name) or created, and the rows, dates,
        /// references and the original document are filled in from what the receipt reader found.
        /// The document leaves the bag (status converted). The draft keeps the document's number
        /// (issuedNumber) for registerIssued.
        /// </summary>
        public static SwecialObject draftFromDocument(SwecialObject company, SwecialObject document, string userId) {
            lock (Db.Sync) {
                if (Db.str(document, "status") != Expenses.Inbox) throw new LiquidException("Dokumentet finns inte i påsen.", "DOCUMENT NOT FOUND IN INTAKE HOPPER.");
                var sale = Db.str(document, "aiSaleJson").Length > 0 ? Newtonsoft.Json.Linq.JObject.Parse(Db.str(document, "aiSaleJson")) : new Newtonsoft.Json.Linq.JObject();
                string text(string key) => ((string)sale[key] ?? "").Trim();

                var invoice = createDraft(company);
                string customerName = text("customerName");
                if (customerName.Length > 0) {
                    string digits(string s) => new string((s ?? "").Where(char.IsDigit).ToArray());
                    string orgNumber = text("customerOrgNumber");
                    var customers = Db.forCompany("customer", company.id);
                    var customer = (digits(orgNumber).Length >= 10 ? customers.FirstOrDefault(c => digits(Db.str(c, "orgNumber")) == digits(orgNumber)) : null)
                        ?? customers.FirstOrDefault(c => string.Equals(Db.str(c, "name").Trim(), customerName, StringComparison.OrdinalIgnoreCase));
                    if (customer == null) {
                        customer = Db.create("customer", company.id);
                        customer.set("name", customerName);
                        customer.set("orgNumber", orgNumber);
                        customer.set("address", text("customerAddress"));
                        customer.set("postalAddress", text("customerPostalAddress"));
                        customer.set("reference", text("customerReference"));
                        Db.commit(customer);
                    }
                    invoice.set("customerId", customer.id);
                }
                if (text("invoiceDate").Length > 0) invoice.set("invoiceDate", text("invoiceDate"));
                if (text("dueDate").Length > 0) invoice.set("dueDate", text("dueDate"));
                invoice.set("ourReference", text("ourReference"));
                invoice.set("note", text("note"));
                var lines = (sale["lines"] as Newtonsoft.Json.Linq.JArray)?.Select(l => new InvoiceRow {
                    text = (string)l["text"], quantity = (decimal)l["quantity"], unitPrice = (long)l["unitPrice"], vatRate = (int)l["vatRate"],
                }).ToList() ?? new List<InvoiceRow>();
                setRows(invoice, lines);
                invoice.set("issuedNumber", text("invoiceNumber"));
                invoice.set("documentFileId", Db.str(document, "receiptFileId"));
                invoice.set("documentTotal", sale["total"] != null ? (long)sale["total"] : 0L);
                Db.commit(invoice);

                document.set("status", Expenses.Converted);
                document.set("invoiceId", invoice.id);
                Db.commit(document);
                return invoice;
            }
        }

        /// <summary>
        /// Registers an invoice that was already issued outside Liquid, with its own number: like
        /// send, but the number is the document's. It must be new for the company, and Liquid's own
        /// series move past it so they never give the same number later.
        /// </summary>
        public static void registerIssued(SwecialObject invoice, string numberText, string userId) {
            lock (Db.Sync) {
                if (!long.TryParse((numberText ?? "").Trim(), out long number) || number <= 0) {
                    throw new LiquidException("Fakturanumret ska vara ett heltal, som på fakturan.", "INVOICE NUMBER MUST BE AN INTEGER, AS ON THE DOCUMENT.");
                }
                string companyId = Db.str(invoice, "companyId");
                if (Db.forCompany("invoice", companyId).Any(i => i.id != invoice.id && Db.num(i, "number") == number)) {
                    throw new LiquidException("Fakturanummer " + number + " finns redan.", "INVOICE NUMBER " + number + " ALREADY EXISTS.");
                }
                send(invoice, userId, number);
                invoice.set("issuedOutside", true);
                Db.commit(invoice);
            }
        }

        /// <summary>Moves Liquid's own number series past a number used outside it.</summary>
        static void reserveNumber(string companyId, long number) {
            if (number <= Settings.MaxSerial) {
                raiseCounter(companyId, Settings.SerialCounter, number);
            } else {
                string text = number.ToString();
                if (text.Length == 10 && DateTime.TryParseExact(text.Substring(0, 8), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) {
                    raiseCounter(companyId, "invoice-" + text.Substring(0, 8), long.Parse(text.Substring(8)));
                }
            }
        }

        static void raiseCounter(string companyId, string key, long atLeast) {
            var counter = Db.forCompany("counter", companyId).FirstOrDefault(c => Db.str(c, "key") == key);
            if (counter == null) {
                counter = Db.create("counter", companyId);
                counter.set("key", key);
            }
            if (Db.num(counter, "value") >= atLeast) return;
            counter.set("value", atLeast);
            Db.commit(counter);
        }

        /// <summary>
        /// Gives the invoice its number. Nothing is booked yet: with the cash method the sale is booked
        /// when the money comes in (markPaid). Invoices still unpaid at year end are booked in the closing.
        /// </summary>
        public static void send(SwecialObject invoice, string userId, long? issuedNumber = null) {
            lock (Db.Sync) {
                requireDraft(invoice);
                string companyId = Db.str(invoice, "companyId");
                var customer = Db.getOwned("customer", Db.str(invoice, "customerId"), companyId);
                if (customer == null) throw new LiquidException("Välj en kund innan fakturan skickas.", "ASSIGN A CLIENT BEFORE ISSUING.");
                var rows = Invoicing.rows(invoice);
                if (rows.Count == 0) throw new LiquidException("Fakturan har inga rader.", "INVOICE HAS NO LINES.");
                if (rows.Any(r => (r.text ?? "").Trim().Length == 0)) throw new LiquidException("Varje rad behöver en beskrivning.", "EVERY LINE REQUIRES A DESCRIPTION.");
                var t = totals(rows);
                if (t.total <= 0) throw new LiquidException("Fakturans belopp måste vara större än noll (kreditfakturor kommer senare).", "INVOICE TOTAL MUST BE ABOVE ZERO.");
                Dates.parse(Db.str(invoice, "invoiceDate"));
                Dates.parse(Db.str(invoice, "dueDate"));
                var company = Db.get("company", companyId);
                bool accrual = Settings.accountingMethod(company) == Settings.AccrualMethod;
                string date = Dates.clean(Db.str(invoice, "invoiceDate"));
                // The accounts it will be booked on must exist before the invoice gets a number
                Ledger.validate(companyId, date, "Kundfaktura", saleRows(Bas.Bank, t));
                if (accrual) Ledger.validate(companyId, date, "Kundfaktura", saleRows(Bas.Receivables, t));

                long number;
                if (issuedNumber.HasValue) {
                    number = issuedNumber.Value;
                    reserveNumber(companyId, number);
                } else {
                    number = nextNumber(company, date);
                }
                invoice.set("number", number);
                if (accrual) {
                    var voucher = Ledger.book(companyId, Ledger.Invoices, date,
                        "Kundfaktura " + number + ", " + Db.str(customer, "name"), saleRows(Bas.Receivables, t), userId, "invoice", invoice.id);
                    invoice.set("voucherId", voucher.id);
                }
                invoice.set("status", Sent);
                invoice.set("sentBy", userId);
                invoice.set("customerName", Db.str(customer, "name"));
                invoice.set("total", t.total);
                Db.commit(invoice);
            }
        }

        /// <summary>
        /// The next invoice number, unique and in sequence as the VAT rules require: a plain series
        /// (1, 2, 3 …) or, by the company's choice, the invoice date and a two-digit number for the
        /// day (the first invoice dated 2026-10-21 is 2026102101, the second 2026102102). Serial
        /// numbers stay below the date numbers, so switching between them never repeats a number.
        /// </summary>
        public static long nextNumber(SwecialObject company, string invoiceDate) {
            if (Settings.invoiceNumbering(company) == Settings.SerialNumbers) {
                long next = Db.nextNumber(company.id, Settings.SerialCounter);
                if (next > Settings.MaxSerial) throw new LiquidException("Fakturanumren är slut i löpnummerserien. Byt till datumnummer under Inställningar.", "SERIAL NUMBER RANGE EXHAUSTED. SWITCH TO DATE NUMBERING IN SYSTEM CONFIG.");
                return next;
            }
            string day = Dates.parse(invoiceDate).ToString("yyyyMMdd");
            long serial = Db.nextNumber(company.id, "invoice-" + day);
            if (serial > 99) throw new LiquidException("Högst 99 fakturor per fakturadag.", "MAXIMUM 99 INVOICES PER INVOICE DATE.");
            return long.Parse(day) * 100 + serial;
        }

        /// <summary>
        /// Books the full payment: with the cash method the sale and its VAT against 1930; for an
        /// invoice already booked as a receivable, when sent (accrual method) or in the closing of the year
        /// it was unpaid at (closingVoucherId, set by the closing), 1930 against the receivable. A cash
        /// method invoice from an earlier year that no closing booked is booked as a sale when paid.
        /// </summary>
        public static void markPaid(SwecialObject invoice, string paidDate, string userId) {
            lock (Db.Sync) {
                if (Db.str(invoice, "status") != Sent) throw new LiquidException("Bara skickade, obetalda fakturor kan markeras som betalda.", "ONLY ISSUED, OUTSTANDING INVOICES CAN BE SETTLED.");
                paidDate = Dates.clean(paidDate);
                var t = totals(rows(invoice));
                bool bookedWhenSent = Db.str(invoice, "voucherId").Length > 0 || Db.str(invoice, "closingVoucherId").Length > 0;
                var paymentRows = bookedWhenSent
                    ? new List<VoucherRow> {
                        new VoucherRow { account = Bas.Bank, debit = t.total },
                        new VoucherRow { account = Bas.Receivables, credit = t.total },
                    }
                    : saleRows(Bas.Bank, t);
                var voucher = Ledger.book(Db.str(invoice, "companyId"), Ledger.Payments, paidDate,
                    "Inbetalning kundfaktura " + Db.num(invoice, "number") + ", " + Db.str(invoice, "customerName"),
                    paymentRows, userId, "invoice", invoice.id);
                invoice.set("status", Paid);
                invoice.set("paidDate", paidDate);
                invoice.set("paymentVoucherId", voucher.id);
                Db.commit(invoice);
            }
        }

        /// <summary>
        /// Undoes a registered payment, e.g. one booked on the wrong date: the payment voucher is
        /// reversed (dated like the original) and the invoice is unpaid again, ready to be paid anew.
        /// </summary>
        public static void unmarkPaid(SwecialObject invoice, string userId) {
            lock (Db.Sync) {
                if (Db.str(invoice, "status") != Paid) throw new LiquidException("Fakturan är inte betald.", "INVOICE IS NOT SETTLED.");
                var voucher = Db.getOwned("voucher", Db.str(invoice, "paymentVoucherId"), Db.str(invoice, "companyId"));
                if (voucher != null) Ledger.reverse(voucher, Db.str(voucher, "date"), userId, fromSource: true);
                invoice.set("status", Sent);
                invoice.set("paidDate", "");
                invoice.set("paymentVoucherId", "");
                Db.commit(invoice);
            }
        }

        /// <summary>True when an invoice dated invoiceDate and paid on paidDate was unpaid at a year end.</summary>
        public static bool overYearEnd(string invoiceDate, string paidDate) {
            return string.CompareOrdinal(Dates.clean(invoiceDate).Substring(0, 4), paidDate.Substring(0, 4)) < 0;
        }

        /// <summary>The sale: the total on debitAccount (1930 when paid, 1510 when sent), sales and VAT per rate.</summary>
        static List<VoucherRow> saleRows(string debitAccount, InvoiceTotals t) {
            var rows = new List<VoucherRow> { new VoucherRow { account = debitAccount, debit = t.total } };
            foreach (var rate in t.byRate) {
                if (rate.Value.net != 0) rows.Add(credit(Bas.salesAccount(rate.Key), rate.Value.net));
                if (rate.Value.vat != 0) rows.Add(credit(Bas.outputVatAccount(rate.Key), rate.Value.vat));
            }
            return rows;
        }

        static VoucherRow credit(string account, long amount) {
            return amount >= 0
                ? new VoucherRow { account = account, credit = amount }
                : new VoucherRow { account = account, debit = -amount };
        }
    }
}
