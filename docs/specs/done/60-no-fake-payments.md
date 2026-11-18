# No fake payments on the live site  (#60)
Status: done · Area: orders · Tests: unit + full suite

## Goal
Without a Stripe key the live site must never fake a payment: no paid order, no invoice, no account. Staff no
longer have to find and cancel free "mock" orders by hand.
As a LoopCasts staff member, I want the live site to refuse fake (mock) payments when no Stripe key is set,
so that nobody gets course access for free and I don't have to find and cancel those orders by hand.

## Acceptance criteria
1. [x] Given a non-Development environment without `Stripe:SecretKey`, when a customer opens /Pay or posts the mock
   payment, then the order stays unpaid: no invoice, no account.
2. [x] Given that same situation, the customer sees a clear message (no error page, no fake "thank you"): online
   payment is not available right now, the order is saved, contact us to pay by bank transfer.
3. [x] Given Development without a key, the mock checkout works as before.
4. [x] Given a missing key outside Development, one warning is logged at startup, without any key value.
5. [x] Tests cover the non-Development (refused) and Development (mock works) cases.

## Affected code
- `Pages/Pay.cshtml.cs`: `Unavailable` = no key and not Development; `OnPost` returns the page without paying
- `Pages/Pay.cshtml`: message with support e-mail instead of the pay button
- `Program.cs`: one startup warning (existing `StripeService.Enabled` reused, no change there)
- Migration: none
- Security: the refusal must be server-side on the POST, not only a hidden button

## Out of scope
- Setting the real Stripe key on Azure; webhook signature check.
- Admin "mark paid" (bank-transfer fallback) stays unchanged.

## Reviews
- Reviewer (business): criteria 1-5 met by the diff and `PayPageTests`; nothing out of scope built → criteria 1-5. Left: none
- Reviewer (security): refusal server-side in `OnPostAsync`; only admin mark-paid (kept on purpose) and verified
  Stripe sync mark orders paid; warning logs no key → criteria 1, 4. Left: none
- Reviewer (UX): message clear, layout holds without the form; support e-mail is still the placeholder
  `support@loopcasts.example` → criterion 2. Left: set `Seller:SupportEmail` on Azure before go-live.

## Notes
- Decisions by Koze (18/11/2026):
  - Message: "unavailable + contact"; order stays saved and unpaid, admin can mark it paid after bank transfer.
  - Mock allowed in **Development only**; Production, Staging and any other environment refuse.
- Tests: `PayPageTests` (4 tests: Production + Staging POST refused, GET shows message, Development mock works)

- Why the "old tab" trick tests a direct POST (manual check 3): every form carries a hidden antiforgery token, a
  stamp that proves "this site made this form". A curl POST has no stamp, so ASP.NET answers 400 before our code
  runs: it tests nothing. A /Pay tab opened in Development has a real stamp, and the Production run still accepts
  it (same machine, same signing keys in `~/.aspnet/DataProtection-Keys`). So clicking that old button sends a
  real POST that only our `Unavailable` check can stop. Done by Koze 18/11/2026: order stayed unpaid.

## Manual check
- Development, no key: `http://localhost:5180/Order` → order → /Pay → mock pay → expect OrderThanks as before.
- `ASPNETCORE_ENVIRONMENT=Production dotnet run --no-launch-profile --urls http://localhost:5180` (no key): /Order → /Pay → expect the
  "not available, contact us" message, no pay button; `/Admin/Orders` shows the order unpaid; log has one warning.
- Same Production run: POST the mock form directly (curl / devtools) → still unpaid.
