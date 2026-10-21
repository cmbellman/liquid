using com.swecial.server;
using System.Collections.Generic;
using System.Linq;

namespace com.swecial.code {
    public class AccountBalance {
        public string account;
        public long opening;
        public long debit;
        public long credit;
        public long closing => opening + debit - credit;
    }

    public class LedgerLine {
        public SwecialObject voucher;
        public long debit;
        public long credit;
        public long balance;
    }

    /// <summary>
    /// Reports computed from the vouchers. Dates are "yyyy-MM-dd" strings, so they compare as text.
    /// Balance sheet accounts (1xxx, 2xxx) carry an opening balance from everything before the period;
    /// income statement accounts start at zero every period.
    /// </summary>
    public static class Reports {
        /// <summary>Trial balance (saldobalans) for the period, sorted by account.</summary>
        public static List<AccountBalance> trialBalance(string companyId, string from, string to) {
            var result = new Dictionary<string, AccountBalance>();
            foreach (var voucher in Ledger.vouchers(companyId)) {
                string date = Db.str(voucher, "date");
                if (string.CompareOrdinal(date, to) > 0) continue;
                bool before = string.CompareOrdinal(date, from) < 0;
                foreach (var row in Ledger.rows(voucher)) {
                    if (before && !Bas.isBalanceAccount(row.account)) continue;
                    if (!result.TryGetValue(row.account, out var b)) {
                        b = new AccountBalance { account = row.account };
                        result[row.account] = b;
                    }
                    if (before) {
                        b.opening += row.debit - row.credit;
                    } else {
                        b.debit += row.debit;
                        b.credit += row.credit;
                    }
                }
            }
            return result.Values.OrderBy(b => b.account).ToList();
        }

        /// <summary>General ledger (huvudbok) lines for one account in the period, with running balance.</summary>
        public static List<LedgerLine> generalLedger(string companyId, string account, string from, string to, out long opening) {
            opening = 0;
            var lines = new List<LedgerLine>();
            bool balanceAccount = Bas.isBalanceAccount(account);
            foreach (var voucher in Ledger.vouchers(companyId)) {
                string date = Db.str(voucher, "date");
                if (string.CompareOrdinal(date, to) > 0) continue;
                bool before = string.CompareOrdinal(date, from) < 0;
                foreach (var row in Ledger.rows(voucher).Where(r => r.account == account)) {
                    if (before) {
                        if (balanceAccount) opening += row.debit - row.credit;
                    } else {
                        lines.Add(new LedgerLine { voucher = voucher, debit = row.debit, credit = row.credit });
                    }
                }
            }
            long balance = opening;
            foreach (var line in lines) {
                balance += line.debit - line.credit;
                line.balance = balance;
            }
            return lines;
        }

        /// <summary>
        /// The VAT return boxes for the period, from the booked accounts. Sales are credit-positive.
        /// Box 05 is taxable sales (3001–3003), 10–12 output VAT, 48 input VAT, 49 the VAT to pay.
        /// </summary>
        public static List<(string box, string label, long amount)> vatReturn(string companyId, string from, string to) {
            var balances = trialBalance(companyId, from, to).ToDictionary(b => b.account);
            long period(string account) => balances.TryGetValue(account, out var b) ? b.credit - b.debit : 0;

            long sales = period("3001") + period("3002") + period("3003");
            long vat25 = period("2611");
            long vat12 = period("2621");
            long vat6 = period("2631");
            long input = -period(Bas.InputVat);
            return new List<(string, string, long)> {
                ("05", "Momspliktig försäljning", sales),
                ("10", "Utgående moms 25 %", vat25),
                ("11", "Utgående moms 12 %", vat12),
                ("12", "Utgående moms 6 %", vat6),
                ("48", "Ingående moms att dra av", input),
                ("49", "Moms att betala (+) eller få tillbaka (−)", vat25 + vat12 + vat6 - input),
            };
        }
    }
}
