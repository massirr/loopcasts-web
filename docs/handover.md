# Handover: current state

Replaced (not appended) at the end of every session; max ~60 lines. History: `docs/log/`. Plans per area: `docs/architecture.md`.
Last update: **21/10/2026**.

## Open PRs (merge in this order)
| PR | Issue | State |
|---|---|---|
| #34 | #33 VAT numbers checked before 0% VAT | CI green, waiting for Koze's review + merge |
| #35 | #35 Keep agent context small | stacked on #34 (base `feat/33-vat-number-check`); after #34 is merged, set its base to `main` |

`main` = #31 (full course catalogue). Everything before is in `docs/log/2026-10.md`.

## Next steps
1. Merge #34, then #35.
2. Koze, after #34: Admin → Orders, order an NL company with VAT number `301` (Development = VIES test service) and
   check the "VAT number not verified" badge; screenshot to `docs/design/screenshots/33-admin-orders.png`.
3. Course data (#31): durations in the catalogue are too short for most courses (cross-check report). Fix in the
   xlsx → export again (spec `docs/specs/done/31-course-catalogue.md`) → restart the site.
4. Chat wording (#29 review): q3 says "see the VAT section of our FAQ", but there is no FAQ page. A /Faq page or leave it.
5. #8 Deploy to Azure: checklist in `docs/deploy-azure.md`.

## Blocked / needs Koze
- Merging PRs: the agent may not merge without a human review.
- Keys live only in n8n (Groq) and `.env` / user-secrets (`N8n:Secret`, `Mollie:ApiKey`), never in the repo.
