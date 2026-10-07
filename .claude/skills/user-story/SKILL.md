---
name: user-story
description: Write a backlog item as a user story GitHub issue ("As a ..., I want ..., so that ...") with acceptance criteria, and add it to the project board. Use when adding or refining backlog items.
---
# User story

1. Title: short imperative ("Customer can log in").
2. Body:
   ```
   **As a** <role: customer | team admin | LoopCasts staff | developer>,
   **I want** <capability>,
   **so that** <business value, ideally a manual step that disappears>.

   ### Acceptance criteria
   - [ ] Given … when … then …   (testable, 3–6 items)

   ### Notes
   Handbook chapter / old manual step it replaces, dependencies (#issue), out of scope.
   ```
3. Create: `gh issue create --title "…" --body-file <file> --label user-story`
4. Add to board: `gh project item-add <number> --owner massirr --url <issue-url>`
5. Never put customer data, passwords or keys in an issue (repo is private, but still).
