---
name: security-check
description: Pre-commit / pre-PR security checklist for LoopCastsWeb (secrets, db files, authz, input validation). Use before every commit, PR or deployment.
---
# Security check

Run and inspect:
```bash
git status --short
git diff --cached --name-only | grep -Ei '\.db|\.db-(shm|wal)$|secrets|\.env|paswoord|/bin/|/obj/|\._' && echo "STOP"
git diff --cached | grep -Ei 'password\s*[=:]\s*"|apikey|api_key|secret\s*[=:]|live_[a-z0-9]|test_[a-z0-9]{20,}' && echo "CHECK"
```
Checklist:
- [ ] No secrets / keys / passwords in the diff (only via `dotnet user-secrets` or env vars).
- [ ] No `*.db`, customer data or files from `../LoopCasts`.
- [ ] New pages: authorization correct (Admin under `/Admin`, customer pages check ownership).
- [ ] Every POST validates input server-side; price/plan from `Plans`, never from the form.
- [ ] Webhooks: never trust the payload; re-fetch status from the provider by id.
- [ ] No sensitive data (passwords, tokens, full card/bank data) written to logs.
- [ ] Repo still private: `gh repo view --json visibility`.
Report findings; fix before committing.
