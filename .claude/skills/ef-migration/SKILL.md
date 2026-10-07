---
name: ef-migration
description: Change the EF Core model and create a migration safely (SQLite, never edit committed migrations). Use whenever a Model or AppDbContext changes.
---
# EF migration

1. Change `Models/*` and/or `Data/AppDbContext.cs`.
2. `dotnet ef migrations add <PascalCaseName>` (in repo root).
3. Read the generated `Up()`: no unexpected `DropTable`/`DropColumn`; SQLite rebuilds tables for some changes — fine.
4. `dotnet build` and `dotnet test tests/LoopCastsWeb.Tests` (tests use `EnsureCreated`, so the model must be valid).
5. `Program.cs` applies migrations at startup; no manual `database update` needed.

Rules:
- **Never edit or delete a migration that is already on `main`.** Fix forward with a new migration.
- Unmerged and wrong? `dotnet ef migrations remove` (only the last one, only if not applied elsewhere).
- Snapshot fields (`Order.Price`, `Invoice.Billing*`) never get recomputed in a migration.
- Money `decimal`, date-only `DateOnly`, times UTC.
