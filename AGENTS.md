# Working Rules for Liquid

These rules apply to both humans and AI agents working in this repository.

Liquid is a bookkeeping and invoicing system for Swedish companies. It is not a standalone server: it is an app (app id `liquid`) in [Swecial Server](https://github.com/Swecial/swecial-server), like Big Ring and Digolux, but web only (no Unity client). Everything in `server/` is the app's cloud code and web pages.

## Project Communication

- **English Only:** All official project communication must be in English: commit messages, branch names, pull requests, code comments, issue descriptions and documentation. Chat conversations between AI agents and developers may be in other languages.
- **Exception:** text shown to users in the web pages (labels, messages, `LiquidException` messages) is Swedish, since the product is for Swedish companies.

## Agent Instructions Structure

- **`AGENTS.md`** is the single source of truth for all rules, conventions, and workflows.
- **`CLAUDE.md`** points to `@AGENTS.md` for Claude agents.
- **`GEMINI.md`** is a symbolic link pointing directly to `AGENTS.md` for Gemini agents.
- **Important:** Always and only edit `AGENTS.md` if you want to update the rules. Never delete or overwrite the symbolic links manually.

## Branches

- `master` is the production branch. It must always be stable and deployable.
- `develop` is the integration branch and the default branch for daily development.
- Do not make changes directly to `master` or `develop`. Always use a short-lived branch and a pull request.
- Create working branches from the latest `develop` and name them `feature/<short-name>`, `fix/<short-name>`, or `chore/<short-name>`.
- Never force-push to `master` or `develop`.

## Standard Workflow

1. Fetch the latest changes with `git switch develop` and `git pull --ff-only`.
2. Create a working branch from `develop`.
3. Make atomic, scoped changes and keep commits small and self-explanatory.
4. Verify the change on the local dev server (below).
5. Push your working branch and open a pull request targeting `develop`.
6. Describe what has changed, why, and how it was verified.
7. Delete the working branch after merging.

## Releases and Hotfixes

- Create a release via a pull request from `develop` to `master`, then deploy `master` to production (see Deploy).
- Create urgent hotfixes as `hotfix/<short-name>` from `master` and open a pull request targeting `master`. Port every merged hotfix back into `develop` immediately.

## Environments

- **Dev:** the local Swecial Server in `../../swecial/swecial-server` (`~/dev/swecial/swecial-server`). Link this repo once with `./link-app.sh liquid ~/dev/cmbellman/liquid` (it symlinks `server/` to `SwecialServer/apps/liquid/code`), then start it with `./start-server.sh`. The site is at `http://localhost/liquid/`. Local data is plain JSON under `swecial-server/SwecialServer/apps/liquid/classes/`; user accounts are in `SwecialServer/data/users/`.
- **Prod:** `deepthought.swecial.se`, the same server as the games: `https://deepthought.swecial.se/liquid/`.
- The file watcher doesn't follow symlinks: **restart the local server after changing a `.cs` file**. `.html` and `.css` changes show on reload. Compile errors only appear in the server console (`compiling liquid` … `CSxxxx`); the old code keeps running when compiling fails.

## Deploy

- Production deploys run from the swecial-server repository: the GitHub Action **CD (Deploy Liquid Cloud Code)** (`deploy-liquid.yml`, input `branch`, default `master`), or `deploy-liquid.ps1` on the server. It pulls this repo into `D:\swecial\liquid` and mirrors `server/` into `D:\data\swecialserver\apps\liquid\code`; the server recompiles by itself, no restart.
- `gh workflow run deploy-liquid.yml -R Swecial/swecial-server -f branch=master`

## Code Layout

- `server/Cloud.cs` – the app's `Cloud` hooks. There is no SDK client, so `beforeSave` turns any SDK save into a harmless object.
- `server/Liquid/` – domain logic, `namespace com.swecial.code`:
  - `Db` data access, `Money`/`Dates`, `Bas` chart of accounts, `Ledger` vouchers, `Reports`, `Companies`.
  - `Invoicing` customer invoices, `Expenses` + `Categories` everyday purchases with receipts, `BankEvents` other money in and out of the company account.
- `server/www/` – pages, `namespace com.swecial.web`. Each `x.html` starts with `<% class=WebX %>` and is a shell with `<%title%>`, `<%nav%>`, `<%error%>`, `<%message%>` and `<%content%>`; `WebX : LiquidPage` builds the content in `processPage()`.

## Conventions and Rules

- **Server naming:** class names are snake_case (`voucher`, `customer`), field names camelCase. Fields starting with `_` belong to the server.
- **Company scope:** every class except `membership` has `companyId`. Pages read objects with `Db.getOwned(class, id, companyId)` / `Db.forCompany(...)`, never `Db.get` with an id from the request.
- **Money** is whole öre in a `long` (`Money.parse`/`Money.format`), never `double`. Dates are `"yyyy-MM-dd"` strings, which compare as text.
- **Names and look:** the app has a pub theme. Baren (overview: Kranen = money in/out over time, In/Ut, Glaset = how long the money lasts, Notan = unpaid customer invoices, ticked off when paid), Pantpåsen (receipts thrown in now, sorted and booked later), Kassan (the company account), Stamgästerna (customers), Avancerat (vouchers, chart of accounts). Every themed name has a plain subtitle under it (`heading(title, subtitle)`); Fakturor and Utgifter keep their plain names. Dark theme only: tokens at the top of `style.css`, cyan = money in and the brand, amber = money out and things to act on. Charts and icons are inline SVG made on the server (`FlowChart`, `Icons`); the printable invoice stays light.
- **Users aren't bookkeepers:** the everyday pages (invoices, expenses, bank) never ask for accounts, debit or credit. The user picks what something is (an expense `Category`, a `BankEventKind`) and Liquid books it. Accounts and vouchers live under "Avancerat". Keep new features that way.
- **Cash method (kontantmetoden):** things are booked when money moves. A sent invoice gets its number but is booked when it's paid (1930 D / 300x K / 26x1 K); an expense is booked on the day it was paid. Unpaid invoices at year end are booked in the closing (not built yet). The VAT return is yearly for now.
- **Voucher series:** A manual, I invoice payments, U expenses, B bank events (F: invoices booked when sent, only in data from before the cash method).
- **Source objects own their vouchers:** an expense or bank event that changes or is removed reverses its voucher (dated like the original) and books a new one (`Ledger.reverse(..., fromSource: true)`); its voucher can't be reversed from the voucher page. An edit that changes nothing booked (only the note or receipt) keeps the voucher.
- **Purchases from abroad without Swedish VAT** (reverse charge, `Expenses.ForeignEu/ForeignNonEu`): cost D total, 1930 K total, plus 2645 D / 2614 K at 25 %. They show in VAT return boxes 21/22 (from the expenses), 30 and 48.
- **Pantpåsen:** a receipt in the bag is an expense with status `inbox` (nothing booked). Sorting it on the expense page books it (status `booked`) and moves on to the next one. Lists and sums of expenses only count `booked` ones (`Expenses.list`); the bag is `Expenses.inbox`. Several files come in one upload (`req.body.fileList`).
- **Kranen and Glaset** (`Flow`) read the bank account's vouchers (1930) and leave reversal pairs out. The forecast is the unpaid invoices by due date; Glaset is the balance divided by the average money out per month over the last three months.
- **Receipts** are `_file` objects with `companyId`, served only by `receipt.html` to the company's users (links carry `&v=<file id>` so a replaced receipt isn't cached). Uploads use `req.body.files`, which needs swecial-server with multipart support (Swecial/swecial-server PR "Parse multipart/form-data POST bodies"): deploy that before a Liquid version that has it.
- **Bookkeeping rules:** a voucher must balance and use accounts in the chart (`Ledger.validate`). A booked voucher is never changed or deleted; mistakes are corrected with a reversing voucher (`Ledger.reverse`). Only invoice drafts can be edited or deleted. Vouchers booked from an invoice can't be reversed by hand.
- **No transactions:** the server has none. Anything that numbers or books takes `Db.Sync` and writes with `Db.commit` (save + saveNow). Keep a business event in one object (rows are embedded as JSON in `rowsJson`) so it is written as a whole. Validate before taking a number, so numbers stay gap-free.
- **HTML escaping:** template values are inserted raw. Pass all user text through `h()` (and `u()` inside URLs). Never put `<%` in generated markup.
- **Errors:** throw `LiquidException` with a Swedish message for user mistakes; `LiquidPage` shows it and calls `renderAfterError()` so the form keeps what was typed. After a successful POST, redirect (with `?ok=<key>` for the message) so a reload doesn't post again.
- **Server limits to keep in mind:** only `.html`, `.css`, `.json` and `.ico` are served (put JavaScript inline), POST bodies are urlencoded or multipart (`enctype='multipart/form-data'` for uploads), there is no PDF library (invoices print from the browser), and the cloud code compiler only references what the server references (e.g. not `System.Net.Mail`).

## Known Gaps

- The SDK `list` command has no access control in Swecial Server, so a client with the app's key could read Liquid data. Liquid has no SDK client and its key must never be shipped. A server-side "web only" switch for an app is the proper fix.
- One company per user, the fiscal year is the calendar year, no credit notes, partial payments, SIE export yet.
- Next steps: income statement and balance sheet (K2 layout), the year-end closing (unpaid invoices, VAT to the tax account, corporate tax, result to equity, locking the year), then the annual report (K2) and INK2 figures.
