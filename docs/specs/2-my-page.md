# Spec: /My page  (#2)

## User story
As a logged-in customer, I want a /My page with my subscription, access end date and invoices,
so that I don't have to e-mail LoopCasts to ask when my access ends or for a copy of an invoice.

## Design
- `[Authorize]` pages `/My` and `/Invoice?number=`; e-mail always from the login, never from the URL.
- `Services/CustomerData.cs`: subscriptions by **Account** e-mail (team seat sees own seat), invoices by **buyer's
  order** e-mail, case-insensitive. `/Invoice`: own or Admin, otherwise 404 (no leak of which numbers exist).
- `Invoice.Description` = snapshot of the product name (migration `AddInvoiceDescription` backfills).
- Seller details from config `Seller:*` (not secret → appsettings.json).
- PDF = browser print (`@media print` hides navbar). No PDF package (YAGNI; n8n can render PDF in #5 if needed).

## Test plan
- Unit: own invoices only, other customer's number → null, admin sees all, team seat sees seat not invoice.
- Manual: anonymous /My → login; own invoice 200, other's 404.
