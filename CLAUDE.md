# LoopCastsWeb — notes for Claude Code

**Current status, open work and next steps: `docs/handover.md`** (read it first, update it at the end of a session).

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
| `Data/CourseCsvImporter.cs` | Loads `Data/Seed/course-catalogue.csv` (export of the full 60-course xlsx) at every start: update by title, Ids kept, nothing deleted; cleans messy values, logs fixes for new/changed rows only (#31) |
| `docs/analysis/` | Analysis evidence, e.g. course catalogue vs Vimeo vs old website (`course-catalogue-crosscheck.mjs`, read-only) |
| `Data/AdminSeeder.cs` | Creates Admin role + admin users from config `SeedAdmins:N:Email/Password` (**user-secrets**) |
| `Services/PaymentService.cs` | `MarkPaidAsync`: paid → invoice (number + VAT, snapshot) → account, in ONE SaveChanges |
| `Services/MollieService.cs` | Mollie REST (typed HttpClient): create payment, `SyncAsync` re-fetches status → `MarkPaidAsync`. Webhook `POST /webhooks/mollie` in Program.cs |
| `Services/N8nNotifier.cs` | Event `invoice.created` to the n8n webhook (invoice e-mail). Never throws. See `docs/n8n.md` |
| `Services/LoginEmailSender.cs` | Identity's `IEmailSender` (Forgot password, Change e-mail): straight over SMTP (`Smtp:Host/Port/From`, `System.Net.Mail`), NOT via n8n (#27). Our wording lives here. Never throws; SMTP down → log + TempData `LoginEmailNotSent` (shown by `_Flash`, only when logged in) |
| `Services/RenewalReminders.cs`, `Api/AutomationApi.cs` | Renewal reminders for n8n: `GET /api/automation/expiring?days=14`, `POST /api/automation/reminded` (header `X-LoopCasts-Secret` = `N8n:Secret`) |
| `docs/decisions/` | ADRs. **0001 n8n or code**: code = money, data, security, must-always-work; n8n = outside services, schedules, notifications, texts the business changes, AI. Check it before placing a new automation |
| `docker-compose.yml`, `n8n/` | Local n8n + Mailpit (only mail server, SMTP also on `localhost:1025` for the site); workflows "LoopCasts e-mails" + "Renewal reminder" as JSON. `.env` = `LOOPCASTS_SECRET` (git-ignored). See `docs/n8n.md` |
| `Api/PublicApi.cs`, `Pages/Shared/_Chat.cshtml` | Chat assistant (#22): public read-only `/api/public/courses|plans|faq` for the n8n "LoopCasts assistant" (Groq `openai/gpt-oss-120b`; was Gemini); widget only when `Chat:Url` is set. FAQ/prompt in `n8n/knowledge/`, eval in `n8n/eval/` + `docs/ai-evaluation.md`. Only public data goes to the model |
| `Services/VatCalculator.cs` | Handbook VAT rules (BE 21%, EU private 21%, EU company with VAT nr 0% reverse charge, non-EU 0%) |
| `Services/MemberService.cs` | Customer logins: Identity user, role **Member**, no password → password-setup code |
| `Services/CustomerData.cs` | "Which rows belong to this e-mail?" in one place (used by /My and /Invoice) |
| `Services/TeamSeats.cs` | Team licence: buyer adds/removes colleagues on /My (`Plan.Seats`), seat = Account on the team order |
| `Services/OldDataImport.cs` | One-time import of the old orders sheet + invoice register (clean, report, idempotent); UI `/Admin/Import` |
| `Services/OldMemberImport.cs` | One-time import of the old website's `users` table (.sql): Account (plan on the account, no order) + Member login without password. Never reads the password column |
| `Services/WorkflowThumb.cs` | Course → workflow thumbnail geometry (pure, unit-tested); drawn by `Pages/Shared/_WorkflowThumb` |
| `Services/AccountExpiry.cs` | Deactivates accounts past `EndsOn`; `ExpireAccountsService` runs it at startup + every 24h |
| `Pages/` | Index, Courses, Pricing, Order (form), Pay (**mock** checkout), OrderThanks |
| `tests/LoopCastsWeb.Tests/` | xUnit tests; `TestDb.Create()` = SQLite in-memory `AppDbContext` |
| `.github/workflows/ci.yml` | build + test on every PR and on main |
| `.github/workflows/deploy.yml` | after green CI on main → Azure App Service (inactive until set up, `docs/deploy-azure.md`) |
| `docs/specs/`, `docs/agent-log.md` | specs per issue, log of agent work |
| `Pages/My`, `Pages/Invoice` | Logged-in customer: subscriptions + invoices; printable invoice (own only, Admin all) |
| `Pages/Shared/` | `_Layout`, `_Logo`, `_CourseCard`, `_WorkflowThumb`, `_PersonChip`, `_SupportLine`, `_Progress` (Order → Pay → Done), `_LoginPartial` (whole right side of the nav: links, account menu, phone sheet; keep the name, the Identity UI looks for it) |
| `Areas/Identity/Pages/Account/` | Our views for Login, Register, Forgot/Reset password, Access denied, Lockout, LoginWith2fa/RecoveryCode (Identity package PageModels); `_Flash` = status messages in our words; own `ConfirmEmailChange` PageModel (moves orders/seats to the new e-mail) |
| `Areas/Identity/Pages/Account/Manage/` | Account settings (`_ManageLayout`): Login and e-mail, Password, Two-factor login, Your data. No username/phone |
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
  also e-mailed via n8n (#5) when `N8n:WebhookUrl` is set. "Forgot password"/"Change e-mail" are sent by the site
  over SMTP (#27), so they work without n8n. `LoginEmailSender` has no SMTP login/TLS yet (needed on a real server, #8).
- Mollie (#3): active only when user-secret `Mollie:ApiKey` (test_…) is set, otherwise /Pay is the mock.
  `Mollie:WebhookBaseUrl` = public URL (tunnel/Azure); without it the status is checked when the customer returns.
- Team seats (#4): colleague setup link is shown to the buyer once, only for a NEW login (else takeover risk).
- `Customer` is not split out of `Order` yet (planned: `Order.CustomerId`, Customer = SCD Type 1).
- Invoice = printable page (browser → PDF); seller address/VAT nr in `appsettings.json` `Seller:*` (to fill in). Invoice e-mail + set-password link via n8n (#5, `N8n:WebhookUrl`); monthly re-billing not built.
- Old data (#7): imported invoices keep their old numbers/amounts. Credit notes are not modelled yet (report only).
- Old members (#20): `Account.OrderId` is null for imported members (`Account.PlanCode` holds the plan). Old team rows
  are single seats without a buyer, so they can't be managed on /My.
- Polish (#18): "Download my data" holds only the login data (Identity); orders/invoices are on /My. Deleting an account
  keeps orders and invoices (law); no anonymising yet. Authenticator setup shows the key + an otpauth link, no QR code (would need a package).
- Seller address + VAT number are not in the handbook (ch. 1–6) → `Seller:Address`/`Seller:VatNumber` still empty.
- Chat assistant (#22): model switched from Gemini (free tier 20 requests/day, 503s) to Groq (free, 1 000/day). In n8n,
  **Save is not Publish**: the site's chat runs the published version. Evaluation results in docs/ai-evaluation.md (#26).
- Later in the semester: newsletter unsubscribe.
- Password manager (Bitwarden/Vaultwarden) for `paswoorden.xlsx` is a recommendation, not code.
- Never commit the old LoopCasts Dropbox copy (passwords, customer e-mails) or `*.db` files.
