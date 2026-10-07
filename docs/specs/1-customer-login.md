# Spec: Customer account + login after payment  (#1)

## User story
As a customer who just paid, I want a login for my LoopCasts account (I choose my own password),
so that nobody at LoopCasts has to create my login by hand or send me a password.

## Acceptance criteria
- [ ] `MarkPaidAsync` ensures an Identity user (role **Member**, no password) for the order e-mail; idempotent.
- [ ] OrderThanks shows a "Set your password" link (reset token) while the user has no password.
- [ ] Returning customer: same user, no duplicate.
- [ ] Navbar: "Log in" (for customers and staff).

## Design
- `Services/MemberService.cs`: `EnsureMemberAsync(email)`, `PasswordSetupCodeAsync(user)` (UserManager).
- `PaymentService` calls `EnsureMemberAsync` **after** the payment transaction: a failure never loses the payment;
  the next call (webhook retry) tries again.
- Link = default Identity UI `/Identity/Account/ResetPassword?code=<base64url token>` (valid 1 day).
- Security (found while specifying):
  - `/Pay?id=` and `/OrderThanks?id=` were guessable (sequential ids) → add `Order.AccessKey` (random, 128 bit),
    required as `key=` in both URLs. Migration `AddOrderAccessKey` fills existing rows.
  - Overposting: `[BindProperty] Order` let a crafted POST set `IsPaid`/`Id` → `[BindNever]` on server-owned fields.
  - Setup link only when this order is the e-mail's **first** paid order: otherwise someone could order with
    another person's e-mail and take over their account. Returning customers: "Forgot password" (e-mail via #5).

## Out of scope
E-mailing the link (#5), /My page (#2).

## Test plan
- Unit: member created once with role, no password; payment twice → one user; returning customer → one user.
- Manual: order → pay → "Set your password" → set → log in.
