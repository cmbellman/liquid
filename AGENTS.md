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

## Branches and Workflow

- One developer: no pull requests. Commit straight to `develop` (the default branch) and push.
- `master` is what runs in production. Release by fast-forwarding `master` to `develop` and deploying (see Deploy).
- Keep commits small and self-explanatory, and verify on a local server before pushing (below). Never force-push.

## Environments

- **Testing:** an agent tests on its own Swecial Server instance (a git worktree of swecial-server with its own data and port), never on the developer's local server or its data.
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
- `server/www/` – pages, `namespace com.swecial.web`: **markup lives in the templates, never in C#.**
  - `x.html` starts with `<% class=WebX %>`, includes `partials/top.part` (head, sidebar, error and message) and `partials/bottom.part`, and holds the page's markup. Shared pieces are partials (`.part`, never served): `dropzone.part`, `expense-form.part`, `invoice-totals.part`.
  - `WebX : LiquidPage` only sets values: `set`/`val` (val HTML-encodes), `rows(key, …)` of `row(…)` objects for `<swe:iterator source="key">` (plus the flag `keyAny`), bools for `<swe:condition>`, `options(…)` for selects. Template syntax is in swecial-server's AGENTS.md ("Templates").
  - Generated graphics stay in C#: the chart and gauges (`FlowChart`) and icons (`Icons`).
- `server/themes/<id>.json` – the themes (see Themes).

## Conventions and Rules

- **Server naming:** class names are snake_case (`voucher`, `customer`), field names camelCase. Fields starting with `_` belong to the server.
- **Company scope:** every class except `membership` has `companyId`. Pages read objects with `Db.getOwned(class, id, companyId)` / `Db.forCompany(...)`, never `Db.get` with an id from the request.
- **Money** is whole öre in a `long` (`Money.parse`/`Money.format`), never `double`. Dates are `"yyyy-MM-dd"` strings, which compare as text.
- **Themes:** every user picks a theme (Inställningar → Utseende; stored on the user as `liquidTheme`). A theme is data: `server/themes/<id>.json` (name, description, `language` sv/en, `extends`, `gauge` beer/tank, `brand`, `clock`, `fonts`, `icons` per place, and **all UI texts by key**) plus `server/www/theme-<id>.css` (tokens and typography, scoped to `body.theme-<id>`). Texts missing in a theme come from the theme it extends, then from `neon`, so a new theme can override only what it wants. In templates a text is `<%t.bar.tab.title%>`; in C# `T("key", args…)` (encoded, `{0}` placeholders), `Tr` (plain) and `Tn` (`.one`/`.many`). A key can't be both a text and a group: use `x.label` for the text. Errors from the books are `LiquidException(swedish, english)`; the theme's `language` picks one. The printable invoice is always Swedish and neutral (a document for the customer). Adding a theme: copy a JSON file, change what you want, add its CSS file; it shows up under Utseende by itself.
- **Names and look:** the app has a pub theme. Baren (overview: Kranen = money in/out over time, In/Ut, Glaset = how long the money lasts, Notan = unpaid customer invoices, ticked off when paid), Pantpåsen (receipts thrown in now, sorted and booked later), Kassan (the company account), Stamgästerna (customers), Avancerat (vouchers, chart of accounts). Every themed name has a plain subtitle under it (`heading(title, subtitle)`); Fakturor and Utgifter keep their plain names. Neon is the default (Swedish, pub names, beer glass); Nostromo is a green phosphor ship terminal in corporate English (tank gauge, clock, crate and console icons). Neon's tokens are the defaults at the top of `style.css`, cyan = money in and the brand, amber = money out and things to act on. Charts and icons are inline SVG made on the server (`FlowChart`, `Icons`); the printable invoice stays light.
- **Users aren't bookkeepers:** the everyday pages (invoices, expenses, bank) never ask for accounts, debit or credit. The user picks what something is (an expense `Category`, a `BankEventKind`) and Liquid books it. Accounts and vouchers live under "Avancerat". Keep new features that way.
- **Settings** (Inställningar, `company.html`, `Settings`), stored on the company: invoice numbering (`serial` 1, 2, 3 … with a settable next number, or `date` = invoice date + two-digit serial for the day, `2026102101`), accounting method (`cash` default, or `accrual`) and VAT period (`year` default, `quarter`, `month`; the VAT report defaults to the current one). Serial numbers stay below 1 000 000 000 so they never collide with date numbers, and a new next number must be above every serial already used.
- **Accounting method:** with the cash method (kontantmetoden) a sent invoice gets its number and is booked when paid (1930 D / 300x K / 26x1 K). With the accrual method (faktureringsmetoden) it is booked when sent (1510 D / 300x K / 26x1 K, series F) and the payment clears the receivable (1930 D / 1510 K). Each invoice remembers how it was booked (`voucherId` set when booked at sending), so switching method never books an invoice twice. Expenses are always booked on the day they were paid. With the cash method, unpaid invoices at year end are booked in the closing (1510 D / 300x K / 26x1 K; not built yet), so a cash method invoice paid in a later year than its invoice date only clears the receivable when paid (1930 D / 1510 K, `Invoicing.inClosing`). A registered payment can be undone (`Invoicing.unmarkPaid`), which reverses its voucher.
- **Invoice numbers** are given when the invoice is sent (`Invoicing.nextNumber`): counter `invoice` for serial numbers, `invoice-<yyyyMMdd>` for date numbers.
- **Voucher series:** A manual, F invoices booked when sent (accrual method), I invoice payments, U expenses, B bank events.
- **Source objects own their vouchers:** an expense or bank event that changes or is removed reverses its voucher (dated like the original) and books a new one (`Ledger.reverse(..., fromSource: true)`); its voucher can't be reversed from the voucher page. An edit that changes nothing booked (only the note or receipt) keeps the voucher.
- **Purchases from abroad without Swedish VAT** (reverse charge, `Expenses.ForeignEu/ForeignNonEu`): cost D total, 1930 K total, plus 2645 D / 2614 K at 25 %. They show in VAT return boxes 21/22 (from the expenses), 30 and 48.
- **Pantpåsen:** a receipt in the bag is an expense with status `inbox` (nothing booked). Sorting it on the expense page books it (status `booked`) and moves on to the next one. Lists and sums of expenses only count `booked` ones (`Expenses.list`); the bag is `Expenses.inbox`. Several files come in one upload (`req.body.fileList`).
- **Receipt reader (AI)** (`ReceiptReader`): a receipt put in Pantpåsen is read in the background by the Antigravity CLI (`agy -p … --sandbox --output-format json --json-schema …`), at most two at a time, in an empty temp folder with only the receipt. The answer is checked field by field and stored as a suggestion on the expense (`aiStatus` reading/done/failed, `aiSupplier`, `aiDate`, `aiTotal`, `aiVat`, `aiVatRate`, `aiCategory`, `aiForeign`, `aiCurrency`), which fills in the sorting form. **Nothing is booked from AI output**: the user checks and books. Text on a receipt is data, never instructions (said in the prompt; the schema and the checks enforce it). Amounts in other currencies than SEK are left for the user. The reader is off unless the app's `settings.json` on the server has `receiptReaderCommand` (see README).
- **Own invoices in Pantpåsen:** the receipt reader is told which company it reads for and marks an invoice that company issued (`aiKind=sales`, details in `aiSaleJson`), so it's never booked as a purchase. `Invoicing.draftFromDocument` turns it into a draft (customer found by organisation number or name, or created; rows, dates, references; the original as `documentFileId`, served by `receipt.html?invoice=`), and `Invoicing.registerIssued` registers it with the document's own number: it must be new for the company, and Liquid's series move past it (`reserveNumber`). It's then booked like any sent invoice for the company's method. The sorting page also offers this by hand, and "Läs kvittot igen" re-runs the reader.
- **Kranen and Glaset** (`Flow`) read the bank account's vouchers (1930) and leave reversal pairs out. The forecast is the unpaid invoices by due date; Glaset is the balance divided by the average money out per month over the last three months.
- **Receipts** are `_file` objects with `companyId`, served only by `receipt.html` to the company's users (links carry `&v=<file id>` so a replaced receipt isn't cached). Uploads use `req.body.files`, which needs swecial-server with multipart support (Swecial/swecial-server PR "Parse multipart/form-data POST bodies"): deploy that before a Liquid version that has it.
- **Bookkeeping rules:** a voucher must balance and use accounts in the chart (`Ledger.validate`). A booked voucher is never changed or deleted; mistakes are corrected with a reversing voucher (`Ledger.reverse`). Only invoice drafts can be edited or deleted. Vouchers booked from an invoice can't be reversed by hand.
- **No transactions:** the server has none. Anything that numbers or books takes `Db.Sync` and writes with `Db.commit` (save + saveNow). Keep a business event in one object (rows are embedded as JSON in `rowsJson`) so it is written as a whole. Validate before taking a number, so numbers stay gap-free.
- **HTML escaping:** template values are inserted raw. User text goes through `val()`/`h()` (and `u()` inside URLs); theme texts are encoded already. Never put `<%` in a value.
- **Errors:** throw `LiquidException(swedish, english)` for user mistakes; `LiquidPage` shows it and calls `renderAfterError()`, which sets the page's values again so the form keeps what was typed. After a successful POST, redirect (with `?ok=<key>` for the message) so a reload doesn't post again.
- **Server limits to keep in mind:** only `.html`, `.css`, `.json` and `.ico` are served (put JavaScript inline), POST bodies are urlencoded or multipart (`enctype='multipart/form-data'` for uploads), there is no PDF library (invoices print from the browser), and the cloud code compiler only references what the server references (e.g. not `System.Net.Mail`).

## Known Gaps

- The SDK `list` command has no access control in Swecial Server, so a client with the app's key could read Liquid data. Liquid has no SDK client and its key must never be shipped. A server-side "web only" switch for an app is the proper fix.
- One company per user, the fiscal year is the calendar year, no credit notes, partial payments, SIE export yet.
- Next steps: income statement and balance sheet (K2 layout), the year-end closing (unpaid invoices, VAT to the tax account, corporate tax, result to equity, locking the year), then the annual report (K2) and INK2 figures.
