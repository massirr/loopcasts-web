# Spec: Keep agent context small  (#35)

## User story
As a developer working with AI agents, I want the context an agent loads by default small and free of duplicated or
stale facts, so that sessions cost fewer tokens, start faster, and docs don't drift (teacher: cost, tokens, latency, drift).

## Acceptance criteria
- [ ] Before/after measured with `docs/analysis/context-budget.mjs`, written up in `docs/analysis/context-budget.md`.
- [ ] CLAUDE.md < 60 lines: hard rules, working habits, map. No status. Structure table → `docs/architecture.md`.
- [ ] `docs/handover.md` replaced each session, max ~60 lines, current state only.
- [ ] Agent log per month in `docs/log/`; the agent-log skill appends with the shell and never reads it.
- [ ] Merged specs in `docs/specs/done/`.
- [ ] One fact, one place; `DocsTests` checks paths in the docs exist, line limits, one active spec.

## Design
- Data/model changes: none.
- Docs: `CLAUDE.md` (rewritten), `docs/architecture.md` (new: map, flow, design rules, known limits),
  `docs/handover.md` (replaced), `docs/n8n.md` §7 pitfalls, `docs/deploy-azure.md` go-live list,
  `docs/agent-log.md` → `docs/log/2026-10.md`, `docs/specs/*` → `docs/specs/done/`.
- Skill `agent-log`: append with `cat >>`, never Read/Edit (Edit needs a full Read first).
- Test: `DocsTests`.

## Out of scope
- Size of the n8n chat prompt/FAQ (that's the chat assistant's token budget, not the coding agent's).
- `~/.claude/CLAUDE.md` (Koze's global file, outside the repo).

## Test plan
- Unit: `DocsTests` (fails on a renamed file, CLAUDE.md > 60 lines, two active specs).
- Manual: `node docs/analysis/context-budget.mjs` before (`--before`) and after.
