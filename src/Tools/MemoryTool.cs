namespace Hermes.Agent.Tools;

using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hermes.Agent.Core;
using Hermes.Agent.Memory;
using Hermes.Agent.Plugins;

/// <summary>
/// Python-compatible curated memory tool.
/// Reference: external/hermes-agent-main/tools/memory_tool.py.
/// </summary>
public sealed class MemoryTool : ITool, IToolSchemaProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly MemoryManager _memoryManager;
    private readonly PluginManager? _pluginManager;
    private readonly bool _isAvailable;

    public string Name => "memory";

    public string Description =>
        "Save durable information to persistent memory that survives across sessions. " +
        "Memory is injected into future turns, so keep it compact and focused on facts that will still matter later.\n\n" +
        "WHEN TO SAVE: user corrections or explicit remember/don't-do-that-again requests; user preferences, habits, " +
        "personal details, communication style; environment facts, project conventions, tool quirks, and stable facts " +
        "that reduce future user steering.\n\n" +
        "Do NOT save task progress, session outcomes, completed-work logs, or temporary TODO state to memory; use " +
        "session_search for past transcript recall. Write declarative facts, not imperative instructions.\n\n" +
        "Targets: 'user' for who the user is; 'memory' for agent notes such as environment facts and project conventions. " +
        "Actions: add, replace, remove.";

    public Type ParametersType => typeof(MemoryToolParameters);

    public MemoryTool(
        MemoryManager memoryManager,
        PluginManager? pluginManager = null,
        bool isAvailable = true)
    {
        _memoryManager = memoryManager;
        _pluginManager = pluginManager;
        _isAvailable = isAvailable;
    }

    public JsonElement GetParameterSchema()
    {
        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["action"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["enum"] = new[] { "add", "replace", "remove" },
                    ["description"] = "The action to perform."
                },
                ["target"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["enum"] = new[] { "memory", "user" },
                    ["description"] = "Which memory store: 'memory' for personal notes, 'user' for user profile."
                },
                ["content"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "The entry content. Required for 'add' and 'replace'."
                },
                ["old_text"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Short unique substring identifying the entry to replace or remove."
                }
            },
            ["required"] = new[] { "action", "target" }
        };

        return JsonSerializer.SerializeToElement(schema, JsonOptions);
    }

    public async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        if (!_isAvailable)
        {
            var unavailable = MemoryOperationResult.ToolError(
                "Memory is not available. It may be disabled in config or this environment.");
            return ToolResult.Fail(JsonSerializer.Serialize(unavailable, JsonOptions));
        }

        var p = (MemoryToolParameters)parameters;
        var action = p.Action?.Trim().ToLowerInvariant() ?? "";
        var target = string.IsNullOrWhiteSpace(p.Target) ? "memory" : p.Target.Trim().ToLowerInvariant();

        var result = action switch
        {
            "add" when string.IsNullOrWhiteSpace(p.Content) =>
                MemoryOperationResult.ToolError("Content is required for 'add' action."),
            "add" => await _memoryManager.AddAsync(target, p.Content, ct),
            "replace" when string.IsNullOrWhiteSpace(p.OldText) =>
                MemoryOperationResult.ToolError("old_text is required for 'replace' action."),
            "replace" when string.IsNullOrWhiteSpace(p.Content) =>
                MemoryOperationResult.ToolError("content is required for 'replace' action."),
            "replace" => await _memoryManager.ReplaceAsync(target, p.OldText, p.Content, ct),
            "remove" when string.IsNullOrWhiteSpace(p.OldText) =>
                MemoryOperationResult.ToolError("old_text is required for 'remove' action."),
            "remove" => await _memoryManager.RemoveAsync(target, p.OldText, ct),
            _ => MemoryOperationResult.ToolError($"Unknown action '{p.Action}'. Use: add, replace, remove")
        };

        if (result.Success && _pluginManager is not null && action is "add" or "replace")
            await _pluginManager.OnMemoryWriteAsync(action, target, p.Content ?? "", ct);

        var json = JsonSerializer.Serialize(result, JsonOptions);
        return result.Success ? ToolResult.Ok(json) : ToolResult.Fail(json);
    }
}

public sealed class MemoryToolParameters
{
    [Description("The action to perform: add, replace, or remove.")]
    public required string Action { get; init; }

    [Description("Which memory store to mutate: 'memory' for agent notes, 'user' for user profile.")]
    public string Target { get; init; } = "memory";

    [Description("The entry content. Required for add and replace.")]
    public string? Content { get; init; }

    [JsonPropertyName("old_text")]
    [Description("Short unique substring identifying the entry to replace or remove.")]
    public string? OldText { get; init; }
}
