# Handover: where things stand

For whoever picks this up next (Koze, Claude Code, Cowork). Update this file at the end of every working session.
Last update: **21/10/2026** (#33 merged).

## Done and merged on `main`
| Issue | PR | What |
|---|---|---|
| #18 Polish every page | #19 | Account menu, own Account settings, checkout progress, wording |
| #20 Import old members | #23 | /Admin/Import reads the old `users` table (.sql), never the password column |
| #21 n8n + Mailpit + renewal reminder | #24 | `docker compose up -d`, workflows, `/api/automation/*` |
| #22 Chat assistant | #25 | n8n "LoopCasts assistant" + `/api/public/*` + widget on the site |
| #26 AI evaluation (Groq) | — (045b25b, directly on main) | Gemini → Groq `openai/gpt-oss-120b`; 14/15 auto, 15/15 on review (docs/ai-evaluation.md) |
| #27 Login e-mails by the site | #28 | "Forgot password?"/"Change e-mail" over SMTP from the site, not n8n; ADR 0001 |
| #29 Chat answers: clickable links, no JSON | #30 | *Check answer* node, full URLs in the public API, prompt review fixes; eval 17/17 |
| #31 Full course catalogue (60) + cross-check | #32 | 53 live courses on site/API/chat; importer updates by title; `docs/analysis/course-catalogue-crosscheck.md` |
| #33 VAT number check | #34 | Format + BE checksum + EU VIES; VIES down → 21% + "not verified" in Admin → Orders (ADR 0002) |

## Where each automation runs
Rule + full table: `docs/decisions/0001-n8n-or-code.md`. In short: code = money, data, security, must-always-work;
n8n = invoice e-mail, renewal reminder, chat assistant.

## Next steps
1. Course data (#31): durations in the catalogue are too short for most courses (cross-check report). Fix in the
   xlsx → export again (spec 31) → restart the site: the importer updates by title. Or calculate them from Vimeo (not built).
2. Small chat wording left (docs/ai-evaluation.md, #29 review): q3 says "see the VAT section of our FAQ" but there is
   no FAQ page on the site. Either a /Faq page (the markdown is already there) or leave it.
3. #8 Deploy to Azure: also set `Smtp:*` as App Service settings; `LoginEmailSender` needs SMTP login + TLS first.
   n8n on a server: `LOOPCASTS_PUBLIC_URL` = the public site (same as `App:BaseUrl`), Chat Trigger *Allowed origins*.

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
- Groq free = 8 000 tokens/minute: questions sent fast get "the assistant isn't available" (the *Check answer*
  fallback). The eval waits 20 s between questions for that reason.
- Two site addresses in n8n: `LOOPCASTS_SITE_URL` (how n8n reaches the site, host.docker.internal) and
  `LOOPCASTS_PUBLIC_URL` (how a visitor's browser does; used for chat links). Don't mix them up.

## Blocked / needs Koze
- Keys live only in n8n (Groq) and `.env` / user-secrets (`N8n:Secret`), never in the repo.
