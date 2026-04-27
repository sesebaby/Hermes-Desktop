namespace Hermes.Agent.Memory;

using System.Text.Json;
using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Hermes.Agent.Plugins;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// Periodic post-response memory review.
/// Mirrors Python's background memory nudge: after every configured N user
/// turns, a detached review pass inspects the conversation and may call the
/// same built-in memory tool. User responses are never delayed by this pass.
/// </summary>
public sealed class MemoryReviewService
{
    private const string MemoryReviewPrompt =
        "Review the conversation above and consider saving to memory if appropriate.\n\n" +
        "Focus on:\n" +
        "1. Has the user revealed things about themselves -- their persona, desires, preferences, or personal details worth remembering?\n" +
        "2. Has the user expressed expectations about how you should behave, their work style, or ways they want you to operate?\n\n" +
        "If something stands out, save it using the memory tool. If nothing is worth saving, just say 'Nothing to save.' and stop.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IChatClient _chatClient;
    private readonly MemoryManager _memoryManager;
    private readonly PluginManager? _pluginManager;
    private readonly ILogger<MemoryReviewService> _logger;
    private readonly int _nudgeInterval;
    private int _turnsSinceReview;

    public MemoryReviewService(
        IChatClient chatClient,
        MemoryManager memoryManager,
        ILogger<MemoryReviewService> logger,
        PluginManager? pluginManager = null,
        int nudgeInterval = 10)
    {
        _chatClient = chatClient;
        _memoryManager = memoryManager;
        _logger = logger;
        _pluginManager = pluginManager;
        _nudgeInterval = nudgeInterval;
    }

    public bool QueueAfterTurn(IReadOnlyList<Message> messagesSnapshot, string finalResponse, bool interrupted)
    {
        if (!MarkTurnAndCheckDue(finalResponse, interrupted))
            return false;

        var snapshot = messagesSnapshot.Select(CloneMessage).ToList();
        _ = Task.Run(async () =>
        {
            try
            {
                await ReviewConversationAsync(snapshot, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background memory review failed non-fatally");
            }
        });

        return true;
    }

    public bool MarkTurnAndCheckDue(string finalResponse, bool interrupted)
    {
        if (_nudgeInterval <= 0 || interrupted || string.IsNullOrWhiteSpace(finalResponse))
            return false;

        var turns = Interlocked.Increment(ref _turnsSinceReview);
        if (turns < _nudgeInterval)
            return false;

        Interlocked.Exchange(ref _turnsSinceReview, 0);
        return true;
    }

    public async Task<IReadOnlyList<ToolResult>> ReviewConversationAsync(
        IReadOnlyList<Message> messagesSnapshot,
        CancellationToken ct)
    {
        var reviewMessages = messagesSnapshot.Select(CloneMessage).ToList();
        reviewMessages.Add(new Message { Role = "user", Content = MemoryReviewPrompt });

        var response = await _chatClient.CompleteWithToolsAsync(
            reviewMessages,
            new[] { BuildMemoryToolDefinition() },
            ct);

        var results = new List<ToolResult>();
        foreach (var toolCall in response.ToolCalls ?? new List<ToolCall>())
        {
            if (!string.Equals(toolCall.Name, "memory", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var parameters = JsonSerializer.Deserialize<MemoryToolParameters>(
                    toolCall.Arguments,
                    JsonOptions);
                if (parameters is null)
                    continue;

                var tool = new MemoryTool(_memoryManager, _pluginManager);
                results.Add(await tool.ExecuteAsync(parameters, ct));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background memory review tool call failed");
            }
        }

        return results;
    }

    private static ToolDefinition BuildMemoryToolDefinition()
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new Dictionary<string, object>
            {
                ["action"] = new
                {
                    type = "string",
                    @enum = new[] { "add", "replace", "remove" },
                    description = "The action to perform."
                },
                ["target"] = new
                {
                    type = "string",
                    @enum = new[] { "memory", "user" },
                    description = "Which memory store: memory for agent notes, user for user profile."
                },
                ["content"] = new
                {
                    type = "string",
                    description = "The entry content. Required for add and replace."
                },
                ["old_text"] = new
                {
                    type = "string",
                    description = "Short unique substring identifying the entry to replace or remove."
                }
            },
            required = new[] { "action", "target" }
        });

        return new ToolDefinition
        {
            Name = "memory",
            Description = "Save durable information to persistent memory that survives across sessions.",
            Parameters = schema
        };
    }

    private static Message CloneMessage(Message message)
        => new()
        {
            Role = message.Role,
            Content = message.Content,
            Timestamp = message.Timestamp,
            ToolCallId = message.ToolCallId,
            ToolName = message.ToolName,
            ToolCalls = message.ToolCalls?.Select(CloneToolCall).ToList()
        };

    private static ToolCall CloneToolCall(ToolCall toolCall)
        => new()
        {
            Id = toolCall.Id,
            Name = toolCall.Name,
            Arguments = toolCall.Arguments
        };
}
