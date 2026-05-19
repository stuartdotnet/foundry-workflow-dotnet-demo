# foundry-workflow-dotnet-demo

A .NET console app demonstrating how to invoke an [Azure AI Foundry](https://ai.azure.com) workflow agent via the REST API, using streaming responses.

## What it does

1. Authenticates using `DefaultAzureCredential` (supports Azure CLI, Managed Identity, etc.)
2. Creates a conversation against the Foundry project endpoint
3. Invokes the `ContosoPay-Customer-Support-Triage` workflow agent with streaming enabled
4. Parses the server-sent event stream and prints completed response output
5. Deletes the conversation to clean up

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- An Azure AI Foundry project with a deployed workflow agent
- Azure credentials configured locally (e.g. `az login`)

## Configuration

Update the following constants at the top of `Program.cs` to point at your own project:

| Variable | Description |
|---|---|
| `projectEndpoint` | Your Foundry project endpoint URL |
| `workflowName` | Name of the workflow agent to invoke |
| `TenantId` | Your Azure AD tenant ID |

## Run

```bash
dotnet run
```

## Dependencies

- [`Azure.Identity`](https://www.nuget.org/packages/Azure.Identity) — credential handling
- [`Azure.AI.Projects`](https://www.nuget.org/packages/Azure.AI.Projects) — Azure AI Projects client
