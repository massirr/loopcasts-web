# Handover: where things stand

For whoever picks this up next (Koze, Claude Code, Cowork). Update this file at the end of every working session.
Last update: **20/10/2026** (#31).

## Done and merged on `main`
| Issue | PR | What |
|---|---|---|
| #18 Polish every page | #19 | Account menu, own Account settings, checkout progress, wording |
| #20 Import old members | #23 | /Admin/Import reads the old `users` table (.sql), never the password column |
| #21 n8n + Mailpit + renewal reminder | #24 | `docker compose up -d`, workflows, `/api/automation/*` |
| #22 Chat assistant | #25 | n8n "LoopCasts assistant" + `/api/public/*` + widget on the site |
| #26 AI evaluation (Groq) | — (045b25b, directly on main) | Gemini → Groq `openai/gpt-oss-120b`; 14/15 auto, 15/15 on review (docs/ai-evaluation.md) |
| #27 Login e-mails by the site | #28 | "Forgot password?"/"Change e-mail" over SMTP from the site, not n8n; ADR 0001 |

## Where each automation runs
Rule + full table: `docs/decisions/0001-n8n-or-code.md`. In short: code = money, data, security, must-always-work;
n8n = invoice e-mail, renewal reminder, chat assistant.

## Open PRs (review + merge; #30 = #29 chat links, on its own branch)
| Issue | PR | What |
|---|---|---|
| #31 Full course catalogue (60) + cross-check | #PR31 | 53 live courses on the site/API/chat; importer updates by title; `docs/analysis/course-catalogue-crosscheck.md`. Review + merge |

## Next steps
1. **n8n re-import (to check):** Docker is fixed (n8n + Mailpit running again on 20/10). Still to check: re-import +
   publish "LoopCasts e-mails" (docs/n8n.md §2) so n8n loses the old `email` branch (harmless until then: the site
   no longer sends it).
2. Chat prompt follow-ups from the #26 review (not done yet, new issue when picked up):
   - eval check: accept `'` and `’` in `n8n/eval/questions.json` (question 9);
   - VAT wording: rename/drop `priceInclBelgianVat` in `Api/PublicApi.cs` (+ test), prompt line "VAT depends on
     country and company, see the FAQ";
   - off-topic: "I only help with LoopCasts" without sending people to support@;
   - never share card details in the chat; renewing = ordering again via /Pricing;
   - decide whether "I'm an AI assistant" goes in every first answer.
   Prompt changes go in **both** `n8n/knowledge/system-prompt.md` and the workflow JSON (a test checks), then import
   + publish + restart, run the eval again.
3. #8 Deploy to Azure: also set `Smtp:*` as App Service settings; `LoginEmailSender` needs SMTP login + TLS first.

Course data (#31): durations in the catalogue are too short for most courses (cross-check report). Fix in the xlsx →
export again (spec 31) → restart the site: the importer updates by title. Or calculate them from Vimeo (not built).

## Pitfalls (learned the hard way)
- Docker Desktop + external disk: after stopping a container with a bind mount on /Volumes, it may refuse to start
  again ("file exists"). Restart Docker Desktop.
- **Save is not Publish** in n8n 2.x. The site's chat runs the *published* version; Save only changes the draft.
- **Reload the n8n editor tab after a command-line import.** An open tab keeps the old version, and saving it
  overwrites the import without warning (this put back the 700-token limit on 12/10).
- After changing a workflow in the editor, export it to the repo (docs/n8n.md §2), or the repo and n8n drift apart.
- Credential ids differ per n8n install: the repo JSON points to the key-less placeholders in `n8n/credentials/`;
  never import those placeholders over a credential that already has a key.
- Free-tier limits change often: check the provider's page before blaming the workflow.

## Blocked / needs Koze
- Confirm the n8n re-import + publish (step 1 above).
- Issue #6 (Chatbot) is still open on GitHub although #22 built the chat assistant: close it or note what's missing.
- Keys live only in n8n (Groq) and `.env` / user-secrets (`N8n:Secret`), never in the repo.
