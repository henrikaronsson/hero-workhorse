using System.Text.Json.Serialization;

namespace HeroWorkhorse.AgentLoop;

/// <summary>
/// Events emitted by <see cref="AgentLoop"/> while it runs. The JSON polymorphism attributes make
/// these serialize with a "type" discriminator so a TypeScript client can switch on it directly.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextDelta), "text-delta")]
[JsonDerivedType(typeof(ToolCallStarted), "tool-call-started")]
[JsonDerivedType(typeof(ToolResult), "tool-result")]
[JsonDerivedType(typeof(Completed), "completed")]
[JsonDerivedType(typeof(AgentError), "error")]
[JsonDerivedType(typeof(ConversationStarted), "conversation-started")]
public abstract record AgentEvent;

/// <summary>
/// Not emitted by the loop itself: hosts that persist conversations prepend this to the stream
/// so the client learns the id to send on follow-up turns. Declared here because the JSON
/// polymorphism registry lives on the base type.
/// </summary>
public sealed record ConversationStarted(string ConversationId) : AgentEvent;

/// <summary>A chunk of assistant text as it streams in.</summary>
public sealed record TextDelta(string Text) : AgentEvent;

/// <summary>The model requested a tool call; emitted right before the tool runs.</summary>
public sealed record ToolCallStarted(string CallId, string Name, string ArgumentsJson) : AgentEvent;

/// <summary>A tool finished; result serialized to JSON (or an error message if it threw).</summary>
public sealed record ToolResult(string CallId, string Name, string ResultJson, bool IsError) : AgentEvent;

/// <summary>
/// The loop finished. <see cref="StopReason"/> is one of: "done", "max-steps", "token-budget", "cancelled".
/// </summary>
public sealed record Completed(string StopReason, int Steps, long? InputTokens, long? OutputTokens) : AgentEvent;

/// <summary>Something went wrong; the loop stops after emitting this.</summary>
public sealed record AgentError(string Message) : AgentEvent;
