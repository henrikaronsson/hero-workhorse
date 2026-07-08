using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HeroWorkhorse.AgentLoop;

/// <summary>
/// The core agent while-loop: call the model, run any requested tools, feed results back,
/// repeat until the model answers with plain text or a policy limit (steps / timeout / tokens) hits.
/// Tool invocation is handled here (not via FunctionInvokingChatClient) so every tool call can be
/// surfaced as an event to the client.
/// </summary>
public sealed class AgentLoop
{
    private readonly IChatClient _chatClient;
    private readonly AgentLoopOptions _options;
    private readonly ILogger _logger;

    public AgentLoop(IChatClient chatClient, AgentLoopOptions? options = null, ILogger<AgentLoop>? logger = null)
    {
        _chatClient = chatClient;
        _options = options ?? new AgentLoopOptions();
        _logger = (ILogger?)logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Runs the loop. Assistant and tool messages are appended to <paramref name="messages"/>,
    /// so the caller keeps the full conversation history for persistence or follow-up turns.
    /// </summary>
    public async IAsyncEnumerable<AgentEvent> RunAsync(
        IList<ChatMessage> messages,
        ChatOptions? chatOptions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_options.Timeout is { } timeout)
        {
            cts.CancelAfter(timeout);
        }

        long inputTokens = 0;
        long outputTokens = 0;
        var step = 0;
        string? stopReason = null;

        while (step < _options.MaxSteps)
        {
            step++;
            _logger.LogDebug("Agent step {Step} starting ({MessageCount} messages)", step, messages.Count);

            var updates = new List<ChatResponseUpdate>();
            string? errorMessage = null;

            var stream = _chatClient
                .GetStreamingResponseAsync(messages, chatOptions, cts.Token)
                .GetAsyncEnumerator(cts.Token);
            try
            {
                while (true)
                {
                    ChatResponseUpdate update;
                    try
                    {
                        if (!await stream.MoveNextAsync())
                        {
                            break;
                        }
                        update = stream.Current;
                    }
                    catch (OperationCanceledException)
                    {
                        stopReason = "cancelled";
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Model call failed on step {Step}", step);
                        errorMessage = ex.Message;
                        break;
                    }

                    updates.Add(update);
                    foreach (var content in update.Contents)
                    {
                        if (content is TextContent { Text.Length: > 0 } text)
                        {
                            yield return new TextDelta(text.Text);
                        }
                    }
                }
            }
            finally
            {
                await stream.DisposeAsync();
            }

            if (errorMessage is not null)
            {
                yield return new AgentError(errorMessage);
                yield break;
            }
            if (stopReason == "cancelled")
            {
                break;
            }

            var response = updates.ToChatResponse();
            if (response.Usage is { } usage)
            {
                inputTokens += usage.InputTokenCount ?? 0;
                outputTokens += usage.OutputTokenCount ?? 0;
            }
            foreach (var message in response.Messages)
            {
                messages.Add(message);
            }

            var calls = response.Messages
                .SelectMany(m => m.Contents)
                .OfType<FunctionCallContent>()
                .ToList();
            if (calls.Count == 0)
            {
                stopReason = "done";
                break;
            }

            var resultContents = new List<AIContent>();
            foreach (var call in calls)
            {
                var argumentsJson = JsonSerializer.Serialize(call.Arguments, AIJsonUtilities.DefaultOptions);
                yield return new ToolCallStarted(call.CallId, call.Name, argumentsJson);

                var (result, isError) = await InvokeToolAsync(call, chatOptions, cts.Token);
                var resultJson = JsonSerializer.Serialize(result, AIJsonUtilities.DefaultOptions);

                yield return new ToolResult(call.CallId, call.Name, resultJson, isError);
                resultContents.Add(new FunctionResultContent(call.CallId, result));
            }
            messages.Add(new ChatMessage(ChatRole.Tool, resultContents));

            if (_options.MaxTotalTokens is { } budget && inputTokens + outputTokens > budget)
            {
                _logger.LogWarning("Token budget exceeded: {Used}/{Budget}", inputTokens + outputTokens, budget);
                stopReason = "token-budget";
                break;
            }
        }

        stopReason ??= "max-steps";
        yield return new Completed(
            stopReason,
            step,
            inputTokens > 0 ? inputTokens : null,
            outputTokens > 0 ? outputTokens : null);
    }

    private async Task<(object? Result, bool IsError)> InvokeToolAsync(
        FunctionCallContent call,
        ChatOptions? chatOptions,
        CancellationToken cancellationToken)
    {
        var tool = chatOptions?.Tools?.OfType<AIFunction>().FirstOrDefault(t => t.Name == call.Name);
        if (tool is null)
        {
            _logger.LogWarning("Model requested unknown tool {Tool}", call.Name);
            return ($"Unknown tool: {call.Name}", true);
        }

        try
        {
            var result = await tool.InvokeAsync(new AIFunctionArguments(call.Arguments), cancellationToken);
            return (result, false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Tool {Tool} failed", call.Name);
            return ($"Tool failed: {ex.Message}", true);
        }
    }
}
