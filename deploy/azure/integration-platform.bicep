targetScope = 'resourceGroup'

@description('Short lowercase prefix used in globally unique Azure resource names.')
@minLength(3)
@maxLength(12)
param namePrefix string = 'integration'

@description('Azure region for API Management and Service Bus.')
param location string = resourceGroup().location

@description('Public HTTPS URL of the integration API running in Container Apps or another backend.')
param backendApiUrl string

@description('Contact address required by API Management. Replace for each environment.')
param publisherEmail string

@description('Organization name shown by API Management.')
param publisherName string = 'Integration Platform Team'

var suffix = uniqueString(subscription().id, resourceGroup().id)
var baseName = '${toLower(namePrefix)}-${suffix}'

resource apiManagement 'Microsoft.ApiManagement/service@2024-05-01' = {
  name: '${baseName}-apim'
  location: location
  sku: {
    name: 'Consumption'
    capacity: 0
  }
  properties: {
    publisherEmail: publisherEmail
    publisherName: publisherName
  }
}

resource integrationApi 'Microsoft.ApiManagement/service/apis@2024-05-01' = {
  parent: apiManagement
  name: 'business-application-integration'
  properties: {
    apiType: 'http'
    displayName: 'Business Application Integration API'
    format: 'openapi'
    path: 'integration'
    protocols: [
      'https'
    ]
    serviceUrl: backendApiUrl
    subscriptionRequired: true
    value: loadTextContent('../../contracts/openapi.yaml')
  }
}

resource apiPolicy 'Microsoft.ApiManagement/service/apis/policies@2024-05-01' = {
  parent: integrationApi
  name: 'policy'
  properties: {
    format: 'rawxml'
    value: '<policies><inbound><base /><rate-limit-by-key calls="100" renewal-period="60" counter-key="@(context.Subscription?.Key ?? context.Request.IpAddress)" /><set-header name="X-Correlation-ID" exists-action="skip"><value>@(Guid.NewGuid().ToString())</value></set-header></inbound><backend><base /></backend><outbound><base /></outbound><on-error><base /></on-error></policies>'
  }
}

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: '${baseName}-servicebus'
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    disableLocalAuth: true
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    zoneRedundant: false
  }
}

resource integrationEventsQueue 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' = {
  parent: serviceBusNamespace
  name: 'integration-events'
  properties: {
    deadLetteringOnMessageExpiration: true
    defaultMessageTimeToLive: 'P14D'
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    lockDuration: 'PT1M'
    maxDeliveryCount: 10
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: true
    requiresSession: false
    status: 'Active'
  }
}

output apiGatewayUrl string = apiManagement.properties.gatewayUrl
output apiManagementName string = apiManagement.name
output integrationApiPath string = '${apiManagement.properties.gatewayUrl}/integration'
output serviceBusNamespaceName string = serviceBusNamespace.name
output serviceBusQueueName string = integrationEventsQueue.name
