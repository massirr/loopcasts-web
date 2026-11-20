# Login e-mails work on the live site  (#64)
Status: done · Area: accounts · Tests: unit + full suite

## Goal
The live site sends no e-mail, so "Forgot password?" leaves a customer locked out until support helps. The site
will log in to a hosted SMTP server over TLS; for the demo that is the Mailtrap sandbox (a hosted Mailpit).
As a customer of the live site, I want the "Forgot password?" and "Change e-mail" mails to be sent,
so that I can get back into my account without mailing support.

## Acceptance criteria
1. [x] Given SMTP settings with a username, when a login e-mail is sent, then the site logs in over TLS
   (STARTTLS), never in plain text.
2. [x] Given SMTP settings without a username (local Mailpit), then it works as before (no login, no TLS).
3. [x] Given SMTP down or a wrong login, then the existing behaviour stays: logged, no error page, flag only
   for a logged-in user.
4. [x] No SMTP password in code, appsettings, logs or the repo: only user-secrets / App Service settings.
5. [x] Docs say how to set `Smtp__*` on Azure for the Mailtrap sandbox.
6. [x] Tests cover 1–3.

## Affected code
- `Services/LoginEmailSender.cs`: `Client()`: `Smtp:User` / `Smtp:Password` → credentials + TLS
- `docs/deploy-azure.md` (Mailtrap `Smtp__*` settings), `docs/architecture.md` Known limits
- Migration: none
- Security: password only from config; TLS whenever a password is sent

## Out of scope
- n8n hosting (invoice + reminder mails live), Tailscale Funnel fallback, newsletter, a real mail provider.

## Reviews
- Reviewer (business): 1, 2, 5 met by `Client()` + 2 tests + docs; 3 by the existing SMTP-down test (wrong login
  hits the same `catch`). Out of scope respected. Left: no dedicated wrong-login test.
- Reviewer (security): password only from config, not in code/docs/appsettings/logs; credentials and STARTTLS set
  together → criteria 1, 4. Left: CLI example puts the password in shell history (doc note added: or use Portal).
- Reviewer (UX): no UI change; "not sent" flag and Forgot-password behaviour untouched. Left: none.

## Notes
- Decisions by Koze (20/11/2026):
  - Live = Mailtrap Email Testing sandbox: every mail (also to fake demo users) lands in its inbox, nobody real
    gets mail, no bounces. Real provider later = settings only.
  - Invoice/reminder mails: host n8n as the main path (later issue); Tailscale Funnel to the laptop as fallback.
- Tests: `LoginEmailSenderTests` (+3: `WithUser_LogsInOverTls`, `WithoutUser_NoLoginNoTls` x2); suite 235 green.
- Manual check with Mailtrap: needs Koze's Mailtrap account (not done by the agent).

## Manual check
- Local, no `Smtp:User`: `http://localhost:5180/Identity/Account/ForgotPassword` → own test account → mail in
  Mailpit (http://localhost:8025) as before.
- Local with Mailtrap: `dotnet user-secrets set` `Smtp:Host`/`Port`/`User`/`Password` (Mailtrap values) → same
  page → mail in the Mailtrap inbox. Remove them again afterwards to go back to Mailpit.
- Live: App Service settings `Smtp__*` → same page on https://loopcasts-koze.azurewebsites.net → Mailtrap inbox.
