# LoopCastsWeb — notes for Claude Code

School prototype (Automation Tools, Thomas More) for the fictional company **LoopCasts BV**
(video training subscriptions). Goal: **remove manual steps** from their order → invoice → account
process, as the teacher asked. Built by Koze, who is **learning ASP.NET**.

## How to work with Koze
- He is learning: explain each change in 1–2 sentences, compare to WPF/MVVM when useful.
- Prefer guiding over doing: for small learning pieces, give hints/TODOs and let him write the code.
- Concise and visual (tables, small diagrams). His input is partly voice-typed.
- Ask before big changes (architecture, new packages, deleting files or migrations).

## Stack
- .NET 10, ASP.NET Core **Razor Pages**, Bootstrap 5 (from the template)
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
| `Services/VatCalculator.cs` | Handbook VAT rules (BE 21%, EU private 21%, EU company with VAT nr 0% reverse charge, non-EU 0%) |
| `Services/AccountExpiry.cs` | Deactivates accounts past `EndsOn`; `ExpireAccountsService` runs it at startup + every 24h |
| `Pages/` | Index, Courses, Pricing, Order (form), Pay (**mock** checkout), OrderThanks |
| `Pages/Admin/` | Orders (mark paid = fallback for bank transfers), Invoices & accounts. Folder locked by `AdminOnly` policy |

## Flow
Pricing → `/Order?plan=…` (validation, company fields only for companies, team licence = company only)
→ `/Pay?id=` (mock payment) → `PaymentService.MarkPaidAsync` → `/OrderThanks` (invoice nr + access end date).

## Design rules (keep these)
- **Never trust the browser**: price comes from `Plans`, plan/country are checked server-side.
- **Snapshots**: `Order.Price`, `Order.CreatedAt` and all `Invoice.Billing*`/amount fields are copies at that moment and never change. Live data (`Plans`, later `Customer`) may change.
- **One place for "order paid"**: always call `PaymentService.MarkPaidAsync` (mock checkout, admin button, future Mollie webhook). It is idempotent.
- **No secrets in code or appsettings** — use `dotnet user-secrets`. Never print password values.
- Money is `decimal` (stored as TEXT in SQLite). Dates without time are `DateOnly`. Times are UTC.
- After every model change: `dotnet ef migrations add <Name>`; `Program.cs` applies migrations at startup.
- Post-Redirect-Get after every POST.

## Known limitations / next steps
- **Customer login (planned right after the class demo):** payment creates an `Account` row but no
  Identity user, so customers can't log in. Plan: in `PaymentService` also create an `IdentityUser`
  with role "Member" and no password → password-setup link (`GeneratePasswordResetTokenAsync`), shown on
  OrderThanks for now, later e-mailed via n8n → `/My` page (subscription, end date, invoices).
  Staff already have `/Identity/Account/Manage` (linked from the e-mail in the navbar).
- Payment is a mock; real version = Mollie test mode + webhook (needs a public URL, e.g. a tunnel).
- Team licence creates only the buyer's seat; the other 4 seats are not built yet.
- `Customer` is not split out of `Order` yet (planned: `Order.CustomerId`, Customer = SCD Type 1).
- No invoice PDF/e-mail yet; monthly re-billing not built.
- Later in the semester: chatbot (AI component), n8n workflows/webhooks, newsletter unsubscribe.
- Password manager (Bitwarden/Vaultwarden) for `paswoorden.xlsx` is a recommendation, not code.
- Never commit the old LoopCasts Dropbox copy (passwords, customer e-mails) or `*.db` files.
