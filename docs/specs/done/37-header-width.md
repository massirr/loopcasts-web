# Use the full header width with accessible navigation (#37)

## User story
As a LoopCasts visitor, I want the logo and navigation to use the available header width, so that the shared header feels balanced and remains easy to use on phones.

## Acceptance criteria
- [x] Given a wide screen, when the shared header renders, then the logo sits at the left edge and navigation/account controls sit at the right edge.
- [x] Given a narrow screen, when the header renders, then the logo and menu control fit without wrapping or horizontal overflow and the full-width sheet remains usable.
- [x] Given the account menu, when a pointer hovers an item, then it gets a subtle smooth treatment; keyboard focus remains clearly visible and distinct from hover.
- [x] The current-page item has no round blue highlight.
- [x] Review the layout at 360px, 720px, and 1320px.

## Design
- Data/model changes (migration name): none.
- Pages / endpoints: shared header in `Pages/Shared/_Layout.cshtml` and `_LoginPartial.cshtml`.
- Services (where the logic lives): none.
- Security (authz, validation, secrets): no behavior or authorization changes.

## Out of scope
- Changing header contents, account menu behavior, or the mobile sheet interaction.

## Test plan
- Unit: no business rule changes; no UI test framework is established for this shared Razor/CSS behavior.
- Manual (browser): inspect 360px, 720px, and 1320px widths; verify no sideways overflow, hover, keyboard focus, and menu open/close behavior.
