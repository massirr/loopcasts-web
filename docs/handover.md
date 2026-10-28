# Handover: current state

Replaced at the end of each session; max ~60 lines. History: `docs/log/`. Plans: `docs/architecture.md`.
Last update: **28/10/2026**.

## Current state
- Repository: `massirr/loopcasts-web`, default branch `main`, currently private.
- `main` includes merged PR #38 (header width), commit `6e9feaf`.
- Issue #39 remains open on `feat/39-public-readme-protection`; PR #40 contains the README and repository-policy updates. Check its current CI/review state on GitHub.
- Only the owner is listed as collaborator. Preserve manual human review; do not require an approval count that prevents merging.
- `main` is protected: PR required; `build-test` required; review conversations must be resolved; admins included; force push and deletion disabled; 0 approvals required.

## Publication blocker
- A password-reset URL token appears in tracked screenshot `docs/design/screenshots/before-my-1320.png` and Git history. Do not make the repository public.
- Before publication, invalidate/rotate the token as applicable, replace or remove the screenshot, and purge its blob from all Git history. `main` history rewrite is prohibited by the current hard rule; resolve that policy and coordinate cleanup before publication.
- The repository is still private. No visibility change has been made.

## Next steps
1. Review and merge PR #40 only after green CI and human review.
2. Re-verify the requested `main` protection settings after PR review.
3. Keep issue #39 open and visibility private until the historical token has been remediated and a clean publication audit passes.
