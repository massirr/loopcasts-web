---
name: spec-close
description: Finish an issue inside its PR - tick the spec, record the review, move it to done/, agent-log, replace the handover, security-check, commit. Use when the work for an issue is built, tested and reviewed.
---
# Spec close

In this order, on the issue's branch:

1. **Verify first:** `dotnet test tests/LoopCastsWeb.Tests` green, and the spec's Manual check done locally.
2. **Spec** `docs/specs/<n>-<slug>.md`: set `Status: done`; tick every criterion that is met (`[x]`). One not met → leave it, say why
   under Notes, and tell Koze; don't close silently. Fix "(to confirm)" paths to the real files. Paste the
   closing lines of the `reviewer` agent into Reviews (`Left:` = a finding you accept and don't fix now), and fill
   Notes → Tests.
3. `git mv docs/specs/<n>-<slug>.md docs/specs/done/` (DocsTests fails on unticked boxes there).
4. Skill `agent-log` (append only).
5. **Replace** `docs/handover.md`: current state only, max ~60 lines, next steps.
6. Skill `security-check`, then commit and open the PR with `Closes #<n>`. Koze reviews and merges.
