targetScope = 'resourceGroup'

@description('Short lowercase prefix used in Azure resource names.')
@minLength(3)
@maxLength(12)
param namePrefix string = 'integration'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Published Linux container image for the ASP.NET Core API.')
param containerImage string

@description('SQL administrator login used by the application.')
param sqlAdministratorLogin string = 'integrationadmin'

@description('SQL administrator password stored as a Container Apps secret.')
@secure()
param sqlAdministratorPassword string

@description('Inbound API key stored as a Container Apps secret.')
@secure()
param integrationApiKey string

var suffix = uniqueString(subscription().id, resourceGroup().id)
var baseName = '${toLower(namePrefix)}-${suffix}'
var sqlServerName = '${toLower(namePrefix)}${suffix}'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${baseName}-logs'
  location: location
  properties: {
    retentionInDays: 30
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${baseName}-insights'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource azureServicesFirewallRule 'Microsoft.Sql/servers/firewallRules@2023-08-01' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: sqlServer
  name: 'Integration'
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
}

resource containerEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${baseName}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${baseName}-api'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
      }
      secrets: [
        {
          name: 'api-key'
          value: integrationApiKey
        }
        {
          name: 'sql-connection'
          value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabase.name};Persist Security Info=False;User ID=${sqlAdministratorLogin};Password=${sqlAdministratorPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'integration-api'
          image: containerImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'IntegrationSecurity__ApiKey'
              secretRef: 'api-key'
            }
            {
              name: 'DatabaseProvider'
              value: 'SqlServer'
            }
            {
              name: 'ConnectionStrings__IntegrationDatabase'
              secretRef: 'sql-connection'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: applicationInsights.properties.ConnectionString
            }
            {
              name: 'OTEL_SERVICE_NAME'
              value: 'business-application-integration-service'
            }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 10
              periodSeconds: 15
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
}

output apiUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output applicationInsightsName string = applicationInsights.name
output sqlDatabaseName string = sqlDatabase.name
