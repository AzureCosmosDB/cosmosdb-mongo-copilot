metadata description = 'Create an Azure Cosmos DB for MongoDB vCore cluster.'

@description('Azure Cosmos DB MongoDB vCore cluster name')
@maxLength(44)
param clusterName string = 'msdocs-${uniqueString(resourceGroup().id)}'

@description('Location for the cluster.')
param location string = resourceGroup().location

@description('Username for admin user')
param adminUsername string

//@secure()
@description('Password for admin user')
@minLength(8)
@maxLength(128)
param adminPassword string

@description('Azure Resource Tags')
param tags object = {}

resource cluster 'Microsoft.DocumentDB/mongoClusters@2022-10-15-preview' = {
  name: clusterName
  location: location
  properties: {
    tags: tags
    administratorLogin: adminUsername
    administratorLoginPassword: adminPassword
    nodeGroupSpecs: [
        {
            kind: 'Shard'
            shardCount: 1
            sku: 'M40'
            diskSizeGB: 128
            enableHa: false
            nodeCount : 1
        }
    ]
  }
}

resource firewallRules 'Microsoft.DocumentDB/mongoClusters/firewallRules@2022-10-15-preview' = {
  parent: cluster
  name: 'AllowAllAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output mongoDBConnection string = 'mongodb+srv://${adminUsername}:${adminPassword}@${clusterName}.mongocluster.cosmos.azure.com/?tls=true&authMechanism=SCRAM-SHA-256&retrywrites=false&maxIdleTimeMS=120000'