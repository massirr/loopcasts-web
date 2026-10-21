# Context budget: what an agent reads before it starts working  (#35)

Teacher feedback: every file an AI agent reads costs **money** (tokens), **time** (latency before the first useful
step) and **attention** (more text, more stale facts to trip over). So we measured what Claude Code loads in this repo
by default, cut it, and added a test so it stays cut.

Measured 21/10/2026 with `node docs/analysis/context-budget.mjs` (bytes; tokens ≈ bytes / 4).
Not counted: Claude Code's own system prompt and tools (the same in every repo) and Koze's global `~/.claude/CLAUDE.md`.

## Result

| | Before | After | Change |
|---|---:|---:|---:|
| **Loaded by default, per issue** | **12,331 tokens** | **2,225 tokens** | **−82%** |
| of which `CLAUDE.md` (always) | 2,892 | 824 | −72% |
| of which `docs/handover.md` (session start) | 919 | 356 | −61% |
| of which the agent log (every issue) | 7,618 | 0 | −100% |
| All text in `docs/` | 36,970 | 39,105 | +6% (moved, not deleted) |

The biggest cost wasn't `CLAUDE.md`, it was the **agent log**. To append an entry with the Edit tool, an agent
first has to Read the whole file: 30 KB, and it grew with every issue. Now the skill appends with `cat >>` and never
reads it, so that cost is 0 and stays 0 as the log grows.

`docs/` as a whole got slightly bigger: the structure table, flow and known limits moved from `CLAUDE.md` into
`docs/architecture.md`. That's the point: the same facts, read **when needed** instead of **every time**.

## Before and after, line by line

| Loaded by default | Before (tokens) | After (tokens) |
|---|---:|---:|
| `CLAUDE.md`: auto-loaded every session | 2,892 | 824 |
| Skill list (name + description of 7 skills): auto-loaded | 373 | 385 |
| `docs/handover.md`: "read it first" | 919 | 356 |
| `docs/specs/_template.md`: every issue | 86 | 86 |
| `security-check` skill body: every commit | 279 | 279 |
| `agent-log` skill body: every issue | 166 | 296 |
| `docs/agent-log.md`: read whole to append an entry | 7,618 | 0 |
| **Total** | **12,333** | **2,226** |

(Totals differ by 1–2 from the script's because of rounding per row.)

## Three tiers

| Tier | What | ~Tokens |
|---|---|---:|
| **Always** | `CLAUDE.md` (hard rules, working habits, map), skill list, handover, spec template, security-check + agent-log skills | 2,225 |
| **On demand**: only when the task needs it, found via the map in `CLAUDE.md` | `docs/architecture.md` (1,998), ADRs (1,577), `docs/n8n.md` (3,151), `docs/deploy-azure.md` (624), the active spec, other skill bodies (`loopcasts-ui` ~3,200 for UI work) | per task |
| **Never**, unless the task is about them | `docs/log/` (7,623 and growing), `docs/ai-evaluation.md` (4,150), `docs/specs/done/` (11,647), `docs/design/` (preview + 4 MB of screenshots), `../cowork` | 0 |

## The rules (in `CLAUDE.md` → Working habits)
1. **Measure first.** `docs/analysis/context-budget.mjs` prints the table; run it again after a change.
2. **`CLAUDE.md` under 60 lines**: hard rules, working habits, a map of where things live. No status, no structure
   table: those are in `docs/handover.md` and `docs/architecture.md`.
3. **Handover = current state only**, replaced (not appended) every session, max ~60 lines. History is in the log.
4. **Agent log per month** (`docs/log/2026-10.md`, …). Agents **append, never read**.
5. **Merged specs → `docs/specs/done/`.** Only the active spec sits in `docs/specs/`.
6. **One fact, one place.** Link instead of copying. Examples removed: "where each automation runs" (was in the
   handover and ADR 0001 → ADR only), n8n pitfalls (handover + n8n.md → n8n.md only), deploy settings
   (handover → `docs/deploy-azure.md`), "no secrets / never trust the browser" (hard rules + design rules → hard rules).
7. **Where a doc must match the code, a test checks it.** `DocsTests`: every file path named in `CLAUDE.md`,
   `docs/architecture.md` and `docs/handover.md` exists; `CLAUDE.md` and the handover stay ≤ 60 lines; at most one
   active spec. (Same idea as the existing test that the n8n system prompt equals `n8n/knowledge/system-prompt.md`.)
   On its first run it caught a wrong path in the new `docs/architecture.md`.
8. **One issue per session, read only the files you need.** Never load `docs/log/`, `docs/ai-evaluation.md` or
   `../cowork` unless the task is about them.

## Why the test matters more than the cleanup
A cleanup is a snapshot: in a month `CLAUDE.md` would have grown back. The test makes the budget part of CI, just like
a failing unit test: a renamed file, a 61st line in `CLAUDE.md` or a forgotten merged spec makes the build red.
