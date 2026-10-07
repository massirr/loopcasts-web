# Deploy to Azure App Service (Free F1, Linux)

One-time setup (`az login` first). Names are examples; the app name must be globally unique.

```bash
RG=loopcasts-rg; PLAN=loopcasts-plan; APP=loopcasts-koze; LOC=westeurope

az group create -n $RG -l $LOC
az appservice plan create -g $RG -n $PLAN --sku F1 --is-linux
az webapp create -g $RG -p $PLAN -n $APP --runtime "DOTNETCORE:10.0"
az webapp update -g $RG -n $APP --https-only true

# Settings (= user-secrets on the server). Values typed by you, never committed.
az webapp config appsettings set -g $RG -n $APP --settings \
  "ConnectionStrings__Default=Data Source=/home/data/loopcasts.db" \
  "App__BaseUrl=https://$APP.azurewebsites.net" \
  "ASPNETCORE_FORWARDEDHEADERS_ENABLED=true" \
  "SeedAdmins__0__Email=<you@…>" "SeedAdmins__0__Password=<strong password>"
# later: Mollie__ApiKey, Mollie__WebhookBaseUrl=https://$APP.azurewebsites.net, N8n__WebhookUrl, N8n__Secret

# GitHub Actions deploy (publish profile needs basic-auth publishing enabled)
az resource update -g $RG --namespace Microsoft.Web --resource-type basicPublishingCredentialsPolicies \
  --parent sites/$APP -n scm --set properties.allow=true
az webapp deployment list-publishing-profiles -g $RG -n $APP --xml | gh secret set AZURE_WEBAPP_PUBLISH_PROFILE
gh variable set AZURE_WEBAPP_NAME --body $APP
```

After that every green CI run on `main` deploys (`.github/workflows/deploy.yml`).
Re-run manually: GitHub → Actions → Deploy → Re-run.

## Notes
- **Database:** `/home/data/loopcasts.db` is on persistent storage; migrations run at startup.
  Back up: `az webapp ssh` → copy the file, or Kudu (https://$APP.scm.azurewebsites.net) → File manager.
- **F1 limits:** 60 CPU-minutes/day, no "Always On" → the app sleeps; the 24 h expiry job runs at every start.
- **Mock payment:** without `Mollie__ApiKey` the public site gives free access via the test checkout.
  Set the Mollie test key before sharing the URL.
- **Roll back:** re-run the Deploy workflow of an older green commit, or `git revert` + push.
- **Remove everything:** `az group delete -n $RG`.
