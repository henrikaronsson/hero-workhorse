using HeroWorkhorse.ConversationStore;
using HeroWorkhorse.SqlTools;
using HeroWorkhorse.Streaming;
using Microsoft.Extensions.AI;
using OllamaSharp;
using Serilog;
using SqlAgent.Api;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSerilog();

// Provider: Ollama by default (free, local). To use Azure OpenAI instead, replace this
// registration with an Azure OpenAI IChatClient and keep everything else unchanged.
builder.Services.AddSingleton<IChatClient>(_ => new OllamaApiClient(
    new Uri(builder.Configuration["Ollama:Url"] ?? "http://localhost:11434"),
    builder.Configuration["Ollama:Model"] ?? "gpt-oss:20b"));

builder.Services.AddSingleton(new SqlTools(new SqlToolsOptions
{
    ConnectionString = builder.Configuration.GetConnectionString("Shop")
        ?? throw new InvalidOperationException("Missing connection string 'Shop'."),
    MaxRows = 100,
    CommandTimeoutSeconds = 15,
}));

// Conversation persistence: in-memory by default; flip ConversationStore:UseSqlServer to true
// to persist into the docker SQL Server (tables are created on startup).
if (builder.Configuration.GetValue<bool>("ConversationStore:UseSqlServer"))
{
    var store = new SqlServerConversationStore(
        builder.Configuration.GetConnectionString("Conversations")
            ?? throw new InvalidOperationException("Missing connection string 'Conversations'."));
    await store.EnsureCreatedAsync();
    builder.Services.AddSingleton<IConversationStore>(store);
}
else
{
    builder.Services.AddSingleton<IConversationStore, InMemoryConversationStore>();
}

builder.Services.AddSingleton<IAgent, SqlAgentChat>();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration["AllowedOrigin"] ?? "http://localhost:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
app.MapAgentChat();
app.MapGet("/health", () => "ok");

app.Run();
