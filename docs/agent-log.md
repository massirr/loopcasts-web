# Agent log

What the coding agent did, per issue. Newest at the bottom. Format: see `.claude/skills/agent-log/SKILL.md`.

## 2026-10-07 — Setup: repo, agent rules, tests, CI, backlog
- **What:** private repo `massirr/loopcasts-web`; CLAUDE.md hard rules on top; 6 skills in `.claude/skills/`;
  `docs/specs/_template.md`; xUnit project (VAT, invoice numbering, account expiry, 22 tests); CI build + test;
  8 user-story issues.
- **Why / decisions:** rules first in CLAUDE.md ("lost in the middle"); details in skills so they load only when
  needed (context budget). Tests run on SQLite in-memory = real unique indexes, no mocking package.
  Test project lives in `tests/` → `Compile Remove="tests/**"` in the web csproj (the Web SDK globs every .cs).
- **Manual steps for Koze:** `gh auth refresh -s project` so the agent can create the Project board.
- **Learn:** the in-memory SQLite db exists only while its connection is open — like a ViewModel that lives as long
  as its View.

## 2026-10-07 — #1 Customer account + login after payment  (PR, see below)
- **What:** `MemberService` creates an Identity user (role Member, no password) when an order is paid; OrderThanks
  shows a "Set your password" link (default Identity ResetPassword page). Navbar: "Log in".
- **Security fixes found while specifying:**
  1. `/Pay?id=` and `/OrderThanks?id=` were guessable (1, 2, 3…) → anyone could read other customers' names/e-mails
     (and with #1 grab their setup link). Now `Order.AccessKey` (128-bit random) is required in both URLs.
  2. Overposting: the order form bound the whole `Order` entity, so a crafted POST with `Order.IsPaid=true`
     gave access without paying → `[BindNever]` on Id/Price/CreatedAt/IsPaid/PaidAt/AccessKey.
  3. Setup link only on the e-mail's first paid order: otherwise ordering with someone else's e-mail = their account.
- **Why / decisions:** login is created after the payment transaction (a failing login never loses a payment;
  idempotent, retried on the next call). Link not e-mailed yet → #5.
- **Tests:** 2 member tests + key test (27 total). Manual e2e with curl on a scratch db: overposting ignored,
  404 without key, set password via link, log in, link gone afterwards, 2nd order with same e-mail shows no link.
- **Manual steps for Koze:** none (migration `AddOrderAccessKey` runs at startup and gives old orders a key).
- **Learn:** `[BindNever]` is the Razor Pages answer to "the View may only change what the ViewModel exposes":
  model binding fills every public property it finds in the form unless you forbid it.

## 2026-10-07 — #2 /My page: my subscription and invoices
- **What:** `/My` (subscriptions + invoices) and printable `/Invoice?number=` (also linked from Admin → Invoices);
  navbar "My LoopCasts"; `Invoice.Description` snapshot; `Seller:*` in appsettings.json; CI `push` only on main
  (PRs ran CI twice).
- **Why / decisions:** ownership rules in one testable place (`CustomerData`). Not yours → 404 instead of 403 so
  invoice numbers can't be probed. PDF via the browser's print dialog instead of a PDF package.
  Seller address/VAT number left empty: the old company files are off-limits for the agent (customer data).
- **Tests:** 31 passing (3 ownership tests). Manual e2e: anon → login redirect, own invoice 200, other's 404,
  migration backfilled descriptions of existing invoices.
- **Manual steps for Koze:** fill `Seller:Address` and `Seller:VatNumber` in `appsettings.json`.
- **Learn:** `[Authorize]` on a PageModel = WPF's "you can't open this window unless logged in", but enforced by the
  server on every request.

## 2026-10-07 — #3 Mollie payment + webhook
- **What:** `MollieService` + `POST /webhooks/mollie`; `/Pay` redirects to Mollie when `Mollie:ApiKey` is set and
  checks the status when the customer returns; `Order.MolliePaymentId` (migration `AddMolliePaymentId`).
  Pay page now shows the total incl. VAT (what Mollie charges).
- **Why / decisions:** no Mollie NuGet package: two REST calls with `HttpClient` + `System.Text.Json` are enough
  and easier to read. The webhook trusts only Mollie's answer (status, own payment id, amount).
  Return-check means it works on localhost without a tunnel; the webhook is the safety net when the customer
  closes the tab.
- **Bug found by manual test:** without an API key a fake `id=tr_…` returned 500 (Mollie 401) → `SyncAsync` now
  does nothing when disabled (+ test).
- **Tests:** 44 passing (11 Mollie). Not tested against the real Mollie API (no key available to the agent).
- **Manual steps for Koze:** Mollie dashboard → test API key →
  `dotnet user-secrets set "Mollie:ApiKey" "test_..."`; order → Pay → pick "Paid" on Mollie's test screen →
  back on OrderThanks with invoice. Optional webhook: `cloudflared tunnel --url http://localhost:5180` →
  `dotnet user-secrets set "Mollie:WebhookBaseUrl" "https://<tunnel>"`.
- **Learn:** a typed HttpClient (`AddHttpClient<MollieService>`) is like injecting a pre-configured service in
  WPF: base address + auth header set once in Program.cs; tests swap only the "network cable" (the handler).

## 2026-10-07 — #4 Team licence: manage 5 seats
- **What:** `TeamSeats` service + seat card on `/My` (add/remove colleagues); `Plan.Seats`;
  `MemberService.EnsureMemberCreatedAsync` tells whether a login is new.
- **Why / decisions:** a seat is just an `Account` on the team order, so OrderThanks, /My and the expiry job work
  without changes. Setup link for a colleague only when their login is new, otherwise a buyer could "add" an
  existing customer and set that customer's password.
- **Tests:** 51 passing (7 seat rules). Manual e2e: add, duplicate, invalid e-mail, remove (2/5 → 1/5),
  anonymous POST → login.
- **Manual steps for Koze:** none.
- **Learn:** named handlers (`OnPostAddSeatAsync` + `asp-page-handler="AddSeat"`) are like separate ICommands on
  one ViewModel: one page, several buttons, each with its own method.

## 2026-10-07 — #5 Invoice page + e-mail via n8n
- **What:** `N8nNotifier` → events `invoice.created` and `email` to one n8n webhook; it is also Identity's
  `IEmailSender`, so "Forgot password" now really sends. `docs/n8n.md` = workflow to build in n8n.
- **Why / decisions:** one webhook + Switch node in n8n instead of one URL per e-mail. The e-mailed
  set-password link is the safe version of the OrderThanks link (only the mailbox owner gets it).
  No PDF library: the e-mail links to the printable invoice; n8n can add a PDF node later.
- **Bug found by manual test:** `JsonContent` sends chunked (no Content-Length) → my test receiver saw an empty
  body. Switched to `StringContent` (works with every receiver).
- **Tests:** 56 passing. Manual: real app → fake n8n (python) got both events with secret; link works.
- **Manual steps for Koze:** build the workflow from `docs/n8n.md`, then
  `dotnet user-secrets set "N8n:WebhookUrl" "…"` and `"N8n:Secret" "…"`.
- **Learn:** registering one class as two services (`N8nNotifier` and `IEmailSender`) = in WPF, one service
  implementing two interfaces that different ViewModels ask for.

## 2026-10-07 — #8 Deploy to Azure App Service (F1) — prepared, NOT deployed (blocked on approval)
- **What:** `deploy.yml` (after green CI on main, publish-profile deploy; skipped until `AZURE_WEBAPP_NAME` exists);
  `docs/deploy-azure.md` (az commands, settings, rollback, delete); Program.cs creates the SQLite folder
  (`/home/data` on Azure).
- **Why blocked:** creating Azure resources makes a public site where the mock checkout gives free access, and
  needs admin passwords as App Settings → Koze decides and types the secrets.
- **Tests:** 56 passing; `dotnet publish` output has no db/tests; published app started with a db in a
  non-existing folder → folder created, /Courses 200.
- **Manual steps for Koze:** follow `docs/deploy-azure.md` (≈10 min), ideally with a Mollie test key set.

## 2026-10-07 — Blocked / waiting for Koze
- **#6 Chatbot:** provider/model must be decided (with the teacher) + API key in user-secrets.
- **#7 Customer import:** the agent may not read the old Dropbox copy (customer data). Needed: the column list of
  the old orders/customers sheet (headers only) or an anonymised sample of ~5 rows.
- **Project board:** `gh auth refresh -s project` (browser login), then the agent creates the board.

## 2026-10-07 — #7 Import customers from the old data
- **What:** `Services/OldDataImport.cs` (parse + clean + report, then apply in one transaction) and `/Admin/Import`
  (upload → dry-run report → Import). Paid rows get order + invoice (old number/amounts) + account + Member login
  (no password, no e-mail). First page in the `loopcasts-ui` style: tokens + buttons added to `site.css`
  (only `:root` variables and new classes, so Bootstrap pages look the same).
- **Decisions (Koze):** invoices as registered + report; credit notes report-only; logins without e-mail; admin page.
  **Agent decision:** an order with an invoice number counts as paid (register "paid" column is empty from 0014 on),
  each such row is reported as "check the bank".
- **Findings in the samples:** 0021 Vermeulen BV (BE company) invoiced at 0% VAT, should be 21%; 0005 total typo
  180.92; 0009 paid 590 of 599; number 2026-0029 used twice (invoice + credit note); 2026-0023 missing; duplicate
  Sofie order; Schmidt unpaid. Bakker's credited 0015 is replaced by 0030 → new invoices continue at 2026-0031.
- **Tests:** 83 passing (27 importer). Manual: real samples via the admin page on a scratch db → 29 orders,
  28 invoices, 28 accounts (24 active), 28 logins; second import → 0 new. Browser check at 1320 and 360 px
  (no sideways page scroll) with the fake test CSVs.
- **Manual steps for Koze:** in the real app, Admin → Import → upload the two exports. Give the report to the
  accountant (0021, 0005, credit note).
- **Learn:** the page does two POSTs (Preview, Import) with the same data in hidden fields — like a WPF wizard
  that keeps state in the ViewModel between steps; here the "ViewModel" is rebuilt from the form each request,
  so the server parses again instead of trusting the preview.

## 2026-10-07 — #16 Redesign the site with the loopcasts-ui style
- **What:** every page ported from Bootstrap to the `loopcasts-ui` style (reference `docs/design/loopcasts-home-preview.html`):
  sticky translucent nav + logo, footer with Lotte's support line, Home hero drawing the site's own order workflow,
  course cards with workflow thumbnails, Pricing/Order/Pay/OrderThanks (step sequence)/My/Invoice/Admin/Privacy/Error,
  and our own views for the Identity account pages. `Services/WorkflowThumb.cs` (pure, 26 tests), `Course.Instructor`
  (migration `AddCourseInstructor`, default "Marieke De Smet"), `Course.DurationText` ("1 h 20 min").
  Bootstrap CSS/JS removed from the layout; jQuery only with form validation. No monospace font.
- **Why / decisions:** thumbnails are server-rendered SVG from a tested C# function instead of the reference's JS
  (same picture, no script needed). Course cards are not links: there is no course page yet and a single-course
  order does not record the course. "E-mail" step on OrderThanks only lights up when n8n is configured (no false
  claims). Support line only in the footer (+ home hero), not twice per page.
  `Account/Manage/*` keeps the Identity package layout (follow-up).
- **Seller details:** handbook ch. 1–6 do not state LoopCasts' address or VAT number (ch. 3 only mentions the
  invoice template "NEW.docx"; ch. 7 = passwords, not opened) → `Seller:Address`/`Seller:VatNumber` left empty.
  Added `Seller:SupportEmail` = support@loopcasts.example (ch. 6).
- **Checks:** 109 tests. Headless Chrome script (DevTools protocol, no packages; Playwright MCP was disconnected):
  18 pages × 360/720/1320 px → no sideways scroll, focus outline on real Tab presses. Found and fixed: hidden company
  fields shown (`display:grid` beat `[hidden]`), `.visually-hidden` escaping `.table-scroll` (+14 px at 360),
  invoice numbers wrapping, sticky summary over the form on phones, hero labels outside CSS isolation, initials MS→MD.
  Before/after screenshots: `docs/design/screenshots/` (before = old `main` in a temporary worktree).
- **Manual steps for Koze:** fill `Seller:Address` / `Seller:VatNumber` when known (invoice template NEW.docx).
- **Learn:** CSS isolation (`Page.cshtml.css`) adds a `b-xxxx` attribute to the page's own tags; markup written with
  `Html.Raw` or by a partial doesn't get it, so its styles must live in `site.css` — like a WPF style defined in a
  UserControl's resources that doesn't reach controls created somewhere else.

## 2026-10-12 — #18 Polish every page  (PR #19, merged)
- **What:** account menu (Admin only for the Admin role) + phone menu sheet (Esc, outside click, arrows), fixed fading
  dot grid; our own Account settings views (Login and e-mail, Password, Two-factor login, Your data; no username/phone)
  + LoginWith2fa/RecoveryCode; Order → Pay → Done progress, "Paying…" blocks a double submit; /My: Copy link, team as
  one card; more thumbnail shapes (`WorkflowThumb.ShapeOf`), no lone last card, no "Recorded by" with one instructor,
  no initials strip; hover/active/focus/disabled/busy on every control; reduced motion/transparency, more contrast.
- **Why / decisions:** changing the login e-mail would have emptied /My (orders are found by e-mail), so our own
  `ConfirmEmailChange` moves orders and seats (`CustomerData.MoveToEmailAsync`). Identity's e-mails are rewritten in
  one place (`N8nNotifier.InOurWords`) instead of overriding three PageModels. Status messages via `_Flash`, because
  the package's own `Manage/_StatusMessage` wins over a same-named partial one folder up. Skipped: QR code (needs a
  package; key + `otpauth:` link), orders in the data download, anonymising after delete. `break-ui`, `mobile-native`,
  `apple-design` skills not installed; skill §10 rules used instead.
- **Wording (§6):**

  | Where | Old | New |
  |---|---|---|
  | Home caption | This site runs the same kind of workflow we teach: nobody types your invoice by hand. | Pay, and your invoice and your login are ready right away. |
  | Home hero | Questions? Lotte answers, not a bot. (+ initials strip) | removed |
  | Support line | Questions? Lotte answers, not a bot. | Questions? Mail support@loopcasts.example |
  | Footer | © LoopCasts BV, Ghent · Privacy | © LoopCasts BV, Ghent / Privacy (separate) |
  | Nav | My LoopCasts / Admin / Account / Log out | menu: Signed in as … / My courses / Account settings / Admin / Log out |
  | /My | My LoopCasts; View / print; "… is shown only once." | My courses; View or print; "… you see it only once." + Copy link → Copied |
  | /My team | 3 of 5 seats used · team licence, order 1 | 3 of 5 seats used / Team licence, order 1 |
  | Pricing | 5 seats · for companies | 5 seats, for companies |
  | Order | excl. VAT · {plan detail} | €… excl. VAT / {plan detail} |
  | Pay | This page stands in for the payment provider (no Mollie key set). No real money moves. | No real money moves: the button marks your order as paid. |
  | Pay | LoopCasts BV · order 1; €… excl. VAT · {e-mail} | Order 1 at LoopCasts BV; Incl. VAT (€… excl. VAT); Invoice and login go to {e-mail}; Paying… |
  | OrderThanks | Everything below happened by itself the moment you paid. Nobody had to retype anything. | Your invoice and your access are ready. You find both on My courses. |
  | OrderThanks | {nr} · €… incl. 21% VAT | {nr} / €… incl. 21% VAT |
  | Invoice | Questions? Lotte answers at support@… | Questions? Mail support@… |
  | Privacy | Mail … and Lotte will help you. | You can download your data or delete your account yourself … / Mail … and we'll help you. |
  | Login | With the e-mail you ordered with. Staff use their personal account. | Use the e-mail you ordered with. |
  | Access denied | Go to My LoopCasts | Go to My courses |
  | Admin | 12 orders · 3 waiting; Company · VAT; "· 4 invoices · calculated, not summed by colour" | 12 orders, 3 waiting for payment.; Company VAT; "from 4 invoices." |
  | E-mails (Identity) | Reset Password: Please reset your password by clicking here. / Confirm your email: Please confirm your account by clicking here. | Choose a new LoopCasts password … / Confirm your LoopCasts e-mail … (full text in the spec) |
  | Invoice e-mail (n8n) | greeting, total, button | Hi {name}, Thanks for your order. Your invoice {nr} for {description} is ready: €{total} incl. VAT, already paid. |

  Full table incl. status messages: `docs/specs/18-polish.md`.
- **Tests:** 124 (new: account-button name, e-mail move, thumbnail shapes, e-mails in our words). CI green. Headless
  Chrome (DevTools protocol, scratch DB): 42 page/role combinations × 360/720/1320, no sideways scroll; menus, focus
  ring, Paying…, Copy link, two-factor with a real TOTP code all checked. Found + fixed during the check: package
  `_StatusMessage` winning over ours, thumbnail `max-height` also shrinking its width.
  Screenshots: `docs/design/screenshots/polish-*.png`.
- **Manual steps for Koze:** in the browser, try Account settings → Two-factor login with your phone's authenticator
  app; copy the n8n invoice template from `docs/n8n.md` into the Send Email node.
- **Learn:** nested layouts — `Manage/_ManageLayout.cshtml` sets `Layout = "/Pages/Shared/_Layout.cshtml"` and
  re-renders the `Scripts` section, like a WPF UserControl that hosts a ContentPresenter inside the main window's
  template: the page fills the inner slot, the inner layout fills the outer one.

## 2026-10-12 — #20 Import the old members database  (PR #23, merged)
- **What:** /Admin/Import reads the old website's `users` table export (.sql): preview (counts per product, expired,
  report, "active but end date passed" list with yearly value) → import (Account + Member login without password).
  Migration `AccountsWithoutOrder`: `Account.OrderId` nullable, `Account.PlanCode`.
- **Why / decisions:** no fake orders for old members (Admin → Orders would show ~1 900 invented prices). The password
  column is skipped by name while reading, so the MD5 hashes never become strings; Preview → Import carries a sanitised
  CSV. Monthly rows have no end date (rolling) → access until the next renewal date. The old site has one row per team
  seat, so a team row counts as €599 / 5 in Lotte's list (first run counted €599 per row: found in the screenshot).
  Skipped: grouping old team seats into licences (no buyer in the data).
- **Tests:** 134 (10 new, made-up fixture incl. a hash with quotes/commas/`)`); the tests also caught a reader bug
  (space before quoted values). CI green. Real export on a scratch DB: 1 940 members in 10 s, 21 duplicates skipped,
  313 active-but-ended = €42 928.60 a year; second run imports nothing.
- **Manual steps for Koze:** on the real site, import the orders CSVs first, then the members `.sql`. Give Lotte the
  "active but end date passed" list. Members choose a password with "Forgot password" (needs the n8n e-mails, #21).
- **Learn:** `int?` foreign key = an optional relation, like a nullable navigation in a ViewModel: the query uses
  `a.PlanCode ?? (order's plan)` instead of an inner join, which would silently drop every row without an order.

## 2026-10-12 — #21 n8n locally with e-mails and a renewal reminder  (PR #24, merged)
- **What:** `docker-compose.yml` (n8n 2.42.7 + Mailpit v1.31.5, localhost only), workflows "LoopCasts e-mails" and
  "Renewal reminder" as JSON in `n8n/workflows/`, `GET /api/automation/expiring` + `POST /api/automation/reminded`
  (secret header), `Account.RemindedFor` (migration `AccountRemindedFor`). docs/n8n.md rewritten as the how-to.
- **Why / decisions:** Mailpit as the only mail server, so a demo can never e-mail a real customer. The secret lives in
  `.env` (git-ignored) and the site's user-secrets; the webhook checks it in an IF node, so no secret sits in an
  importable credential file. Reminders are keyed on the end date, so a renewal gets a new reminder without extra state.
  Team colleagues aren't reminded (they can't renew). Endpoints as static handlers → tested without a new package.
  **Bug found end to end:** Identity 10's reset subject is "Reset your password", so #18's rewrite never applied
  (its unit test used the wrong subject too) → now matched on Identity's body text, test covers both subjects.
- **Tests:** 143, CI green. End to end on a scratch DB: invoice + password e-mails in Mailpit, wrong secret ignored,
  reminder run → 2 of 5 made-up members (ending within 14 days), second run → 0. Screenshots `docs/design/screenshots/n8n-*.png`.
- **Manual steps for Koze:** create `.env` + user-secrets as in docs/n8n.md §1, `docker compose up -d`, import + publish
  the workflows, create the n8n owner login in the browser (local account, blocked for me).
- **Learn:** the API handlers are plain static methods that get `HttpRequest`, `IConfiguration` and `AppDbContext` as
  parameters — ASP.NET fills them in (dependency injection per parameter), like a WPF command that receives its
  CommandParameter instead of reaching into the window.
