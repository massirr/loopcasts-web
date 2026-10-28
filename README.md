# LoopCastsWeb

LoopCastsWeb is a school prototype for LoopCasts BV, a fictional video-training company. It demonstrates how to automate the customer journey from choosing a course plan through ordering, payment, invoicing, and account access.

## Features

Implemented:

- A catalogue of 60 video courses and four subscription plans.
- Individual and company orders, VAT calculation, and EU VAT-number checks through VIES.
- A mock checkout for local development, with an optional Mollie payment integration.
- Customer accounts, course access, printable invoices, and five-seat team licences.
- Admin pages for orders, invoices, accounts, and importing legacy data.
- Password setup and recovery e-mails, two-factor login, and renewal reminders.
- Optional n8n workflows for invoice e-mails, renewal reminders, and the course FAQ assistant.

Planned or not configured for production:

- Azure App Service deployment and production SMTP settings.
- Monthly recurring billing, credit notes, and a QR-code flow for authenticator setup.
- More complete account-data export and anonymization workflows.

## Technology

- .NET 10, ASP.NET Core Razor Pages, and Entity Framework Core 10.
- SQLite for local data and ASP.NET Core Identity for accounts and roles.
- Custom CSS, with jQuery used for form validation.
- Optional n8n and Mailpit services through Docker Compose.
- xUnit tests; GitHub Actions also runs a Node.js check for the assistant’s sample answers.

## Run locally

Install the .NET 10 SDK, then run from the repository root:

```sh
dotnet restore
dotnet watch
```

The app applies database migrations at startup, creates a local SQLite database, and loads the course catalogue from `Data/Seed/course-catalogue.csv`. The database file is ignored by Git.

To create a local admin account, set credentials with .NET user-secrets before starting the app:

```sh
dotnet user-secrets set "SeedAdmins:0:Email" "admin@example.com"
dotnet user-secrets set "SeedAdmins:0:Password" "<choose-a-strong-password>"
```

Without `SeedAdmins` settings, the public course and ordering pages can still run, but the app will not seed an admin login.

## Configuration and secrets

Do not commit passwords, API keys, database files, or `.env` files. Use .NET user-secrets for local development and environment variables or App Service settings on a server. ASP.NET configuration uses double underscores in environment-variable names, for example `Mollie__ApiKey` and `N8n__Secret`.

Optional settings include:

- `Mollie:ApiKey` and `Mollie:WebhookBaseUrl` for Mollie payments. Without the API key, checkout uses the mock flow.
- `N8n:WebhookUrl` and `N8n:Secret` for invoice e-mails and renewal workflows.
- `Chat:Url` for the n8n course assistant.
- `Smtp:Host`, `Smtp:Port`, and `Smtp:From` for Identity e-mails.

See [the n8n and Mailpit guide](docs/n8n.md) for the optional automation services and [the Azure deployment guide](docs/deploy-azure.md) for server configuration.

## Tests

Run the application test suite:

```sh
dotnet test tests/LoopCastsWeb.Tests
```

The GitHub Actions CI also runs:

```sh
node n8n/eval/check-answer.test.mjs
```

CI runs on pull requests and pushes to `main`.

## Deployment status

The Azure deployment workflow is present but inactive: the repository has no `AZURE_WEBAPP_NAME` variable or `AZURE_WEBAPP_PUBLISH_PROFILE` secret, and its recent deployment runs were skipped. No Azure deployment is confirmed. See [the deployment guide](docs/deploy-azure.md) for the setup checklist.
