# Recipe: swap Ollama for Azure OpenAI

The blocks never talk to a provider directly — everything goes through `IChatClient`. Swapping providers is one DI registration.

1. Add packages:

```bash
dotnet add package Azure.AI.OpenAI
dotnet add package Microsoft.Extensions.AI.OpenAI
```

2. Replace the registration in `Program.cs`:

```csharp
// Before (Ollama):
builder.Services.AddSingleton<IChatClient>(_ =>
    new OllamaApiClient(new Uri("http://localhost:11434"), "gpt-oss:20b"));

// After (Azure OpenAI + Managed Identity — no key at all):
builder.Services.AddSingleton<IChatClient>(_ =>
    new AzureOpenAIClient(
            new Uri(builder.Configuration["AzureOpenAI:Endpoint"]!),
            new DefaultAzureCredential())
        .GetChatClient(builder.Configuration["AzureOpenAI:Deployment"]!)
        .AsIChatClient());
```

3. Config (endpoint and deployment are not secrets; a key, if you must use one, goes in user secrets locally and Key Vault on Azure — see `infra/bicep/container-app.bicep`):

```json
"AzureOpenAI": { "Endpoint": "https://<resource>.openai.azure.com", "Deployment": "gpt-4o-mini" }
```

Nothing else changes: the loop, tools, streaming and both Angular blocks are provider-agnostic. The same applies to Anthropic, OpenAI proper, or anything else with an `IChatClient` implementation.
