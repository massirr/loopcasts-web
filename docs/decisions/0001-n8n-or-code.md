# ADR 0001: n8n or code?

Status: accepted (15/10/2026). Trigger: teacher feedback on #21 ("security-critical flows must not depend on n8n").

## The rule
Ask the questions in this order. The first "yes" decides.

| # | Question | Yes → |
|---|---|---|
| 1 | Does it touch **money** (price, payment, VAT, invoice number)? | code |
| 2 | Does it change **data** the site relies on (orders, accounts, logins)? | code |
| 3 | Is it **security** (logins, passwords, access)? | code |
| 4 | Must it **always work**, even when n8n is down or misconfigured? | code |
| 5 | Does it talk to an **outside service** (mail, AI model, later Slack/CRM)? | n8n |
| 6 | Does it run on a **schedule** and only notify? | n8n |
| 7 | Is it a **notification** or a **text the business wants to change** without a developer? | n8n |
| 8 | Is it **AI** (chat, summaries)? | n8n |

Code is tested in CI (xUnit), versioned with the site and runs on every request. n8n is easy to change and to show,
but it is one more server that can be stopped, and a workflow change goes live without a test. So n8n only
*informs* people; the site *decides*. When n8n needs data, it asks the site's API, and the site checks the secret
and does the bookkeeping (e.g. `reminded`).

## All automations
| Automation | Runs in | Why (rule #) |
|---|---|---|
| Order → paid → invoice (number, VAT) → account, in one save (`PaymentService`) | code | 1, 2 |
| Mollie payment + webhook + status check (`MollieService`) | code | 1 (Mollie is outside, but the money rule wins) |
| VAT rules (`VatCalculator`) | code | 1 |
| Customer login without password + setup code (`MemberService`) | code | 2, 3 |
| Team seats (`TeamSeats`) | code | 2, 3 |
| Accounts past `EndsOn` switched off, every 24 h (`AccountExpiry`) | code | 2, 3: a schedule, but it decides access |
| Imports: course CSV, old orders + invoices, old members | code | 2 |
| Admin role + admin logins at startup (`AdminSeeder`) | code | 3 |
| **Login e-mails: "Forgot password?", "Change e-mail" (`LoginEmailSender`)** | **code (since #27)** | 3, 4 |
| Invoice e-mail after payment (`invoice.created` → "LoopCasts e-mails") | n8n | 5, 7: the invoice itself is made in code; the mail only informs |
| Renewal reminder, daily 08:00 ("Renewal reminder") | n8n | 5, 6, 7: the site's API decides *who* (`/api/automation/expiring`) |
| Chat assistant ("LoopCasts assistant") | n8n | 5, 8: only public read-only data (`/api/public/*`) |

## Example: login e-mails (#27)
Before, Identity's "Forgot password?" mail went site → n8n webhook → Mailpit. If n8n was stopped, the customer saw
"Check your mailbox" and nothing ever arrived: locked out, and only support could help. That is rule 3 and 4, so it
now goes site → SMTP directly (`System.Net.Mail`, no package). The wording ("Choose a new LoopCasts password") lives in
`Services/LoginEmailSender.cs`. When SMTP is down the site logs it, and a logged-in user sees "We couldn't send the
e-mail just now". The invoice e-mail stayed in n8n: if it is late, nobody is locked out, because the invoice is
already on /My.

## Consequences
- Two places send mail: the site (login e-mails) and n8n (invoice, reminder). Both use the same SMTP server
  (locally Mailpit), so there is still one inbox to check.
- On a server the site needs its own SMTP settings (`Smtp:*` as App Service settings), next to n8n's credential.
- A new automation is placed with the table above. If it's unclear, it goes in code.
