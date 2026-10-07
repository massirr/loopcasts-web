# Agent log

What the coding agent did, per issue. Newest at the bottom. Format: see `.claude/skills/agent-log/SKILL.md`.

## 2026-10-07 — Setup: repo, agent rules, tests, CI, backlog
- **What:** private repo `massirr/loopcasts-web`; CLAUDE.md hard rules on top; 6 skills in `.claude/skills/`;
  `docs/specs/_template.md`; xUnit project (VAT, invoice numbering, account expiry, 22 tests); CI build + test;
  8 user-story issues.
- **Why / decisions:** rules first in CLAUDE.md ("lost in the middle"); details in skills so they load only when
  needed (context budget). Tests run on SQLite in-memory = real unique indexes, no mocking package.
  Test project lives in `tests/` → `Compile Remove="tests/**"` in the web csproj (the Web SDK globs every .cs).
- **Manual steps for Koze:** `gh auth refresh -s project` so the agent can create the Project board.
- **Learn:** the in-memory SQLite db exists only while its connection is open — like a ViewModel that lives as long
  as its View.

## 2026-10-07 — #1 Customer account + login after payment  (PR, see below)
- **What:** `MemberService` creates an Identity user (role Member, no password) when an order is paid; OrderThanks
  shows a "Set your password" link (default Identity ResetPassword page). Navbar: "Log in".
- **Security fixes found while specifying:**
  1. `/Pay?id=` and `/OrderThanks?id=` were guessable (1, 2, 3…) → anyone could read other customers' names/e-mails
     (and with #1 grab their setup link). Now `Order.AccessKey` (128-bit random) is required in both URLs.
  2. Overposting: the order form bound the whole `Order` entity, so a crafted POST with `Order.IsPaid=true`
     gave access without paying → `[BindNever]` on Id/Price/CreatedAt/IsPaid/PaidAt/AccessKey.
  3. Setup link only on the e-mail's first paid order: otherwise ordering with someone else's e-mail = their account.
- **Why / decisions:** login is created after the payment transaction (a failing login never loses a payment;
  idempotent, retried on the next call). Link not e-mailed yet → #5.
- **Tests:** 2 member tests + key test (27 total). Manual e2e with curl on a scratch db: overposting ignored,
  404 without key, set password via link, log in, link gone afterwards, 2nd order with same e-mail shows no link.
- **Manual steps for Koze:** none (migration `AddOrderAccessKey` runs at startup and gives old orders a key).
- **Learn:** `[BindNever]` is the Razor Pages answer to "the View may only change what the ViewModel exposes":
  model binding fills every public property it finds in the form unless you forbid it.
