---
name: unit-test
description: Write xUnit tests in tests/LoopCastsWeb.Tests for services and business rules (VAT, invoices, accounts, payments). Use when adding logic or fixing a bug.
---
# Unit test

- Project: `tests/LoopCastsWeb.Tests` (xUnit). Run: `dotnet test tests/LoopCastsWeb.Tests`.
- Database tests: `TestDb.Create()` gives an `AppDbContext` on SQLite **in-memory** (real SQL, real unique indexes).
- Name: `Method_Situation_Expected`. Arrange / Act / Assert.
- Test business rules, not the framework: one test per rule from the handbook, plus the edge case that bit us.
- Bug fix: first a failing test that shows the bug, then the fix.
- Logic in a PageModel that needs a test → move it to `Services/` first.
- No new mocking packages; use `NullLogger<T>.Instance` and small fakes (e.g. a fake `HttpMessageHandler`).
