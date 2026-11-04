# <title>  (#<issue>)
Status: in progress · Area: <orders|invoices|accounts|admin|ui|infra> · Tests: <unit|full suite>

## Goal
2–3 sentences: what changes, for whom, which manual step disappears.
As a …, I want …, so that ….

## Acceptance criteria
<!-- numbered so Reviews can point at them; skill spec-close ticks them -->
1. [ ] Given … when … then …
2. [ ] Tests cover each criterion.

## Affected code
<!-- written from intent, not from reading code; mark guesses "(to confirm)" and fix them while building -->
- `Services/…` (to confirm)
- Migration: <Name> or none
- Security: authz / validation / price from `Plans`

## Out of scope
-

## Reviews
<!-- closing lines of agent `reviewer`; only reviews that ran -->
- Reviewer: <finding> → criteria 1, 3. Left: <accepted leftover or none>

## Notes
- Decisions by Koze:
- Root cause (bugs):
- Tests: `<TestClass>` (n tests)

## Manual check
<!-- local first (CLAUDE.md); live only for what needs the server -->
- `http://localhost:5180/<path>` → do … → expect …
