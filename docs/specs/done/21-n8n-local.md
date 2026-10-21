# Spec: n8n locally, with e-mails and a renewal reminder  (#21)

## User story
As a customer, I want an e-mail a few days before my access ends, so that I can renew in time, and Lotte doesn't have
to check end dates and write reminders by hand.

## Acceptance criteria
- [x] `docker compose up -d` → n8n (5678, named volume `n8n_data`) + Mailpit (SMTP 1025, UI 8025); n8n reaches the site
      at `host.docker.internal:5180`. Ports bound to 127.0.0.1 only.
- [x] "LoopCasts e-mails": the site's webhook events (`invoice.created`, `email`) → Send Email via Mailpit SMTP.
- [x] "Renewal reminder": daily 08:00 → `GET /api/automation/expiring?days=14` → one e-mail per account →
      `POST /api/automation/reminded`; never the same reminder twice.
- [x] Both endpoints only answer with the right `X-LoopCasts-Secret` (= `N8n:Secret`), tested.
- [x] docs/n8n.md: start/stop/import, and Mailpit is the only mail server.
- [x] Both workflows run end to end; e-mails arrive in Mailpit (screenshots `docs/design/screenshots/n8n-*.png`).

## Design
- Data (migration `AccountRemindedFor`): `Account.RemindedFor` = the end date a reminder was sent for. Same end date →
  no second reminder; renewed (new end date) → a new reminder later.
- `Services/RenewalReminders.cs`: `DueAsync` (active, end date within N days, not reminded for it; team colleagues left
  out; plan from the account or the order; first name) and `MarkAsync`.
- `Api/AutomationApi.cs`: minimal API group `/api/automation`; constant-time secret compare; no secret configured =
  closed (401). Static handlers, so the tests call them without a test server (no new package).
- Docker: `docker-compose.yml` pins n8n 2.42.7 and Mailpit v1.31.5. `LOOPCASTS_SECRET` from the git-ignored `.env`
  (compose refuses to start without it); `LOOPCASTS_SITE_URL` defaults to `http://host.docker.internal:5180`.
- Workflows in `n8n/workflows/` with fixed ids (re-import updates them), imported with `n8n import:workflow`, then
  `publish:workflow`. SMTP credential `n8n/credentials/mailpit-smtp.json` (host `mailpit`, no password).
  The webhook checks the secret in an IF node instead of n8n "Header Auth" (no secret stored in a credential file).
- Fix found in the end-to-end run: Identity 10's subject is "Reset your password" (not "Reset Password"), so the #18
  rewrite never fired. `N8nNotifier` now recognises Identity's mails by their body text.

## Out of scope
- Real SMTP and hosted n8n (docs/n8n.md §5). Automatic monthly re-billing.
- n8n owner login: created by Koze in the n8n UI (local account).

## Test plan
- Unit: `RenewalRemindersTests` (due list, buyers only, soonest first, mark twice, renewal → again, 401 for missing/
  wrong/partial secret and when no secret is configured, 204/404). `N8nNotifierTests` (both Identity subjects). 143 tests.
- End to end (scratch DB, site on 5190 via `LOOPCASTS_SITE_URL`): order + mock payment → "Your LoopCasts invoice …";
  Forgot password → "Choose a new LoopCasts password"; wrong secret → ignored; made-up members imported via #20 →
  `n8n execute` → reminders to the two accounts ending within 14 days only; second run → nothing new.
