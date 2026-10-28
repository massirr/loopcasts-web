---
name: agent-log
description: Append an entry to this month's docs/log/YYYY-MM.md after finishing an issue/PR (what, why, decisions, tests, follow-ups). Append only, never read the log. Use at the end of every issue or when blocked.
---
# Agent log

**Append, never read.** The log is for people (Koze, the teacher), not context for agents. Don't open it with
Read/Edit (that loads the whole month). Append with the shell; a new month starts a new file automatically:
```bash
f=docs/log/$(date +%Y-%m).md
[ -f "$f" ] || printf '# Agent log: %s\n\nAppend only (skill agent-log); agents never read this.\n' "$(date +'%B %Y')" > "$f"
cat >> "$f" <<'EOF'

## YYYY-MM-DD — #<issue> <title>  (PR #<n>, <ready to merge|merged|blocked>)
- **What:** 1–3 bullets of the change.
- **Why / decisions:** trade-offs, what was skipped and when to add it.
- **Tests:** what is covered; CI result.
- **Manual steps for Koze:** secrets to set, things to try in the browser.
- **Learn:** one ASP.NET concept used (compare to WPF/MVVM when useful).
EOF
```
Short and factual (max ~12 lines). No secrets. In the same PR: move the issue's spec to `docs/specs/done/` and
replace `docs/handover.md`.
