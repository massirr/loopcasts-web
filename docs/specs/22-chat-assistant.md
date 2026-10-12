# Spec: Chat assistant for visitors (n8n + Gemini)  (#22)

## User story
As a visitor, I want to ask a chat assistant about courses, plans, prices, VAT and ordering, so that I get an answer
right away, and support only gets the questions that need a person. (The course's AI component.)

## Acceptance criteria
- [x] `Chat:Url` set → chat button bottom-right in loopcasts-ui colours, "Answers by an AI assistant" under the input;
      not set → nothing is loaded.
- [x] Public data only: `GET /api/public/courses`, `/api/public/plans` (read-only, tested), `/api/public/faq`
      (= `n8n/knowledge/faq.md`, written from the support answers + handbook ch. 1, 3, 6 without names/customer details).
- [x] System prompt: LoopCasts topics only, never asks for or handles personal/payment data, support@ when unsure or
      personal, says it is an AI assistant, visitor's language, short answers with links.
- [x] The model is one node ("Model (swap here)"); Azure AI Foundry switch documented (docs/n8n.md §5).
- [ ] docs/ai-evaluation.md: 15 questions **with Gemini's answers** → blocked on the Gemini API key (runner ready).
- [x] Privacy note (Gemini free tier uses inputs to improve Google's products → only public data goes in).

## Design
- Workflow `n8n/workflows/loopcasts-assistant.json` (n8n 2.42.7 node versions read from the container): Chat Trigger
  1.5 (public, embedded/webhook mode, allowed origin http://localhost:5180) → AI Agent 3.1 (system message, max 6
  iterations) + Google Gemini 1.2 (`gemini-2.5-flash`, temperature 0.2, credential "Gemini") + Window Buffer Memory
  1.4 (session from the chat, 10 messages) + 3 × HTTP Request tool 4.5 on `{LOOPCASTS_SITE_URL}/api/public/…`.
- Credential `n8n/credentials/gemini.json`: name "Gemini", **empty key** → Koze pastes the key in the UI, the workflow
  is already linked to it.
- Site: `Api/PublicApi.cs` (courses: live only; plans: from `Plans` + Belgian VAT total + order URL; faq: the markdown
  file, shipped with publish via the csproj, other `n8n/` files are not published). `Pages/Shared/_Chat.cshtml`:
  `@n8n/chat` 1.41.5 from jsDelivr (pinned), CSS variables on `body` (the widget's CSS sets them on `:root` later),
  the AI note added to the chat footer (the widget has no option for it in this version).
- `n8n/knowledge/system-prompt.md` = the agent's system message (unit test keeps them equal).
- Evaluation: `n8n/eval/questions.json` (15 questions, expect/forbid patterns) + `n8n/eval/run-eval.mjs` (writes the
  answers and pass/fail into docs/ai-evaluation.md).

## Security / privacy
- Tools are GET-only on public data; the test checks every tool URL is under `/api/public/`.
- FAQ test: no staff names, no e-mail address except support@.
- Chat webhook is public by design (like the website); CORS limits embedding to the site's origin.
- Gemini free tier → public data only; real customers → paid tier or Azure (documented).

## Out of scope
- Answers about a visitor's own account/order (would need login + private data; the assistant points to My courses/support@).
- Rate limiting the public chat webhook (n8n side), chat transcripts review.

## Test plan
- Unit: `PublicApiTests` (live courses only, plans = order-form prices, FAQ public-only + 404, workflow prompt/tool guard). 148 tests.
- Manual: chat webhook + CORS (only localhost:5180), n8n → site tools via host.docker.internal, workflow runs up to the
  model node, widget screenshots `docs/design/screenshots/chat-widget-{1320,360}.png`, eval runner run once (no key → 0/15, doc restored).
- After the key: `node n8n/eval/run-eval.mjs <chat URL>` → results in docs/ai-evaluation.md.
