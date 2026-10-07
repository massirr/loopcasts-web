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
