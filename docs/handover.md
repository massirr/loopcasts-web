# Handover: current state

Replaced at the end of each session; max ~60 lines. History: `docs/log/`. Plans: `docs/architecture.md`.
Last update: **04/11/2026**.

## Current state
- Repository: `massirr/loopcasts-web`, default branch `main`, public. `main` protected (PR + green `build-test`).
- **Live** since 04/11/2026: https://loopcasts-koze.azurewebsites.net, release `v1.0.0` (issue #8, PRs #47–#50).
  - Azure for Students, `loopcasts-rg` in `belgiumcentral` (only allowed regions: see `docs/deploy-azure.md`).
  - Plan `loopcasts-plan` F1 Free (€0), web app `loopcasts-koze`, .NET 10, HTTPS-only, run from package.
  - SQLite on `/home/data`; 60 courses seeded; admin login works (Identity v3 hash); seed password setting deleted.
  - Deploy: green CI on `main` → `.github/workflows/deploy.yml` → GitHub environment `production`.
  - Lessons (stalled Kudu copy, F1 quota, unpublished CSV): `docs/deploy-azure.md` Notes, `docs/log/2026-11.md`.
- **Spec flow (#51):** skill `spec` (intent first, no code reading) → build → agent `reviewer` → skill `spec-close`.
  Template `docs/specs/_template.md`; DocsTests fails on unticked criteria in `docs/specs/done/`.

## Open on the live site
- Mock checkout gives free access: do **not** share the URL before the Mollie test key is set.
- Old fictional data (orders, invoices, members): Koze imports via `/Admin/Import` (orders + invoice register, then
  users `.sql`). Verify counts after import; when copying the DB for a check, include `loopcasts.db-wal`.
- No SMTP on the server yet, so login/reset e-mails are not sent.

## Next steps
1. **Mollie** (new issue, first real use of skill `spec`): test key as App Service settings `Mollie__ApiKey` +
   `Mollie__WebhookBaseUrl=https://loopcasts-koze.azurewebsites.net`; build and test locally first, then one
   end-to-end test payment on the live site (the webhook needs the server). Run agent `reviewer` (security!).
2. Then new features from `docs/architecture.md` (Known limits), one issue per branch.
