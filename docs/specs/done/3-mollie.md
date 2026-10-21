# Spec: Mollie payment + webhook  (#3)

## User story
As LoopCasts, I want Mollie to call a webhook when a payment is paid, so that orders are marked paid automatically.

## Design
- `MollieService` (typed HttpClient, base `https://api.mollie.com/v2/`, Bearer `Mollie:ApiKey`). No key → mock.
- `/Pay` POST → `POST /payments` (amount = total incl. VAT, metadata.orderId, redirectUrl = /Pay?id&key,
  webhookUrl only if `Mollie:WebhookBaseUrl`) → store `Order.MolliePaymentId` → redirect to checkout.
- `/Pay` GET with a stored payment id → `SyncAsync` (customer returning; works on localhost).
- `POST /webhooks/mollie` (minimal API, form `id`) → `SyncAsync`.
- `SyncAsync` trusts nothing from the caller: id must match `^tr_[A-Za-z0-9]+$`, status from Mollie must be
  `paid`, payment id must equal the order's stored id, amount must equal the order total. Then `MarkPaidAsync`.
- Mollie unreachable → exception → webhook 500 → Mollie retries. Unknown id (404) → 200.

## Test plan
- Unit (fake HttpMessageHandler): paid → invoice; open/canceled/wrong amount/other payment → nothing; bad ids →
  no HTTP call; 404 ignored; create sends 180.29 and stores id; disabled → no call.
- Manual: needs a Mollie **test** key (see agent log).
