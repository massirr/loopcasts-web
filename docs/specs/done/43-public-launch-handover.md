# Spec: Record verified public launch (#43)

## User story
As the project maintainer, I want the handover and publication spec to reflect the verified public repository state, so that the next session starts with accurate status.

## Acceptance criteria
- [ ] Handover records public visibility, merged PRs #38, #40, and #42, and verified `main` protection.
- [ ] Publication spec records that visibility was changed only after the historical Mailpit link was assessed and the audit correction was reviewed and merged.
- [ ] Handover is under 60 lines; append to the monthly log without reading it.
- [ ] CI is green and human review is recorded before merge.

## Design
- Data/model changes (migration name): none.
- Pages / endpoints: documentation only.
- Services (where the logic lives): none.
- Security (authz, validation, secrets): do not reproduce the historical reset token; record only the localhost Mailpit context and expiry.

## Out of scope
- Further application changes or repository setting changes.

## Test plan
- Unit: none; docs-only change.
- Manual (browser): verify GitHub visibility and `main` protection, and review the handover line count.
