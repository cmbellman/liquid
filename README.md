# Liquid

Bookkeeping and invoicing for Swedish companies, running as the `liquid` app in [Swecial Server](https://github.com/Swecial/swecial-server). Web only: server-rendered pages and C# cloud code in `server/`.

## What it does (first version)

- Accounts on the shared Swecial user base (sign up, sign in), one company per user.
- A starter BAS chart of accounts, extendable per company.
- Manual vouchers (series A) that must balance; corrections by reversing vouchers.
- Customers and invoices: draft → sent (numbered, booked 1510 / 300x / 26x1, series F) → paid (booked 1930 / 1510, series I). Printable invoice page (print to PDF from the browser).
- Reports: trial balance, general ledger per account, VAT return boxes (05, 10–12, 48, 49).

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
| `/liquid/` | Overview |
| `/liquid/login.html`, `/liquid/signup.html` | Sign in, create account |
| `/liquid/company.html` | Create the company / company settings |
| `/liquid/customers.html` | Customers |
| `/liquid/invoices.html`, `/liquid/invoice.html?id=` | Invoices, one invoice |
| `/liquid/invoice-print.html?id=` | Printable invoice |
| `/liquid/vouchers.html`, `/liquid/voucher.html` | Voucher journal, new or one voucher |
| `/liquid/accounts.html` | Chart of accounts |
| `/liquid/reports.html` | Trial balance, general ledger, VAT |

## Deploy

Production is `https://deepthought.swecial.se/liquid/`. Deploy from swecial-server's GitHub Actions (**CD (Deploy Liquid Cloud Code)**), or:

```bash
gh workflow run deploy-liquid.yml -R Swecial/swecial-server -f branch=master
```

See [AGENTS.md](AGENTS.md) for conventions and rules.
