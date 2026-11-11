# Handover: current state

Replaced at the end of each session; max ~60 lines. History: `docs/log/`. Plans: `docs/architecture.md`.
Last update: **11/11/2026**.

## Current state
- Repository: `massirr/loopcasts-web`, default branch `main`, public. `main` protected (PR + green `build-test`).
- **Live** since 04/11/2026: https://loopcasts-koze.azurewebsites.net, release `v1.0.0` (issue #8).
  - Azure for Students, `loopcasts-rg` in `belgiumcentral` (only allowed regions: see `docs/deploy-azure.md`).
  - Plan `loopcasts-plan` F1 Free (€0), web app `loopcasts-koze`, .NET 10, HTTPS-only, run from package.
  - SQLite on `/home/data`; deploy: green CI on `main` → `.github/workflows/deploy.yml`.
  - Lessons (stalled Kudu copy, F1 quota, unpublished CSV): `docs/deploy-azure.md` Notes.
- **Payments: Stripe** (#53, ADR 0003) replaced Mollie (Mollie needs a real company). Stripe sandbox = test mode.
  Locally works with user-secret `Stripe:SecretKey` (test card `4242 4242 4242 4242`).
- **E-mail only locally** by choice (Mailpit + n8n on Koze's laptop): `docs/architecture.md`, Known limits.
- Spec flow (#51): skill `spec` (intent first) → build → agent `reviewer` → skill `spec-close`.

## Open on the live site
- No Stripe key on Azure yet → mock checkout gives free access: do **not** share the URL.
- Old fictional data: Koze imports via `/Admin/Import` (orders + invoice register, then users `.sql`); when copying
  the DB for a check, include `loopcasts.db-wal`.

## Next steps
1. **After merging #53:** App Service setting `Stripe__SecretKey`; in the Stripe sandbox add webhook endpoint
   `https://loopcasts-koze.azurewebsites.net/webhooks/stripe` (event `checkout.session.completed`); one live test
   payment → Admin/Orders shows it paid.
2. Decide: block the mock checkout in Production when no key is set (follow-up from #53).
3. **New issue: logged-in checkout flow** (Koze's phone test 11/11): course page has no way to buy; /Order does not
   show what is bought and asks the e-mail while logged in; after paying it says "log in" while logged in.
   Open question: is there a single-course plan? Then the customer must be able to choose the course.
4. **New issue: chat + loaders:** chat window does not fit on zoom/phone, rounder corners; loaders (thinking-orbs,
   check licence) in the chat and on slow buttons (Pay, Order). No full-page loader.
5. Later (free tools): Sentry, UptimeRobot, custom domain via Combell (needs B1, Azure credit).
