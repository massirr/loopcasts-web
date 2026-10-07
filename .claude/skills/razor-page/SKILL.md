---
name: razor-page
description: Add or change a Razor Page (PageModel + .cshtml) in LoopCastsWeb following project conventions (PRG, server-side validation, AdminOnly folder, Bootstrap). Use when creating pages or page handlers.
---
# Razor Page

- Files: `Pages/<Name>.cshtml` + `Pages/<Name>.cshtml.cs`, namespace `LoopCastsWeb.Pages[.Folder]`.
- Inject `AppDbContext` / services through the constructor (like a ViewModel getting its services in WPF).
- `OnGetAsync` loads, `OnPost<Handler>Async` changes, then **`return RedirectToPage(...)`** (Post-Redirect-Get).
- Validate on the server: `if (!ModelState.IsValid) return Page();`. Never trust hidden fields for price/plan/owner.
- Pages that show customer data: check ownership (`User` e-mail/id must match the row) or return `NotFound()`.
- Admin pages go in `Pages/Admin/` (folder already locked by the `AdminOnly` policy).
- Logged-in-only pages: `[Authorize]` on the PageModel or `AuthorizeFolder` in `Program.cs`.
- Use Bootstrap 5 classes from the template; no new CSS/JS libraries.
- Money: `@($"€{x:0.00}")`; dates `dd/MM/yyyy`.
- Business logic goes in `Services/` (testable), not in the PageModel.
