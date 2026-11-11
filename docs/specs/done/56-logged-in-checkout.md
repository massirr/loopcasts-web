# Logged-in customer buys without friction  (#56)
Status: done · Area: orders · Tests: full suite

## Goal
A logged-in customer can start buying from a course page, sees what they buy on every step, and is not asked
again for what the site already knows (e-mail, login). Found by Koze testing on a phone after #53.
As a logged-in customer, I want a quick, clear checkout, so that I know I bought the right thing.

## Acceptance criteria
1. [x] Given a course page, then there is a clear button to get access: to that course when a single-course plan
   exists, else to /Pricing ("all courses").
2. [x] Given a single-course plan, when I order it, then the course is chosen (pre-selected from the course page),
   saved on the order server-side (must be a real course) and shown on /Order, /Pay, OrderThanks and the invoice.
3. [x] Given I'm logged in, when I open /Order, then the e-mail comes from my account and is not asked again
   (server uses the account e-mail, not a posted one).
4. [x] Given I'm logged in and just paid, then OrderThanks links straight to /My, no "log in" message.
5. [x] /Order and /Pay show the plan name and what it includes.
6. [x] Tests cover criteria 2 and 3 (and 4 where the logic is in a service).

## Affected code
- `Pages/Shared/_CourseCard.cshtml` (+ `.course-buy` in `site.css`): "Buy for €39" → `/Order?plan=single&course=<id>`
- `Pages/Order.cshtml(.cs)`: course dropdown, login e-mail (server-side), name from last paid order
- `Pages/Pay.cshtml(.cs)`, `Pages/OrderThanks.cshtml(.cs)`: course title, plan detail, `SignedInAsBuyer`
- `Models/Order.cs` (`CourseId`, `Course`, `ProductName`), `Models/Plan.cs` (`Plans.Describe`)
- `Services/PaymentService.cs` (invoice description), `Services/CustomerData.cs` (/My)
- Migration: `AddOrderCourse` (nullable column + index + FK)
- Security: course must be live, other plans never keep one; logged-in e-mail from the login, not the form

## Out of scope
- Chat, footer, account chip, loaders (#57). New pricing or plans.

## Reviews
- Reviewer (Business): /Pay lacked the plan detail; card had no fallback without a single plan; GET side untested
  → criteria 1, 5, 6. Fixed all three (3 extra tests). Left: subscribers still see "Buy for €39" on cards.
- Reviewer (Security): name prefill could come from an unpaid order typed by anyone → criterion 3. Fixed: paid
  orders only. Left: none.
- Reviewer (UX): course summary changed without `aria-live` → criterion 2. Fixed. Left: none.

## Notes
- Decisions by Koze: LoopCasts sells subscriptions; if a single-course option exists, the customer must be able
  to choose the course (11/11/2026). It exists ("single", €39) but had no course choice: root cause of finding 2.

- Tests: `CheckoutPagesTests` (14 tests incl. theories). Browser at 390px: visitor + logged-in flows with Stripe
  test card, /My shows the course.

## Manual check (phone size, logged in)
- `http://localhost:5180/Courses` → open a course → buy button → expect plans or that course pre-selected.
- `/Order` → expect plan name + e-mail from account, no e-mail field.
- Pay with `4242 4242 4242 4242` → OrderThanks → expect link to /My, no "log in" text.
