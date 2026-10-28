# LoopCastsWeb — notes for Claude Code

School prototype (Automation Tools, Thomas More) for the fictional company **LoopCasts BV** (video training
subscriptions). Goal: **remove manual steps** from their order → invoice → account process. Built by Koze, who is
**learning ASP.NET**. **Current state and next steps: `docs/handover.md`** (read it first).

## HARD RULES (never break, no exceptions)
1. **No secrets** in code, appsettings*.json, commits, issues, PRs or logs. Never print password/API-key values.
2. **Secrets only via `dotnet user-secrets`** (dev) or environment variables / App Service settings (server).
3. **Never commit `*.db`, `*.db-shm`, `*.db-wal`**, `bin/`, `obj/`, `._*`, `.DS_Store`, or anything from the old
   LoopCasts Dropbox copy (`../LoopCasts`: passwords, customer e-mails). Check `git status` before every commit.
4. **Never edit a committed migration** (`Migrations/*`). Change the model → `dotnet ef migrations add <Name>`.
   Only a migration that is not yet on `main` may be removed (`dotnet ef migrations remove`).
5. **The GitHub repo stays private** (`massirr/loopcasts-web`). Never change visibility, never force-push `main`.
6. Never trust the browser: prices from `Plans`, plan/country/ownership checked server-side.

## Working habits
- **One issue per session** = one branch `feat/<issue>-<slug>` = one PR. CI (build + test) green before merge.
- **Read only the files you need.** Never load `docs/log/`, `docs/ai-evaluation.md` or `../cowork` unless the task
  is about them. Find code via the map below, not by reading whole folders.
- Non-trivial feature: write `docs/specs/<issue>-<slug>.md` from `docs/specs/_template.md` before coding.
- Skill `security-check` before every commit.
- Finish an issue in its PR: skill `agent-log` (append only), move the spec to `docs/specs/done/`, **replace**
  `docs/handover.md` (current state only, max ~60 lines).
- **One fact, one place**: link instead of copying. Where a doc must match the code, add a test (`DocsTests`).

## Map
| Need | Look in |
|---|---|
| Where is the code for X, design rules, known limits | `docs/architecture.md` |
| Does a new automation go in code or n8n? | `docs/decisions/0001-n8n-or-code.md` (all ADRs: `docs/decisions/`) |
| n8n, Mailpit, chat assistant | `docs/n8n.md` |
| Deploy | `docs/deploy-azure.md` |
| Any UI work | skill `loopcasts-ui` (read it first) |
| Why the context is kept small | `docs/analysis/context-budget.md` |

## Stack and commands
- .NET 10, ASP.NET Core Razor Pages, EF Core 10 + SQLite (`loopcasts.db`), ASP.NET Identity (roles Admin, Member).
  Own CSS in `wwwroot/css/site.css`, no Bootstrap; jQuery only for form validation.
- Run: `dotnet watch` (port 5180; exit code 134 = port still used by another run). macOS, VS Code.
- Test: `dotnet test tests/LoopCastsWeb.Tests` (xUnit, SQLite in-memory). Not in the repo root (two project files → MSB1011).

## How to work with Koze
- He is learning: explain each change in 1–2 sentences, compare to WPF/MVVM when useful.
- Prefer guiding over doing: for small learning pieces, give hints/TODOs and let him write the code.
- Concise and visual (tables, small diagrams). His input is partly voice-typed.
- Ask before big changes (architecture, new packages, deleting files or migrations).
