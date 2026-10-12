# n8n: e-mails and reminders for LoopCasts

n8n does the automation around the site: it sends the e-mails and the daily renewal reminder.
Locally it runs in Docker next to **Mailpit**, a fake mail server.

> **Mailpit is the only mail server.** Every e-mail n8n sends ends up in Mailpit's web page
> (http://localhost:8025). No real inbox can receive anything, so testing with real customer
> addresses is safe: nothing leaves your computer.

| What | Where |
|---|---|
| n8n editor | http://localhost:5678 |
| Mailpit (all sent e-mails) | http://localhost:8025 |
| SMTP for n8n | `mailpit:1025` (inside Docker) |
| The site, seen from n8n | `http://host.docker.internal:5180` |
| Workflows / credentials | `n8n/workflows/*.json`, `n8n/credentials/mailpit-smtp.json` (no secrets) |

## 1. First time
```bash
# a shared secret: the site sends it to n8n, n8n sends it back to the site's API
echo "LOOPCASTS_SECRET=$(openssl rand -hex 24)" > .env          # .env is git-ignored
dotnet user-secrets set "N8n:Secret" "<the same value>"          # copy it from .env, don't print it in class
dotnet user-secrets set "N8n:WebhookUrl" "http://localhost:5678/webhook/loopcasts"

docker compose up -d                                             # n8n + Mailpit
docker compose exec n8n n8n import:credentials --input=/loopcasts/credentials/mailpit-smtp.json
docker compose exec n8n n8n import:workflow --separate --input=/loopcasts/workflows
docker compose exec n8n n8n publish:workflow --id=lcEmailsFlow0001
docker compose exec n8n n8n publish:workflow --id=lcRenewalFlow001
docker compose restart n8n                                       # published workflows start after a restart
```
Then open http://localhost:5678 and create the n8n owner login (local only, your own e-mail + password).
Start the site as usual (`dotnet watch`, port 5180).

## 2. Every day
```bash
docker compose up -d      # start (data stays in the Docker volume n8n_data)
docker compose down       # stop
docker compose logs -f n8n
```
Changed a workflow JSON? Run the `import:workflow` + `publish:workflow` + `restart` lines again (same ids → updated, not doubled).
Changed it in the n8n editor? Export it back into the repo:
`docker compose exec n8n n8n export:workflow --id=lcRenewalFlow001 --pretty --output=/tmp/w.json` and copy it out with `docker compose cp`.

## 3. Workflow "LoopCasts e-mails" (`lcEmailsFlow0001`)
`Webhook POST /webhook/loopcasts` → **Secret OK?** (header `X-LoopCasts-Secret` = `LOOPCASTS_SECRET`, else ignored)
→ **Which event?** → *Invoice e-mail* or *Login e-mail* → Mailpit.

The site sends (`Services/N8nNotifier.cs`):
```jsonc
// after a payment (once per invoice)
{ "event": "invoice.created", "to": "ann@example.com", "name": "Ann Peeters", "invoiceNumber": "2026-0007",
  "description": "Yearly subscription", "total": 180.29,
  "invoiceUrl": "https://…/Invoice?number=2026-0007",
  "setPasswordUrl": "https://…/Identity/Account/ResetPassword?code=…" }   // null if the customer has a password

// any e-mail from the login system, already in our words ("Choose a new LoopCasts password", "Confirm your LoopCasts e-mail")
{ "event": "email", "to": "ann@example.com", "subject": "Choose a new LoopCasts password", "html": "…" }
```
Invoice e-mail text (in the workflow, loopcasts-ui §6): "Hi Ann, Thanks for your order. Your invoice 2026-0007 for
Yearly subscription is ready: €180.29 incl. VAT, already paid." + "View or print your invoice" (+ "Your login is ready too.
Choose your password first; the link works for 24 hours." for a new customer).

## 4. Workflow "Renewal reminder" (`lcRenewalFlow001`)
`Every day at 08:00` (Ghent time; or **Run now (test)** in the editor)
→ `GET {LOOPCASTS_SITE_URL}/api/automation/expiring?days=14` → one **Reminder e-mail** per account
→ `POST {LOOPCASTS_SITE_URL}/api/automation/reminded` `{"accountId":5,"endsOn":"2026-10-20"}`.

The site's API (`Api/AutomationApi.cs`, `Services/RenewalReminders.cs`):
- Both endpoints need the header `X-LoopCasts-Secret` with the site's `N8n:Secret` → otherwise **401**. No secret set = always 401.
- `expiring`: active accounts ending between today and today + days (1–60), not reminded for that end date yet.
  Team colleagues are left out (only the buyer can renew); lifetime accounts never end.
  Each item: `accountId, email, name, plan, endsOn, endsOnText, renewUrl`.
- `reminded`: stores the end date in `Account.RemindedFor` → the same reminder is never sent twice. After a renewal the
  end date changes, so the next period gets its own reminder.

Test it now instead of waiting for 08:00:
```bash
docker compose exec -e N8N_RUNNERS_BROKER_PORT=5690 n8n n8n execute --id lcRenewalFlow001
```
(the extra port: the running n8n already uses the default one). Run it twice: the second run sends nothing.

## 5. On a server (later)
Same two workflows on a hosted n8n; replace the Mailpit credential with a real SMTP account, set `LOOPCASTS_SITE_URL`
to the public site and `N8n:WebhookUrl` to the public n8n webhook. Without `N8n:WebhookUrl` the site still works:
nothing is sent and the set-password link stays on the OrderThanks page.

Optional: an *HTML → PDF* node (e.g. Gotenberg) on `invoiceUrl` to attach a PDF. The link is enough for the prototype.
