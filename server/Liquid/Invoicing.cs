using com.swecial.server;
using Newtonsoft.Json;
using System;
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

        public static string statusName(string status) {
            switch (status) {
                case Draft: return "Utkast";
                case Sent: return "Skickad";
                case Paid: return "Betald";
                default: return status;
            }
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
            if (Db.str(invoice, "status") != Draft) throw new LiquidException("Fakturan är redan skickad och kan inte ändras.");
        }

        /// <summary>
        /// Gives the invoice its number. Nothing is booked yet: with the cash method the sale is booked
        /// when the money comes in (markPaid). Invoices still unpaid at year end are booked in the closing.
        /// </summary>
        public static void send(SwecialObject invoice, string userId) {
            lock (Db.Sync) {
                requireDraft(invoice);
                string companyId = Db.str(invoice, "companyId");
                var customer = Db.getOwned("customer", Db.str(invoice, "customerId"), companyId);
                if (customer == null) throw new LiquidException("Välj en kund innan fakturan skickas.");
                var rows = Invoicing.rows(invoice);
                if (rows.Count == 0) throw new LiquidException("Fakturan har inga rader.");
                if (rows.Any(r => (r.text ?? "").Trim().Length == 0)) throw new LiquidException("Varje rad behöver en beskrivning.");
                var t = totals(rows);
                if (t.total <= 0) throw new LiquidException("Fakturans belopp måste vara större än noll (kreditfakturor kommer senare).");
                Dates.parse(Db.str(invoice, "invoiceDate"));
                Dates.parse(Db.str(invoice, "dueDate"));
                var company = Db.get("company", companyId);
                bool accrual = Settings.accountingMethod(company) == Settings.AccrualMethod;
                string date = Dates.clean(Db.str(invoice, "invoiceDate"));
                // The accounts it will be booked on must exist before the invoice gets a number
                Ledger.validate(companyId, date, "Kundfaktura", saleRows(Bas.Bank, t));
                if (accrual) Ledger.validate(companyId, date, "Kundfaktura", saleRows(Bas.Receivables, t));

                long number = nextNumber(company, date);
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
                if (next > Settings.MaxSerial) throw new LiquidException("Fakturanumren är slut i löpnummerserien. Byt till datumnummer under Inställningar.");
                return next;
            }
            string day = Dates.parse(invoiceDate).ToString("yyyyMMdd");
            long serial = Db.nextNumber(company.id, "invoice-" + day);
            if (serial > 99) throw new LiquidException("Högst 99 fakturor per fakturadag.");
            return long.Parse(day) * 100 + serial;
        }

        /// <summary>
        /// Books the full payment: with the cash method the sale and its VAT against 1930; for an
        /// invoice booked when it was sent (accrual method), 1930 against the receivable.
        /// </summary>
        public static void markPaid(SwecialObject invoice, string paidDate, string userId) {
            lock (Db.Sync) {
                if (Db.str(invoice, "status") != Sent) throw new LiquidException("Bara skickade, obetalda fakturor kan markeras som betalda.");
                paidDate = Dates.clean(paidDate);
                var t = totals(rows(invoice));
                bool bookedWhenSent = Db.str(invoice, "voucherId").Length > 0;
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
