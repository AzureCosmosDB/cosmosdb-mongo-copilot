# Quickstart exercises

This document walks you through the concepts implemented in this sample so you can understand it's capabilities and how to do the same.

# Context window (chat history)

Humans interact with each other through conversations that have some *context* of what is being discussed. OpenAI's ChatGPT can also interact this way with humans. However, this capability is not native to an LLM itself. It must be implemented. Let's explore what happens when we test contextual follow up questions with our LLM where we ask follow up questions that imply an existing context like you would have in a conversation with another person.

## Quickstart: Conversational context
Let's observe this in action. Follow the steps after launching the application:

1. Start a new Chat Session.
1. Enter a question, `What is the largest lake in North America?`, wait for response, `Lake Superior`
1. Enter a follow up without context, `What is the second largest?`, wait for repsonse, `Lake Huron`
1. Enter a third follow up, `What is the third largest?`, wait for resopnse, `Great Bear lake`

Clearly the LLM is able to keep context for the conversation and answer appropriately. While this concept is simple enough it can present some challenges. It also introduces the concept of tokens for services like OpenAI.

## Tokens

Large language models require chat history to generate contextually relevant results. But there is a limit how much text you can send. Large language models have limits on how much text they can process in a request and output in a response. These limits are not expressed as words, but as **tokens**. Tokens represent words or part of a word. On average 4 characters is one token. Tokens are essentially the compute currency for large language model. Because of this limit on tokens, it is therefore necessary to limit them. This can be a bit tricky in certain scenarios. You will need to ensure enough context for the LLM to generate a correct response, while avoiding negative results of consuming too many tokens which can include incomplete results or unexpected behavior.

This application allows you to configure how large the context window can be (length of chat history). This is done using the configuration value, **MaxConversationTokens** that you can adjust in the appsettings.json file.

## Conclusion

We hope you enjoyed this series of quick starts. Feel free to take this sample and customize and use.
