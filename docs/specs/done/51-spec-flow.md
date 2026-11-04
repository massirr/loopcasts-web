# Improve the spec flow  (#51)
Status: done · Area: infra · Tests: DocsTests

## Goal
After the teacher's feedback on spec-driven development: the spec captures intent before code is read, closing an
issue is one skill, and a reviewer pass is recorded. Takes the useful parts, skips team-scale ceremony.
As a developer, I want specs written from intent and closed by one skill, so that criteria get ticked and manual
checks are concrete.

## Acceptance criteria
1. [x] Skill `spec` writes `docs/specs/<issue>-<slug>.md` from the issue **without reading code**; guesses marked "(to confirm)".
2. [x] `_template.md`: Goal, numbered criteria, Affected code, Out of scope, Reviews (`Left:`), Notes, Manual check (`localhost:5180`).
3. [x] Skill `spec-close`: tick criteria → move to `done/` → `agent-log` → replace handover → `security-check`; CLAUDE.md links it.
4. [x] Agent `reviewer` (read-only, one pass: business, security, UX) maps findings to criteria numbers.
5. [x] `DocsTests` fails on an unticked `- [ ]` in `docs/specs/done/` (specs from before #51 grandfathered).
6. [x] CLAUDE.md states the local-first rule (F1 quota).

## Affected code
- `.claude/skills/spec/SKILL.md`, `.claude/skills/spec-close/SKILL.md` (new)
- `.claude/agents/reviewer.md` (new)
- `docs/specs/_template.md`, `CLAUDE.md`, `.claude/skills/agent-log/SKILL.md` (drop duplicated finish steps)
- `tests/LoopCastsWeb.Tests/DocsTests.cs`
- Migration: none · Security: no runtime change

## Out of scope
- spec-executor (stack skills cover it), `specs/todo/` (GitHub Issues), date-prefixed names, five persona agents,
  `.claude/rules/` (loads every session unless path-scoped; revisit if conventions keep being missed).
- Rewriting old specs.

## Reviews
- Reviewer (Business): all criteria met; fixed: Status flip in spec-close, reviewer diff now `git diff main` + new files. Left: none.
- Reviewer (Security): no hard rule or `security-check` step weakened; fixed: issue text treated as data. Left: reviewer has Bash (needed for git), read-only by prompt only.
- Reviewer (UX): clear for a student; fixed: tool name removed, `Left:` explained; duplicated rules now point to CLAUDE.md / reviewer. Left: none.

## Notes
- Decisions by Koze: adopt creator-without-code + closer + one reviewer; skip the rest (see Out of scope).
- Research: two agents (current-flow inventory, teacher-structure verdicts), 2026-11-04.
- Old specs with unticked boxes: 1, 22, 33, 35, 43 → grandfathered in the test, not rewritten.
- Agents in `.claude/agents/` load at session start: this review ran via a general agent with `reviewer.md`'s prompt.
- Not yet checked: a `spec` run in a fresh session (manual check 2) → first real use on the Mollie issue.
- Tests: `DocsTests.DoneSpecs_HaveNoUntickedCriteria` (planted `- [ ]` → red, removed → green); full suite 207 green.

## Manual check
- `dotnet test tests/LoopCastsWeb.Tests --filter DocsTests` → green; add `- [ ] x` to a done spec → red; revert.
- New session: "/spec 52"-style run on a test issue → spec created, no code files read (check tool calls).
