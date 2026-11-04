# Handover: current state

Replaced at the end of each session; max ~60 lines. History: `docs/log/`. Plans: `docs/architecture.md`.
Last update: **04/11/2026**.

## Current state
- Repository: `massirr/loopcasts-web`, default branch `main`, public. `main` protected (PR + green `build-test`).
- **Live** since 04/11/2026: https://loopcasts-koze.azurewebsites.net (issue #8, PRs #47, #48 and this one).
  - Azure for Students, `loopcasts-rg` in `belgiumcentral` (only allowed regions: see `docs/deploy-azure.md`).
  - Plan `loopcasts-plan` F1 Free (€0), web app `loopcasts-koze`, .NET 10, HTTPS-only, run from package.
  - SQLite on `/home/data`; migrations and admin seed ran; admin login checked; password stored as Identity v3 hash.
  - Deploy: green CI on `main` → `.github/workflows/deploy.yml` → GitHub environment `production`.
- First deploy failed (Kudu copy stalled → missing `libe_sqlite3.so` → crash loop → F1 daily quota used up).
  Fixed by `-r linux-x64` publish and `WEBSITE_RUN_FROM_PACKAGE=1`; lessons in `docs/deploy-azure.md` Notes.

## Open on the live site
- Mock checkout gives free access: do **not** share the URL before the Mollie test key is set.
- `SeedAdmins__0__Password` can be deleted from App Service settings (account exists; seeder skips it).
- No SMTP on the server yet, so login/reset e-mails are not sent.

## Next steps
1. Mollie test key: App Service settings `Mollie__ApiKey` + `Mollie__WebhookBaseUrl=https://loopcasts-koze.azurewebsites.net`;
   test a payment end to end (webhook must reach the live site).
2. Create GitHub release `v1.0.0` for this first live version (`gh release create v1.0.0 --generate-notes`).
3. Then new features from `docs/architecture.md` (Known limits), one issue per branch.
