# n8n: e-mails and reminders for LoopCasts

n8n does the automation around the site: the invoice e-mail, the daily renewal reminder and the chat assistant.
Locally it runs in Docker next to **Mailpit**, a fake mail server.
Login e-mails ("Forgot password?", "Change e-mail") are **not** in n8n: the site sends them itself over SMTP, so
nobody gets locked out when n8n is down (#27). What goes where, and why: [ADR 0001](decisions/0001-n8n-or-code.md).

> **Mailpit is the only mail server.** Every e-mail n8n and the site send ends up in Mailpit's web page
> (http://localhost:8025). No real inbox can receive anything, so testing with real customer
> addresses is safe: nothing leaves your computer.

| What | Where |
|---|---|
| n8n editor | http://localhost:5678 |
| Mailpit (all sent e-mails) | http://localhost:8025 |
| SMTP for n8n | `mailpit:1025` (inside Docker) |
| SMTP for the site (login e-mails) | `localhost:1025` (`Smtp:*` in appsettings.Development.json) |
| The site, seen from n8n | `http://host.docker.internal:5180` (`LOOPCASTS_SITE_URL`) |
| The site, seen by a visitor (chat links) | `http://localhost:5180` (`LOOPCASTS_PUBLIC_URL`; on a server: the public address = the site's `App:BaseUrl`) |
| Workflows / credentials | `n8n/workflows/*.json`, `n8n/credentials/*.json` (no secrets: the Groq key is added in the UI) |
| Chat assistant (FAQ, prompt, tests) | `n8n/knowledge/`, `n8n/eval/`, `docs/ai-evaluation.md` |

## 1. First time
```bash
# a shared secret: the site sends it to n8n, n8n sends it back to the site's API
echo "LOOPCASTS_SECRET=$(openssl rand -hex 24)" > .env          # .env is git-ignored
dotnet user-secrets set "N8n:Secret" "<the same value>"          # copy it from .env, don't print it in class
dotnet user-secrets set "N8n:WebhookUrl" "http://localhost:5678/webhook/loopcasts"

docker compose up -d                                             # n8n + Mailpit
docker compose exec n8n n8n import:credentials --separate --input=/loopcasts/credentials   # Mailpit + an empty "Groq"
docker compose exec n8n n8n import:workflow --separate --input=/loopcasts/workflows
docker compose exec n8n n8n publish:workflow --id=lcEmailsFlow0001
docker compose exec n8n n8n publish:workflow --id=lcRenewalFlow001
docker compose exec n8n n8n publish:workflow --id=lcAssistant00001
docker compose restart n8n                                       # published workflows start after a restart
```
Then open http://localhost:5678 and create the n8n owner login (local only, your own e-mail + password).
For the chat assistant: Credentials → **Groq** → paste your API key (https://console.groq.com/keys) → Save, and
`dotnet user-secrets set "Chat:Url" "http://localhost:5678/webhook/6f0d3c1e-2b7a-4c8e-9a51-1c2d3e4f5a60/chat"`.
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
→ **Which event?** → `invoice.created`: *Invoice e-mail* → Mailpit; any other event: *Other event: ignore*.

The site sends (`Services/N8nNotifier.cs`):
```jsonc
// after a payment (once per invoice)
{ "event": "invoice.created", "to": "ann@example.com", "name": "Ann Peeters", "invoiceNumber": "2026-0007",
  "description": "Yearly subscription", "total": 180.29,
  "invoiceUrl": "https://…/Invoice?number=2026-0007",
  "setPasswordUrl": "https://…/Identity/Account/ResetPassword?code=…" }   // null if the customer has a password
```
(Until #27 there was also an `email` event for the login e-mails. They now go out from the site:
`Services/LoginEmailSender.cs`. Test it with n8n stopped: `docker compose stop n8n`, "Forgot password?", look in Mailpit.)
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

## 5. Workflow "LoopCasts assistant" (`lcAssistant00001`): the chat on the site
`Chat on the website` (Chat Trigger, public, embedded, only http://localhost:5180 may embed it)
→ **LoopCasts assistant** (AI Agent, rules in `n8n/knowledge/system-prompt.md`)
with **Model (swap here)** (Groq `openai/gpt-oss-120b`, temperature 0.2, max 2000 tokens, retry 3× on failure,
credential "Groq"), **Chat memory (per session)**
(last 10 messages of that chat) and three tools that call the site's read-only public API:
`courses` → `/api/public/courses`, `plans` → `/api/public/plans`, `faq` → `/api/public/faq` (= `n8n/knowledge/faq.md`).
→ **Check answer** (Code node, #29), the safety net after the AI:
- no answer (the model call failed: bad key, quota, timeout; the agent is set to *continue on error*) → "Sorry, the
  assistant isn't available right now. See courses or prices, or mail support@…";
- JSON or a code block → "Sorry, I couldn't answer that properly. See courses or prices, or mail support@…";
- a bare `/Pricing`, `/Order?plan=…`, `/Identity/…` path, a relative link or a link to another host → a markdown link
  on `LOOPCASTS_PUBLIC_URL`.

The Chat Trigger answers with the output of the **last node** (Respond: *When Last Node Finishes*), so the visitor
gets the checked text, not the agent's raw answer. Every URL the site's API gives is already full (`App:BaseUrl`):
`url`, `pricingUrl`, `orderUrl`, and the paths in the FAQ. Test the node without n8n: `node n8n/eval/check-answer.test.mjs`
(also runs in CI).

The site shows the n8n chat widget bottom-right (`Pages/Shared/_Chat.cshtml`) only when `Chat:Url` is set, with
"Answers by an AI assistant" under the input. Another site address? Change *Allowed origins* in the Chat Trigger
(n8n doesn't accept an expression there). Changed the prompt? Edit `system-prompt.md` **and** the agent's system message
(a unit test checks they are equal). Test questions and results: `docs/ai-evaluation.md`.

### When the model says "busy" (503) or "quota exceeded" (429)
- **503 high demand**: Google is overloaded for a moment. Node *Model (swap here)* → Settings → **Retry On Fail** on,
  Max Tries 3, Wait Between Tries 5000 ms. Then the visitor gets an answer instead of an error.
- **429 quota exceeded**: the free daily limit is used up (Gemini free: 20 requests/day per model; one chat answer =
  2–3 requests). Retrying doesn't help; switch to a model with a bigger free limit (below).
- Changed the model in the editor? **Save is not enough: click Publish.** The chat on the site runs the *published*
  version; Save only changes the draft. (From the repo: `import:workflow` → `publish:workflow` → `restart`, as in §1.)
- Imported a workflow from the command line? **Reload the editor tab** (Cmd+R) before you touch it. An open tab keeps
  the old version, and Save there overwrites the import without a warning. (n8n keeps every saved version, so nothing is lost: it can be restored from the workflow history.)

### Why Groq (since 12/10/2026; before: Google Gemini)
Gemini's free tier allowed 20 requests a day per model and was often "busy" (503): the first evaluation got only 3 of
15 answers. Groq's free plan: `openai/gpt-oss-120b` with 30 requests/min and 1 000/day, fast, supports tool calling,
doesn't train on API data. No credit card.
Max tokens is 2000, not 700: gpt-oss is a reasoning model and its hidden reasoning counts toward the limit; 2000 is a
ceiling, a short answer costs the same.

### Swapping the model (any provider)
1. Make a credential for the provider (n8n → Credentials → Add → e.g. **Groq**, **Google Gemini(PaLM) Api**, **Azure OpenAI**).
2. In the workflow: delete *Model (swap here)*, add the provider's **Chat Model** node, pick the model, Options →
   temperature 0.2; connect it to the agent's *Chat Model* input; name it *Model (swap here)*; Settings → Retry On Fail.
   **Save, then Publish.**
3. Export it to the repo (§2): keep the credential reference pointing to the key-less placeholder in
   `n8n/credentials/` (every n8n has its own credential ids). Run `node n8n/eval/run-eval.mjs <chat URL>` again.
Watch the free limit of 8 000 tokens per minute: each answer uses roughly 2 000–4 000 tokens (prompt ~500 + FAQ ~1 000 + chat history), so a fast burst of
questions can hit it. For the evaluation that is fine (7 s between questions); for a busy site, use a paid plan or Azure.

A local model (Ollama) is free and private, but this laptop (M1, 8 GB) only runs small models that follow the rules and
call tools poorly; not recommended for the demo.

### Switching the model to Azure AI Foundry later
The model is one node, so nothing else changes:
1. Azure portal → **Azure AI Foundry** → create a project (region West Europe or Sweden Central) → *Models + endpoints*
   → deploy e.g. `gpt-4.1-mini`. Note the resource name, the deployment name and a key.
2. n8n → Credentials → **Azure OpenAI** (`azureOpenAiApi`): resource name, API key, API version (as shown in Foundry).
3. In the workflow: delete *Model (swap here)*, add **Azure OpenAI Chat Model**, model = your deployment name,
   temperature 0.2, connect it to the agent's *Chat Model* input, save and publish.
4. Run `node n8n/eval/run-eval.mjs <chat URL>` again and compare with the Groq results.
Why: with Azure the prompts are not used to train models and stay in the EU region you choose (see docs/ai-evaluation.md).

## 6. On a server (later)
Same workflows on a hosted n8n; replace the Mailpit credential with a real SMTP account, set `LOOPCASTS_SITE_URL`
to the public site and `N8n:WebhookUrl` to the public n8n webhook. Without `N8n:WebhookUrl` the site still works:
no invoice e-mail is sent and the set-password link stays on the OrderThanks page.
The site needs its own `Smtp:Host`/`Smtp:Port`/`Smtp:From` (App Service settings) for the login e-mails; a real
server also needs a login + TLS, which `LoginEmailSender` doesn't do yet.

Optional: an *HTML → PDF* node (e.g. Gotenberg) on `invoiceUrl` to attach a PDF. The link is enough for the prototype.

## 7. Pitfalls (learned the hard way)
- Docker Desktop + external disk: a stopped container with a bind mount on /Volumes may refuse to start again
  ("file exists"). Restart Docker Desktop.
- Changed a workflow in the editor? Export it to the repo (§2), or the repo and n8n drift apart.
- Credential ids differ per n8n install: the repo JSON points to the key-less placeholders in `n8n/credentials/`;
  never import those placeholders over a credential that already has a key.
- Free-tier limits change often: check the provider's page before blaming the workflow.
- Save vs Publish, reloading the editor tab, Groq's tokens/minute: §5. The two site addresses: the table at the top.
