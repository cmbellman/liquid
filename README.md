# Liquid

Bookkeeping and invoicing for Swedish companies, running as the `liquid` app in [Swecial Server](https://github.com/Swecial/swecial-server). Web only: server-rendered pages and C# cloud code in `server/`.

## What it does

Made for small limited companies (AB) that use the cash method (kontantmetoden) and report VAT once a year, run by people who aren't bookkeepers: no accounts, debit or credit in the everyday pages.

Dark, pub-themed web app (Baren, Kranen, Glaset, Notan, Pantpåsen, Kassan, Stamgästerna), with a plain explanation under every themed name.

- Accounts on the shared Swecial user base (sign up, sign in), one company per user.
- **Baren:** money in and out over time with a forecast of unpaid invoices (Kranen), totals for the period, how long the money lasts (Glaset), unpaid invoices to tick off when paid (Notan), and a drop zone for receipts.
- **Pantpåsen:** drop, pick or photograph receipts (several at once) without filling anything in; sort them later, one after the other, and they are booked. Optionally read with AI, which fills in a suggestion to check.
- **Invoices:** customers, invoice drafts, sending (numbered), a printable invoice (print to PDF from the browser), and registering the payment, which books the sale and VAT.
- **Expenses:** what was bought, from whom and what it's for (plain categories like "Programvara och prenumerationer" or "Resor"), the amount and VAT (suggested), and the receipt as a photo or PDF. Purchases from abroad without Swedish VAT are handled (reverse charge).
- **Bank:** everything in and out of the company account, other events (share capital, owner loans, the tax account, bank fees, interest, dividends), and a check against the bank's balance.
- **Overview:** money in the account, what customers owe, this year's result and VAT, and a to-do list (overdue invoices, missing receipts).
- **Reports:** trial balance, general ledger per account and the VAT return boxes.
- **Avancerat:** the chart of accounts (a starter BAS chart) and all vouchers, with manual vouchers and reversals.

## Receipt reading with AI (optional)

Receipts in Pantpåsen can be read with AI. Liquid fills in a suggestion (supplier, date, amount, VAT, category, bought abroad, or an invoice your own company issued) that the user checks before booking. It's turned on in the app's settings on the server, `SwecialServer/apps/liquid/settings/settings.json` locally or `D:\data\swecialserver\apps\liquid\settings\settings.json` in production (not in this repo). Settings are read when the server starts.

**Production: Gemini with an API key (preferred).** Liquid calls the Gemini API itself, so it works whatever Windows account the server runs as, and the key doesn't expire on its own:

```json
"geminiApiKey": "<key from Google AI Studio or Cloud Console>",
"geminiModel": "gemini-flash-latest"
```

- Create the key in a Google Cloud project with billing enabled: on the free tier Google may use the content to improve its products, and receipts are personal data.
- `geminiModel` is optional.
- When a key is set it's used, even if `receiptReaderCommand` is set too.

**Locally: the Antigravity CLI.** On a machine where you're signed in to `agy`:

```json
"receiptReaderCommand": "/Users/aad01/.local/bin/agy",
"receiptReaderModel": "gemini-3.8-flash-low"
```

- `agy` keeps its sign-in per Windows/macOS account, encrypted for that account. A Windows service running as LocalSystem can't use another account's sign-in (tested: "You are not logged into Antigravity"), which is why production uses the API key.
- `receiptReaderHome` (optional) sets `HOME`, `USERPROFILE`, `APPDATA` and `LOCALAPPDATA` for `agy`.

**When it stops working**, Baren and Pantpåsen say why (agy not signed in, API key refused, quota, timeout, other), until a reading succeeds again. The receipts stay in the bag; "Läs kvittot igen" retries one.

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

Production is `https://m.swecial.se/liquid/`. Deploy from swecial-server's GitHub Actions (**CD (Deploy Liquid Cloud Code)**), or:

```bash
gh workflow run deploy-liquid.yml -R Swecial/swecial-server -f branch=master
```

See [AGENTS.md](AGENTS.md) for conventions and rules.
