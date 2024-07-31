metadata description = 'Create web apps.'

param planName string
param appName string
param serviceTag string
param location string = resourceGroup().location
param tags object = {}

@description('SKU of the App Service Plan.')
param sku string = 'S1'

type managedIdentityType = {
  resourceId: string
  clientId: string
}

@description('Unique identifier for user-assigned managed identity.')
param userAssignedManagedIdentity managedIdentityType

type configSettingsType = {
    OpenAiEndpoint: string
    OpenAiCompletionsDeployment : string
    OpenAiEmbeddingsDeployment : string
    MongoDbConnection : string

}

@description('Environment App Settings Variables. ')
param configSettings configSettingsType

module appServicePlan '../core/host/app-service/plan.bicep' = {
  name: 'app-service-plan'
  params: {
    name: planName
    location: location
    tags: tags
    sku: sku
    kind: 'linux'
  }
}

module appServiceWebApp '../core/host/app-service/site.bicep' = {
  name: 'app-service-web-app'
  params: {
    name: appName
    location: location
    tags: union(tags, {
      'azd-service-name': serviceTag
    })
    parentPlanName: appServicePlan.outputs.name
    runtimeName: 'dotnetcore'
    runtimeVersion: '8.0'
    kind: 'app,linux'
    enableSystemAssignedManagedIdentity: false
    userAssignedManagedIdentityIds: [
      userAssignedManagedIdentity.resourceId
    ]
  }
}

module appServiceWebAppConfig '../core/host/app-service/config.bicep' = {
  name: 'app-service-config'
  params: {
    parentSiteName: appServiceWebApp.outputs.name
    appSettings: {
       OpenAi__Endpoint : configSettings.OpenAiEndpoint
       OpenAi__CompletionsDeployment : configSettings.OpenAiCompletionsDeployment
       OpenAi__EmbeddingsDeployment : configSettings.OpenAiEmbeddingsDeployment
       OpenAi__MaxEmbeddingTokens : '2000'
       OpenAi__MaxConversationTokens : '1500'
       OpenAi__MaxCompletionTokens : '2000'
       OpenAi__MaxContextTokens : '2000'
       MongoDb__Connection : configSettings.MongoDbConnection
       MongoDb__DatabaseName : 'retaildb'
       MongoDb__CollectionNames : 'products, customers, salesOrders, completions'
       MongoDb__MaxVectorSearchResults : '20'
       MongoDb__VectorIndexType : 'hnsw'
       AZURE_CLIENT_ID: userAssignedManagedIdentity.clientId
    }
  }
}


output name string = appServiceWebApp.outputs.name
output endpoint string = appServiceWebApp.outputs.endpoint
