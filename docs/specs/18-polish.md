# Spec: Polish every page  (#18)

## User story
As a customer, I want every page to feel finished: clear wording, a menu that works with keyboard and on my phone,
and my own account settings, so that I can order, pay and manage my login and data without asking support.

## Acceptance criteria
- [x] Given I'm logged in, when I open the account button, then I see "Signed in as {e-mail}", My courses, Account settings,
      Admin (only for the Admin role) and Log out; Esc, a click outside, Tab and the arrow keys work.
- [x] Given a phone (< 720px), when I tap the menu icon, then a full-width sheet shows all links and closes the same ways.
- [x] Given Account settings, then I can change my login e-mail and password, turn on two-factor login, download my data
      and delete my account (invoices kept). No username or phone fields.
- [x] Given Order → Pay → Done, then a 3-step progress is shown and Pay can't be clicked twice ("Paying…").
- [x] Given any page, e-mail and the invoice, then the text follows loopcasts-ui §6 (table below).
- [x] Given 360, 720 and 1320px, logged out, as a customer and as an admin, then no page scrolls sideways and every
      control has hover, active, focus, disabled and busy states.

## Design
- Data/model changes: none (no migration).
- Layout / nav (`_Layout`, `_LoginPartial`, `site.css`, `site.js`):
  - Dot grid = a `position:fixed` `body::before` layer, masked from 30% to 75% of the window height.
  - Account button (initials chip + first name; name from the newest order, else the e-mail before the @ —
    `CustomerData.NameAsync`) opens a menu. Below 720px one menu icon opens a sheet with all links.
  - One small menu behaviour in `site.js` for both: `aria-expanded`, focus to the first item, arrows, Esc (focus back
    to the button), outside click, focus leaving. CSS transitions (scale .96 → 1 + fade, `transform-origin` = the button),
    so a second click reverses midway. Without JS the menu opens on hover/focus (`.no-js`).
- Pages:
  - `_Progress` partial (Order → Pay → Done as workflow nodes) on Order, Pay, OrderThanks. Order = centred 960px block,
    summary sticky on desktop, on top below 720px.
  - Every submit gets a busy state (`site.js`, document-level listener so jQuery validation runs first; a second submit
    is blocked). `data-busy-text` changes the label: Pay → "Paying…". `data-no-busy` for the download form.
  - /My: title "My courses"; setup link as a "Copy link" button ("Copied" for 2 s; the text field is the fallback without
    JS or clipboard); team = one card with the seat list and the add row inline as its last item.
  - Invoice page and invoice table: body font, tabular numbers.
  - Courses/Home: `.course-grid` (12 columns) rebalances a lone last card (4+1 → 3+2, 3+1 → 2+2, 2+1 → 3);
    thumbnails keep their size in wider cards (`max-height`). "Recorded by" only when there is more than one instructor
    (`ViewData["ManyInstructors"]`). Initials team strip removed.
  - Thumbnails: `WorkflowThumb.ShapeOf` adds a loop arc (schedule words or `Id % 3 == 1`), a tool node (AI words) and an
    early split for branches with an even Id. The §5 branch rule is unchanged and comes first.
- Account settings: our own views in `Areas/Identity/Pages/Account/Manage/` with `_ManageLayout` (list left / on top):
  Login and e-mail (Index + Email), Password (ChangePassword/SetPassword), Two-factor login (TwoFactorAuthentication,
  EnableAuthenticator, ShowRecoveryCodes, Disable2fa, GenerateRecoveryCodes, ResetAuthenticator), Your data
  (PersonalData, DeletePersonalData). Plus LoginWith2fa, LoginWithRecoveryCode, ConfirmEmail views.
  - `ConfirmEmailChange` has our own PageModel: after the e-mail change it calls `CustomerData.MoveToEmailAsync`, because
    orders and seats are found by e-mail (else /My would be empty after a change). Login name = e-mail stays true.
  - Status messages: `_Flash` partial maps Identity's texts to ours (not called `_StatusMessage`: the package has one
    with that name in `Manage/` that would win).
- E-mails: `N8nNotifier.InOurWords` rewrites Identity's "Reset Password" / "Confirm your email" mails (keeps only the
  link). Invoice e-mail template written out in `docs/n8n.md`.
- States (§10): hover (only on `hover:hover` devices for buttons), `:active` (opacity, + `translateY(1px)`/`scale(.98)` when
  motion is welcome), global `:focus-visible` ring, `:disabled`/`aria-disabled`, `.is-busy` ring.
  `prefers-reduced-motion: reduce` → menus only fade, no flowing dash, no spinner turn. `prefers-reduced-transparency`
  → solid nav. `prefers-contrast: more` → no dot grid, darker lines, 2px borders.
- Security: no new endpoints; Manage pages keep Identity's `[Authorize]`; Admin menu item only for the Admin role (the
  `/Admin` folder stays locked by `AdminOnly`, checked: a customer gets Access denied). E-mail change only after the
  link in the mail is clicked (Identity token).

## Wording (loopcasts-ui §6): every changed sentence
| Where | Old | New |
|---|---|---|
| Home hero | "Questions? Lotte answers, not a bot." (+ initials strip) | removed |
| Home workflow caption | "This site runs the same kind of workflow we teach: nobody types your invoice by hand." | "Pay, and your invoice and your login are ready right away." |
| Footer | "© LoopCasts BV, Ghent · Privacy" | "© LoopCasts BV, Ghent" and "Privacy" as separate items |
| Support line (footer) | Lotte's chip + "Questions? Lotte answers, not a bot." + address | "Questions? Mail support@loopcasts.example" |
| Nav | "My LoopCasts", "Admin", "Account", "Log out" as separate links | account menu: "Signed in as {e-mail}", "My courses", "Account settings", "Admin", "Log out" |
| /My title | "My LoopCasts" | "My courses" |
| /My setup link | "…It works for 24 hours and is shown only once." + text field | "…It works for 24 hours and you see it only once." + "Copy link" → "Copied" |
| /My team | "3 of 5 seats used · team licence, order 1" | "3 of 5 seats used" and, under the list, "Team licence, order 1" |
| /My team full | add form hidden | "All 5 seats are in use. Remove someone to add a colleague." |
| /My invoices | "View / print" | "View or print" |
| Pricing | "5 seats · for companies" | "5 seats, for companies" |
| Order summary | "€149" / "excl. VAT · All courses for 12 months" | "€149 excl. VAT" / "All courses for 12 months" |
| Pay notice | "Test payment. This page stands in for the payment provider (no Mollie key set). No real money moves." | "Test payment. No real money moves: the button marks your order as paid." |
| Pay card | "LoopCasts BV · order 1" | "Order 1 at LoopCasts BV" |
| Pay card | "€599.00 excl. VAT · {e-mail}" | "Incl. VAT (€599.00 excl. VAT)" / "Invoice and login go to {e-mail}" |
| Pay button (busy) | — | "Paying…" |
| OrderThanks | "Everything below happened by itself the moment you paid. Nobody had to retype anything." | paid: "Your invoice and your access are ready. You find both on My courses." / waiting: "As soon as your payment arrives, your invoice and your access appear here." |
| OrderThanks invoice | "2026-0001 · €724.79 incl. 21% VAT" | "2026-0001" and on the next line "€724.79 incl. 21% VAT" |
| Invoice | "Paid online. Thank you! Questions? Lotte answers at support@…" | "Paid online. Thank you! Questions? Mail support@…" |
| Privacy | "Want to see, change or remove your details? Mail … and Lotte will help you." | "You can download your data or delete your account yourself, under Account settings, Your data. Invoices stay in our books, as the law requires." + "Something else you want to see or change? Mail … and we'll help you." |
| Login | "With the e-mail you ordered with. Staff use their personal account." | "Use the e-mail you ordered with." |
| Access denied | "Go to My LoopCasts" | "Go to My courses" |
| Admin Orders | "12 orders · 3 waiting for payment" | "12 orders, 3 waiting for payment." |
| Admin Orders row | "{Company} · {VAT nr}" | "{Company} {VAT nr}" (VAT nr muted) |
| Admin Invoices | "Revenue this month excl. VAT: €… · 4 invoices · calculated, not summed by colour" | "Revenue this month excl. VAT: €… from 4 invoices." |
| E-mail "Reset Password" (Identity) | "Reset Password" / "Please reset your password by clicking here." | "Choose a new LoopCasts password" / "You asked to choose a new password for your LoopCasts login. The button works for 24 hours. Didn't ask? Then ignore this e-mail; your password stays the same." + button "Choose a new password" |
| E-mail "Confirm your email" (Identity) | "Confirm your email" / "Please confirm your account by clicking here." | "Confirm your LoopCasts e-mail" / "Click the button to use this address for your LoopCasts login and invoices. Didn't ask? Then ignore this e-mail; nothing changes." + button "Confirm this e-mail" |
| Invoice e-mail (n8n, `docs/n8n.md`) | "greeting, total, button" (no text) | "Hi {name}, Thanks for your order. Your invoice {nr} for {description} is ready: €{total} incl. VAT, already paid." + "View or print your invoice" (+ "Your login is ready too. Choose your password first; the link works for 24 hours.") |
| Status messages (Identity → ours) | "Your password has been changed." / "Your authenticator app has been verified." / "Confirmation link to change email sent. Please check your email." / … | "Your password is changed." / "Two-factor login is on." / "We sent a link to your new e-mail. Click it to finish the change." / … (full list in `_Flash.cshtml`) |
| Account settings (new) | Identity's "Profile / Email / Password / Two-factor authentication / Personal data", username + phone fields | "Login and e-mail", "Password", "Two-factor login", "Your data"; no username or phone |

## Out of scope
- QR code for the authenticator app (needs a package; the key + an `otpauth:` link work).
- Orders/invoices in the personal-data download; anonymising orders after account deletion.
- Dark theme.

## Test plan
- Unit: `CustomerDataTests` (NameAsync, MoveToEmailAsync), `WorkflowThumbTests` (ShapeOf, loop, tool, early split,
  viewBox), `N8nNotifierTests` (Identity mail in our words, unknown mail unchanged). 124 tests.
- Manual (headless Chrome over the DevTools protocol, scratch DB): 42 page/role combinations × 360/720/1320 → no
  sideways scroll; account menu + sheet keys; focus ring on Tab; "Paying…" + second submit blocked; Copy link shown;
  two-factor turned on with a real TOTP code, login asks for the code and accepts it; customer gets Access denied on /Admin.
  Screenshots: `docs/design/screenshots/polish-*.png`.
