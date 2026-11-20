# ADR 0004: Stripe calls the site directly (no Azure Event Grid)

Status: accepted (20/11/2026). Trigger: setting up the Stripe webhook on the live site; Stripe offers Azure Event
Grid as a destination next to a plain webhook endpoint.

## Decision
Stripe sends `checkout.session.completed` **straight to** `POST /webhooks/stripe` (Snapshot payload).
No Event Grid, Service Bus or Function in between.

```
Now:          Stripe ─────────────────────────────► /webhooks/stripe
Event Grid:   Stripe ──► Event Grid (partner topic) ──► /webhooks/stripe (or a Function, n8n, …)
```

## Why
- **One receiver.** Event Grid earns its place when several systems need the same event (fan-out), or with
  filtering/buffering. LoopCasts has one: the site.
- **It removes nothing.** Event Grid still has to deliver to an endpoint, so the handler stays; we would only add
  a partner topic + subscription to set up, watch and explain.
- **Retries already exist.** Stripe retries a failed delivery for up to 3 days; `/Pay` also checks the status when
  the customer returns. Event Grid's retries would not add safety.
- **€0 and one service less** (same reasoning as ADR 0001: every extra service is one more thing that can stop).

## How it stays safe (unchanged, ADR 0003)
The payload is never trusted: only the session id is read, then the status is fetched from Stripe and must match
the order. A fake POST to the public URL can't mark anything paid.

## Revisit when
More than one system must react to the same payment (e.g. n8n and a Fabric pipeline): then put Event Grid in
front and let each one subscribe.
