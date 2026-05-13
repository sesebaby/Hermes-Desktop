namespace Hermes.Agent.Games.Stardew;

using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.LLM;
using Hermes.Agent.Runtime;
using Hermes.Agent.Skills;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

public sealed class StardewPrivateChatOrchestrator : IDisposable
{
    private readonly PrivateChatOrchestrator _inner;

    public StardewPrivateChatOrchestrator(
        IGameEventSource events,
        IGameCommandService commands,
        INpcPrivateChatAgentRunner agentRunner,
        StardewPrivateChatOptions? options = null,
        IPrivateChatSessionLeaseCoordinator? sessionLeaseCoordinator = null)
    {
        var stardewOptions = options ?? new StardewPrivateChatOptions();
        var conversationIdScope = NormalizeConversationIdScope(stardewOptions.ConversationIdScope);
        _inner = new PrivateChatOrchestrator(
            events,
            commands,
            new StardewPrivateChatAgentRunnerAdapter(agentRunner),
            new PrivateChatOrchestratorOptions(
                new PrivateChatPolicy(
                    NpcId: stardewOptions.NpcId ?? string.Empty,
                    SaveId: stardewOptions.SaveId,
                    GameId: "stardew-valley",
                    OpenPrompt: "Say something.",
                    OpenTriggerEventTypes:
                    [
                        "vanilla_dialogue_completed",
                        "vanilla_dialogue_unavailable"
                    ],
                    IsRetryableOpenFailure: IsRetryableOpenFailure,
                    BodyBinding: stardewOptions.BodyBinding,
                    BodyBindingResolver: stardewOptions.BodyBindingResolver,
                    GetConversationId: conversationIdScope is null
                        ? null
                        : record => ExtractScopedConversationId(record, conversationIdScope)),
                ToCoreReopenPolicy(stardewOptions.ReopenPolicy),
                stardewOptions.MaxTurnsPerSession,
                stardewOptions.MaxOpenAttempts,
                SessionLeaseCoordinator: sessionLeaseCoordinator,
                ReplyTimeout: stardewOptions.ReplyTimeout));
    }

    public StardewPrivateChatState State => ToStardewState(_inner.State);

    public string? ConversationId => _inner.ConversationId;

    internal Task<int> DrainExistingEventsAsync(CancellationToken ct)
        => _inner.DrainExistingEventsAsync(ct);

    public void DrainRecords(IReadOnlyList<GameEventRecord> records)
        => _inner.DrainRecords(records);

    public void Dispose() => _inner.Dispose();

    internal async Task<StardewPrivateChatStepResult> ProcessNextAsync(CancellationToken ct)
    {
        var result = await _inner.ProcessNextAsync(ct);
        return new StardewPrivateChatStepResult(
            ToStardewState(result.State),
            result.ConversationId,
            result.EventsProcessed);
    }

    public async Task<StardewPrivateChatStepResult> ProcessRecordsAsync(
        IReadOnlyList<GameEventRecord> records,
        CancellationToken ct)
    {
        var result = await _inner.ProcessRecordsAsync(records, ct);
        return new StardewPrivateChatStepResult(
            ToStardewState(result.State),
            result.ConversationId,
            result.EventsProcessed);
    }

    private static bool IsRetryableOpenFailure(GameCommandResult result)
        => result.Retryable ||
           string.Equals(result.FailureReason, StardewBridgeErrorCodes.MenuBlocked, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(result.FailureReason, StardewBridgeErrorCodes.WorldNotReady, StringComparison.OrdinalIgnoreCase);

    private static PrivateChatSessionReopenPolicy ToCoreReopenPolicy(PrivateChatReopenPolicy policy)
        => policy switch
        {
            PrivateChatReopenPolicy.Never => PrivateChatSessionReopenPolicy.Never,
            PrivateChatReopenPolicy.OnceAfterReply => PrivateChatSessionReopenPolicy.OnceAfterReply,
            PrivateChatReopenPolicy.UntilCancelled => PrivateChatSessionReopenPolicy.UntilCancelled,
            _ => PrivateChatSessionReopenPolicy.OnceAfterReply
        };

    private static string? NormalizeConversationIdScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
            return null;

        var builder = new StringBuilder(scope.Length);
        foreach (var c in scope.Trim())
            builder.Append(char.IsAsciiLetterOrDigit(c) ? c : '_');

        var normalized = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? ExtractScopedConversationId(GameEventRecord record, string scope)
    {
        var explicitConversationId = GetPayloadString(record, "conversationId");
        if (!string.IsNullOrWhiteSpace(explicitConversationId))
            return explicitConversationId.Trim();

        if (!string.IsNullOrWhiteSpace(record.CorrelationId))
        {
            var correlationId = record.CorrelationId.Trim();
            return correlationId.StartsWith("pc_", StringComparison.OrdinalIgnoreCase)
                ? correlationId
                : $"{scope}_{correlationId}";
        }

        return string.IsNullOrWhiteSpace(record.EventId)
            ? null
            : $"{scope}_{record.EventId.Trim()}";
    }

    private static string? GetPayloadString(GameEventRecord record, string propertyName)
        => record.Payload is not null && record.Payload.TryGetPropertyValue(propertyName, out var node)
            ? node?.GetValue<string>()
            : null;

    private static StardewPrivateChatState ToStardewState(PrivateChatState state)
        => state switch
        {
            PrivateChatState.Idle => StardewPrivateChatState.Idle,
            PrivateChatState.PendingOpen => StardewPrivateChatState.PendingOpen,
            PrivateChatState.AwaitingPlayerInput => StardewPrivateChatState.AwaitingPlayerInput,
            PrivateChatState.WaitingAgentReply => StardewPrivateChatState.WaitingAgentReply,
            PrivateChatState.ShowingReply => StardewPrivateChatState.ShowingReply,
            PrivateChatState.WaitingReplyDismissal => StardewPrivateChatState.WaitingReplyDismissal,
            _ => StardewPrivateChatState.Idle
        };

    private sealed class StardewPrivateChatAgentRunnerAdapter : IPrivateChatAgentRunner
    {
        private readonly INpcPrivateChatAgentRunner _inner;

        public StardewPrivateChatAgentRunnerAdapter(INpcPrivateChatAgentRunner inner)
        {
            _inner = inner;
        }

        public async Task<PrivateChatAgentReply> ReplyAsync(PrivateChatAgentRequest request, CancellationToken ct)
        {
            var reply = await _inner.ReplyAsync(
                new NpcPrivateChatRequest(request.NpcId, request.SaveId, request.ConversationId, request.PlayerText),
                ct);
            return new PrivateChatAgentReply(reply.Text);
        }
    }
}

public interface INpcPrivateChatAgentRunner
{
    Task<NpcPrivateChatReply> ReplyAsync(NpcPrivateChatRequest request, CancellationToken ct);
}

public sealed class StardewNpcPrivateChatAgentRunner : INpcPrivateChatAgentRunner
{
    private readonly IChatClient _chatClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _runtimeRoot;
    private readonly NpcRuntimeSupervisor _runtimeSupervisor;
    private readonly SkillManager _skillManager;
    private readonly ICronScheduler _cronScheduler;
    private readonly StardewNpcRuntimeBindingResolver _bindingResolver;
    private readonly INpcToolSurfaceSnapshotProvider _toolSnapshotProvider;
    private readonly bool _includeMemory;
    private readonly bool _includeUser;
    private readonly int _maxToolIterations;
    private readonly IChatClient? _delegationChatClient;

    public StardewNpcPrivateChatAgentRunner(
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        string runtimeRoot,
        NpcRuntimeSupervisor runtimeSupervisor,
        SkillManager skillManager,
        ICronScheduler cronScheduler,
        StardewNpcRuntimeBindingResolver bindingResolver,
        INpcToolSurfaceSnapshotProvider toolSnapshotProvider,
        bool includeMemory = true,
        bool includeUser = true,
        int maxToolIterations = 25,
        IChatClient? delegationChatClient = null)
    {
        _chatClient = chatClient;
        _loggerFactory = loggerFactory;
        _runtimeRoot = runtimeRoot;
        _runtimeSupervisor = runtimeSupervisor;
        _skillManager = skillManager;
        _cronScheduler = cronScheduler;
        _bindingResolver = bindingResolver;
        _toolSnapshotProvider = toolSnapshotProvider;
        _includeMemory = includeMemory;
        _includeUser = includeUser;
        _maxToolIterations = Math.Max(2, maxToolIterations);
        _delegationChatClient = delegationChatClient;
    }

    public StardewNpcPrivateChatAgentRunner(
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        string runtimeRoot,
        NpcRuntimeSupervisor runtimeSupervisor,
        SkillManager skillManager,
        ICronScheduler cronScheduler,
        StardewNpcRuntimeBindingResolver bindingResolver,
        bool includeMemory = true,
        bool includeUser = true,
        Func<IEnumerable<ITool>>? discoveredToolProvider = null,
        int maxToolIterations = 25,
        IChatClient? delegationChatClient = null)
        : this(
            chatClient,
            loggerFactory,
            runtimeRoot,
            runtimeSupervisor,
            skillManager,
            cronScheduler,
            bindingResolver,
            new NpcToolSurfaceSnapshotProvider(discoveredToolProvider ?? (() => Enumerable.Empty<ITool>())),
            includeMemory,
            includeUser,
            maxToolIterations,
            delegationChatClient)
    {
    }

    public async Task<NpcPrivateChatReply> ReplyAsync(NpcPrivateChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NpcId))
            throw new ArgumentException("NPC id is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.SaveId))
            throw new ArgumentException("Save id is required.", nameof(request));

        var logger = _loggerFactory.CreateLogger<StardewNpcPrivateChatAgentRunner>();
        var stopwatch = Stopwatch.StartNew();
        var saveId = request.SaveId.Trim();
        try
        {
            var binding = _bindingResolver.Resolve(request.NpcId, saveId);
            var descriptor = binding.Descriptor;
            var runtimeDriver = await _runtimeSupervisor.GetOrCreateDriverAsync(descriptor, _runtimeRoot, ct);
            var toolSnapshot = _toolSnapshotProvider.Capture();
            var privateChatToolSurface = NpcToolSurface.FromTools(
            [
                new NpcNoWorldActionTool(_loggerFactory.CreateLogger<NpcNoWorldActionTool>()),
                ..toolSnapshot.ToolSurface.Tools,
                new StardewSubmitHostTaskTool(
                    descriptor,
                    runtimeDriver,
                    _loggerFactory.CreateLogger<StardewSubmitHostTaskTool>())
            ]);
            var sessionId = $"{descriptor.SessionId}:private_chat:{request.ConversationId}";
            logger.LogInformation(
                "Stardew private-chat parent agent started; npc={NpcId}; saveId={SaveId}; conversationId={ConversationId}; sessionId={SessionId}; playerTextChars={PlayerTextChars}; toolCount={ToolCount}; toolSurfaceVersion={ToolSurfaceVersion}; maxToolIterations={MaxToolIterations}",
                descriptor.NpcId,
                saveId,
                request.ConversationId,
                sessionId,
                request.PlayerText.Length,
                privateChatToolSurface.Tools.Count,
                toolSnapshot.SnapshotVersion,
                _maxToolIterations);
            var handle = await _runtimeSupervisor.GetOrCreatePrivateChatHandleAsync(
                descriptor,
                binding.Pack,
                _runtimeRoot,
                new NpcRuntimeAgentBindingRequest(
                    ChannelKey: "private_chat",
                    SystemPromptSupplement: BuildPrivateChatSystemPrompt(descriptor.DisplayName),
                    IncludeMemory: _includeMemory,
                    IncludeUser: _includeUser,
                    MaxToolIterations: _maxToolIterations,
                    Services: new NpcRuntimeCompositionServices(
                        _chatClient,
                        _loggerFactory,
                        _skillManager,
                        _cronScheduler,
                        _delegationChatClient),
                    ToolSurface: privateChatToolSurface,
                    ToolSurfaceSnapshotVersion: toolSnapshot.SnapshotVersion),
                ct);

            var chatSession = new Session
            {
                Id = sessionId,
                ToolSessionId = descriptor.SessionId,
                Platform = descriptor.AdapterId
            };
            var response = await handle.Agent.ChatAsync(
                BuildPrivateChatMessage(descriptor.DisplayName, request.PlayerText),
                chatSession,
                ct);
            if (ShouldRunDelegationSelfCheck(response, chatSession))
            {
                logger.LogInformation(
                    "Stardew private-chat completed without host task submission; running parent self-check once; npc={NpcId}; saveId={SaveId}; conversationId={ConversationId}; sessionId={SessionId}",
                    descriptor.NpcId,
                    saveId,
                    request.ConversationId,
                    sessionId);
                response = await handle.Agent.ChatAsync(
                    BuildPrivateChatDelegationSelfCheckMessage(request.PlayerText, response),
                    chatSession,
                    ct);
            }
            if (ShouldRunCommitmentTodoSelfCheck(chatSession))
            {
                logger.LogInformation(
                    "Stardew private-chat host task submission missing commitment todo; running parent self-check once; npc={NpcId}; saveId={SaveId}; conversationId={ConversationId}; sessionId={SessionId}",
                    descriptor.NpcId,
                    saveId,
                    request.ConversationId,
                    sessionId);
                response = await handle.Agent.ChatAsync(
                    BuildPrivateChatCommitmentTodoSelfCheckMessage(request.PlayerText, response),
                    chatSession,
                    ct);
            }

            if (ShouldRunReplySelfCheck(response, chatSession))
            {
                logger.LogInformation(
                    "Stardew private-chat host task submission missing player-visible reply; running parent self-check once; npc={NpcId}; saveId={SaveId}; conversationId={ConversationId}; sessionId={SessionId}",
                    descriptor.NpcId,
                    saveId,
                    request.ConversationId,
                    sessionId);
                response = await handle.Agent.ChatAsync(
                    BuildPrivateChatReplySelfCheckMessage(request.PlayerText, response),
                    chatSession,
                    ct);
            }

            var finalReply = response.Trim();
            if (SessionHasSuccessfulToolCall(chatSession, "stardew_submit_host_task") &&
                string.IsNullOrWhiteSpace(finalReply))
            {
                logger.LogWarning(
                    "Stardew private-chat host task submission still missing player-visible reply after bounded self-check; blocking queued ingress; npc={NpcId}; saveId={SaveId}; conversationId={ConversationId}; sessionId={SessionId}",
                    descriptor.NpcId,
                    saveId,
                    request.ConversationId,
                    sessionId);
                await BlockQueuedPrivateChatHostTaskSubmissionAsync(
                    descriptor,
                    runtimeDriver,
                    request.ConversationId,
                    ct);
            }

            stopwatch.Stop();
            logger.LogInformation(
                "Stardew private-chat parent agent completed; npc={NpcId}; saveId={SaveId}; conversationId={ConversationId}; sessionId={SessionId}; responseChars={ResponseChars}; durationMs={DurationMs}",
                descriptor.NpcId,
                saveId,
                request.ConversationId,
                sessionId,
                finalReply.Length,
                stopwatch.ElapsedMilliseconds);

            return new NpcPrivateChatReply(finalReply);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            logger.LogWarning(
                ex,
                "Stardew private-chat parent agent timed out; npc={NpcId}; saveId={SaveId}; conversationId={ConversationId}; durationMs={DurationMs}",
                request.NpcId,
                saveId,
                request.ConversationId,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogWarning(
                ex,
                "Stardew private-chat parent agent failed; npc={NpcId}; saveId={SaveId}; conversationId={ConversationId}; durationMs={DurationMs}",
                request.NpcId,
                saveId,
                request.ConversationId,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private static string BuildPrivateChatMessage(string displayName, string playerText)
        =>
            $"{displayName} 的私聊。\n" +
            "玩家找你说话时，先认真回应玩家；如果原本有没做完的事，回应后再接着处理。\n\n" +
            $"Player: {playerText}";

    private static string BuildPrivateChatSystemPrompt(string displayName)
        =>
            "如果玩家现在就请你做一件会改变游戏世界的事，而你决定答应，必须先按承诺类型写入 todo，再调用 stardew_submit_host_task，最后自然回复玩家。只口头答应不会让动作发生。\n" +
            "如果你决定这轮没有当前就要发生的游戏世界动作，必须先调用 npc_no_world_action，再自然回复玩家。\n" +
            "如果你接受了当前就要发生的玩家承诺，先用 todo 把承诺记成 in_progress 短句，再调用 stardew_submit_host_task；动作完成后下一轮再由你自己收口 todo。\n" +
            "action=move 时，你必须先用 skill_view 读取 stardew-navigation、references/index.md、相关 region 和最具体的 POI 文件；只有已加载 POI/reference 明确给出 target(locationName,x,y,source) 后，才调用 stardew_submit_host_task。\n" +
            "stardew_submit_host_task 是延迟执行入口：私聊回复关闭后，宿主会按你传入的 mechanical target 进入 host task submission lifecycle 执行真实移动，并由 Stardew bridge 返回结果。不要使用 destinationId，不要编造坐标，不要把自然语言地点直接当 locationName。\n" +
            "玩家提出当前私聊中就要发生的移动请求时，如果你接受，action 填 move，target 必须原样填写已加载 POI/reference 的 locationName、x、y、source；可选 facingDirection。\n" +
            $"你是星露谷里的 {displayName}，现在正在和玩家私聊。\n" +
            "玩家找你说话时，你先像角色本人一样自然回应，不要装成助手。\n" +
            "如果玩家给了以后要兑现的约定、邀请、请求或共同计划，你自己判断要不要接；接了就用 todo 记到长期任务里。\n" +
            "如果玩家告诉你稳定事实、偏好、关系变化或重要地点，用 memory 记住。\n" +
            "如果你需要想起以前答应过什么，先用 session_search 查旧对话和旧约定。\n" +
            "你可以先用工具处理任务和记忆，再给玩家一句简短自然的回复。\n" +
            "如果任务做不了或被卡住，要把 todo 标成 blocked 或 failed，并写清短 reason；能告诉玩家时，要直接告诉玩家卡在哪里。\n" +
            "最终回复会显示在玩家手机私聊里，必须直接对玩家说话；不要写内心独白、旁白、动作描写或只给自己看的想法。\n" +
            "不要把工具过程讲给玩家听，不要输出标签、markdown 或系统说明。";

    private static string BuildPrivateChatDelegationSelfCheckMessage(string playerText, string previousReply)
        =>
            "自检：上一轮私聊回复没有调用 stardew_submit_host_task。\n" +
            "请你自己判断：如果上一轮已经接受了玩家当前就要发生、且会改变游戏世界的行动请求，不能只口头答应；现在必须先用 todo 写入一条 in_progress 承诺短任务，再用 skill_view 读取 stardew-navigation 的分层地图资料，拿到 target(locationName,x,y,source) 后调用 stardew_submit_host_task，action 填 move，target 原样填写已加载 POI/reference 的机械目标；工具调用后再给玩家一句自然回复。\n" +
            "如果上一轮没有接受当前就要发生的游戏世界行动请求，不要调用 stardew_submit_host_task；必须先调用 npc_no_world_action，再直接原样回复上一轮给玩家的话。\n" +
            "不要使用 destinationId，不要编造坐标，不要只说“我现在过去”。\n\n" +
            $"Player: {playerText}\n" +
            $"Previous reply: {previousReply}";

    private static string BuildPrivateChatCommitmentTodoSelfCheckMessage(string playerText, string previousReply)
        =>
            "自检：上一轮私聊已经调用 stardew_submit_host_task 提交当前世界动作，但缺少 todo 承诺记录。\n" +
            "如果上一轮确实接受了玩家当前就要发生的承诺，现在必须只用 todo 写入或更新一条 in_progress 短任务，让后续 autonomy 能在动作 terminal 后收口。\n" +
            "不要重复调用 stardew_submit_host_task，不要重新解析地点或坐标，不要把工具过程讲给玩家听；todo 后直接原样回复上一轮给玩家的话。\n" +
            "如果上一轮并没有形成承诺，不要写 todo，直接调用 npc_no_world_action 并原样回复。\n\n" +
            $"Player: {playerText}\n" +
            $"Previous reply: {previousReply}";

    private static string BuildPrivateChatReplySelfCheckMessage(string playerText, string previousReply)
        =>
            "自检：上一轮私聊已经成功调用 stardew_submit_host_task 提交当前世界动作，但缺少玩家可见回复。\n" +
            "现在不要重复调用 stardew_submit_host_task，不要重写 todo，不要解释工具过程；只补一条会展示给玩家的自然回复，直接对玩家说话。\n" +
            "如果上一轮回复是空白、只剩格式噪音或不适合展示，就直接给出这次最终要显示给玩家的一句简短自然回复。\n\n" +
            $"Player: {playerText}\n" +
            $"Previous reply: {previousReply}";

    private static bool ShouldRunDelegationSelfCheck(string response, Session session)
        => !SessionHasSuccessfulToolCall(session, "stardew_submit_host_task") &&
           !SessionHasSuccessfulToolCall(session, "npc_no_world_action");

    private static bool ShouldRunCommitmentTodoSelfCheck(Session session)
        => SessionHasSuccessfulToolCall(session, "stardew_submit_host_task") &&
           !SessionHasToolCall(session, "todo") &&
           !SessionHasToolCall(session, "todo_write");

    private static bool ShouldRunReplySelfCheck(string response, Session session)
        => SessionHasSuccessfulToolCall(session, "stardew_submit_host_task") &&
           string.IsNullOrWhiteSpace(response);

    private static bool SessionHasToolCall(Session session, string toolName)
        => session.Messages.Any(message =>
            message.ToolCalls?.Any(call => string.Equals(call.Name, toolName, StringComparison.OrdinalIgnoreCase)) ?? false);

    private static bool SessionHasSuccessfulToolCall(Session session, string toolName)
    {
        var pendingToolCallIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var message in session.Messages)
        {
            if (string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase) &&
                message.ToolCalls is not null)
            {
                foreach (var call in message.ToolCalls.Where(call => string.Equals(call.Name, toolName, StringComparison.OrdinalIgnoreCase)))
                {
                    pendingToolCallIds.Add(call.Id);
                }
            }
            else if (string.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(message.ToolName, toolName, StringComparison.OrdinalIgnoreCase) &&
                     !string.IsNullOrWhiteSpace(message.ToolCallId) &&
                     pendingToolCallIds.Contains(message.ToolCallId) &&
                     ToolResultSucceeded(message.Content))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ToolResultSucceeded(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            return (document.RootElement.TryGetProperty("success", out var success) &&
                    success.ValueKind == JsonValueKind.True) ||
                   (document.RootElement.TryGetProperty("queued", out var queued) &&
                    queued.ValueKind == JsonValueKind.True) ||
                   (document.RootElement.TryGetProperty("noWorldAction", out var noWorldAction) &&
                    noWorldAction.ValueKind == JsonValueKind.True);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static async Task BlockQueuedPrivateChatHostTaskSubmissionAsync(
        NpcRuntimeDescriptor descriptor,
        NpcRuntimeDriver runtimeDriver,
        string conversationId,
        CancellationToken ct)
    {
        var snapshot = runtimeDriver.Snapshot();
        JsonObject? queuedPayload = null;
        var targetWorkItem = snapshot.IngressWorkItems.LastOrDefault(item =>
            string.Equals(item.WorkType, "stardew_host_task_submission", StringComparison.OrdinalIgnoreCase) &&
            item.Payload is JsonObject payload &&
            string.Equals(
                ReadPayloadString(payload, "conversationId"),
                conversationId,
                StringComparison.OrdinalIgnoreCase) &&
            (queuedPayload = payload) is not null);
        if (targetWorkItem is null)
            return;

        const string errorCode = "private_chat_reply_missing";
        await runtimeDriver.SetLastTerminalCommandStatusAsync(
            new GameCommandStatus(
                targetWorkItem.WorkItemId,
                descriptor.NpcId,
                ReadPayloadString(queuedPayload!, "action") ?? targetWorkItem.WorkType,
                StardewCommandStatuses.Blocked,
                1,
                errorCode,
                errorCode,
                UpdatedAtUtc: DateTime.UtcNow),
            ct);
        await runtimeDriver.RemoveIngressWorkItemAsync(targetWorkItem.WorkItemId, ct);
        await runtimeDriver.SetNextWakeAtUtcAsync(DateTime.UtcNow, ct);
        await new NpcRuntimeLogWriter(Path.Combine(runtimeDriver.Instance.Namespace.ActivityPath, "runtime.jsonl")).WriteAsync(
            new NpcRuntimeLogRecord(
                DateTime.UtcNow,
                string.IsNullOrWhiteSpace(targetWorkItem.TraceId) ? targetWorkItem.WorkItemId : targetWorkItem.TraceId!,
                descriptor.NpcId,
                descriptor.GameId,
                descriptor.SessionId,
                "ingress",
                targetWorkItem.WorkType,
                "blocked",
                errorCode,
                CommandId: targetWorkItem.WorkItemId,
                Error: errorCode),
            ct);
    }

    private static string? ReadPayloadString(JsonObject payload, string propertyName)
        => payload.TryGetPropertyValue(propertyName, out var value) && value is JsonValue jsonValue &&
           jsonValue.TryGetValue<string>(out var text)
            ? text
            : null;

}

public sealed class StardewPrivateChatRuntimeAdapter : IDisposable
{
    private readonly object _gate = new();
    private readonly INpcPrivateChatAgentRunner _agentRunner;
    private readonly StardewPrivateChatOptions _options;
    private readonly ILogger<StardewPrivateChatRuntimeAdapter> _logger;
    private readonly IPrivateChatSessionLeaseCoordinator? _sessionLeaseCoordinator;
    private readonly StardewNpcRuntimeBindingResolver? _bindingResolver;
    private readonly string? _runtimeRoot;
    private readonly NpcRuntimeSupervisor? _runtimeSupervisor;
    private string? _bridgeKey;
    private StardewPrivateChatOrchestrator? _orchestrator;

    public StardewPrivateChatRuntimeAdapter(
        INpcPrivateChatAgentRunner agentRunner,
        ILogger<StardewPrivateChatRuntimeAdapter> logger,
        StardewPrivateChatOptions? options = null,
        IPrivateChatSessionLeaseCoordinator? sessionLeaseCoordinator = null,
        StardewNpcRuntimeBindingResolver? bindingResolver = null,
        string? runtimeRoot = null,
        NpcRuntimeSupervisor? runtimeSupervisor = null)
    {
        _agentRunner = agentRunner;
        _logger = logger;
        _options = options ?? new StardewPrivateChatOptions();
        _sessionLeaseCoordinator = sessionLeaseCoordinator;
        _bindingResolver = bindingResolver;
        _runtimeRoot = runtimeRoot;
        _runtimeSupervisor = runtimeSupervisor;
    }

    public async Task ProcessAsync(
        string bridgeKey,
        string saveId,
        IGameAdapter adapter,
        IReadOnlyList<GameEventRecord> records,
        CancellationToken ct,
        bool drainOnly = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bridgeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(saveId);
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(records);

        EnsureBridgeAttachment(bridgeKey, saveId, adapter, ct);

        StardewPrivateChatOrchestrator? orchestrator;
        lock (_gate)
        {
            orchestrator = _orchestrator;
        }

        if (orchestrator is null)
            return;

        if (drainOnly)
        {
            orchestrator.DrainRecords(records);
            return;
        }

        await orchestrator.ProcessRecordsAsync(records, ct);
    }

    public void Reset()
    {
        lock (_gate)
        {
            _bridgeKey = null;
            DisposeOrchestratorNoThrow();
        }
    }

    public void Dispose() => Reset();

    private void EnsureBridgeAttachment(
        string bridgeKey,
        string saveId,
        IGameAdapter adapter,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (string.Equals(_bridgeKey, bridgeKey, StringComparison.Ordinal))
                return;

            DisposeOrchestratorNoThrow();
            _orchestrator = new StardewPrivateChatOrchestrator(
                adapter.Events,
                CreateCommandService(adapter.Commands, saveId),
                _agentRunner,
                BuildOptions(saveId, bridgeKey),
                _sessionLeaseCoordinator);
            _bridgeKey = bridgeKey;
        }

        _logger.LogInformation("Stardew private-chat runtime bridge attached: {BridgeKey}", bridgeKey);
    }

    private IGameCommandService CreateCommandService(IGameCommandService commands, string saveId)
    {
        if (_bindingResolver is null ||
            _runtimeSupervisor is null ||
            string.IsNullOrWhiteSpace(_runtimeRoot))
        {
            return commands;
        }

        return new StardewPrivateChatLifecycleCommandService(
            commands,
            _bindingResolver,
            _runtimeSupervisor,
            _runtimeRoot,
            saveId);
    }

    private StardewPrivateChatOptions BuildOptions(string saveId, string bridgeKey)
    {
        var options = _options with
        {
            SaveId = saveId,
            ConversationIdScope = string.IsNullOrWhiteSpace(_options.ConversationIdScope)
                ? BuildBridgeConversationIdScope(bridgeKey)
                : _options.ConversationIdScope
        };
        if (options.BodyBinding is not null || options.BodyBindingResolver is not null || _bindingResolver is null)
            return options;

        return options with
        {
            BodyBindingResolver = npcId =>
            {
                try
                {
                    return _bindingResolver.Resolve(npcId, saveId).Descriptor.EffectiveBodyBinding;
                }
                catch (InvalidOperationException ex) when (ex.Message.StartsWith("Could not resolve Stardew NPC pack for ", StringComparison.Ordinal))
                {
                    throw new PrivateChatUnsupportedNpcException(npcId);
                }
            }
        };
    }

    private static string BuildBridgeConversationIdScope(string bridgeKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(bridgeKey.Trim()));
        return "bridge_" + Convert.ToHexString(bytes.AsSpan(0, 8)).ToLowerInvariant();
    }

    private void DisposeOrchestratorNoThrow()
    {
        if (_orchestrator is null)
            return;

        try
        {
            _orchestrator.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Disposing Stardew private-chat orchestrator failed non-fatally");
        }

        _orchestrator = null;
    }

    private sealed class StardewPrivateChatLifecycleCommandService : IGameCommandService
    {
        private readonly IGameCommandService _inner;
        private readonly StardewNpcRuntimeBindingResolver _bindingResolver;
        private readonly NpcRuntimeSupervisor _runtimeSupervisor;
        private readonly string _runtimeRoot;
        private readonly string _saveId;

        public StardewPrivateChatLifecycleCommandService(
            IGameCommandService inner,
            StardewNpcRuntimeBindingResolver bindingResolver,
            NpcRuntimeSupervisor runtimeSupervisor,
            string runtimeRoot,
            string saveId)
        {
            _inner = inner;
            _bindingResolver = bindingResolver;
            _runtimeSupervisor = runtimeSupervisor;
            _runtimeRoot = runtimeRoot;
            _saveId = saveId;
        }

        public async Task<GameCommandResult> SubmitAsync(GameAction action, CancellationToken ct)
        {
            if (action.Type is not GameActionType.OpenPrivateChat and not GameActionType.Speak)
                return await _inner.SubmitAsync(action, ct);

            var runtimeActions = await CreateRuntimeActionsAsync(action, ct);
            var preparedAction = await runtimeActions.TryBeginAsync(action, ct);
            if (preparedAction?.BlockedResult is not null)
                return preparedAction.BlockedResult;

            var result = await _inner.SubmitAsync(action, ct);
            await runtimeActions.RecordSubmitResultAsync(preparedAction, result, ct);
            return result;
        }

        public Task<GameCommandStatus> GetStatusAsync(string commandId, CancellationToken ct)
            => _inner.GetStatusAsync(commandId, ct);

        public Task<GameCommandStatus?> TryGetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
            => _inner.TryGetByIdempotencyKeyAsync(idempotencyKey, ct);

        public Task<GameCommandStatus> CancelAsync(string commandId, string reason, CancellationToken ct)
            => _inner.CancelAsync(commandId, reason, ct);

        private async Task<StardewRuntimeActionController> CreateRuntimeActionsAsync(GameAction action, CancellationToken ct)
        {
            var binding = _bindingResolver.Resolve(action.NpcId, _saveId);
            var driver = await _runtimeSupervisor.GetOrCreateDriverAsync(binding.Descriptor, _runtimeRoot, ct);
            driver.Instance.Namespace.SeedPersonaPack(binding.Pack);
            return new StardewRuntimeActionController(driver, null, null, null);
        }
    }
}

public sealed record NpcPrivateChatRequest(
    string NpcId,
    string SaveId,
    string ConversationId,
    string PlayerText);

public sealed record NpcPrivateChatReply(string Text);

public sealed record StardewPrivateChatOptions(
    string? NpcId = null,
    string SaveId = "",
    PrivateChatReopenPolicy ReopenPolicy = PrivateChatReopenPolicy.OnceAfterReply,
    int MaxTurnsPerSession = 3,
    int MaxOpenAttempts = 60,
    TimeSpan PollInterval = default,
    NpcBodyBinding? BodyBinding = null,
    Func<string, NpcBodyBinding>? BodyBindingResolver = null,
    TimeSpan ReplyTimeout = default,
    string? ConversationIdScope = null);

public enum PrivateChatReopenPolicy
{
    Never,
    OnceAfterReply,
    UntilCancelled
}

public enum StardewPrivateChatState
{
    Idle,
    PendingOpen,
    AwaitingPlayerInput,
    WaitingAgentReply,
    ShowingReply,
    WaitingReplyDismissal
}

public sealed record StardewPrivateChatStepResult(
    StardewPrivateChatState State,
    string? ConversationId,
    int EventsProcessed);
