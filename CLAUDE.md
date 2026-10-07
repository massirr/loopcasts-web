# LoopCastsWeb — notes for Claude Code

## HARD RULES (never break, no exceptions)
1. **No secrets** in code, appsettings*.json, commits, issues, PRs or logs. Never print password/API-key values.
2. **Secrets only via `dotnet user-secrets`** (dev) or environment variables / App Service settings (server).
3. **Never commit `*.db`, `*.db-shm`, `*.db-wal`**, `bin/`, `obj/`, `._*`, `.DS_Store`, or anything from the old
   LoopCasts Dropbox copy (`../LoopCasts`: passwords, customer e-mails). Check `git status` before every commit.
4. **Never edit a committed migration** (`Migrations/*`). Change the model → `dotnet ef migrations add <Name>`.
   Only a migration that is not yet on `main` may be removed (`dotnet ef migrations remove`).
5. **The GitHub repo stays private** (`massirr/loopcasts-web`). Never change visibility, never force-push `main`.
6. Never trust the browser: prices from `Plans`, plan/country/ownership checked server-side.

## Workflow (spec-driven, one outcome per session)
- Work comes from a GitHub issue (user story). Write/update `docs/specs/<issue>-<slug>.md` from
  `docs/specs/_template.md` **before** coding non-trivial features.
- One issue = one branch `feat/<issue>-<slug>` = one PR. CI (build + test) must be green before merge.
- Log every finished issue in `docs/agent-log.md` (skill `agent-log`).
- Skills in `.claude/skills/`: `user-story`, `razor-page`, `ef-migration`, `unit-test`, `security-check`, `agent-log`, `loopcasts-ui` (read before any UI work).
  Run `security-check` before every commit.
- Tests: `dotnet test tests/LoopCastsWeb.Tests` (xUnit, SQLite in-memory). Don't run `dotnet test` in the
  repo root (two project files → MSB1011).

School prototype (Automation Tools, Thomas More) for the fictional company **LoopCasts BV**
(video training subscriptions). Goal: **remove manual steps** from their order → invoice → account
process, as the teacher asked. Built by Koze, who is **learning ASP.NET**.

## How to work with Koze
- He is learning: explain each change in 1–2 sentences, compare to WPF/MVVM when useful.
- Prefer guiding over doing: for small learning pieces, give hints/TODOs and let him write the code.
- Concise and visual (tables, small diagrams). His input is partly voice-typed.
- Ask before big changes (architecture, new packages, deleting files or migrations).

## Stack
- .NET 10, ASP.NET Core **Razor Pages**; UI follows the `loopcasts-ui` skill (own CSS in `wwwroot/css/site.css`, no Bootstrap;
  jQuery only for form validation via `_ValidationScriptsPartial`)
- EF Core 10 + **SQLite** (`loopcasts.db`, connection string "Default" in appsettings.json)
- ASP.NET **Identity** (default UI) with an **Admin** role
- macOS, VS Code. Run with `dotnet watch` (port 5180). Exit code 134 = port still used by another run.

## Structure
| Path | What |
|---|---|
| `Models/` | `Course`, `Order`, `Invoice`, `Account` (DB entities); `Plan`/`Plans`, `Countries` (fixed lists in code) |
| `Data/AppDbContext.cs` | `IdentityDbContext`; unique indexes on `Invoice.Number` and `Invoice.OrderId` |
| `Data/CourseCsvImporter.cs` | Seeds courses from `Data/Seed/course-catalogue.csv` (cleans messy values, logs fixes) |
| `Data/AdminSeeder.cs` | Creates Admin role + admin users from config `SeedAdmins:N:Email/Password` (**user-secrets**) |
| `Services/PaymentService.cs` | `MarkPaidAsync`: paid → invoice (number + VAT, snapshot) → account, in ONE SaveChanges |
| `Services/MollieService.cs` | Mollie REST (typed HttpClient): create payment, `SyncAsync` re-fetches status → `MarkPaidAsync`. Webhook `POST /webhooks/mollie` in Program.cs |
| `Services/N8nNotifier.cs` | Events to the n8n webhook (`invoice.created`, `email`); also Identity's `IEmailSender`. Never throws. See `docs/n8n.md` |
| `Services/VatCalculator.cs` | Handbook VAT rules (BE 21%, EU private 21%, EU company with VAT nr 0% reverse charge, non-EU 0%) |
| `Services/MemberService.cs` | Customer logins: Identity user, role **Member**, no password → password-setup code |
| `Services/CustomerData.cs` | "Which rows belong to this e-mail?" in one place (used by /My and /Invoice) |
| `Services/TeamSeats.cs` | Team licence: buyer adds/removes colleagues on /My (`Plan.Seats`), seat = Account on the team order |
| `Services/OldDataImport.cs` | One-time import of the old orders sheet + invoice register (clean, report, idempotent); UI `/Admin/Import` |
| `Services/WorkflowThumb.cs` | Course → workflow thumbnail geometry (pure, unit-tested); drawn by `Pages/Shared/_WorkflowThumb` |
| `Services/AccountExpiry.cs` | Deactivates accounts past `EndsOn`; `ExpireAccountsService` runs it at startup + every 24h |
| `Pages/` | Index, Courses, Pricing, Order (form), Pay (**mock** checkout), OrderThanks |
| `tests/LoopCastsWeb.Tests/` | xUnit tests; `TestDb.Create()` = SQLite in-memory `AppDbContext` |
| `.github/workflows/ci.yml` | build + test on every PR and on main |
| `.github/workflows/deploy.yml` | after green CI on main → Azure App Service (inactive until set up, `docs/deploy-azure.md`) |
| `docs/specs/`, `docs/agent-log.md` | specs per issue, log of agent work |
| `Pages/My`, `Pages/Invoice` | Logged-in customer: subscriptions + invoices; printable invoice (own only, Admin all) |
| `Pages/Shared/` | `_Layout`, `_Logo`, `_CourseCard`, `_WorkflowThumb`, `_PersonChip`, `_SupportLine`, `_LoginPartial` |
| `Areas/Identity/Pages/Account/` | Our views for Login, Register, Forgot/Reset password, Access denied, Lockout (Identity package PageModels) |
| `Pages/Admin/` | Orders (mark paid = fallback for bank transfers), Invoices & accounts. Folder locked by `AdminOnly` policy |

## Flow
Pricing → `/Order?plan=…` (validation, company fields only for companies, team licence = company only)
→ `/Pay?id=&key=` (mock payment) → `PaymentService.MarkPaidAsync` → `/OrderThanks?id=&key=` (invoice nr,
access end date, "Set your password" link on the e-mail's first paid order).
`key` = `Order.AccessKey` (random): order ids are guessable, so Pay/OrderThanks always check `Order.Opens(key)`.

## Design rules (keep these)
- **Never trust the browser**: price comes from `Plans`, plan/country are checked server-side.
- **Snapshots**: `Order.Price`, `Order.CreatedAt` and all `Invoice.Billing*`/amount fields are copies at that moment and never change. Live data (`Plans`, later `Customer`) may change.
- **One place for "order paid"**: always call `PaymentService.MarkPaidAsync` (mock checkout, admin button, future Mollie webhook). It is idempotent.
- **No secrets in code or appsettings** — use `dotnet user-secrets`. Never print password values.
- Money is `decimal` (stored as TEXT in SQLite). Dates without time are `DateOnly`. Times are UTC.
- After every model change: `dotnet ef migrations add <Name>`; `Program.cs` applies migrations at startup.
- Post-Redirect-Get after every POST.

## Known limitations / next steps
- Customer login (#1, done): set-password link is shown on OrderThanks for the first paid order only; it is
  also e-mailed via n8n (#5) when `N8n:WebhookUrl` is set; "Forgot password" goes through n8n too.
- Mollie (#3): active only when user-secret `Mollie:ApiKey` (test_…) is set, otherwise /Pay is the mock.
  `Mollie:WebhookBaseUrl` = public URL (tunnel/Azure); without it the status is checked when the customer returns.
- Team seats (#4): colleague setup link is shown to the buyer once, only for a NEW login (else takeover risk).
- `Customer` is not split out of `Order` yet (planned: `Order.CustomerId`, Customer = SCD Type 1).
- Invoice = printable page (browser → PDF); seller address/VAT nr in `appsettings.json` `Seller:*` (to fill in). Invoice e-mail + set-password link via n8n (#5, `N8n:WebhookUrl`); monthly re-billing not built.
- Old data (#7): imported invoices keep their old numbers/amounts. Credit notes are not modelled yet (report only).
- Redesign (#16): `Account/Manage/*` (2FA, personal data) still uses the Identity package layout (its own Bootstrap).
  Seller address + VAT number are not in the handbook (ch. 1–6) → `Seller:Address`/`Seller:VatNumber` still empty.
- Later in the semester: chatbot (AI component), n8n workflows/webhooks, newsletter unsubscribe.
- Password manager (Bitwarden/Vaultwarden) for `paswoorden.xlsx` is a recommendation, not code.
- Never commit the old LoopCasts Dropbox copy (passwords, customer e-mails) or `*.db` files.
