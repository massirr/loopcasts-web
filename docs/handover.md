# Handover: where things stand

For whoever picks this up next (Koze, Claude Code, Cowork). Update this file at the end of every working session.
Last update: **12/10/2026, evening.** Deadline: working demo on **Wednesday**.

## Done and merged on `main`
| Issue | PR | What |
|---|---|---|
| #18 Polish every page | #19 | Account menu, own Account settings, checkout progress, wording |
| #20 Import old members | #23 | /Admin/Import reads the old `users` table (.sql), never the password column |
| #21 n8n + Mailpit + renewal reminder | #24 | `docker compose up -d`, two workflows, `/api/automation/*` |
| #22 Chat assistant | #25 | n8n "LoopCasts assistant" + `/api/public/*` + widget on the site |

## In progress: #26 "Run the AI evaluation" (NOT committed yet, on `main` working tree)
Uncommitted changes (keep them, they become one PR for #26):
- `n8n/workflows/loopcasts-assistant.json`: model switched from **Gemini to Groq** `openai/gpt-oss-120b`,
  max 2000 tokens, temperature 0.2, Retry On Fail (3×, 5 s); points to the key-less placeholder credential.
- `n8n/credentials/groq.json` (replaces `gemini.json`, no key inside).
- `docs/ai-evaluation.md`: model choice, first results (14/15) and the review of every answer.
- `docs/n8n.md`, `CLAUDE.md`, `docs/specs/22-chat-assistant.md`: Groq instead of Gemini, pitfalls.

Why Groq: Gemini's free tier allows 20 requests/day per model (one chat answer = 2–3 requests) and was often "busy"
(503): only 3 of 15 questions got an answer. Groq's free plan: 1 000 requests/day, fast, doesn't train on API data.

The live n8n (Docker on Koze's Mac) runs exactly the repo version, with Koze's own "Groq" credential (has the key).

## Evaluation status
First full run with Groq: **14/15 pass**, but the review says **15/15 behaved correctly** (the fail is a bug in the
check: `don’t` with a curly apostrophe). That run used max tokens **700** (an old editor tab overwrote the 2000; see
"Pitfalls"). Details per answer: `docs/ai-evaluation.md` → "Review of the first Groq run".

## Next steps (agreed for tomorrow, in this order)
1. Fix the eval check: accept `'` and `’` in `n8n/eval/questions.json` patterns (question 9).
2. VAT wording: the model says "Belgian 21% VAT is added at checkout" for everyone. Make the plans tool clearer
   (rename or drop `priceInclBelgianVat` in `Api/PublicApi.cs` + test) and add a prompt line: VAT depends on country
   and company, see the FAQ.
3. Off-topic questions: answer "I only help with LoopCasts" without sending people to support@ (rule 2 of the prompt).
4. Card numbers: tell visitors never to share card details in the chat; renewing = ordering again via /Pricing.
5. Decide: keep "I'm an AI assistant" in every first answer (rule 1) or only when asked (the widget already says it).
6. Prompt changes go in **both** `n8n/knowledge/system-prompt.md` and the workflow JSON (a unit test checks they match),
   then import + publish + restart (docs/n8n.md §2), run the eval again, then one PR for #26 and an agent-log entry.

## Pitfalls (learned the hard way)
- **Save is not Publish** in n8n 2.x. The site's chat runs the *published* version; Save only changes the draft.
- **Reload the n8n editor tab after a command-line import.** An open tab keeps the old version, and saving it
  overwrites the import without warning (this put back the 700-token limit on 12/10).
- After changing a workflow in the editor, export it to the repo (docs/n8n.md §2), or the repo and n8n drift apart.
- Credential ids differ per n8n install: the repo JSON points to the key-less placeholders in `n8n/credentials/`;
  never import those placeholders over a credential that already has a key.
- Free-tier limits change often: check the provider's page before blaming the workflow.

## Blocked / needs Koze
- Nothing blocked right now. Keys live only in n8n (Groq) and `.env` / user-secrets (`N8n:Secret`), never in the repo.
