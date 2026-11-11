# ADR 0003: Stripe instead of Mollie

Status: accepted (11/11/2026). Trigger: #53, turning on real (test) payments on the live site.

## Decision
Payments go through **Stripe Checkout in test mode** (`StripeService`). Mollie (#3) is removed.

## Why
- **Mollie needs a real company**: its sign-up asks for legal form, company number and address before it gives
  a test key. LoopCasts BV is fictional; entering made-up company data would break Mollie's user agreement.
- **Stripe test mode needs only an e-mail**, is free, and moves no money (test card `4242 4242 4242 4242`).
- Same design as before, so the swap stayed small: one service, one webhook; `PaymentService.MarkPaidAsync`
  (invoice → account) did not change. `Order.MolliePaymentId` became the neutral `Order.PaymentId`.

## How it stays safe
- REST via typed `HttpClient`, **no new package** (same as the Mollie code).
- The webhook body is never trusted: only the session id is read, then the status is fetched from Stripe.
  The session must be the one stored on the order, `payment_status` = `paid`, amount and currency must match.
  So no webhook signature check is needed for trust (could be added later as defence in depth).
- Key only in user-secrets (`Stripe:SecretKey`) / App Service setting (`Stripe__SecretKey`).

## Not done
Live key, refunds, credit notes.
