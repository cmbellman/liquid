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
    /// Customer invoices: draft → sent (numbered and booked: 1510 D / 300x K / 26x1 K) → paid
    /// (booked: 1930 D / 1510 K). Only drafts can be edited or deleted.
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

        /// <summary>Gives the invoice its number and books the receivable, output VAT and sales.</summary>
        public static void send(SwecialObject invoice, string userId) {
            lock (Db.Sync) {
                requireDraft(invoice);
                string companyId = Db.str(invoice, "companyId");
                var customer = Db.getOwned("customer", Db.str(invoice, "customerId"), companyId);
                if (customer == null) throw new LiquidException("Välj en kund innan fakturan skickas.");
                var rows = Invoicing.rows(invoice);
                if (rows.Count == 0) throw new LiquidException("Fakturan har inga rader.");
                var t = totals(rows);
                if (t.total <= 0) throw new LiquidException("Fakturans belopp måste vara större än noll (kreditfakturor kommer senare).");
                string date = Dates.clean(Db.str(invoice, "invoiceDate"));
                Dates.parse(Db.str(invoice, "dueDate"));

                var voucherRows = new List<VoucherRow> { new VoucherRow { account = Bas.Receivables, debit = t.total } };
                foreach (var rate in t.byRate) {
                    if (rate.Value.net != 0) voucherRows.Add(credit(Bas.salesAccount(rate.Key), rate.Value.net));
                    if (rate.Value.vat != 0) voucherRows.Add(credit(Bas.outputVatAccount(rate.Key), rate.Value.vat));
                }

                // Validate first so a refused voucher doesn't use up an invoice number
                Ledger.validate(companyId, date, "Kundfaktura", voucherRows);
                long number = Db.nextNumber(companyId, "invoice");
                var voucher = Ledger.book(companyId, Ledger.Invoices, date,
                    "Kundfaktura " + number + ", " + Db.str(customer, "name"), voucherRows, userId, "invoice", invoice.id);

                invoice.set("number", number);
                invoice.set("status", Sent);
                invoice.set("voucherId", voucher.id);
                invoice.set("customerName", Db.str(customer, "name"));
                invoice.set("total", t.total);
                Db.commit(invoice);
            }
        }

        /// <summary>Books the full payment into the bank account.</summary>
        public static void markPaid(SwecialObject invoice, string paidDate, string userId) {
            lock (Db.Sync) {
                if (Db.str(invoice, "status") != Sent) throw new LiquidException("Bara skickade, obetalda fakturor kan markeras som betalda.");
                paidDate = Dates.clean(paidDate);
                long total = Db.num(invoice, "total");
                var voucher = Ledger.book(Db.str(invoice, "companyId"), Ledger.Payments, paidDate,
                    "Inbetalning kundfaktura " + Db.num(invoice, "number") + ", " + Db.str(invoice, "customerName"),
                    new List<VoucherRow> {
                        new VoucherRow { account = Bas.Bank, debit = total },
                        new VoucherRow { account = Bas.Receivables, credit = total },
                    }, userId, "payment", invoice.id);
                invoice.set("status", Paid);
                invoice.set("paidDate", paidDate);
                invoice.set("paymentVoucherId", voucher.id);
                Db.commit(invoice);
            }
        }

        static VoucherRow credit(string account, long amount) {
            return amount >= 0
                ? new VoucherRow { account = account, credit = amount }
                : new VoucherRow { account = account, debit = -amount };
        }
    }
}
