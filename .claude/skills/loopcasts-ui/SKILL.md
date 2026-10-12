---
name: loopcasts-ui
description: LoopCasts visual identity (workflow thumbnails + human touch). Use before creating or restyling ANY page, partial, component, CSS or animation in LoopCastsWeb. Overrides generic frontend-design choices and Bootstrap defaults.
---
# LoopCasts UI

Direction chosen by Koze: **"Workflow Canvas + Above the Bike Shop"**.
LoopCasts teaches automation, so every course is drawn as a small workflow. And LoopCasts is
five real people above a bike shop in Ghent, so real names and one handwritten note give it a face.
Reference picture: the "LoopCasts Style Directions" artifact (A + B).

Precedence: Koze's words > this skill > `frontend-design` skill > anything else.
The `frontend-design` skill is installed: use it for craft, but never let it change the tokens below.

## 1. Tokens (copy into `wwwroot/css/site.css`, see `tokens.css` next to this file)
| Token | Value | Use |
|---|---|---|
| `--canvas` | `#f6f7f4` | page background. Dot grid (`--dot #d9ddd6`, 16px) is **fixed to the window** (a `position:fixed` layer behind the page, not `background-attachment:fixed`) and **fades out** from ~30% to ~75% of the window height, so forms and tables sit on plain canvas |
| `--surface` | `#ffffff` | cards, forms, nav |
| `--thumb` | `#eef1ec` | workflow thumbnail background |
| `--ink` | `#17202a` | text, borders, filled end node, primary dark button |
| `--ink-2` | `#5a6573` | secondary text, meta |
| `--line` | `#d4d9d2` | 1px borders, workflow edges use `#9aa59a` (`--edge`) |
| `--sign` | `#f2c230` | THE main action (See plans, Order, Pay). One per screen. |
| `--enamel` | `#1f5f8b` | links, handwritten note, focus ring |
| `--beginner` / `--intermediate` / `--advanced` | `#3f9d7a` / `#e0a030` / `#d4513c` | ONLY for course level (trigger node, level tag) |
| semantic | ok `#2f8a5b`, warn `#b7791f`, error `#c0392b` | form errors, paid/unpaid badges |

Light theme only for now. Never put a level colour on a button or nav.

## 2. Type (Google Fonts link in `_Layout.cshtml` `<head>`, with fallbacks)
- **Archivo** (variable width 62–125, weights 500/800): headings.
  - Page hero `h1`: `font-stretch:118%`, weight 800, UPPERCASE, `clamp(1.9rem,4.5vw,3.4rem)`, line-height .98, letter-spacing -.01em, `text-wrap:balance`. Only the hero h1 is uppercase.
  - `h2`–`h4`: normal width, weight 800, sentence case.
  - Logo word "LoopCasts": Archivo 800.
- **Public Sans** 400/600: body, forms, buttons. 16px base, line-height 1.55, max 65ch for running text.
- No monospace font. Numbers (meta, prices, invoice numbers, tables) use Public Sans with `font-variant-numeric: tabular-nums`.
- Meta is plain text separated by spacing (gap 12px), never middle dots: `Beginner  12 videos  1 h 20 min`.
- No uppercase labels or eyebrows above headings. The hero h1 is the only uppercase text.
- **Gochi Hand**: the human note. Max ONE per page, short (≤ 10 words), colour `--enamel`, rotated -2deg, starts with `↳`.
  Example: `↳ made by five people above a bike shop in Ghent`.

## 3. Layout rules (from Koze's STYLEGUIDE.md, keep exactly)
- Spacing scale: multiples of 4px (4, 8, 12, 16, 20, 24, 32, 48, 64). Lay out groups with flex/grid + `gap`, not margins.
- Container: `width:min(100%,1320px); margin-inline:auto; padding-inline:24px` (12px below 720px).
- Breakpoints: **720px** (stack to one column, smaller hero) and **480px**.
- Card grids: `grid-template-columns:repeat(auto-fill,minmax(280px,1fr)); gap:20px`.
- **Containment:** nothing crosses its parent's padding box. `min-width:0` on every flex/grid child that holds text;
  `overflow-wrap:anywhere` on titles, e-mails, VAT numbers; tables inside `.table-scroll { overflow-x:auto }`.
- The body never scrolls sideways at 360px width. Check this for every page.
- Radius: cards 8px, buttons 6px, thumbnails 5px, tags 4px. No big shadows; cards are 1px `--line` borders.

## 4. Components
- **Nav (decided):** sticky, translucent (`rgb(255 255 255 / .55)` + `backdrop-filter: blur(6px)`), 1px bottom border, z-index 10.
  Left: logo. Right, always visible: Courses, Pricing. Logged out: "Log in" outline button.
  Logged in: ONE account button (initials chip + first name) that opens a menu: "Signed in as {email}",
  My courses, Account settings, Admin (**only for the Admin role**), divider, Log out.
  Menu: `aria-expanded`, Esc and outside click close it, focus moves into it, opens with scale .96→1 + fade
  from the button's corner (`transform-origin: top right`), closes the same way.
  Below 720px: Courses/Pricing/account collapse into one menu icon that opens a full-width sheet with all links.
  Logo mark = two rounded rectangles joined by a line (outline node → filled node), SVG in `Pages/Shared/_Logo.cshtml`.
- **Buttons:** `.btn-sign` (yellow `--sign`, ink text, `box-shadow: inset 0 -3px 0 rgb(0 0 0 / .18)`),
  `.btn-ink` (ink fill, white text), `.btn-outline` (transparent, 2px inset ink). Min height 40px.
- **Course card:** surface, 1px border, 8px radius, padding 10–12px. Order: workflow thumbnail → title (Archivo 800)
  → meta line (level word in its level colour, darkened for contrast) → "Recorded by {Instructor}" with initials avatar. Same order on every card.
- **Workflow thumbnail** (the signature, see §5).
- **Person chip:** 20–28px circle, `--enamel` fill, white initials. Used for instructors and support.
- **Support line** (footer): "Questions? Mail support@…" with the address as plain, selectable text.
- **Level:** the word (Beginner / Intermediate / Advanced) in bold, coloured with a darkened level tone (`#2e7a5e`, `#8a5d0c`, `#b23e2b`) for 4.5:1 contrast.
- **Forms:** labels above inputs, 1px `--line` border, 6px radius, focus = 2px `--enamel` outline with 2px offset.
  Errors under the field in `error` colour with what to fix ("Enter a VAT number like BE0123456789").
- **Team strip:** not used until real photos exist.

## 5. Workflow thumbnail (Razor partial `Pages/Shared/_WorkflowThumb.cshtml`, model = Course)
Drawn as inline SVG, `viewBox="0 0 160 70"`, background `--thumb`, aspect-ratio 16/7, `max-width:100%`.
Deterministic from the course, so the same course always looks the same:
- **Trigger** (circle r=13 at x=20): fill = level colour. Icon: Beginner ▶ play, Intermediate + plus, Advanced ⚡ bolt.
- **Steps:** `n = clamp(ceil(VideoCount/4), 2, 4)` white rounded squares (2px ink stroke), 22px (18px when n = 4). Null VideoCount → 2.
- **Branch:** if `Course.Id % 3 == 0` OR the title contains "error", "if", "routing", "human" → split into two
  parallel steps after the first step, merging before the end.
- **More variety (#18):** when there is no branch, AI words ("ai", "agent", "rag", "mcp", "llm", "prompt") hang a small tool
  node under one step; schedule words ("cron", "schedule", "loop", "retry", "batch", "every") or `Id % 3 == 1` draw a loop arc
  from the last step back to the first. A branch with an even Id splits right after the trigger.
- **End node:** filled ink square. Edges: 2px `--edge`, curved (`C`) on branches.
- Put the geometry in a C# helper `Services/WorkflowThumb.cs` (pure function → list of nodes/edges) so it's unit-tested.
- `aria-hidden="true"` on the SVG; the card title carries the meaning.

## 6. Human touch (data)
- Courses get an `Instructor` (string, default "Marieke De Smet") via a migration when the course card needs it.
- Real names only where they help the customer (e.g. "Recorded by Marieke" once there are several instructors); never invent people.
- Copy voice: plain, friendly, small-company, first person plural ("we"). Slogan: "Learn automation. Stop doing boring work."
  Never invent numbers; use the handbook (60 courses, 2 new videos a week, since 2019, Ghent).
- **Write about what the customer gets, never about how we work inside.** No "nobody retypes", "not a bot",
  "the same workflow we teach", Excel-colour jokes. No defensive claims that raise suspicion.
- Keep: "↳ made by five people above a bike shop in Ghent" (home hero only).
- Support line everywhere: "Questions? Mail support@…" (plain text e-mail from `Seller:SupportEmail`). No staff names in it.
- "Recorded by …" on course cards only when more than one instructor exists in the catalogue.
- No initials-only team strip until real photos exist.
- No middle dots (·) anywhere in UI text; use sentences, line breaks or separate elements.

## 7. Motion (CSS in `site.css`, tiny JS in `wwwroot/js/site.js`; no animation libraries)
In ASP.NET the server only sends HTML; animation is plain CSS/JS that runs in the browser, exactly like a static site.
Rule from `frontend-design`: ONE motion that plays by itself; everything else answers what the user does.
- **The one automatic moment (home hero):** the panel "What happens when you order" draws the site's own workflow
  (Customer → New order → Payment → Invoice → Access): edges draw with `stroke-dashoffset`, nodes pop in, ~1.6s total, once.
  A "Play again" button replays it.
- **Course card hover/focus:** edges turn ink and a dash flows along them (`stroke-dasharray:4 5; animation: flow .9s linear infinite`).
  Border turns ink. No lift, no shadow.
- **Order confirmation:** the steps Paid → Invoice → Account → E-mail light up (ink fill) one by one, 350ms apart,
  reflecting what really happened on the server.
- **Buttons:** press = `translateY(1px)` and the inset shadow shrinks, 0.12s.
- Never: staggered fade-in of cards, scroll-triggered reveals, card lift on hover, parallax.
- **Always** wrap animations in `@media (prefers-reduced-motion: no-preference)`; with reduced motion show the final state.
- Reference implementation: `docs/design/loopcasts-home-preview.html` (open in a browser): thumbnail rules in JS, hero draw, flow, step sequence. Port it to Razor partials + site.css/site.js.

## 8. Page map (multi-page site, one job per page)
| Page | Content in this style |
|---|---|
| Home `/` | Hero (uppercase slogan, handwritten note, See plans + Browse courses, team strip) + "What happens when you order" workflow panel; **6** course cards + "All courses" link; support line |
| Courses `/Courses` | All live courses as cards, grouped or filterable by level |
| Pricing `/Pricing` | 4 plans as cards; one `.btn-sign` per plan; Team plan mentions "5 seats" |
| Order → Pay → Done | 3-step progress drawn as workflow nodes; form + summary as one centred block (max ~960px), summary top-aligned and sticky on desktop, stacked under 720px; Pay = one `.btn-sign` with a "Paying…" state that blocks double clicks |
| OrderThanks | The Paid → Invoice → Account → E-mail step sequence, then invoice summary + set-password link |
| My courses `/My` | Plan, access end date, invoices table, team seats (one card: list + inline add row); setup link as a "Copy link" button |
| Invoice | Print-friendly: no dot grid, no nav (`@media print`), plain ink on white |
| Admin `/Admin/*` | Same tokens, quieter: no handwritten note, no hero, no thumbnails; dense tables in `.table-scroll` |
| Account settings (`/Identity/Account/Manage/*`, our own views) | Left list (desktop) / stacked (phone) with: **Login and e-mail**, **Password**, **Two-factor login** (for every account), **Your data** (download, delete account; invoices kept as the law requires). No username or phone number fields. |
| Identity pages (login, reset password) | Centered card on the canvas, logo above, one `.btn-sign` |

## 9. Implementation rules
- New markup uses these classes, not Bootstrap. Migrate page by page; when no page uses Bootstrap CSS any more,
  remove `lib/bootstrap` from `_Layout.cshtml` (keep jQuery validation for forms).
- Global styles in `wwwroot/css/site.css` (tokens + components). Page-only tweaks in CSS isolation files
  (`Pages/X.cshtml.css`). Keep `asp-append-version="true"` on CSS/JS links so browsers reload changes.
- Repeated markup → partials (`_CourseCard`, `_WorkflowThumb`, `_PersonChip`, `_Logo`) instead of copy-paste.
- Before finishing: check 360px, 720px and 1320px widths, keyboard focus visible on every control, colour contrast
  ≥ 4.5:1 for text (yellow `--sign` only with ink text).

## 10. Feel (from the `apple-design` skill, adapted; loopcasts-ui wins on fonts and colours)
- Feedback on press, not release: every button/link has an `:active` state (translateY(1px) or scale(.97)).
- Every interactive element has hover, `:active`, `:focus-visible`, disabled and (for submits) loading states.
- Transitions never block input and can be interrupted (CSS transitions on transform/opacity, not keyframes, for menus).
- Things close the way they opened (same path, mirrored easing); `transform-origin` = the element that opened it.
- `prefers-reduced-motion: reduce` → opacity cross-fades only, no movement, no looping flow on cards.
- `prefers-reduced-transparency: reduce` → solid nav and menus, no blur.
- `prefers-contrast: more` → solid surfaces, 2px borders, no dot grid.
- Ignore apple-design's "prefer the system font" and its drag/gesture/spring sections (no drag UI here).
- Stress-test with `break-ui` (long names, long e-mails, empty lists, 5/5 seats) and phones with `mobile-native` when installed.
