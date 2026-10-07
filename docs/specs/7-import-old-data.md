# Spec: Import customers from the old data  (#7)

## User story
As LoopCasts staff, I want the existing customers and orders from the old spreadsheets imported once,
so that existing customers get an account/login and we stop maintaining two systems.

## Decisions (Koze, 2026-10-07)
- Invoices imported **as registered** (number, date, amounts = legal documents already sent); differences with the
  VAT rules are reported, not corrected.
- Credit notes: **report only** (no credit-note model yet). A credited invoice is replaced by the customer's later
  invoice in the register (2026-0015 → 2026-0030).
- Paid orders get an Account (end = order date + plan months) and a Member login **without e-mail**.
- Staff run it on **/Admin/Import**: upload both CSVs → dry-run report → Import. Files are never stored.

## Input (Excel exports, `;`-separated, UTF-8)
- `orders-2026.csv`: date;name;email;company;vat_number;country;product;price_excl;invoice_nr;account_created;remark
- `invoice-register.csv`: invoice_nr;date;customer;amount_excl;vat;total;paid;remark

## Cleaning (each fix is reported)
| Field | Old values | Rule |
|---|---|---|
| date | 05/01/2026, 5-1-26, 2026-01-06, 6 jan, 01/15/2026 | dd/MM first; US only if dd/MM is impossible; no year → year of the previous row |
| country | BE, Belgium, België, belgie, Nederland | code, English name or alias, accents ignored |
| price / amounts | 149, € 149, 149,00, 149.00 | strip €/spaces, comma = decimal |
| e-mail | spaces, capitals, `example,com` | trim, lower-case, comma → dot; still invalid → skip row |
| VAT number | BE0456.789.123, BE 0678 234 519 | remove spaces/dots, upper-case |
| no invoice nr | duplicate row / "waiting" | same e-mail + product as an invoiced row → skip; else unpaid order |
| paid column | green, JA, yes, x, betaald…, orange, empty | invoice exists → paid; orange/empty reported |

Also reported: VAT amount ≠ rules, total ≠ amount + VAT, price ≠ plan price, register name ≠ order name,
register rows without order, missing numbers, number used twice, remarks.

## Design
- `Services/OldDataImport.cs`: `Parse(ordersCsv, registerCsv)` (pure → rows + report, unit-tested) and
  `ApplyAsync(db, members)` (one transaction; idempotent: existing invoice number / same unpaid order → skipped).
- `Pages/Admin/Import` (AdminOnly folder). CSV text travels in hidden fields between preview and import.
- Next new invoice number continues after the highest imported one (existing `NextInvoiceNumberAsync`).

## Test plan
- Unit: every cleaning rule, credit-note replacement, duplicates, idempotent apply, accounts/logins created.
- Manual: upload the two sample files on a scratch db, check report, import, re-import → 0 new.
