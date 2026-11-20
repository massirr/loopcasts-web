# Handover: current state

Replaced at the end of each session; max ~60 lines. History: `docs/log/`. Plans: `docs/architecture.md`.
Last update: **20/11/2026**.

## Current state
- Repository: `massirr/loopcasts-web`, default branch `main`, public. `main` protected (PR + green `build-test`).
- **Live** since 04/11/2026: https://loopcasts-koze.azurewebsites.net (Azure for Students, F1, `belgiumcentral`).
  Deploy: green CI on `main` → `.github/workflows/deploy.yml`. Lessons: `docs/deploy-azure.md` Notes.
- **Payments: Stripe** test mode (ADR 0003). No key outside Development → /Pay refuses, no mock (#60, merged).
  Webhook direct to `/webhooks/stripe`, no Event Grid (ADR 0004). Stripe calls it a "destination" in Workbench.
- **E-mail live (#64):** login mails over SMTP to the **Mailtrap sandbox** (hosted fake inbox) once `Smtp__*` is
  set on Azure. Invoice/reminder mails go via n8n, still on the laptop.
- Spec flow: skill `spec` → build → agent `reviewer` → skill `spec-close`.

## Waiting on Koze
1. App Service setting `Stripe__SecretKey`; Stripe Workbench → Webhooks → Add destination (Snapshot payload,
   `checkout.session.completed`, `https://loopcasts-koze.azurewebsites.net/webhooks/stripe`); test payment
   `4242 4242 4242 4242` → Admin/Orders paid, delivery 200 in Workbench.
2. Merge the #64 PR; Mailtrap account → `Smtp__Host/Port/User/Password` (deploy-azure.md) → Forgot password live
   → mail in the Mailtrap inbox.

## Agreed roadmap (one issue = one PR)
1. #63 Video (lesson) table + Admin → Courses (Pieter: catalogue, chapters, durations). Foundation for 2 and 3.
2. MCP server in the site: `add_course` / `add_video` (like StudyApp's, C# SDK = new package: ask first). Issue to come.
3. Newsletter (Yasmine): subscribe + one-click unsubscribe in code, sending via n8n. Issue to come.
4. #62 Lesson page with AI presenter (SVG/CSS, members only, demo, €0). Depends on #63.
5. Host n8n (Docker → ACR → Azure, main path for invoice/reminder mails live); Tailscale Funnel = demo fallback.
6. Later, paid: chatbot on Microsoft Fabric.
- Team from the handbook: Marieke De Smet (founder, records, invoicing), Lotte Janssens (orders, accounts,
  support), Pieter Claes (editing, upload, catalogue), Yasmine El Amrani (marketing, newsletter), Tom Verhaeghe
  (website).
