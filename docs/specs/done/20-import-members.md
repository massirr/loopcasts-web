# Spec: Import the old members database  (#20)

## User story
As LoopCasts staff, I want to import the members table of the old website into the new site, so that every existing
member keeps access and gets a login, without Lotte copying rows from phpMyAdmin by hand (handbook ch. 3).

## Acceptance criteria
- [x] Upload the users table `.sql` on /Admin/Import → preview (counts per product, expired, report); nothing saved.
- [x] Import → every new e-mail gets an Account (product → plan) + a Member login **without password**; nobody is e-mailed.
- [x] E-mails that already have an account (imported orders #7, new orders) are skipped and counted.
- [x] List "active in the old system but end date passed" with the yearly value at today's prices, for Lotte.
- [x] The password (MD5) column is never read into memory, stored, shown or logged.

## Source (profiled without reading the password column)
phpMyAdmin dump `users (id, email, password md5, product, start_date, end_date, active)`, 1 961 rows.
Products `yearly`/`monthly`/`team`/`single` (= our plan codes). Dates ISO. Monthly rows have no end date (rolling),
single rows mostly none (lifetime). 20 e-mails appear more than once (mostly team rows). The old site has one row
**per team seat**.

## Design
- Model (migration `AccountsWithoutOrder`): `Account.OrderId` → `int?`, new `Account.PlanCode` (only for imported members).
  An old member has no order in our system; a fake "paid" order per member would pollute Admin → Orders with ~1 900
  invented prices. `CustomerData.SubscriptionsAsync` reads the plan from the account, else from the order.
- `Services/OldMemberImport.cs`:
  - `ReadUsersSql`: finds `INSERT INTO users (...) VALUES` by column **name**; any column named like
    pass/hash/salt/token is walked over character by character without being copied (it never becomes a string).
  - `Parse`: clean e-mail (`OldDataImport.CleanEmail`), product → plan, dates. End date: single = lifetime (old end
    ignored), active monthly without end = next monthly renewal on/after today (`NextRenewal`), other missing end =
    start + plan months. Duplicates: keep the active row with the latest end date, report the other.
    `IsActive` = active in the old system and end date not passed.
  - `ActiveButEnded` + `YearlyValue` (monthly × 12, yearly, team / 5 seats, single 0).
  - `ToCsv`/`ParseCsv`: Preview → Import carries `id;email;product;start_date;end_date;active` in a hidden field,
    never the `.sql`. Import parses that again on the server.
  - `ApplyAsync`: one transaction; skip e-mails with an account; Account + `MemberService.EnsureMemberAsync`
    (Member role, no password). No n8n events.
- Page: `/Admin/Import` gets a third card "Old website members" with handlers `PreviewMembers` / `ImportMembers`.
- Security: admin only (`/Admin` folder policy); 1 MB upload limit; no logging of rows; the dump stays outside the repo
  (tests use a made-up fixture).

## Result on the real export (scratch database, 12/10/2026)
1 940 members (342 monthly, 1 116 yearly, 315 team, 167 single; 21 duplicate rows skipped), 1 026 expired.
313 active in the old system with a passed end date: €42 928.60 a year at today's prices.
Import 10 s; second run: 0 imported, 1 940 skipped. No hash in the hidden field or the app log.

## Out of scope
- Grouping old team seats into one team licence (no buyer in the old data; seats can't be managed on /My).
- Inviting members by e-mail (members use "Forgot password"; a mailing can come later via n8n).

## Test plan
- Unit (`OldMemberImportTests`, 10): password never comes out (also a hash with quotes/commas/`)`), cleaning +
  dedupe, end dates, `NextRenewal` (incl. short month), overdue list + yearly value, CSV round trip,
  `ApplyAsync` (accounts, logins without password, skip existing, idempotent, no e-mails, /My shows the plan).
- Manual: real export on a scratch DB via headless Chrome (numbers above).
