# Handover: current state

Replaced at the end of each session; max ~60 lines. History: `docs/log/`. Plans: `docs/architecture.md`.
Last update: **28/10/2026**.

## Current state
- Repository: `massirr/loopcasts-web`, default branch `main`, currently private.
- `main` includes merged PR #38 (header width) and PR #40 (README and repository settings); PR #40 merged at `3168ddb` after `build-test` passed and human review.
- Issue #39 remains open pending final publication. Follow-up issue #41 updates this audit and handover after the security review clarified the screenshot finding.
- Only the owner is listed as collaborator. Preserve manual human review; do not require an approval count that prevents merging.
- `main` is protected: PR required; `build-test` required; review conversations must be resolved; admins included; force push and deletion disabled; 0 approvals required.

## Publication audit
- `docs/design/screenshots/before-my-1320.png` contains a demo password-reset URL in Git history. It points to `localhost`, was generated for the local Mailpit setup, and the UI states a 24-hour lifetime. The screenshot was committed 2026-10-07; the token is expired and cannot address a hosted service. The security review assessed it as a non-live demo artifact, not a publication blocker. Its value is intentionally not copied here.
- The full tracked-file and history audit found no live credentials, customer data, or databases. The repository is still private until the follow-up audit documentation is reviewed and merged.

## Next steps
1. Review and merge the issue #41 audit correction after green CI and human review.
2. Make `massirr/loopcasts-web` public as originally requested; verify visibility and `main` protection afterward.
3. Close issue #39 when publication and verification are complete.
