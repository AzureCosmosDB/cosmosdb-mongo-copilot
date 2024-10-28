targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention.')
param environmentName string

@minLength(1)
@allowed([
  'australiaeast'
  'westeurope'
  'japaneast'
  'uksouth'
  'eastus'
  'southcentralus'
  'eastus2'
])
@description('Primary location for all resources.')
param location string

@description('Id of the principal to assign database and application roles.')
param principalId string = ''

// Optional parameters
param openAiAccountName string = ''
param userAssignedIdentityName string = ''
param appServicePlanName string = ''
param appServiceWebAppName string = ''
@secure()
param mongoPassword string = newGuid()

// serviceName is used as value for the tag (azd-service-name) azd uses to identify deployment host
param serviceName string = 'web'

var abbreviations = loadJsonContent('abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = {
  'azd-env-name': environmentName
  repo: 'https://github.com/AzureCosmosDB/cosmosdb-mongo-copilot'
}

//var chatSettings = {
//  maxConversationTokens: '100'
//  cacheSimilarityScore: '0.99'
//  productMaxResults: '10'
//}

resource resourceGroup 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: environmentName
  location: location
  tags: tags
}

module identity 'app/identity.bicep' = {
  name: 'identity'
  scope: resourceGroup
  params: {
    identityName: !empty(userAssignedIdentityName) ? userAssignedIdentityName : '${abbreviations.userAssignedIdentity}-${resourceToken}'
    location: location
    tags: tags
  }
}

module ai 'app/ai.bicep' = {
  name: 'ai'
  scope: resourceGroup
  params: {
    accountName: !empty(openAiAccountName) ? openAiAccountName : '${abbreviations.openAiAccount}-${resourceToken}'
    location: location
    tags: tags
  }
}

module web 'app/web.bicep' = {
  name: 'web'
  scope: resourceGroup
  params: {
    appName: !empty(appServiceWebAppName) ? appServiceWebAppName : '${abbreviations.appServiceWebApp}-${resourceToken}'
    planName: !empty(appServicePlanName) ? appServicePlanName : '${abbreviations.appServicePlan}-${resourceToken}'
    userAssignedManagedIdentity: {
      resourceId: identity.outputs.resourceId
      clientId: identity.outputs.clientId
    }
    configSettings : {
      OpenAiEndpoint: ai.outputs.endpoint
      OpenAiCompletionsDeployment : ai.outputs.deployments[0].name
      OpenAiEmbeddingsDeployment  : ai.outputs.deployments[1].name
      MongoDbConnection : database.outputs.mongoDBConnection
    }
    location: location
    tags: tags
    serviceTag: serviceName
  }
}

module security 'app/security.bicep' = {
  name: 'security'
  scope: resourceGroup
  params: {
    appPrincipalId: identity.outputs.principalId
    userPrincipalId: !empty(principalId) ? principalId : null
  }
}

module database 'app/database.bicep' = {
  name: 'database'
  scope: resourceGroup
  params: {    
    clusterName: '${abbreviations.cosmosDbCluster}-${resourceToken}'
    location: location
    adminUsername: 'Admin${resourceToken}' 
    adminPassword: mongoPassword //'PAssw0rd${resourceToken}!' 
  }
}

// AI outputs
output AZURE_OPENAI_ACCOUNT_ENDPOINT string = ai.outputs.endpoint
output AZURE_OPENAI_COMPLETION_DEPLOYMENT_NAME string = ai.outputs.deployments[0].name
output AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME string = ai.outputs.deployments[1].name

// CosmosDB outputs
output AZURE_MONGODB_CONNECTION_STRING string = database.outputs.mongoDBConnection
