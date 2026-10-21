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
  - `Db` data access, `Money`/`Dates`, `Bas` chart of accounts, `Ledger` vouchers, `Invoicing`, `Reports`, `Companies`.
- `server/www/` – pages, `namespace com.swecial.web`. Each `x.html` starts with `<% class=WebX %>` and is a shell with `<%title%>`, `<%nav%>`, `<%error%>`, `<%message%>` and `<%content%>`; `WebX : LiquidPage` builds the content in `processPage()`.

## Conventions and Rules

- **Server naming:** class names are snake_case (`voucher`, `customer`), field names camelCase. Fields starting with `_` belong to the server.
- **Company scope:** every class except `membership` has `companyId`. Pages read objects with `Db.getOwned(class, id, companyId)` / `Db.forCompany(...)`, never `Db.get` with an id from the request.
- **Money** is whole öre in a `long` (`Money.parse`/`Money.format`), never `double`. Dates are `"yyyy-MM-dd"` strings, which compare as text.
- **Bookkeeping rules:** a voucher must balance and use accounts in the chart (`Ledger.validate`). A booked voucher is never changed or deleted; mistakes are corrected with a reversing voucher (`Ledger.reverse`). Only invoice drafts can be edited or deleted. Vouchers booked from an invoice can't be reversed by hand.
- **No transactions:** the server has none. Anything that numbers or books takes `Db.Sync` and writes with `Db.commit` (save + saveNow). Keep a business event in one object (rows are embedded as JSON in `rowsJson`) so it is written as a whole. Validate before taking a number, so numbers stay gap-free.
- **HTML escaping:** template values are inserted raw. Pass all user text through `h()` (and `u()` inside URLs). Never put `<%` in generated markup.
- **Errors:** throw `LiquidException` with a Swedish message for user mistakes; `LiquidPage` shows it and calls `renderAfterError()` so the form keeps what was typed. After a successful POST, redirect (with `?ok=<key>` for the message) so a reload doesn't post again.
- **Server limits to keep in mind:** only `.html`, `.css`, `.json` and `.ico` are served (put JavaScript inline), POST bodies are urlencoded only (no file uploads yet), there is no PDF library (invoices print from the browser), and the cloud code compiler only references what the server references (e.g. not `System.Net.Mail`).

## Known Gaps

- The SDK `list` command has no access control in Swecial Server, so a client with the app's key could read Liquid data. Liquid has no SDK client and its key must never be shipped. A server-side "web only" switch for an app is the proper fix.
- One company per user, the fiscal year is the calendar year, no credit notes, partial payments, supplier invoices, SIE export or year-end closing yet.
