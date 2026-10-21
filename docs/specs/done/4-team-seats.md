# Spec: Team licence — manage 5 seats  (#4)

## User story
As a team admin who bought a team licence, I want to add and remove up to 4 colleagues (5 seats incl. me),
so that my team gets access without e-mailing LoopCasts for every person.

## Design
- `Plan.Seats` (team = 5). Seat = `Account` on the team order, `EndsOn` = licence end (expiry job covers it).
- `Services/TeamSeats.cs`: `LicencesAsync(buyer)`, `AddAsync`, `RemoveAsync`. Buyer e-mail always from the login;
  every call re-checks the order was bought (and paid) by that e-mail.
- Rules: max seats incl. buyer; no duplicate (case-insensitive); removed colleague → reactivated, not a new row;
  never remove own seat; licence ended → no adding.
- Colleague login via `MemberService`; setup link shown to the buyer **once (TempData) and only if the login was
  created now** — an existing customer's login is never handed to someone else. E-mailing it: #5.
- UI: card per licence on `/My`, handlers `AddSeat` / `RemoveSeat`, PRG.

## Test plan
- Unit: add (end date, new login), max 5, duplicates, existing login → no link, remove/own seat, only buyer,
  single-seat plan / expired licence.
- Manual: buy team → /My → add, duplicate, invalid e-mail, remove.
