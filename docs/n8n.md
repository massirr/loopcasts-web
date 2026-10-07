# n8n: e-mails for LoopCasts

The site POSTs JSON events to **one** n8n webhook. n8n sends the e-mails. Not configured → nothing is sent
(the site keeps working; set-password link is still shown on OrderThanks for the first order).

## Settings (user-secrets locally, App Service settings on Azure)
```bash
dotnet user-secrets set "N8n:WebhookUrl" "https://<your-n8n>/webhook/loopcasts"
dotnet user-secrets set "N8n:Secret" "<long random string>"     # optional, checked by the webhook node
# appsettings.json: App:BaseUrl = public address of the site (used in e-mail links)
```

## Events
```jsonc
// after a payment (once per invoice)
{ "event": "invoice.created", "to": "ann@example.com", "name": "Ann", "invoiceNumber": "2026-0007",
  "description": "Yearly subscription", "total": 180.29,
  "invoiceUrl": "https://…/Invoice?number=2026-0007",
  "setPasswordUrl": "https://…/Identity/Account/ResetPassword?code=…" }   // null if the customer has a password

// any e-mail from the login system (e.g. "Forgot password")
{ "event": "email", "to": "ann@example.com", "subject": "Reset your password", "html": "…" }
```

## Workflow (4 nodes)
1. **Webhook** — POST, path `loopcasts`, Authentication: *Header Auth* (name `X-LoopCasts-Secret`, value = secret),
   Respond: *Immediately*.
2. **Switch** on `{{$json.body.event}}`: `invoice.created` / `email`.
3. **Send Email** (SMTP or Gmail) for `invoice.created`:
   - To `{{$json.body.to}}`, Subject `Your LoopCasts invoice {{$json.body.invoiceNumber}}`
   - HTML: greeting, total `€{{$json.body.total}}`, button to `{{$json.body.invoiceUrl}}`
     (login required → customer sees/prints it), and if `setPasswordUrl` is not empty:
     "Choose your password: {{$json.body.setPasswordUrl}}" (valid 24 h).
4. **Send Email** for `email`: To `{{$json.body.to}}`, Subject `{{$json.body.subject}}`, HTML `{{$json.body.html}}`.

Optional: an *HTML → PDF* node (e.g. Gotenberg) on `invoiceUrl` to attach a PDF. The link is enough for the prototype.

Local test without n8n: any webhook catcher (e.g. webhook.site) as `N8n:WebhookUrl`.
