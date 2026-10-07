# Spec: Redesign the site with the loopcasts-ui style  (#16)

## User story
As a visitor of LoopCasts, I want a site that looks like LoopCasts (workflow thumbnails, real people, one clear
action per page), so that I trust a five-person company in Ghent and immediately see what an automation course is.

## Source of truth
`.claude/skills/loopcasts-ui/SKILL.md` (wins over everything except Koze), reference
`docs/design/loopcasts-home-preview.html`. Craft from the `frontend-design` skill.

## Design review (frontend-design pass, before building)
- Signature = the workflow thumbnail + the hero drawing the site's OWN order workflow. Everything else stays quiet:
  1px borders, no shadows, one yellow action per screen.
- Kept from the reference: dot-grid canvas, Archivo wide uppercase hero, Gochi Hand note (max one per page).
- Changed vs. a generic build: course cards are **not** links (there is no course page and the single-course order
  doesn't know the course yet) → hover/focus-within runs the flow; the page has one honest "See plans" action.
- Copy from the handbook only: Ghent, above a bike shop, 60 courses, two new videos a week, Lotte answers support
  (handbook ch. 1 + 6). No invented numbers.
- Removed: monospace font (`.mono` → `.num` = tabular figures), Bootstrap classes, card lift, staggered fade-in.

## Design
- **Data:** `Course.Instructor` (default "Marieke De Smet"), migration `AddCourseInstructor`.
- **Logic:** `Services/WorkflowThumb.cs` — pure function course → nodes + edges (geometry of the reference),
  unit-tested: steps = clamp(ceil(videos/4), 2, 4), null → 2; branch if Id % 3 == 0 or the title has the word
  error/if/routing/human; 22px steps, 18px when 4; everything inside the 160×70 viewBox.
  `Course.DurationText` → "1 h 20 min".
- **Partials:** `_Logo`, `_WorkflowThumb`, `_CourseCard`, `_PersonChip`, `_AdminNav`, `_SupportLine`.
- **Layout:** Google Fonts, sticky translucent nav, footer with Lotte's support line; Bootstrap CSS + JS removed
  (jQuery + validation stay). Nav/footer styles in `_Layout.cshtml.css`.
- **Pages (page map §8):** Home (hero + order-workflow panel + 6 cards), Courses (by level), Pricing (4 plan cards),
  Order (form ≤ 560px + summary card), Pay, OrderThanks (Paid → Invoice → Account → E-mail sequence; E-mail only
  "sent" when n8n is configured), My, Invoice (print: no grid/nav), Admin (quiet: no note/hero/thumbs, dense
  tables), Privacy, Error.
- **Identity:** own views for Login, Register, Forgot/Reset password (+ confirmations), Access denied, Lockout under
  `Areas/Identity/Pages/Account/` using the Identity UI PageModels → centered card, logo, one `.btn-sign`.
  `Account/Manage/*` keeps the Identity package layout (its own Bootstrap copy) — follow-up.
- **Motion (§7 only):** hero draw once (~1.6 s) + "Play again"; card hover/focus flow; order-step sequence;
  button press. All inside `prefers-reduced-motion: no-preference`.
- **Seller:** handbook ch. 1–6 state no address or VAT number → `Seller:*` stays empty (logged).

## Test plan
- Unit: thumbnail rules, duration text.
- Manual: every page at 360 / 720 / 1320 px — no sideways scroll, visible focus (scripted headless Chrome);
  before/after screenshots of Home, Courses, Order, /My in `docs/design/screenshots/`.
