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
        if (intent.Action is NpcLocalActionKind.Move or NpcLocalActionKind.IdleMicroAction)
            return Task.FromResult(NpcLocalExecutorRunner.BlockDisabledWriteAction(intent));

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

public sealed partial class NpcLocalExecutorRunner : INpcLocalExecutorRunner
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

        if (IsDisabledWriteAction(intent.Action))
            return BlockDisabledWriteAction(intent);

        if (intent.Action is NpcLocalActionKind.Wait or NpcLocalActionKind.Escalate)
            return CompleteHostInterpreted(intent);

        try
        {
            var selectedTools = SelectTools(intent.Action);
            if (selectedTools.Count == 0)
                return Block("local_executor", "required_tool_unavailable", "required_tool_unavailable", $"required tool unavailable for {FormatAction(intent.Action)}");

            var firstAttempt = await TryRunModelToolCallsAsync(descriptor, intent, facts, traceId, selectedTools, correctiveRetry: false, ct);
            if (firstAttempt.Stage != "blocked" ||
                !string.Equals(firstAttempt.Error, "executor_protocol_violation", StringComparison.Ordinal))
            {
                return firstAttempt;
            }

            var retry = await TryRunModelToolCallsAsync(descriptor, intent, facts, traceId, selectedTools, correctiveRetry: true, ct);
            if (retry.Stage == "blocked" &&
                string.Equals(retry.Error, "executor_protocol_violation", StringComparison.Ordinal))
            {
                return retry with
                {
                    Diagnostics =
                    [
                        ..firstAttempt.Diagnostics,
                        "target=local_executor stage=attempt result=executor_protocol_violation;attempt=1",
                        ..retry.Diagnostics,
                        "target=local_executor stage=retry result=executor_protocol_violation;attempt=2"
                    ]
                };
            }

            return retry with
            {
                Diagnostics =
                [
                    ..firstAttempt.Diagnostics,
                    "target=local_executor stage=attempt result=executor_protocol_violation;attempt=1",
                    ..retry.Diagnostics
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

    private async Task<NpcLocalExecutorResult> TryRunModelToolCallsAsync(
        NpcRuntimeDescriptor descriptor,
        NpcLocalActionIntent intent,
        IReadOnlyList<NpcObservationFact> facts,
        string traceId,
        IReadOnlyList<ITool> selectedTools,
        bool correctiveRetry,
        CancellationToken ct)
    {
        var diagnostics = new List<string>();
        var messages = new List<Message>
        {
            new()
            {
                Role = "user",
                Content = BuildUserMessage(descriptor, intent, facts, traceId, correctiveRetry)
            }
        };
        await foreach (var streamEvent in _chatClient.StreamAsync(
                           BuildSystemPrompt(intent.Action),
                           messages,
                           selectedTools.Select(BuildToolDefinition),
                           ct))
        {
            switch (streamEvent)
            {
                case StreamEvent.ToolUseComplete toolUse:
                {
                    var execution = await ExecuteToolUseAsync(toolUse, selectedTools, ct);
                    diagnostics.AddRange(execution.Diagnostics);
                    if (execution.Result.Stage == "blocked")
                    {
                        return execution.Result with { Diagnostics = diagnostics };
                    }

                    return execution.Result with { Diagnostics = diagnostics };
                }
                case StreamEvent.StreamError error:
                    return Block("local_executor", "stream_error", "stream_error", error.Error.Message) with { Diagnostics = diagnostics };
            }
        }

        return Block("local_executor", "no_tool_call", "no_tool_call", "no tool call returned") with { Diagnostics = diagnostics };
    }

    private IReadOnlyList<ITool> SelectTools(NpcLocalActionKind action)
    {
        var requiredTool = action switch
        {
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

    private async Task<ToolExecutionResult> ExecuteToolUseAsync(
        StreamEvent.ToolUseComplete toolUse,
        IReadOnlyList<ITool> selectedTools,
        CancellationToken ct)
    {
        var tool = selectedTools.FirstOrDefault(candidate => string.Equals(candidate.Name, toolUse.Name, StringComparison.OrdinalIgnoreCase));
        if (tool is null)
            return ToolExecutionResult.Block(toolUse.Name, $"unknown_tool:{toolUse.Name}", "unknown_tool", $"unknown tool {toolUse.Name}");

        object parameters;
        try
        {
            parameters = toolUse.Arguments.Deserialize(tool.ParametersType, ToolArgJsonOptions)
                ?? throw new JsonException($"Failed to deserialize arguments for {toolUse.Name}");
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.Block(
                toolUse.Name,
                "invalid_tool_arguments",
                "invalid_tool_arguments",
                ex.Message,
                $"target={toolUse.Name} stage=blocked result=invalid_tool_arguments;args={Truncate(toolUse.Arguments.GetRawText(), 500)};error={Truncate(ex.Message, 300)}");
        }

        ToolResult result;
        try
        {
            result = await tool.ExecuteAsync(parameters, ct);
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.Block(toolUse.Name, "tool_execution_exception", "tool_execution_exception", ex.Message);
        }

        if (!result.Success)
            return ToolExecutionResult.Block(toolUse.Name, result.Content, "tool_failed", result.Content);

        var evidence = ReadToolEvidence(result.Content);
        var shortResult = evidence.Status ?? "completed";
        var commandId = evidence.CommandId;
        return new ToolExecutionResult(
            new NpcLocalExecutorResult(
                toolUse.Name,
                "completed",
                shortResult,
                $"local_executor_completed:{toolUse.Name}",
                BuildMemorySummary(toolUse.Name, shortResult, commandId),
                commandId,
                ExecutorMode: "model_called",
                Diagnostics: []),
            result.Content,
            []);
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

    private static bool IsDisabledWriteAction(NpcLocalActionKind action)
        => action is NpcLocalActionKind.Move or NpcLocalActionKind.IdleMicroAction;

    internal static NpcLocalExecutorResult BlockDisabledWriteAction(NpcLocalActionIntent intent)
    {
        var action = FormatAction(intent.Action);
        return Block(
            action,
            "local_executor_write_action_disabled",
            "local_executor_write_action_disabled",
            $"{action} is a parent-visible host lifecycle action and cannot be executed by the local executor");
    }

    private static string BuildSystemPrompt(NpcLocalActionKind action)
        => "你是星露谷 NPC 的本地只读辅助执行层。只执行上游已经给出的只读/status intent，只使用当前提供的工具。不要移动、说话、打开私聊、做微动作、替角色做人格、关系、送礼、交易或长期计划决策。";

    private static string BuildUserMessage(
        NpcRuntimeDescriptor descriptor,
        NpcLocalActionIntent intent,
        IReadOnlyList<NpcObservationFact> facts,
        string traceId,
        bool correctiveRetry)
    {
        var retryInstruction = correctiveRetry
            ? "重试指令：上一次没有产生工具调用。现在必须调用当前提供的单个工具；不要输出自然语言回答。\n"
            : "";
        var message =
            retryInstruction +
            $"traceId: {traceId}\n" +
            $"npc: {descriptor.DisplayName} ({descriptor.NpcId})\n" +
            $"intent: {SerializeIntent(intent)}";

        if (intent.Action is NpcLocalActionKind.Move)
            return message;

        var factLines = facts.TakeLast(6).Select(fact =>
            $"- [{fact.SourceKind}] {fact.Summary} ({string.Join("; ", fact.Facts.Take(12))})");
        return message + "\n" +
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
                AddIfNotBlank(values, "destinationText", intent.DestinationText);
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
            case NpcLocalActionKind.IdleMicroAction:
                if (intent.IdleMicroAction is not null)
                {
                    values["idleMicroAction"] = new Dictionary<string, object?>
                    {
                        ["kind"] = intent.IdleMicroAction.Kind,
                        ["animationAlias"] = intent.IdleMicroAction.AnimationAlias,
                        ["intensity"] = intent.IdleMicroAction.Intensity,
                        ["ttlSeconds"] = intent.IdleMicroAction.TtlSeconds
                    }.Where(pair => pair.Value is not null)
                        .ToDictionary(pair => pair.Key, pair => pair.Value);
                }

                break;
        }

        return JsonSerializer.Serialize(values);
    }

    private static void AddIfNotBlank(Dictionary<string, object?> values, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            values[key] = value;
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
            var status = ReadString(root, "result") ?? ReadString(root, "status");
            if (root.TryGetProperty("finalStatus", out var finalStatus) &&
                finalStatus.ValueKind == JsonValueKind.Object)
            {
                commandId ??= ReadString(finalStatus, "commandId");
                status = ReadString(finalStatus, "result") ?? ReadString(finalStatus, "status") ?? status;
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

    private static string Truncate(string value, int maxChars)
        => value.Length <= maxChars ? value : value[..maxChars] + "...";

    private static string FormatAction(NpcLocalActionKind action)
        => action switch
        {
            NpcLocalActionKind.Move => "move",
            NpcLocalActionKind.Observe => "observe",
            NpcLocalActionKind.Wait => "wait",
            NpcLocalActionKind.TaskStatus => "task_status",
            NpcLocalActionKind.Escalate => "escalate",
            NpcLocalActionKind.IdleMicroAction => "idle_micro_action",
            _ => action.ToString()
        };

    private static string ToCamelCase(string name)
        => string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];

    private sealed record ToolEvidence(string? CommandId, string? Status);

    private sealed record ToolExecutionResult(
        NpcLocalExecutorResult Result,
        string ToolContent,
        IReadOnlyList<string> Diagnostics)
    {
        public static ToolExecutionResult Block(string target, string result, string error, string? memorySummary, string? diagnostic = null)
        {
            var blocked = NpcLocalExecutorRunner.Block(target, result, error, memorySummary);
            return new ToolExecutionResult(
                blocked,
                result,
                string.IsNullOrWhiteSpace(diagnostic) ? blocked.Diagnostics : [..blocked.Diagnostics, diagnostic]);
        }
    }

}
