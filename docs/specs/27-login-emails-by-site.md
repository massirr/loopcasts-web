# Spec: Login e-mails are sent by the site, not n8n  (#27)

## User story
As a customer, I want the "Forgot password?" and "Change e-mail" mails to be sent by the site itself, so that I can
always get back into my account, even when n8n is down (teacher feedback: security-critical flows must not depend on n8n).

## Acceptance criteria
- [x] Given n8n is stopped, when I ask for a new password, then "Choose a new LoopCasts password" arrives in Mailpit.
- [x] Given SMTP is unreachable, then the site logs it, shows a clear message and never crashes.
- [x] Login e-mail wording in one place (`Services/LoginEmailSender.cs`), Identity's link kept.
- [x] Invoice e-mail + renewal reminder stay in n8n; the `email` branch is gone from "LoopCasts e-mails" (JSON + docs).
- [x] `Smtp:Host` / `Smtp:Port` / `Smtp:From` in appsettings.Development.json; `System.Net.Mail`, no new package.
- [x] Unit tests with a fake SMTP server.

## Design
- Data/model changes: none.
- Services: new `LoginEmailSender` = Identity's `IEmailSender` (SMTP, 10 s timeout). The "our words" table moves there
  from `N8nNotifier`, which now only sends `invoice.created`.
- Pages: Identity's PageModels are in the package and don't catch exceptions, so the sender never throws. It logs and
  sets TempData `LoginEmailNotSent`; `_Flash` shows "We couldn't send the e-mail just now…" instead of "We sent a link".
- n8n: "Which event?" output 1 → *Other event: ignore* (was *Login e-mail*).
- docker-compose: Mailpit's SMTP `127.0.0.1:1025` is already published to the host (the site uses it, n8n uses `mailpit:1025`).
- Security:
  - The "couldn't send" message is shown **only to a logged-in user** (Change e-mail). On "Forgot password?" it would
    appear only for addresses we know, so during an SMTP outage a stranger could test who is a customer. That page
    always says what to do when nothing arrives.
  - No password needed for Mailpit. A real SMTP server (later, with #8) needs `Smtp:User`/`Smtp:Password` + TLS as
    App Service settings: not built yet.
  - The log line has the subject, not the address.

## Out of scope
- Real SMTP account / TLS / login on a server (#8).
- Moving the invoice e-mail out of n8n (it is a notification; a payment never waits for it).

## Test plan
- Unit: fake SMTP server receives the reset mail (to + subject); our words keep the link; unknown mail unchanged;
  SMTP down → no exception, flag only when logged in; `Smtp:Host` empty → no exception.
- Manual: `docker compose stop n8n`, "Forgot password?" → mail in Mailpit (http://localhost:8025).
  `docker compose stop mailpit`, change e-mail → error message, site keeps working.
