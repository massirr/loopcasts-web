# Spec: Invoice page + e-mail via n8n  (#5)

## User story
As a customer, I want to receive my invoice by e-mail automatically after paying,
so that LoopCasts no longer makes and mails invoices by hand on Fridays.

## Design
- Invoice page: done in #2 (`/Invoice?number=`, print → PDF).
- `N8nNotifier` (typed HttpClient, 10 s timeout) POSTs to `N8n:WebhookUrl`, header `X-LoopCasts-Secret` if set.
  Events `invoice.created` (from `PaymentService`, only when a new invoice was made) and `email`
  (`IEmailSender` → "Forgot password" works).
- Set-password link in the invoice e-mail when the login has no password: safe, it goes to the order's e-mail.
- Errors are logged, never thrown (payment first). Not configured → no call.
- Links use `App:BaseUrl` (no HttpContext in a webhook).
- n8n workflow: `docs/n8n.md`.

## Test plan
- Unit: one event per invoice (retry → none), links + secret, password set → `setPasswordUrl` null,
  n8n 500 → payment still saved, no URL → no call, IEmailSender → `email` event.
- Manual: fake receiver (python) → both events arrive with body + secret; e-mailed link opens the reset page.
