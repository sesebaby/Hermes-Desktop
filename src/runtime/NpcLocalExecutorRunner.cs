namespace Hermes.Agent.Runtime;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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

public sealed partial class NpcLocalExecutorRunner : INpcLocalExecutorRunner
{
    private const int MaxModelToolIterations = 8;
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
        NavigationTargetCue? navigationTargetCue = null;
        for (var iteration = 0; iteration < MaxModelToolIterations; iteration++)
        {
            await foreach (var streamEvent in _chatClient.StreamAsync(
                               BuildSystemPrompt(intent.Action),
                               messages,
                               SelectToolsForIteration(selectedTools, navigationTargetCue).Select(BuildToolDefinition),
                               ct))
            {
                switch (streamEvent)
                {
                    case StreamEvent.ToolUseComplete toolUse:
                    {
                        var validation = ValidateNavigationToolUse(toolUse, navigationTargetCue);
                        if (validation is not null)
                        {
                            diagnostics.Add(validation.Diagnostic);
                            messages.Add(new Message
                            {
                                Role = "assistant",
                                Content = "",
                                ToolCalls =
                                [
                                    new ToolCall
                                    {
                                        Id = toolUse.Id,
                                        Name = toolUse.Name,
                                        Arguments = toolUse.Arguments.GetRawText()
                                    }
                                ]
                            });
                            messages.Add(new Message
                            {
                                Role = "tool",
                                ToolCallId = toolUse.Id,
                                ToolName = toolUse.Name,
                                Content = validation.ToolResult
                            });
                            messages.Add(new Message
                            {
                                Role = "user",
                                Content = validation.UserReminder
                            });

                            goto NextIteration;
                        }

                        var execution = await ExecuteToolUseAsync(toolUse, selectedTools, ct);
                        diagnostics.AddRange(execution.Diagnostics);
                        navigationTargetCue = execution.NavigationTargetCue ?? navigationTargetCue;
                        if (execution.Result.Stage == "blocked" ||
                            !ShouldContinueAfterTool(toolUse.Name))
                        {
                            return execution.Result with { Diagnostics = diagnostics };
                        }

                        messages.Add(new Message
                        {
                            Role = "assistant",
                            Content = "",
                            ToolCalls =
                            [
                                new ToolCall
                                {
                                    Id = toolUse.Id,
                                    Name = toolUse.Name,
                                    Arguments = toolUse.Arguments.GetRawText()
                                }
                            ]
                        });
                        messages.Add(new Message
                        {
                            Role = "tool",
                            ToolCallId = toolUse.Id,
                            ToolName = toolUse.Name,
                            Content = execution.ToolContent
                        });
                        if (execution.NavigationTargetCue is not null)
                        {
                            var targetMatch = ValidateNavigationTargetMatchesIntent(intent, execution.NavigationTargetCue);
                            if (targetMatch is not null)
                            {
                                diagnostics.Add(targetMatch.Diagnostic);
                                return Block(
                                    "local_executor",
                                    targetMatch.Error,
                                    targetMatch.Error,
                                    targetMatch.MemorySummary) with
                                {
                                    Diagnostics = diagnostics
                                };
                            }

                            messages.Add(new Message
                            {
                                Role = "user",
                                Content = BuildNavigationTargetCueMessage(execution.NavigationTargetCue)
                            });
                        }

                        goto NextIteration;
                    }
                    case StreamEvent.StreamError error:
                        return Block("local_executor", "stream_error", "stream_error", error.Error.Message) with { Diagnostics = diagnostics };
                }
            }

            if (intent.Action is NpcLocalActionKind.Move &&
                diagnostics.Any(IsSkillViewSourceDiagnostic) &&
                !diagnostics.Any(IsNavigationTargetLoadedDiagnostic))
            {
                return Block(
                    "local_executor",
                    "unresolved_navigation_target",
                    "unresolved_navigation_target",
                    "stardew-navigation skill references were consulted but no complete target(locationName,x,y,source) was loaded") with
                {
                    Diagnostics =
                    [
                        ..diagnostics,
                        "target=local_executor stage=attempt result=unresolved_navigation_target;attempt=1"
                    ]
                };
            }

            var protocolError = intent.Action is NpcLocalActionKind.Move
                ? "executor_protocol_violation"
                : "no_tool_call";
            return Block("local_executor", protocolError, protocolError, "no tool call returned") with { Diagnostics = diagnostics };

        NextIteration:
            continue;
        }

        return Block(
            "local_executor",
            "tool_iteration_limit",
            "tool_iteration_limit",
            $"local executor exceeded {MaxModelToolIterations} tool iterations") with { Diagnostics = diagnostics };
    }

    private IReadOnlyList<ITool> SelectTools(NpcLocalActionKind action)
    {
        if (action is NpcLocalActionKind.Move)
        {
            var moveTools = new List<ITool>();
            foreach (var toolName in new[] { "skill_view", "stardew_navigate_to_tile" })
            {
                if (!_tools.TryGetValue(toolName, out var moveTool))
                    return [];

                moveTools.Add(moveTool);
            }

            return moveTools;
        }

        var requiredTool = action switch
        {
            NpcLocalActionKind.TaskStatus => "stardew_task_status",
            NpcLocalActionKind.Observe => "stardew_status",
            NpcLocalActionKind.IdleMicroAction => "stardew_idle_micro_action",
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

        var diagnostics = BuildToolDiagnostics(toolUse);
        var navigationTargetCue = string.Equals(toolUse.Name, "skill_view", StringComparison.OrdinalIgnoreCase)
            ? ReadNavigationTargetCue(result.Content)
            : null;
        if (navigationTargetCue is not null)
        {
            diagnostics =
            [
                ..diagnostics,
                $"target=skill_view stage=completed result=navigation_target_loaded;locationName={navigationTargetCue.LocationName};x={navigationTargetCue.X};y={navigationTargetCue.Y};source={navigationTargetCue.Source}"
            ];
        }

        if (ShouldContinueAfterTool(toolUse.Name))
        {
            return new ToolExecutionResult(
                new NpcLocalExecutorResult(
                    toolUse.Name,
                    "completed",
                    "tool_context_loaded",
                    $"local_executor_continue:{toolUse.Name}",
                    ExecutorMode: "model_called",
                    Diagnostics: diagnostics),
                result.Content,
                diagnostics,
                navigationTargetCue);
        }

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
                TargetSource: ReadNavigationTargetSource(toolUse),
                Diagnostics: diagnostics),
            result.Content,
            diagnostics,
            navigationTargetCue);
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

    private static bool ShouldContinueAfterTool(string toolName)
        => string.Equals(toolName, "skill_view", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<ITool> SelectToolsForIteration(
        IReadOnlyList<ITool> selectedTools,
        NavigationTargetCue? navigationTargetCue)
    {
        if (navigationTargetCue is null)
        {
            var skillView = selectedTools.FirstOrDefault(tool =>
                string.Equals(tool.Name, "skill_view", StringComparison.OrdinalIgnoreCase));
            return skillView is null ? selectedTools : [skillView];
        }

        return selectedTools
            .Where(tool => string.Equals(tool.Name, "stardew_navigate_to_tile", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static string BuildSystemPrompt(NpcLocalActionKind action)
    {
        var prompt = "你是星露谷 NPC 的本地行动执行层。只执行上游已经给出的 intent，只使用当前提供的工具。不要替角色做人格、关系、送礼、交易或长期计划决策。";
        if (action is not NpcLocalActionKind.Move)
            return prompt;

        return prompt + "\n" +
               "处理没有具体 target 的移动意图时，只能通过 skill_view 解析地点。每一次调用 skill_view 都必须带 name；读取子文件时也必须同时带 name 和 file_path。\n" +
               "固定调用格式示例：先调用 skill_view({\"name\":\"stardew-navigation\"})；再调用 skill_view({\"name\":\"stardew-navigation\",\"file_path\":\"references/index.md\"})；再按索引读取相关 region 和最具体的 POI 文件。\n" +
               "绝对不要只传 file_path，也不要编造坐标。只有已加载的 skill 参考文件明确给出 locationName、x、y、source 后，才能调用 stardew_navigate_to_tile。\n" +
               "一旦某个已加载 POI 与 intent 匹配并给出完整 target(locationName,x,y,source)，必须立即调用 stardew_navigate_to_tile；不要继续横向读取其他不相关 region/POI，也不要为了比较而把所有地点读完。\n" +
               "如果目标缺失或有歧义，不要导航，让执行结果阻塞。";
    }

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

    private static string BuildNavigationTargetCueMessage(NavigationTargetCue target)
        => "你刚刚已经从 stardew-navigation 的 skill 资料读取到完整机械目标。\n" +
           $"已加载 target(locationName={target.LocationName},x={target.X},y={target.Y},source={target.Source})。\n" +
           "下一步必须调用 stardew_navigate_to_tile，并把 locationName、x、y、source 原样填入工具参数；不要继续调用 skill_view，不要继续横向读取其他地点。";

    private static NavigationToolValidation? ValidateNavigationToolUse(
        StreamEvent.ToolUseComplete toolUse,
        NavigationTargetCue? loadedTarget)
    {
        if (!string.Equals(toolUse.Name, "stardew_navigate_to_tile", StringComparison.OrdinalIgnoreCase))
            return null;

        if (loadedTarget is null)
        {
            return new NavigationToolValidation(
                "navigation_target_not_loaded",
                "target=stardew_navigate_to_tile stage=blocked result=navigation_target_not_loaded",
                "Error: navigation_target_not_loaded. You called stardew_navigate_to_tile before loading a complete target(locationName,x,y,source) from stardew-navigation skill references. First call skill_view(name=\"stardew-navigation\"), then references/index.md, then the relevant region/POI file. Do not use the player's natural-language place text as locationName.",
                "刚才的 stardew_navigate_to_tile 没有执行，因为本轮还没有从 stardew-navigation 的 skill 资料加载完整 target(locationName,x,y,source)。现在必须先调用 skill_view 读取 stardew-navigation、references/index.md 和相关 POI；不要把玩家自然语言地点直接当 locationName。");
        }

        var locationName = ReadString(toolUse.Arguments, "locationName");
        var source = ReadString(toolUse.Arguments, "source");
        var x = ReadInt(toolUse.Arguments, "x");
        var y = ReadInt(toolUse.Arguments, "y");
        if (string.Equals(locationName, loadedTarget.LocationName, StringComparison.Ordinal) &&
            x == loadedTarget.X &&
            y == loadedTarget.Y &&
            string.Equals(source, loadedTarget.Source, StringComparison.Ordinal))
        {
            return null;
        }

        var expected = $"target(locationName={loadedTarget.LocationName},x={loadedTarget.X},y={loadedTarget.Y},source={loadedTarget.Source})";
        return new NavigationToolValidation(
            "navigation_target_mismatch",
            "target=stardew_navigate_to_tile stage=blocked result=navigation_target_mismatch",
            $"Error: navigation_target_mismatch. The tool arguments must exactly match the loaded skill target: {expected}. Retry stardew_navigate_to_tile with those values unchanged.",
            $"刚才的 stardew_navigate_to_tile 没有执行，因为参数和已加载 skill target 不一致。已加载 {expected}。下一步必须调用 stardew_navigate_to_tile，并原样填写 locationName、x、y、source。");
    }

    private static NavigationTargetIntentValidation? ValidateNavigationTargetMatchesIntent(
        NpcLocalActionIntent intent,
        NavigationTargetCue loadedTarget)
    {
        if (intent.Action is not NpcLocalActionKind.Move ||
            string.IsNullOrWhiteSpace(intent.DestinationText))
        {
            return null;
        }

        if (NavigationTargetMatchesDestination(intent.DestinationText, loadedTarget))
            return null;

        var destinationText = intent.DestinationText.Trim();
        return new NavigationTargetIntentValidation(
            "navigation_target_mismatch",
            $"target=local_executor stage=blocked result=navigation_target_mismatch;destinationText={destinationText};source={loadedTarget.Source}",
            $"loaded target {loadedTarget.Source} does not match destinationText '{destinationText}'");
    }

    private static bool NavigationTargetMatchesDestination(string destinationText, NavigationTargetCue loadedTarget)
    {
        var destination = destinationText.Trim();
        var normalizedDestination = NormalizeNavigationText(destination);
        var normalizedSource = NormalizeNavigationText(loadedTarget.Source);
        var normalizedLocation = NormalizeNavigationText(loadedTarget.LocationName);

        if (normalizedDestination.Length == 0)
            return true;

        if (normalizedSource.Contains(normalizedDestination, StringComparison.Ordinal) ||
            normalizedLocation.Contains(normalizedDestination, StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var aliasGroup in NavigationDestinationAliasGroups)
        {
            if (!aliasGroup.Any(alias => DestinationContainsAlias(destination, alias)))
                continue;

            return aliasGroup.Any(alias =>
                normalizedSource.Contains(NormalizeNavigationText(alias), StringComparison.Ordinal) ||
                normalizedLocation.Contains(NormalizeNavigationText(alias), StringComparison.Ordinal));
        }

        return true;
    }

    private static bool DestinationContainsAlias(string destinationText, string alias)
    {
        var normalizedDestination = NormalizeNavigationText(destinationText);
        var normalizedAlias = NormalizeNavigationText(alias);
        if (normalizedAlias.Length == 0)
            return false;

        return normalizedDestination.Contains(normalizedAlias, StringComparison.Ordinal);
    }

    private static string NormalizeNavigationText(string value)
        => new(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

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

    private static IReadOnlyList<string> BuildToolDiagnostics(StreamEvent.ToolUseComplete toolUse)
    {
        if (!string.Equals(toolUse.Name, "skill_view", StringComparison.OrdinalIgnoreCase))
            return [];

        var skillName = ReadString(toolUse.Arguments, "name");
        if (string.IsNullOrWhiteSpace(skillName))
            return [];

        var filePath = ReadString(toolUse.Arguments, "file_path");
        var source = string.IsNullOrWhiteSpace(filePath)
            ? skillName.Trim()
            : $"{skillName.Trim()}/{filePath.Trim()}";
        return [$"target=skill_view stage=completed result=skill_source:{source}"];
    }

    private static bool IsSkillViewSourceDiagnostic(string diagnostic)
        => diagnostic.StartsWith("target=skill_view stage=completed result=skill_source:", StringComparison.Ordinal);

    private static bool IsNavigationTargetLoadedDiagnostic(string diagnostic)
        => diagnostic.StartsWith("target=skill_view stage=completed result=navigation_target_loaded;", StringComparison.Ordinal);

    private static string? ReadNavigationTargetSource(StreamEvent.ToolUseComplete toolUse)
        => string.Equals(toolUse.Name, "stardew_navigate_to_tile", StringComparison.OrdinalIgnoreCase)
            ? ReadString(toolUse.Arguments, "source")
            : null;

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

    private static NavigationTargetCue? ReadNavigationTargetCue(string toolContent)
    {
        var content = ReadSkillViewContent(toolContent) ?? toolContent;
        var match = NavigationTargetPattern().Match(content);
        if (!match.Success)
            return null;

        if (!int.TryParse(match.Groups["x"].Value, out var x) ||
            !int.TryParse(match.Groups["y"].Value, out var y))
        {
            return null;
        }

        return new NavigationTargetCue(
            match.Groups["locationName"].Value.Trim(),
            x,
            y,
            match.Groups["source"].Value.Trim());
    }

    private static string? ReadSkillViewContent(string toolContent)
    {
        try
        {
            using var document = JsonDocument.Parse(toolContent);
            return ReadString(document.RootElement, "content");
        }
        catch (JsonException)
        {
            return null;
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

    private static int? ReadInt(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var intValue)
            ? intValue
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

    private sealed record NavigationTargetCue(string LocationName, int X, int Y, string Source);

    private sealed record NavigationToolValidation(
        string Error,
        string Diagnostic,
        string ToolResult,
        string UserReminder);

    private sealed record NavigationTargetIntentValidation(
        string Error,
        string Diagnostic,
        string MemorySummary);

    private sealed record ToolExecutionResult(
        NpcLocalExecutorResult Result,
        string ToolContent,
        IReadOnlyList<string> Diagnostics,
        NavigationTargetCue? NavigationTargetCue = null)
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

    [GeneratedRegex(@"target\(\s*locationName\s*=\s*(?<locationName>[^,\)]+)\s*,\s*x\s*=\s*(?<x>-?\d+)\s*,\s*y\s*=\s*(?<y>-?\d+)\s*,\s*source\s*=\s*(?<source>[^,\)]+)\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex NavigationTargetPattern();

    private static readonly string[][] NavigationDestinationAliasGroups =
    [
        ["海边", "沙滩", "海岸", "码头", "潮池", "beach", "shore", "shoreline", "dock", "tidepool"],
        ["小镇", "广场", "喷泉", "town", "square", "fountain"],
        ["商店", "皮埃尔", "pierre", "store"],
        ["诊所", "clinic"],
        ["图书馆", "博物馆", "library", "museum"]
    ];
}
