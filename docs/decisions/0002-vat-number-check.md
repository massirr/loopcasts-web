# ADR 0002: VAT number check (format + VIES)

Status: accepted (21/10/2026). Trigger: teacher feedback: `B@ddsfsd` was accepted as a VAT number, and any text in
that field gave an EU company 0% VAT (#33).

## Decision
1. **Format first, in code, no network** (`VatNumber`): clean (uppercase, no spaces/dots/dashes, add the country
   prefix, Greece = `EL`), per-country pattern, Belgian mod-97 checksum. Wrong → error under the field, no order.
2. **Existence only when it changes the price**: EU company outside Belgium (reverse charge) → EU VIES REST API
   (`ViesClient`, typed HttpClient, 5 s timeout). Belgian companies pay 21% anyway; outside the EU is 0% anyway.
3. **No proof, no 0%**: VIES down or timeout → 21%, order marked *VAT number not verified* (Admin → Orders).
   VIES says invalid → form error, no order.
4. **Proof on the invoice**: `VatCheckResult`, `VatCheckedAt` and the name VIES returned are copied onto the invoice.
5. `VatCalculator` gives 0% only for `VatCheck.Valid`; the VAT number text alone no longer counts.

## Why code, not n8n (ADR 0001)
Money (rule 1) and the checkout: the customer is waiting on the order form, so the answer is needed *now*, in the
same request, before the price is shown. A workflow that can be off, or changed without a test, can't decide a VAT rate.

## Why 21% when VIES is down
Two possible mistakes:
| We charge | VIES was really… | Result |
|---|---|---|
| 21% | valid | Customer paid VAT they didn't owe. Fixable later (credit note); the company deducts it anyway. |
| 0% | invalid | LoopCasts owes the 21% to the tax office itself, and can't easily get it from the customer. |
The first is cheap and fixable, the second costs LoopCasts money. So without proof: 21%, flagged for Marieke.

## How it's tested without real companies
- **Unit tests**: checksum numbers built by hand (valid and with one digit wrong / two swapped); a fake HTTP handler
  answers like VIES did when we called it (valid, invalid, `INVALID_INPUT`, `SERVICE_UNAVAILABLE`, `MS_UNAVAILABLE`,
  HTTP 500, non-JSON, timeout, network error); `VatCalculator` 0% only with `Valid`.
- **Browser**: in Development `Vies:BaseUrl` is the official test service (`check-vat-test-service`), where
  `100` = valid, `200` = invalid, `300`/`301` = down. Only against that endpoint, a 3-digit number passes the format
  check. Production uses the real `check-vat-number`.
- Found by calling VIES (not in the swagger): errors come back as **HTTP 200** with `actionSucceed: false`, and an
  unknown name is `"---"`.

## Not done
Re-checking *not verified* orders automatically (could be an n8n schedule calling a site endpoint, ADR 0001 rule 6),
and the credit note when Marieke finds the number valid later.
