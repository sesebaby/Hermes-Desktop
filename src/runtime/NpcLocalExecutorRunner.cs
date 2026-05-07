namespace Hermes.Agent.Runtime;

using System.Text.Json;
using System.Text.Json.Serialization;
using Hermes.Agent.Core;
using Hermes.Agent.LLM;

public interface INpcLocalExecutorRunner
{
    Task<NpcLocalExecutorResult> ExecuteAsync(
        NpcRuntimeDescriptor descriptor,
        NpcLocalActionIntent intent,
        IReadOnlyList<NpcObservationFact> facts,
        string traceId,
        CancellationToken ct);
}

public sealed record NpcLocalExecutorResult(
    string Target,
    string Stage,
    string Result,
    string DecisionResponse,
    string? MemorySummary = null,
    string? CommandId = null,
    string? Error = null,
    string ExecutorMode = "model_called",
    string? TargetSource = null,
    IReadOnlyList<string>? Diagnostics = null)
{
    public IReadOnlyList<string> Diagnostics { get; init; } = Diagnostics ?? [];
}

public sealed class NpcUnavailableLocalExecutorRunner : INpcLocalExecutorRunner
{
    public Task<NpcLocalExecutorResult> ExecuteAsync(
        NpcRuntimeDescriptor descriptor,
        NpcLocalActionIntent intent,
        IReadOnlyList<NpcObservationFact> facts,
        string traceId,
        CancellationToken ct)
    {
        if (intent.Action is NpcLocalActionKind.Wait or NpcLocalActionKind.Escalate)
            return Task.FromResult(NpcLocalExecutorRunner.CompleteHostInterpreted(intent));

        return Task.FromResult(new NpcLocalExecutorResult(
            "local_executor",
            "blocked",
            "local_executor_unavailable",
            "local_executor_blocked:local_executor_unavailable",
            "local executor was unavailable; low-risk action was not delegated or executed.",
            Error: "local_executor_unavailable",
            ExecutorMode: "blocked"));
    }
}

public sealed class NpcLocalExecutorRunner : INpcLocalExecutorRunner
{
    private static readonly JsonSerializerOptions ToolArgJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IChatClient _chatClient;
    private readonly Dictionary<string, ITool> _tools;

    public NpcLocalExecutorRunner(IChatClient chatClient, IEnumerable<ITool> tools)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(tools);

        _chatClient = chatClient;
        _tools = tools.ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<NpcLocalExecutorResult> ExecuteAsync(
        NpcRuntimeDescriptor descriptor,
        NpcLocalActionIntent intent,
        IReadOnlyList<NpcObservationFact> facts,
        string traceId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(facts);

        if (intent.Action is NpcLocalActionKind.Wait or NpcLocalActionKind.Escalate)
            return CompleteHostInterpreted(intent);

        try
        {
            if (intent.Action is NpcLocalActionKind.Move && intent.Target is not null)
                return await ExecuteMechanicalMoveAsync(intent, ct);

            var selectedTools = SelectTools(intent.Action);
            if (selectedTools.Count == 0)
                return Block("local_executor", "required_tool_unavailable", "required_tool_unavailable", $"required tool unavailable for {FormatAction(intent.Action)}");

            var firstAttempt = await TryRunModelToolCallAsync(descriptor, intent, facts, traceId, selectedTools, correctiveRetry: false, ct);
            if (firstAttempt.Stage != "blocked" ||
                !string.Equals(firstAttempt.Error, "no_tool_call", StringComparison.Ordinal))
            {
                return firstAttempt;
            }

            var retry = await TryRunModelToolCallAsync(descriptor, intent, facts, traceId, selectedTools, correctiveRetry: true, ct);
            if (retry.Stage == "blocked" &&
                string.Equals(retry.Error, "no_tool_call", StringComparison.Ordinal))
            {
                return retry with
                {
                    Diagnostics =
                    [
                        "target=local_executor stage=attempt result=no_tool_call;attempt=1",
                        "target=local_executor stage=retry result=no_tool_call;attempt=2"
                    ]
                };
            }

            return retry with
            {
                Diagnostics =
                [
                    "target=local_executor stage=attempt result=no_tool_call;attempt=1"
                ]
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Block("local_executor", "execution_error", "execution_error", ex.Message);
        }
    }

    private async Task<NpcLocalExecutorResult> ExecuteMechanicalMoveAsync(
        NpcLocalActionIntent intent,
        CancellationToken ct)
    {
        const string toolName = "stardew_navigate_to_tile";
        if (!_tools.TryGetValue(toolName, out var tool))
        {
            return Block(
                toolName,
                "required_tool_unavailable:stardew_navigate_to_tile",
                "required_tool_unavailable",
                "required tool unavailable for mechanical move");
        }

        var target = intent.Target!;
        var parameters = CreateMechanicalMoveParameters(tool.ParametersType, target, intent);
        ToolResult result;
        try
        {
            result = await tool.ExecuteAsync(parameters, ct);
        }
        catch (Exception ex)
        {
            return Block(toolName, "tool_execution_exception", "tool_execution_exception", ex.Message);
        }

        if (!result.Success)
            return Block(toolName, result.Content, "tool_failed", result.Content);

        var evidence = ReadToolEvidence(result.Content);
        var shortResult = evidence.Status ?? "completed";
        return new NpcLocalExecutorResult(
            toolName,
            "completed",
            shortResult,
            $"local_executor_completed:{toolName}",
            BuildMemorySummary(toolName, shortResult, evidence.CommandId),
            evidence.CommandId,
            ExecutorMode: "host_deterministic",
            TargetSource: target.Source);
    }

    private async Task<NpcLocalExecutorResult> TryRunModelToolCallAsync(
        NpcRuntimeDescriptor descriptor,
        NpcLocalActionIntent intent,
        IReadOnlyList<NpcObservationFact> facts,
        string traceId,
        IReadOnlyList<ITool> selectedTools,
        bool correctiveRetry,
        CancellationToken ct)
    {
        var messages = new[]
            {
                new Message
                {
                    Role = "user",
                    Content = BuildUserMessage(descriptor, intent, facts, traceId, correctiveRetry)
                }
            };
        await foreach (var streamEvent in _chatClient.StreamAsync(
                           BuildSystemPrompt(),
                           messages,
                           selectedTools.Select(BuildToolDefinition),
                           ct))
        {
            switch (streamEvent)
            {
                case StreamEvent.ToolUseComplete toolUse:
                    return await ExecuteToolUseAsync(toolUse, selectedTools, ct);
                case StreamEvent.StreamError error:
                    return Block("local_executor", "stream_error", "stream_error", error.Error.Message);
            }
        }

        return Block("local_executor", "no_tool_call", "no_tool_call", "no tool call returned");
    }

    private IReadOnlyList<ITool> SelectTools(NpcLocalActionKind action)
    {
        var requiredTool = action switch
        {
            NpcLocalActionKind.Move => "stardew_move",
            NpcLocalActionKind.TaskStatus => "stardew_task_status",
            NpcLocalActionKind.Observe => "stardew_status",
            _ => null
        };

        if (requiredTool is null)
            return [];

        return _tools.TryGetValue(requiredTool, out var tool)
            ? [tool]
            : [];
    }

    private async Task<NpcLocalExecutorResult> ExecuteToolUseAsync(
        StreamEvent.ToolUseComplete toolUse,
        IReadOnlyList<ITool> selectedTools,
        CancellationToken ct)
    {
        var tool = selectedTools.FirstOrDefault(candidate => string.Equals(candidate.Name, toolUse.Name, StringComparison.OrdinalIgnoreCase));
        if (tool is null)
            return Block(toolUse.Name, $"unknown_tool:{toolUse.Name}", "unknown_tool", $"unknown tool {toolUse.Name}");

        object parameters;
        try
        {
            parameters = toolUse.Arguments.Deserialize(tool.ParametersType, ToolArgJsonOptions)
                ?? throw new JsonException($"Failed to deserialize arguments for {toolUse.Name}");
        }
        catch (Exception ex)
        {
            return Block(toolUse.Name, "invalid_tool_arguments", "invalid_tool_arguments", ex.Message);
        }

        ToolResult result;
        try
        {
            result = await tool.ExecuteAsync(parameters, ct);
        }
        catch (Exception ex)
        {
            return Block(toolUse.Name, "tool_execution_exception", "tool_execution_exception", ex.Message);
        }

        if (!result.Success)
            return Block(toolUse.Name, result.Content, "tool_failed", result.Content);

        var evidence = ReadToolEvidence(result.Content);
        var shortResult = evidence.Status ?? "completed";
        var commandId = evidence.CommandId;
        return new NpcLocalExecutorResult(
            toolUse.Name,
            "completed",
            shortResult,
            $"local_executor_completed:{toolUse.Name}",
            BuildMemorySummary(toolUse.Name, shortResult, commandId),
            commandId,
            ExecutorMode: "model_called");
    }

    internal static NpcLocalExecutorResult CompleteHostInterpreted(NpcLocalActionIntent intent)
    {
        var action = FormatAction(intent.Action);
        var result = intent.Action switch
        {
            NpcLocalActionKind.Wait => intent.WaitReason ?? intent.Reason,
            NpcLocalActionKind.Observe => intent.ObserveTarget ?? intent.Reason,
            _ => intent.Reason
        };
        return new NpcLocalExecutorResult(
            action,
            "completed",
            string.IsNullOrWhiteSpace(result) ? "host_interpreted" : result,
            $"local_executor_completed:{action}",
            $"{action} completed by host interpretation; reason: {intent.Reason}",
            ExecutorMode: "host_interpreted");
    }

    private static NpcLocalExecutorResult Block(string target, string result, string error, string? memorySummary)
        => new(
            target,
            "blocked",
            result,
            $"local_executor_blocked:{error}",
            string.IsNullOrWhiteSpace(memorySummary)
                ? null
                : $"local executor blocked: {memorySummary}",
            Error: error,
            ExecutorMode: "blocked");

    private static string BuildSystemPrompt()
        => "You are a local Stardew NPC action executor. Execute only the provided intent using only the provided tools. Do not make personality, relationship, gift, trade, or long-term planning decisions.";

    private static string BuildUserMessage(
        NpcRuntimeDescriptor descriptor,
        NpcLocalActionIntent intent,
        IReadOnlyList<NpcObservationFact> facts,
        string traceId,
        bool correctiveRetry)
    {
        var factLines = facts.TakeLast(6).Select(fact =>
            $"- [{fact.SourceKind}] {fact.Summary} ({string.Join("; ", fact.Facts.Take(12))})");
        var retryInstruction = correctiveRetry
            ? "retryInstruction: Previous attempt returned no tool call. Call the single provided tool now; do not answer in text.\n"
            : "";
        return
            retryInstruction +
            $"traceId: {traceId}\n" +
            $"npc: {descriptor.DisplayName} ({descriptor.NpcId})\n" +
            $"intent: {SerializeIntent(intent)}\n" +
            "facts:\n" +
            string.Join("\n", factLines);
    }

    private static string SerializeIntent(NpcLocalActionIntent intent)
    {
        var values = new Dictionary<string, object?>
        {
            ["action"] = FormatAction(intent.Action),
            ["reason"] = intent.Reason
        };
        switch (intent.Action)
        {
            case NpcLocalActionKind.Move:
                AddIfNotBlank(values, "destinationId", intent.DestinationId);
                if (intent.Target is not null)
                {
                    values["target"] = new Dictionary<string, object?>
                    {
                        ["locationName"] = intent.Target.LocationName,
                        ["x"] = intent.Target.X,
                        ["y"] = intent.Target.Y,
                        ["facingDirection"] = intent.Target.FacingDirection,
                        ["source"] = intent.Target.Source
                    }.Where(pair => pair.Value is not null)
                        .ToDictionary(pair => pair.Key, pair => pair.Value);
                }

                break;
            case NpcLocalActionKind.Observe:
                AddIfNotBlank(values, "observeTarget", intent.ObserveTarget);
                break;
            case NpcLocalActionKind.Wait:
                AddIfNotBlank(values, "waitReason", intent.WaitReason);
                break;
            case NpcLocalActionKind.TaskStatus:
                AddIfNotBlank(values, "commandId", intent.CommandId);
                break;
            case NpcLocalActionKind.Escalate:
                values["escalate"] = true;
                break;
        }

        return JsonSerializer.Serialize(values);
    }

    private static void AddIfNotBlank(Dictionary<string, object?> values, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            values[key] = value;
    }

    private static object CreateMechanicalMoveParameters(
        Type parametersType,
        NpcLocalMoveTargetIntent target,
        NpcLocalActionIntent intent)
    {
        var parameters = Activator.CreateInstance(parametersType)
            ?? throw new InvalidOperationException($"Failed to create parameters for {parametersType.Name}.");
        SetProperty(parameters, "LocationName", target.LocationName);
        SetProperty(parameters, "X", target.X);
        SetProperty(parameters, "Y", target.Y);
        SetProperty(parameters, "FacingDirection", target.FacingDirection);
        SetProperty(parameters, "Reason", intent.Reason);
        return parameters;
    }

    private static void SetProperty(object target, string propertyName, object? value)
    {
        var property = target.GetType().GetProperty(propertyName);
        if (property is null || !property.CanWrite)
            return;

        property.SetValue(target, value);
    }

    private static ToolDefinition BuildToolDefinition(ITool tool)
        => new()
        {
            Name = tool.Name,
            Description = tool.Description,
            Parameters = BuildParameterSchema(tool)
        };

    private static JsonElement BuildParameterSchema(ITool tool)
    {
        if (tool is IToolSchemaProvider schemaProvider)
            return schemaProvider.GetParameterSchema();

        var props = tool.ParametersType.GetProperties();
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var prop in props)
        {
            if (prop.GetCustomAttributes(typeof(JsonIgnoreAttribute), false).Any())
                continue;

            var jsonName = (prop.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false)
                .FirstOrDefault() as JsonPropertyNameAttribute)?.Name ?? ToCamelCase(prop.Name);
            var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            var jsonType = propType switch
            {
                Type t when t == typeof(string) => "string",
                Type t when t == typeof(int) || t == typeof(long) => "integer",
                Type t when t == typeof(double) || t == typeof(float) => "number",
                Type t when t == typeof(bool) => "boolean",
                Type t when t.IsArray || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) => "array",
                _ => "string"
            };

            properties[jsonName] = new Dictionary<string, object> { ["type"] = jsonType };
            if (propType.IsValueType && Nullable.GetUnderlyingType(prop.PropertyType) is null)
                required.Add(jsonName);
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties
        };
        if (required.Count > 0)
            schema["required"] = required;

        return JsonSerializer.SerializeToElement(schema);
    }

    private static ToolEvidence ReadToolEvidence(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var commandId = ReadString(root, "commandId");
            var status = ReadString(root, "status");
            if (root.TryGetProperty("finalStatus", out var finalStatus) &&
                finalStatus.ValueKind == JsonValueKind.Object)
            {
                commandId ??= ReadString(finalStatus, "commandId");
                status = ReadString(finalStatus, "status") ?? status;
            }

            return new ToolEvidence(commandId, status);
        }
        catch (JsonException)
        {
            return new ToolEvidence(null, null);
        }
    }

    private static string BuildMemorySummary(string toolName, string result, string? commandId)
        => string.IsNullOrWhiteSpace(commandId)
            ? $"{toolName} completed with result {result}."
            : $"{toolName} completed with result {result}; command {commandId}.";

    private static string? ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string FormatAction(NpcLocalActionKind action)
        => action switch
        {
            NpcLocalActionKind.Move => "move",
            NpcLocalActionKind.Observe => "observe",
            NpcLocalActionKind.Wait => "wait",
            NpcLocalActionKind.TaskStatus => "task_status",
            NpcLocalActionKind.Escalate => "escalate",
            _ => action.ToString()
        };

    private static string ToCamelCase(string name)
        => string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];

    private sealed record ToolEvidence(string? CommandId, string? Status);
}
