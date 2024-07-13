---
page_type: sample
languages:
- csharp
products:
- azure-cosmos-db 
- azure-openai
name: Build a Copilot Hands-On-Lab using Azure Cosmos DB for MongoDB & Azure OpenAI Service
urlFragment: chat-app
description: Hands-On-Lab with Starter Solution that shows how to build a Generative AI chat application demonstrating: context windows, semantic cache, Semantic Kernel integration and more.
---

# Hands-On-Lab to Build a Copilot app using Azure Cosmos DB for MongoDB & Azure OpenAI Service

This Hands-On-Lab and starter solution walks users step-by-step how to build a Generative-AI application using Azure Cosmos DB for MongoDB using its new vector search capabilities and Azure OpenAI Service and Semantic Kernel. The sample provides practical guidance on many concepts you will need to design and build these types of applications.

To run the Hands-On-Lab, follow the steps below then open the [Lab Guide](./docs/LABGuide.md) and complete the exercises.

![Cosmos DB + ChatGPT user interface](./docs/UserInterface.png)

## Concepts Covered

This Hands-On-Lab demonstrates the following concepts and how to implement them:

- The basics of building a highly scalable Generative-AI chat application using Azure Cosmos DB for NoSQL.
- Generating completions and embeddings using Azure OpenAI Service.
- Managing a context window (chat history) for natural conversational interactions with an LLM.
- Manage token consumption and payload sizes for Azure OpenAI Service requests.
- Using the Semantic Kernel SDK for completion and embeddings generation.
- Building a semantic cache using Azure Cosmos DB and Semantic Kernel with vector indexing for improved performance and cost.

## Getting Started

### Prerequisites

- Azure subscription. [Start free](https://azure.microsoft.com/free)
- .NET 8 or above. [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Developer CLI](https://aka.ms/azd-install)
- Subscription access to Azure OpenAI service. Start here to [Request Access to Azure OpenAI Service](https://aka.ms/oaiapply)
- Subscription access to Azure Cosmos DB for MongoDB (vCore) M40 or above
- Visual Studio, VS Code, GitHub Codespaces or another editor to edit or view the source for this sample.

### Instructions

1. Run the following command to download this project code:

    ```bash
    azd init -t AzureCosmosDB/cosmosdb-mongo-copilot
    ```

1. Open a terminal and navigate to the /infra directory in this solution.

1. Log in to AZD.
    
    ```bash
    azd auth login
    ```

1. Deploy the services to Azure, build your container, and deploy the application.
    
    ```bash
    azd up
    ```

1. To load the data needed for this lab you will need navigate to the web app and click on the "Admin : Load Data" button. 
![LoadingDocumentsClick.png](docs/LoadingDocumentsClick.png)
You can monitor the loading of the data through the application logs.
![LoadingDocumentsStatus.png](docs/LoadingDocumentsStatus.png)
 
### Hands-On-Lab

To run the Hands-On-Lab, follow the Instructions above to download and deploy via AZD then open the [Lab Guide](./docs/LABGuide.md) and complete the exercises.


## Clean up

1. Open a terminal and navigate to the /infra directory in this solution.

1. Type azd down
    
    ```bash
    azd down
    ```

## Resources

To learn more about the services and features demonstrated in this sample, see the following:

- [Azure Cosmos DB for MongoDB (vCore)](https://learn.microsoft.com/azure/cosmos-db/mongodb/vcore/introduction)
- [Azure OpenAI Service documentation](https://learn.microsoft.com/azure/cognitive-services/openai/)
- [Semantic Kernel](https://learn.microsoft.com/semantic-kernel/overview)
- [Azure App Service documentation](https://learn.microsoft.com/azure/app-service/)
- [ASP.NET Core Blazor documentation](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
