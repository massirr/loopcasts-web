---
name: agent-log
description: Append an entry to docs/agent-log.md after finishing an issue/PR (what, why, decisions, tests, follow-ups). Use at the end of every issue or when blocked.
---
# Agent log

Append (newest at the bottom) to `docs/agent-log.md`:
```
## YYYY-MM-DD — #<issue> <title>  (PR #<n>, <merged|blocked>)
- **What:** 1–3 bullets of the change.
- **Why / decisions:** trade-offs, what was skipped and when to add it.
- **Tests:** what is covered; CI result.
- **Manual steps for Koze:** secrets to set, things to try in the browser.
- **Learn:** one ASP.NET concept used (compare to WPF/MVVM when useful).
```
Short and factual. No secrets.
