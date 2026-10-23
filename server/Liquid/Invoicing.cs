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
    /// The customer decides the VAT (vatTreatment, fixed when sent): a business in another EU country
    /// with a VAT number gets no VAT (reverse charge; 3108 goods / 3308 services, VAT return boxes 35/39
    /// and the EU sales list), a customer outside the EU gets no VAT (export; 3105 / 3305, boxes 36/40),
    /// everyone else Swedish VAT per row. These are reported by invoice date, whatever the method.
    /// </summary>
    public static class Invoicing {
        public const string Draft = "draft";
        public const string Sent = "sent";
        public const string Paid = "paid";

        public const string Domestic = "domestic";
        public const string EuReverseCharge = "eu";
        public const string Export = "export";
        public const string Goods = "goods";
        public const string Services = "services";

        /// <summary>Where the customer is: Sweden (default), another EU country or outside the EU.</summary>
        public const string RegionSweden = "se";
        public const string RegionEu = "eu";
        public const string RegionOutsideEu = "noneu";

        /// <summary>How an invoice to the customer is taxed: reverse charge for an EU business, export outside the EU.</summary>
        public static string vatTreatment(SwecialObject customer) {
            string region = Db.str(customer, "region");
            if (region == RegionEu && Db.str(customer, "vatNumber").Trim().Length > 0) return EuReverseCharge;
            if (region == RegionOutsideEu) return Export;
            return Domestic;
        }

        /// <summary>The invoice's treatment: fixed when sent, otherwise from the chosen customer.</summary>
        public static string vatTreatmentOf(SwecialObject invoice) {
            string fixedTreatment = Db.str(invoice, "vatTreatment");
            if (Db.str(invoice, "status") != Draft && fixedTreatment.Length > 0) return fixedTreatment;
            var customer = Db.getOwned("customer", Db.str(invoice, "customerId"), Db.str(invoice, "companyId"));
            return customer == null ? Domestic : vatTreatment(customer);
        }

        public static bool isGoods(SwecialObject invoice) {
            return Db.str(invoice, "supplyType") == Goods;
        }

        /// <summary>Net sales without Swedish VAT by invoice date, for VAT return boxes 35, 36, 39 and 40.</summary>
        public static (long euGoods, long exportGoods, long euServices, long exportServices) foreignSales(string companyId, string from, string to) {
            long euGoods = 0, exportGoods = 0, euServices = 0, exportServices = 0;
            foreach (var invoice in issuedIn(companyId, from, to)) {
                string treatment = Db.str(invoice, "vatTreatment");
                long net = totals(rows(invoice)).net;
                bool goods = isGoods(invoice);
                if (treatment == EuReverseCharge) { if (goods) euGoods += net; else euServices += net; }
                else if (treatment == Export) { if (goods) exportGoods += net; else exportServices += net; }
            }
            return (euGoods, exportGoods, euServices, exportServices);
        }

        /// <summary>The EU sales list (periodisk sammanställning): per customer VAT number, goods and services.</summary>
        public static List<(string vatNumber, string customer, long goods, long services)> euSalesList(string companyId, string from, string to) {
            return issuedIn(companyId, from, to)
                .Where(i => Db.str(i, "vatTreatment") == EuReverseCharge)
                .GroupBy(i => Db.str(i, "customerVatNumber").Replace(" ", "").ToUpperInvariant())
                .Select(g => (g.Key, Db.str(g.First(), "customerName"),
                    g.Where(isGoods).Sum(i => totals(rows(i)).net), g.Where(i => !isGoods(i)).Sum(i => totals(rows(i)).net)))
                .OrderBy(r => r.Item1)
                .ToList();
        }

        static IEnumerable<SwecialObject> issuedIn(string companyId, string from, string to) {
            return Db.forCompany("invoice", companyId).Where(i => Db.str(i, "status") != Draft
                && string.CompareOrdinal(Db.str(i, "invoiceDate"), from) >= 0 && string.CompareOrdinal(Db.str(i, "invoiceDate"), to) <= 0);
        }

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
                // The customer decides the VAT; it is fixed on the invoice from now on
                string treatment = vatTreatment(customer);
                if (treatment != Domestic && rows.Any(r => r.vatRate != 0)) throw new LiquidException("Fakturor till kunder utomlands har ingen svensk moms. Spara utkastet igen så sätts momsen till 0 %.", "NO SWEDISH VAT ON INVOICES TO CLIENTS ABROAD. SAVE THE DRAFT AGAIN TO SET VAT TO 0 %.");
                if (treatment == EuReverseCharge && Db.str(company, "vatNumber").Trim().Length == 0) throw new LiquidException("Fyll i företagets momsregistreringsnummer under Inställningar. Det måste stå på fakturor till EU-kunder.", "ENTER THE COMPANY VAT NUMBER IN SYSTEM CONFIG. REQUIRED ON INVOICES TO EU CLIENTS.");
                invoice.set("vatTreatment", treatment);
                invoice.set("supplyType", isGoods(invoice) ? Goods : Services);
                invoice.set("customerVatNumber", Db.str(customer, "vatNumber").Trim());
                string date = Dates.clean(Db.str(invoice, "invoiceDate"));
                bool accrual = Settings.accountingMethod(company, date) == Settings.AccrualMethod;
                // The accounts it will be booked on must exist before the invoice gets a number
                Ledger.validate(companyId, date, "Kundfaktura", saleRows(Bas.Bank, t, invoice));
                if (accrual) Ledger.validate(companyId, date, "Kundfaktura", saleRows(Bas.Receivables, t, invoice));

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
                        "Kundfaktura " + number + ", " + Db.str(customer, "name"), saleRows(Bas.Receivables, t, invoice), userId, "invoice", invoice.id);
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
        /// invoice already booked as a receivable (when sent with the accrual method, or in a closing:
        /// closingVoucherId), 1930 against the receivable.
        /// An invoice paid in a later year than its invoice date was unpaid at year end, so its sale
        /// belongs to that year: it is booked as a receivable on the year's last day (closingVoucherId,
        /// closingByPayment) and the payment clears it. Only when the books have no income for that year
        /// (earlier years kept elsewhere, invoices booked when paid) may the user take it up as income
        /// in the year of payment instead (incomeWhenPaid).
        /// </summary>
        public static void markPaid(SwecialObject invoice, string paidDate, string userId, bool incomeWhenPaid = false) {
            lock (Db.Sync) {
                if (Db.str(invoice, "status") != Sent) throw new LiquidException("Bara skickade, obetalda fakturor kan markeras som betalda.", "ONLY ISSUED, OUTSTANDING INVOICES CAN BE SETTLED.");
                paidDate = Dates.clean(paidDate);
                string companyId = Db.str(invoice, "companyId");
                string invoiceDate = Db.str(invoice, "invoiceDate");
                if (string.CompareOrdinal(paidDate, invoiceDate) < 0) throw new LiquidException("Betalningen kan inte vara före fakturadatumet " + invoiceDate + ".", "PAYMENT CANNOT PREDATE THE ISSUE DATE " + invoiceDate + ".");
                var t = totals(rows(invoice));
                long number = Db.num(invoice, "number");
                string customerName = Db.str(invoice, "customerName");
                bool receivable = bookedAsReceivable(invoice);
                if (!receivable && overYearEnd(invoiceDate, paidDate) && !(incomeWhenPaid && mayTakeUpWhenPaid(invoice))) {
                    int year = Dates.parse(invoiceDate).Year;
                    var closing = Ledger.book(companyId, Ledger.Invoices, year + "-12-31",
                        "Obetald kundfaktura " + number + " vid årsskiftet, " + customerName, saleRows(Bas.Receivables, t, invoice), userId, "invoice", invoice.id);
                    invoice.set("closingVoucherId", closing.id);
                    invoice.set("closingByPayment", "true");
                    receivable = true;
                }
                var paymentRows = receivable
                    ? new List<VoucherRow> {
                        new VoucherRow { account = Bas.Bank, debit = t.total },
                        new VoucherRow { account = Bas.Receivables, credit = t.total },
                    }
                    : saleRows(Bas.Bank, t, invoice);
                var voucher = Ledger.book(companyId, Ledger.Payments, paidDate,
                    "Inbetalning kundfaktura " + number + ", " + customerName, paymentRows, userId, "invoice", invoice.id);
                invoice.set("status", Paid);
                invoice.set("paidDate", paidDate);
                invoice.set("paymentVoucherId", voucher.id);
                Db.commit(invoice);
            }
        }

        /// <summary>
        /// The closing (cash method): an invoice unpaid at year end is booked as a receivable on the
        /// year's last day (closingVoucherId), and its payment clears it.
        /// </summary>
        public static void bookAtYearEnd(SwecialObject invoice, string date, string userId) {
            lock (Db.Sync) {
                if (Db.str(invoice, "status") != Sent || bookedAsReceivable(invoice)) return;
                var voucher = Ledger.book(Db.str(invoice, "companyId"), Ledger.Invoices, date,
                    "Obetald kundfaktura " + Db.num(invoice, "number") + " vid årsskiftet, " + Db.str(invoice, "customerName"),
                    saleRows(Bas.Receivables, totals(rows(invoice)), invoice), userId, "invoice", invoice.id);
                invoice.set("closingVoucherId", voucher.id);
                Db.commit(invoice);
            }
        }

        /// <summary>Booked as a receivable before payment: when sent (accrual method) or in a closing.</summary>
        public static bool bookedAsReceivable(SwecialObject invoice) {
            return Db.str(invoice, "voucherId").Length > 0 || Db.str(invoice, "closingVoucherId").Length > 0;
        }

        /// <summary>True when an invoice dated invoiceDate and paid on paidDate was unpaid at a year end.</summary>
        public static bool overYearEnd(string invoiceDate, string paidDate) {
            return string.CompareOrdinal(Dates.clean(invoiceDate).Substring(0, 4), Dates.clean(paidDate).Substring(0, 4)) < 0;
        }

        /// <summary>
        /// An invoice from an earlier year may be taken up as income when paid only if the books have
        /// no income in its year: then that year was kept elsewhere and can't get the receivable.
        /// </summary>
        public static bool mayTakeUpWhenPaid(SwecialObject invoice) {
            if (bookedAsReceivable(invoice)) return false;
            string year = Dates.clean(Db.str(invoice, "invoiceDate")).Substring(0, 4);
            return !Ledger.vouchers(Db.str(invoice, "companyId")).Any(v =>
                Db.str(v, "date").StartsWith(year) && Db.str(v, "reversedById").Length == 0 && Db.str(v, "reversesId").Length == 0
                && Ledger.rows(v).Any(r => r.account.StartsWith("3") && r.credit > 0));
        }

        /// <summary>
        /// Undoes a registered payment, e.g. one booked on the wrong date: the payment voucher (and a
        /// year-end receivable booked with it) is reversed, dated like the original, and the invoice is
        /// unpaid again, ready to be paid anew.
        /// </summary>
        public static void unmarkPaid(SwecialObject invoice, string userId) {
            lock (Db.Sync) {
                if (Db.str(invoice, "status") != Paid) throw new LiquidException("Fakturan är inte betald.", "INVOICE IS NOT SETTLED.");
                string companyId = Db.str(invoice, "companyId");
                var voucher = Db.getOwned("voucher", Db.str(invoice, "paymentVoucherId"), companyId);
                if (voucher != null) Ledger.reverse(voucher, Db.str(voucher, "date"), userId, fromSource: true);
                if (Db.str(invoice, "closingByPayment") == "true") {
                    var closing = Db.getOwned("voucher", Db.str(invoice, "closingVoucherId"), companyId);
                    if (closing != null) Ledger.reverse(closing, Db.str(closing, "date"), userId, fromSource: true);
                    invoice.set("closingVoucherId", "");
                    invoice.set("closingByPayment", "");
                }
                invoice.set("status", Sent);
                invoice.set("paidDate", "");
                invoice.set("paymentVoucherId", "");
                Db.commit(invoice);
            }
        }

        /// <summary>The sale: the total on debitAccount (1930 when paid, 1510 when sent), sales and VAT per rate.</summary>
        static List<VoucherRow> saleRows(string debitAccount, InvoiceTotals t, SwecialObject invoice) {
            var rows = new List<VoucherRow> { new VoucherRow { account = debitAccount, debit = t.total } };
            string treatment = Db.str(invoice, "vatTreatment");
            if (treatment == EuReverseCharge || treatment == Export) {
                // No Swedish VAT: the whole amount on the account for sales abroad
                rows.Add(credit(Bas.foreignSalesAccount(treatment, isGoods(invoice)), t.net));
                return rows;
            }
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
