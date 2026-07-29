# Azure deployment assets

This template provisions an Azure Container Apps environment and API, Azure SQL
Database, Log Analytics workspace, and workspace-based Application Insights
resource. The application receives the SQL connection string and Application
Insights connection string through runtime configuration.

The template is an infrastructure-as-code deployment path. It does not claim
that this portfolio service is currently running in an Azure subscription.

## Validate

```powershell
az bicep build --file deploy/azure/main.bicep
az deployment group validate `
  --resource-group <resource-group> `
  --template-file deploy/azure/main.bicep `
  --parameters containerImage=<registry>/<image>:<tag> `
  --parameters integrationApiKey=<secure-api-key> `
  --parameters sqlAdministratorPassword=<secure-password>
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
```

Use a deployment platform secret or an interactive secure prompt for the SQL
password. Do not commit credentials or `.bicepparam` files containing secrets.
