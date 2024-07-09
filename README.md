---
page_type: sample
languages:
- csharp
products:
- azure-cosmos-db-mongodb
- azure-openai
name: Build a Copilot app using Azure Cosmos DB for MongoDB & Azure OpenAI Service
urlFragment: chat-app
description: Sample application that implements a Generative AI chat application that demonstrates context windows, semantic cache, RAG Pattern with custom data and Semantic Kernel integration.
azureDeploy: https://raw.githubusercontent.com/AzureCosmosDB/cosmosdb-nosql-copilot/main/azuredeploy.json
---

# Build a Copilot app using Azure Cosmos DB,Azure OpenAI Service and Azure App Service

This sample application shows how to build a Generative-AI RAG Pattern application using Azure Cosmos DB using its new vector search capabilities and Azure OpenAI Service and Semantic Kernel. The sample provides practical guidance on many concepts you will need to design and build these types of applications.

## Concepts Covered

This application demonstrates the following concepts and how to implement them:

- The basics of building a highly scalable Generative-AI chat application using Azure Cosmos DB for MongoDB.
- Generating completions and embeddings using Azure OpenAI Service.
- Managing a context window (chat history) for natural conversational interactions with an LLM.
- Manage token consumption and payload sizes for Azure OpenAI Service requests.
- Building a semantic cache using Azure Cosmos DB for MonogDB vector index and the Semantic Kernel Connector for improved performance and cost.
- Using the Semantic Kernel SDK for completion and embeddings generation.
- Implementing RAG Pattern using vector search in Azure Cosmos DB for NoSQL on custom data to augment generated responses from an LLM. 

### Architecture Diagram

![Architecture Diagram](cosmos-mongo-copilot-diagram.png)

### User Experience
![Cosmos DB + ChatGPT user interface](screenshot.png)


## Getting Started

### Prerequisites

- Azure Subscription
- [Azure Developer CLI](https://aka.ms/azd-install)
- Subscription access to Azure OpenAI service. Start here to [Request Access to Azure OpenAI Service](https://aka.ms/oaiapply)
- Visual Studio, VS Code, GitHub Codespaces or another editor to edit or view the source for this sample.
- Azure Cosmos DB for MongoDB vCore Service
- Azure App Service
- Azure OpenAI Service

### Instructions

1. Open a terminal and navigate to the / directory in this solution.

1. Log in to AZD.
    
    ```bash
    azd auth login
    ```

1. Deploy the services to Azure, build your container, and deploy the application.
    
    ```bash
    azd up
    ```

### Quickstart

This solution has a number of quickstarts than you can run through to learn about the features in this sample and how to implement them yourself.

Please see [Quickstarts](quickstart.md)


## Clean up

1. Open a terminal and navigate to the /infra directory in this solution.

1. Type azd down
    
    ```bash
    azd down
    ```

## Resources

To learn more about the services and features demonstrated in this sample, see the following:

- [Azure Cosmos DB for MongoDB Vector Index support](https://learn.microsoft.com/en-us/azure/cosmos-db/mongodb/vcore/vector-search)
- [Azure OpenAI Service documentation](https://learn.microsoft.com/azure/cognitive-services/openai/)
- [Semantic Kernel](https://learn.microsoft.com/semantic-kernel/overview)
- [Azure App Service documentation](https://learn.microsoft.com/azure/app-service/)
- [ASP.NET Core Blazor documentation](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
