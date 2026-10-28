# Add a verified README and prepare public repository settings (#39)

## User story
As someone finding LoopCastsWeb on GitHub, I want a clear and accurate project README, so that I can understand the prototype and run it safely.

## Acceptance criteria
- [x] Root README explains the purpose, implemented features and planned work, stack, local setup, secrets/configuration, tests, and deployment status using verified repository details.
- [x] `CLAUDE.md` no longer says the repository must remain private; publication requires a clean full-history audit.
- [x] Tracked files, ignore rules, workflows, documentation, and all Git history were audited for material that should not be public; secret values were not printed.
- [x] The historical screenshot URL was assessed with the security review: it is a local Mailpit demo reset link targeting `localhost`, with a 24-hour lifetime; the screenshot's commit date is 2026-10-07, so it is expired. It is not a live credential or customer data.
- [x] Repository identity and visibility were verified; no visibility change was made.
- [x] `main` protection requires PRs, `build-test`, and resolved review conversations; force pushes and branch deletion are prevented.
- [x] No approval count is required because only the repository owner is available; manual human review remains documented without preventing merges.
- [x] Handover is current and under 60 lines; append to the monthly log without reading it.
- [ ] After issue #41's audit correction is reviewed and merged, make the repository public and verify visibility and branch protection.

## Design
- Data/model changes (migration name): none.
- Pages / endpoints: root `README.md`; repository settings for `main`.
- Services (where the logic lives): none.
- Security (authz, validation, secrets): audit before publication; no credentials in README; keep secrets in user-secrets, environment variables, or server settings.

## Out of scope
- Deploying the application or adding new application features.

## Test plan
- Unit: existing application tests; README claims cross-checked against project files and workflows.
- Manual: validate documented commands/configuration; inspect GitHub visibility and `main` protection after configuration.
- Security follow-up: the password-reset URL in the screenshot was from the localhost Mailpit demo and expired within 24 hours; no token value is reproduced in documentation.
