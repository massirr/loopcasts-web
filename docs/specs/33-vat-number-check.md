# Spec: VAT numbers are checked before 0% VAT is given  (#33)

## User story
As LoopCasts staff (Marieke), I want VAT numbers checked before an order gets 0% VAT, so that nobody gets reverse
charge with a made-up number (teacher: `B@ddsfsd` was accepted) and nobody checks VAT numbers by hand.

## Acceptance criteria
- [ ] Wrong format (per country) or broken Belgian checksum → error under the field, no order.
- [ ] Input cleaned: uppercase, no spaces/dots/dashes, country prefix added when missing (Greece = `EL`).
- [ ] EU company outside Belgium → VIES. Valid → 0% + proof on the invoice. Invalid → form error, no order.
- [ ] VIES down / timeout → 21%, order marked "VAT number not verified", visible in Admin → Orders.
- [ ] Company name + address required for companies; name, e-mail, company, address trimmed with length limits;
      colleague e-mails on /My format-checked (a dot in the domain: `B@ddsfsd` is not an e-mail).
- [ ] `VatCalculator` gives 0% only with check result `Valid`.

## Design
- **Format** `Services/VatNumber.cs` (pure): `Normalise(input, country)`, `Error(normalised, country, allowTestNumbers)`.
  EU patterns from the VIES "VAT number structure" FAQ; Belgium: 10 digits starting 0/1 and
  last 2 = 97 − (first 8 mod 97). Non-EU (no reverse charge possible): letters/digits only, 4–20.
- **Existence** `Services/ViesClient.cs`, typed HttpClient, timeout 5 s. Official REST contract
  (`swagger_publicVAT.yaml`): `POST {countryCode, vatNumber}` → `{valid, name, address, ...}`.
  Found by calling it: errors come back as **HTTP 200** `{actionSucceed:false, errorWrappers:[{error}]}`
  (`SERVICE_UNAVAILABLE`, `MS_UNAVAILABLE`, `INVALID_INPUT`); an invalid number has `name: "---"`.
  Mapping: `valid:true` → Valid (+name), `valid:false` or `INVALID_INPUT` → Invalid, anything else
  (other error, non-2xx, timeout, network, bad JSON) → NotVerified.
- **Config** `Vies:BaseUrl` = the endpoint itself. `appsettings.json`: `.../rest-api/check-vat-number`;
  `appsettings.Development.json`: `.../rest-api/check-vat-test-service`. The test service only knows the exact
  numbers 100/200/300/301, so against the test service a 3-digit body passes the format check.
- **Model** (migration `VatCheck`): `enum VatCheck { Valid, Invalid, NotVerified }` (stored as text).
  `Order.VatCheckResult/VatCheckedAt/VatCheckName` ([BindNever]); same three as snapshot on `Invoice`
  (copied in `PaymentService`). Null = no check needed (private, Belgium, outside the EU).
- **VatCalculator** `Calculate(amount, country, isCompany, VatCheck? check)`: 0% EU only when `check == Valid`.
  Old-data import passes `Valid` for old orders with a VAT number (history is compared, not re-checked).
- **Pages**: Order (validation + VIES), Pay (note when not verified), Admin/Orders (badge).
- **Security**: everything server-side; the form can't set the check fields ([BindNever]); no secrets (VIES is public).

## Out of scope
- Re-checking "not verified" orders automatically; credit note when Marieke later finds it valid.

## Test plan
- Unit: `VatNumberTests` (normalise, patterns, checksum valid + broken), `ViesClientTests` (fake handler: valid,
  invalid, timeout, down 200/500), `VatCalculatorTests` (0% only with Valid), `PaymentServiceTests` (snapshot).
- Manual (browser, Development): NL company with 100 → 0%, 200 → error, 301 → 21% + badge in Admin → Orders.
  Screenshots in `docs/design/screenshots/`.
