# Azure deployment assets

Two templates keep deployable application concerns separate from shared
integration-platform concerns:

- `main.bicep` provisions an Azure Container Apps environment and API, Azure SQL
  Database, Log Analytics workspace, and workspace-based Application Insights.
- `integration-platform.bicep` imports the repository OpenAPI contract into Azure
  API Management and provisions a duplicate-detecting Azure Service Bus queue
  with dead-letter behavior.

The template is an infrastructure-as-code deployment path. It does not claim
that this portfolio service is currently running in an Azure subscription.

## Validate

```powershell
az bicep build --file deploy/azure/main.bicep
az bicep build --file deploy/azure/integration-platform.bicep
az deployment group validate `
  --resource-group <resource-group> `
  --template-file deploy/azure/main.bicep `
  --parameters containerImage=<registry>/<image>:<tag> `
  --parameters integrationApiKey=<secure-api-key> `
  --parameters sqlAdministratorPassword=<secure-password>

az deployment group validate `
  --resource-group <resource-group> `
  --template-file deploy/azure/integration-platform.bicep `
  --parameters backendApiUrl=https://<container-app-fqdn> `
  --parameters publisherEmail=<platform-owner@example.com>
```

## Deploy

Build and publish the repository's Docker image to an accessible registry, then
run:

```powershell
az deployment group create `
  --resource-group <resource-group> `
  --template-file deploy/azure/main.bicep `
  --parameters containerImage=<registry>/<image>:<tag> `
  --parameters integrationApiKey=<secure-api-key> `
  --parameters sqlAdministratorPassword=<secure-password>

az deployment group create `
  --resource-group <resource-group> `
  --template-file deploy/azure/integration-platform.bicep `
  --parameters backendApiUrl=https://<container-app-fqdn> `
  --parameters publisherEmail=<platform-owner@example.com>
```

Use a deployment platform secret or an interactive secure prompt for the SQL
password. Do not commit credentials or `.bicepparam` files containing secrets.

Deploying `integration-platform.bicep` creates platform resources, but it does
not make the current application event-driven. The transactional outbox,
publisher, consumer worker, environment-specific role assignments, private
networking, and recovery tests remain roadmap gates. See
[`docs/integration-strategy.md`](../../docs/integration-strategy.md) and
[`docs/modernization-roadmap.md`](../../docs/modernization-roadmap.md).
