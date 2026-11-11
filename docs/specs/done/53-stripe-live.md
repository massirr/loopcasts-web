# Stripe test payments on the live site  (#53)
Status: done · Area: orders · Tests: full suite

## Goal
Replace Mollie (#3) with Stripe Checkout in test mode, locally and on Azure, so a paid order creates the invoice and
account automatically and the mock checkout no longer gives free access on the live site.
As a customer, I want to pay with a real (test) payment, so that my access starts without a manual step.

## Acceptance criteria
1. [x] Given `Stripe:SecretKey` in user-secrets, when I order locally, then /Pay redirects to Stripe Checkout and
   a paid session creates invoice + account (on return, and via webhook).
2. [x] Given a cancelled or unpaid session, then no invoice or account is created.
3. [x] The webhook only takes the session id and re-fetches the status from Stripe; a forged body cannot mark
   an order paid. The session must belong to the order (metadata/client reference) and the amount must match.
4. Moved to follow-up (handover): live test payment needs merge + deploy first.
5. [x] No key value in code, appsettings, commits or logs.
6. Moved to follow-up (handover): blocking the mock on live, not decided yet.
7. [x] Mollie code and config removed; docs (architecture, deploy, handover) say Stripe; ADR 0003 explains why.
8. [x] Tests cover criteria 2 and 3 (6 moved).

## Affected code
- `Services/StripeService.cs` (new, typed HttpClient, no package); `Services/MollieService.cs` deleted
- `Program.cs`: HttpClient registration + webhook `POST /webhooks/stripe`
- `Pages/Pay.cshtml(.cs)`: Stripe instead of Mollie, mock when no key
- Migration: `RenamePaymentId` (`Order.MolliePaymentId` → `PaymentId`, RenameColumn, data kept)
- Tests: `StripeServiceTests` (replaces `MollieServiceTests`; `FakeMollie` → `FakeApi`)
- Docs: architecture, deploy-azure, README, ADR 0001 row, new ADR 0003
- Security: price from `Plans`, webhook re-fetch, key only via user-secrets / App Service settings

## Out of scope
- Live (non-test) key, refunds, SMTP, Stripe webhook signature check (re-fetch covers trust).

## Reviews
- Reviewer (Business): handover still said Mollie; PaymentId overwrite on a second checkout ignored a paid first
  tab → criteria 3, 7. Fixed: open session reused (`Create_ReusesStillOpenSession`), handover rewritten.
  Left: no page test for "paid on return" (checked by hand).
- Reviewer (Security): nothing blocking. Fixed: webhook refuses bodies > 64 KB. Left: no signature check (out of scope).
- Reviewer (UX): Stripe down on return gave a 500; no notice after "back" → criteria 1, 2. Fixed both on /Pay.

## Notes
- Decisions by Koze: issue via skill `user-story`; criteria 4 and 6 moved to follow-ups; Mollie dropped because onboarding needs a real company
  (LoopCasts BV is fictional); Stripe via REST, no new package; delete Mollie code. No e-mails on the live site (architecture, Known limits).
- Tests: `StripeServiceTests` (19 tests incl. theories); local test payment done by Koze (card 4242).
- Koze enters the key himself (`dotnet user-secrets set`, Azure portal); Claude never sees it.

## Manual check
- `http://localhost:5180/Order` → order a plan → /Pay → Stripe Checkout → card `4242 4242 4242 4242`, any future
  date/CVC → expect OrderThanks, invoice on /My.
- Same, press "back" on Stripe → expect order still unpaid, no invoice.
- Live: one paid test order → Admin/Orders shows it paid (via webhook).
