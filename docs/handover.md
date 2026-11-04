# Handover: current state

Replaced at the end of each session; max ~60 lines. History: `docs/log/`. Plans: `docs/architecture.md`.
Last update: **04/11/2026**.

## Current state
- Repository: `massirr/loopcasts-web`, default branch `main`, public. `main` protected (PR + green `build-test`).
- **Live** since 04/11/2026: https://loopcasts-koze.azurewebsites.net (issue #8, PRs #47–#50).
  - Azure for Students, `loopcasts-rg` in `belgiumcentral` (only allowed regions: see `docs/deploy-azure.md`).
  - Plan `loopcasts-plan` F1 Free (€0), web app `loopcasts-koze`, .NET 10, HTTPS-only, run from package.
  - SQLite on `/home/data`; migrations and admin seed ran; admin login checked; password stored as Identity v3 hash;
    plain-text `SeedAdmins__0__Password` setting deleted afterwards.
  - Deploy: green CI on `main` → `.github/workflows/deploy.yml` → GitHub environment `production`.
- First deploy failed (Kudu copy stalled → missing `libe_sqlite3.so` → crash loop → F1 daily quota used up).
  Fixed by `-r linux-x64` publish and `WEBSITE_RUN_FROM_PACKAGE=1`; lessons in `docs/deploy-azure.md` Notes.
- Live site showed 0 courses: `Data/Seed/course-catalogue.csv` was not published. PR #50 ships it (`CopyToPublishDirectory`).

## Open on the live site
- Mock checkout gives free access: do **not** share the URL before the Mollie test key is set.
- Old orders, invoices and members are not imported live yet (the old dataset is fictional). Admin → `/Admin/Import`:
  orders sheet + invoice register first, then the users `.sql` (files from `../LoopCasts`, never into the repo).
- No SMTP on the server yet, so login/reset e-mails are not sent.

## Next steps
0. After #49 and #50 are merged and deployed (courses visible): `gh release create v1.0.0 --generate-notes`.
1. Mollie test key: App Service settings `Mollie__ApiKey` + `Mollie__WebhookBaseUrl=https://loopcasts-koze.azurewebsites.net`;
   test a payment end to end (webhook must reach the live site). One issue, one branch.
2. Then new features from `docs/architecture.md` (Known limits), one issue per branch.
