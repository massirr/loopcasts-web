# Architecture: where things live and why

Read this when you need to find code or understand a rule. Not loaded by default (see CLAUDE.md).

## Map
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
| `Services/VatCalculator.cs` | Handbook VAT rules (BE 21%, EU private 21%, EU company with VIES-valid VAT nr 0% reverse charge, non-EU 0%) |
| `Services/VatNumber.cs`, `Services/ViesClient.cs` | VAT number format + BE checksum; EU VIES check (`Vies:BaseUrl`, test service in Development). VIES down → 21% + "not verified" (ADR 0002, #33) |
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
| `docs/specs/`, `docs/log/` | active spec (merged ones in `docs/specs/done/`); agent log per month (append only) |
| `Pages/My`, `Pages/Invoice` | Logged-in customer: subscriptions + invoices; printable invoice (own only, Admin all) |
| `Pages/Shared/` | `_Layout`, `_Logo`, `_CourseCard`, `_WorkflowThumb`, `_PersonChip`, `_SupportLine`, `_Progress` (Order → Pay → Done), `_LoginPartial` (whole right side of the nav: links, account menu, phone sheet; keep the name, the Identity UI looks for it) |
| `Areas/Identity/Pages/Account/` | Our views for Login, Register, Forgot/Reset password, Access denied, Lockout, LoginWith2fa/RecoveryCode (Identity package PageModels); `_Flash` = status messages in our words; own `ConfirmEmailChange` PageModel (moves orders/seats to the new e-mail) |
| `Areas/Identity/Pages/Account/Manage/` | Account settings (`_ManageLayout`): Login and e-mail, Password, Two-factor login, Your data. No username/phone |
| `Pages/Admin/` | Orders (mark paid = fallback for bank transfers), Invoices & accounts. Folder locked by `AdminOnly` policy |

## Flow
Pricing → `/Order?plan=…` (validation, VAT number check, company fields only for companies, team licence = company only)
→ `/Pay?id=&key=` (mock payment, or Mollie when configured) → `PaymentService.MarkPaidAsync` → `/OrderThanks?id=&key=`
(invoice nr, access end date, "Set your password" link on the e-mail's first paid order).
`key` = `Order.AccessKey` (random): order ids are guessable, so Pay/OrderThanks always check `Order.Opens(key)`.

## Design rules
- **Snapshots**: `Order.Price`, `Order.CreatedAt`, all `Invoice.Billing*`/amount/VAT-check fields are copies at that
  moment and never change. Live data (`Plans`, later `Customer`) may change.
- **One place for "order paid"**: always `PaymentService.MarkPaidAsync` (mock checkout, admin button, Mollie). Idempotent.
- **No proof, no 0% VAT**: `VatCalculator` gives reverse charge only with `VatCheck.Valid` (ADR 0002).
- Money is `decimal` (TEXT in SQLite). Dates without time are `DateOnly`. Times are UTC.
- `Program.cs` applies migrations at startup. Post-Redirect-Get after every POST.
- Where an automation runs (code or n8n): ADR 0001.

## Known limits (by area)
- **Login e-mails** (#27): `LoginEmailSender` has no SMTP login/TLS yet (needed on a real server, #8).
  Set-password link: on OrderThanks for the first paid order only, and e-mailed via n8n when `N8n:WebhookUrl` is set.
- **Mollie** (#3): active only with user-secret `Mollie:ApiKey` (test_…), else /Pay is the mock.
  Without `Mollie:WebhookBaseUrl` (public URL) the status is checked when the customer returns.
- **VAT check** (#33): "not verified" orders are not re-checked automatically; no credit notes yet.
- **Team seats** (#4): colleague setup link shown to the buyer once, only for a NEW login (else takeover risk).
  Imported old team rows (#20) are single seats without a buyer: not manageable on /My.
- **Customer** is not split out of `Order` yet (planned: `Order.CustomerId`, SCD Type 1).
- **Invoice** = printable page (browser → PDF). `Seller:Address`/`Seller:VatNumber` empty: not in the handbook.
  Monthly re-billing not built. Imported invoices (#7) keep old numbers/amounts; credit notes not modelled.
- **Privacy** (#18): "Download my data" = login data only; deleting an account keeps orders/invoices (law), no
  anonymising. Authenticator setup shows key + otpauth link, no QR code (would need a package).
- **Imported members** (#20): `Account.OrderId` is null, `Account.PlanCode` holds the plan.
- Not code: newsletter unsubscribe (later), password manager for `paswoorden.xlsx` (recommendation).
