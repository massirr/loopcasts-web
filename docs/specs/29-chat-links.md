# Spec: Chatbot answers: clickable links, no JSON, review fixes  (#29)

## User story
As a visitor using the chat on the website, I want the assistant's answers to be plain sentences with links I can
click, so that I get straight to the right page instead of copying paths or reading JSON.

## Acceptance criteria
- [x] Links are markdown `[text](url)` with the public site address (`App:BaseUrl` on the site,
      `LOOPCASTS_PUBLIC_URL` in n8n), never `host.docker.internal`, never invented.
- [x] A bare `/Courses`, `/Pricing`, `/Order?plan=…`, `/Identity/…` path in an answer becomes a link (n8n "Check answer").
- [x] An answer that looks like JSON or a code block is replaced by a short friendly sentence with links + support@.
- [x] Model call fails (bad key, quota, timeout) → friendly message, never a blank chat.
- [x] Review fixes from docs/ai-evaluation.md (VAT wording, off-topic, card details, "AI assistant" only when asked, q9 check).
- [x] Eval: two new questions (JSON temptation, clickable links); run and review every answer.

## Design
- Data/model changes: none.
- Endpoints (`Api/PublicApi.cs`):
  - `/api/public/plans`: drop `priceInclBelgianVat` (only true for Belgium and EU private persons; it made the model
    say "Belgian VAT is added"). VAT rules stay in the FAQ.
  - `/api/public/faq`: site paths in the FAQ (`/Pricing`, `/Courses`, …) become full URLs with `App:BaseUrl`, so
    every link the model can see is a full URL. `url`/`orderUrl` were already absolute.
  - `/api/public/plans` returns `{ pricingUrl, plans }` (found in the eval: without it the model wrote "Pricing page"
    without a link, or invented `https://loopcasts.example/pricing`).
- n8n workflow `lcAssistant00001`:
  - Chat Trigger `responseMode: lastNode` → the chat gets the output of the last node, not the agent's stream.
  - Agent `onError: continueRegularOutput` → a failed model call gives `{ error }` instead of a failed run.
  - New Code node **Check answer** after the agent: no output → "assistant isn't available" message; JSON/code block →
    fallback sentence; bare site paths, relative links and links to another host → full links on the base URL
    (unknown page on another host → text only). Base URL from `$env.LOOPCASTS_PUBLIC_URL`
    (docker-compose, default `http://localhost:5180`; on a server = the public site address).
  - Prompt: plain sentences, never JSON/code; links only as markdown from the tools' url fields; VAT depends on
    country and company; off-topic = one sentence without support@; warn against card details, renew = order again
    on Pricing; "AI assistant" only when asked.
- Security: the Code node only rewrites text; base URL from an env var, not hardcoded. Only public data, as before.

## Out of scope
- Another model or provider. A real FAQ page on the site.

## Test plan
- Unit: plans without the Belgian VAT field; FAQ paths absolute; prompt file = workflow prompt (existing test).
- `node n8n/eval/check-answer.test.mjs`: runs the Check answer code from the workflow JSON on sample answers (also in CI).
- Eval: `node n8n/eval/run-eval.mjs <chat URL>` (17 questions), review every answer in docs/ai-evaluation.md.
- Manual: the widget on http://localhost:5180, ask for prices → links clickable, open the right page.
