# Chat, footer, account chip and loaders look right  (#57)
Status: done · Area: ui · Tests: full suite

## Goal
Fix what Koze saw on his phone (11/11/2026): the chat window does not fit, the footer is not full width, the
account chip shows "TTT", and waiting has no clear loader. As a visitor on a phone or desktop, I want these to
look right, so that the site feels finished and trustworthy.

## Acceptance criteria
1. [x] At 360px, 390px, 720px and 1320px, and at browser zoom 50%–200%, the chat window stays fully on screen
   (header, messages, input and send button visible; messages scroll inside), with rounder corners.
2. [x] While the assistant is answering, the chat shows a thinking-orbs style loader instead of the current one.
3. [x] Slow buttons (Order "Continue to payment", Pay) show the same loader while waiting; still no double submit.
4. [x] With `prefers-reduced-motion: reduce` the loaders do not move (static dots or text), and they are announced
   to screen readers ("Thinking…" / busy text).
5. [x] The footer background spans the full page width on every page (content stays in the container).
6. [x] Logged in, the account chip shows the customer's initials and first name (from their last paid order),
   falling back to the first letter of the e-mail; never "TTT".
7. [x] No sideways scroll at 360px on the pages touched; DocsTests/full suite green; a test covers criterion 6.

## Affected code
- `Pages/Shared/_Chat.cshtml` ("Thinking…" status), `wwwroot/css/site.css` (orb, chat size/radius, footer)
- `Pages/Shared/_Layout.cshtml` (footer full width, `#busy-status` live region), `wwwroot/js/site.js` (busy text announced)
- `Services/CustomerData.cs` `NameAsync` (paid orders only; `_LoginPartial`/`_PersonChip` unchanged)
- Migration: none
- Security: an unpaid order typed with your e-mail can no longer rename your account chip; no new package or script

## Out of scope
- Full-page loader (Koze: chat + slow actions only). Chat answers/content. Hiding "Buy" for subscribers.

## Reviews
- Reviewer (business): criteria 1-7 met. 50% zoom, 720/1320px and a live chat answer checked afterwards in Playwright.
  Koze's own "TTT" is local data on two paid orders, not code.
- Reviewer (security): `NameAsync` ignores unpaid orders; no new secrets or third-party scripts.
  Left: no SRI on the pinned n8n CDN files (pre-existing).
- Reviewer (UX/a11y): one live region, reduced-motion fallback, no sideways scroll at 360px. Fixed: the chat no longer
  clears a button's busy text in the shared region.

## Notes
- Decisions by Koze: loaders in the chat and on slow buttons only (11/11/2026); thinking-orbs as the style
  (licence to check while building).
- thinking-orbs is MIT but a Vue canvas component (npm + build): a CSS look-alike (dotted ring as a mask) instead,
  credited in site.css. n8n's typing dots (`.chat-message-typing*`, @n8n/chat 1.41.5) are restyled; keep the pin.
- Root cause "TTT": Koze typed TTT as the name on orders 34/35 (paid). The chip shows that name, as designed.
- Tests: `CustomerDataTests.Name_IgnoresUnpaidOrders`; full suite 228 green.
- Default chosen (Koze can change): chip name = name on the last paid order, like the Order prefill (#56).

## Manual check (Playwright + Koze's phone)
- `http://localhost:5180/` at 390px → open chat → ask a question → expect orbs loader, window fits, round corners.
- Zoom 200% and 50% on desktop → chat still fits.
- Log in → account chip shows initials + first name.
- Any page → footer edge to edge. `/Order` → submit → loader on the button.
