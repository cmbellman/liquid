# Liquid

Bookkeeping and invoicing for Swedish companies, running as the `liquid` app in [Swecial Server](https://github.com/Swecial/swecial-server). Web only: server-rendered pages and C# cloud code in `server/`.

## What it does

Made for small limited companies (AB) that use the cash method (kontantmetoden) and report VAT once a year, run by people who aren't bookkeepers: no accounts, debit or credit in the everyday pages.

Dark, pub-themed web app (Baren, Kranen, Glaset, Notan, Pantpåsen, Kassan, Stamgästerna), with a plain explanation under every themed name.

- Accounts on the shared Swecial user base (sign up, sign in), one company per user.
- **Baren:** money in and out over time with a forecast of unpaid invoices (Kranen), totals for the period, how long the money lasts (Glaset), unpaid invoices to tick off when paid (Notan), and a drop zone for receipts.
- **Pantpåsen:** drop, pick or photograph receipts (several at once) without filling anything in; sort them later, one after the other, and they are booked.
- **Invoices:** customers, invoice drafts, sending (numbered), a printable invoice (print to PDF from the browser), and registering the payment, which books the sale and VAT.
- **Expenses:** what was bought, from whom and what it's for (plain categories like "Programvara och prenumerationer" or "Resor"), the amount and VAT (suggested), and the receipt as a photo or PDF. Purchases from abroad without Swedish VAT are handled (reverse charge).
- **Bank:** everything in and out of the company account, other events (share capital, owner loans, the tax account, bank fees, interest, dividends), and a check against the bank's balance.
- **Overview:** money in the account, what customers owe, this year's result and VAT, and a to-do list (overdue invoices, missing receipts).
- **Reports:** trial balance, general ledger per account and the VAT return boxes.
- **Avancerat:** the chart of accounts (a starter BAS chart) and all vouchers, with manual vouchers and reversals.

## Local development

```bash
cd ~/dev/swecial/swecial-server
./link-app.sh liquid ~/dev/cmbellman/liquid   # once
./start-server.sh                              # restart after .cs changes
```

Open `http://localhost/liquid/`, create an account and a company.

## Pages

| URL | Page |
|-----|------|
| `/liquid/` | Baren (overview) |
| `/liquid/pant.html` | Pantpåsen (receipts to sort) |
| `/liquid/login.html`, `/liquid/signup.html` | Sign in, create account |
| `/liquid/company.html` | Create the company / company settings |
| `/liquid/customers.html` | Customers |
| `/liquid/invoices.html`, `/liquid/invoice.html?id=` | Invoices, one invoice |
| `/liquid/invoice-print.html?id=` | Printable invoice |
| `/liquid/expenses.html`, `/liquid/expense.html` | Expenses, new or one expense |
| `/liquid/receipt.html?id=` | An expense's receipt file |
| `/liquid/bank.html` | Company account, other events, check against the bank |
| `/liquid/vouchers.html`, `/liquid/voucher.html` | Voucher journal, new or one voucher |
| `/liquid/accounts.html` | Chart of accounts |
| `/liquid/reports.html` | Trial balance, general ledger, VAT |

## Deploy

Production is `https://deepthought.swecial.se/liquid/`. Deploy from swecial-server's GitHub Actions (**CD (Deploy Liquid Cloud Code)**), or:

```bash
gh workflow run deploy-liquid.yml -R Swecial/swecial-server -f branch=master
```

See [AGENTS.md](AGENTS.md) for conventions and rules.
