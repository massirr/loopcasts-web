# Demo script (about 5 minutes)

Before: `dotnet watch`, open http://localhost:5180, be logged OUT.

| # | Do | Say (problem it removes) |
|---|---|---|
| 1 | **Courses** | Loaded from the old messy CSV: "1h20", "55 min", "1:45:00" → minutes. Drafts/retired hidden. |
| 2 | **Pricing → Order** (Yearly) | Before: a `mailto:` link and Lotte retyping. 1 in 3 orders incomplete. |
| 3 | Submit empty form | Required fields → incomplete orders are impossible. |
| 4 | Fill as individual, Belgium → Place order | Company fields only appear for companies. Country = list, not free text. |
| 5 | **Pay (test)** | Mock of Mollie. The customer pays, nobody marks "paid" by hand. |
| 6 | Thank-you page | Invoice number, VAT, access end date: created instantly (before: Friday batch, login on Monday). |
| 7 | Order **Team licence**, NL company with VAT nr, pay | VAT 0% + "reverse charged" automatically (handbook rules). |
| 8 | Open `/Admin/Orders` while logged out | Locked → login. Personal accounts, hashed passwords, secrets outside the code. |
| 9 | Log in → **Orders** | Status is a real field, not an Excel colour. Button only for bank transfers. |
| 10 | **Invoices & accounts** | Numbers in sequence + UNIQUE index (no more 2026-0029 twice). Revenue this month calculated. |
| 11 | Accounts → **Run expiry check now** | Runs automatically every 24h: the 123 forgotten accounts can't happen. |

Optional for step 11 (make one account expired first, app may keep running):
```bash
sqlite3 loopcasts.db "UPDATE Accounts SET EndsOn = '2026-01-01' WHERE Id = 1;"
```
Refresh Invoices & accounts → red "active but expired!" → click Run expiry check now → "inactive".

Not code: `paswoorden.xlsx` → team password manager (e.g. Bitwarden), personal accounts, rotate all passwords, remove Jens.
