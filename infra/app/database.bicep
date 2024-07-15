metadata description = 'Create database cluster.'

param clusterName string
param location string = resourceGroup().location
param tags object = {}
param adminUsername string = ''
@secure()
param adminPassword string = ''

module cluster '../core/database/cosmos-db/mongodb-vcore/cosmosdb-cluster.bicep' = {
  name: 'cosmos-db-mongodb-cluster'
  params: {
    clusterName: clusterName
    location: location
    tags: tags
    adminUsername : adminUsername
    adminPassword : adminPassword
  }
}

output mongoDBConnection string = cluster.outputs.mongoDBConnection
