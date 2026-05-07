namespace Hermes.Agent.Runtime;

using System.Text.Json;
using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Hermes.Agent.Search;
using Hermes.Agent.Tasks;
using Hermes.Agent.Transcript;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class NpcDeveloperInspectorService
{
    private const int DefaultPreviewCharacterLimit = 12000;
    private const int DefaultRuntimeLogMaxLines = 1000;

    private readonly NpcDeveloperInspectorOptions _options;
    private readonly Func<NpcNamespace, TranscriptStore> _transcriptFactory;
    private readonly Func<NpcNamespace, SessionTodoStore?> _todoStoreFactory;

    public NpcDeveloperInspectorService(
        NpcDeveloperInspectorOptions? options = null,
        Func<NpcNamespace, TranscriptStore>? transcriptFactory = null,
        Func<NpcNamespace, SessionTodoStore?>? todoStoreFactory = null)
    {
        _options = options ?? new NpcDeveloperInspectorOptions();
        _transcriptFactory = transcriptFactory ?? CreateTranscriptStore;
        _todoStoreFactory = todoStoreFactory ?? (_ => null);
    }

    public async Task<NpcDeveloperInspectorView> InspectAsync(
        NpcRuntimeSnapshot snapshot,
        string runtimeRoot,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeRoot);

        var npcNamespace = new NpcNamespace(
            runtimeRoot,
            snapshot.GameId,
            snapshot.SaveId,
            snapshot.NpcId,
            snapshot.ProfileId);
        var documents = await LoadDocumentsAsync(npcNamespace, ct);
        var tracePath = Path.Combine(npcNamespace.ActivityPath, "runtime.jsonl");
        var trace = await LoadTraceAsync(tracePath, snapshot.NpcId, snapshot.LastTraceId, ct);
        var transcript = await LoadTranscriptAsync(npcNamespace, snapshot.SessionId, ct);
        var todos = LoadTodos(npcNamespace, snapshot.SessionId);

        return new NpcDeveloperInspectorView(
            snapshot.NpcId,
            snapshot.DisplayName,
            snapshot.GameId,
            snapshot.SaveId,
            snapshot.ProfileId,
            snapshot.SessionId,
            snapshot.State.ToString(),
            snapshot.LastTraceId ?? "",
            snapshot.LastError ?? "",
            ChatRouteNames.StardewAutonomy,
            ChatRouteNames.Delegation,
            npcNamespace.RuntimeRoot,
            tracePath,
            documents,
            trace.Events,
            trace.Diagnostics,
            trace.EmptyState,
            transcript.ModelReplies,
            transcript.ToolCalls,
            transcript.Delegations,
            transcript.ModelReplyEmptyState,
            transcript.ToolCallEmptyState,
            transcript.DelegationEmptyState,
            todos.Todos,
            todos.EmptyState);
    }

    private async Task<IReadOnlyList<NpcDeveloperDocument>> LoadDocumentsAsync(NpcNamespace npcNamespace, CancellationToken ct)
    {
        var paths = new[]
        {
            ("SOUL.md", npcNamespace.SoulFilePath),
            ("MEMORY.md", Path.Combine(npcNamespace.MemoryPath, "MEMORY.md")),
            ("USER.md", Path.Combine(npcNamespace.MemoryPath, "USER.md"))
        };
        var documents = new List<NpcDeveloperDocument>(paths.Length);
        foreach (var (name, path) in paths)
            documents.Add(await LoadDocumentAsync(name, path, ct));

        return documents;
    }

    private async Task<NpcDeveloperDocument> LoadDocumentAsync(string name, string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!File.Exists(path))
            return new NpcDeveloperDocument(name, path, false, "", "未找到文件。", false);

        try
        {
            var content = await File.ReadAllTextAsync(path, ct);
            var truncated = content.Length > PreviewCharacterLimit;
            if (truncated)
                content = content[..PreviewCharacterLimit];

            var status = truncated
                ? $"已截断，仅显示前 {PreviewCharacterLimit} 个字符。"
                : "已读取。";
            return new NpcDeveloperDocument(name, path, true, content, status, truncated);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new NpcDeveloperDocument(name, path, false, "", $"读取失败：{ex.Message}", false);
        }
    }

    private async Task<TraceProjection> LoadTraceAsync(
        string path,
        string npcId,
        string? preferredTraceId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!File.Exists(path))
            return new TraceProjection([], [], "未找到运行时活动日志。");

        var lines = await ReadTailLinesAsync(path, RuntimeLogMaxLines, ct);
        var records = new List<NpcRuntimeLogRecord>();
        var diagnostics = new List<string>();
        var lineNumberOffset = Math.Max(0, CountLines(path) - lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var record = JsonSerializer.Deserialize<NpcRuntimeLogRecord>(line, JsonOptions);
                if (record is not null &&
                    string.Equals(record.NpcId, npcId, StringComparison.OrdinalIgnoreCase))
                {
                    records.Add(record);
                }
            }
            catch (JsonException ex)
            {
                diagnostics.Add($"第 {lineNumberOffset + i + 1} 行无法解析：{ex.Message}");
            }
        }

        if (records.Count == 0)
            return new TraceProjection([], diagnostics, "当前 NPC 没有可显示的运行时事件。");

        var traceId = string.IsNullOrWhiteSpace(preferredTraceId)
            ? records.OrderByDescending(record => record.TimestampUtc).First().TraceId
            : preferredTraceId;
        var selected = records
            .Where(record => string.Equals(record.TraceId, traceId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(record => record.TimestampUtc)
            .Select(ToTraceEvent)
            .ToArray();

        if (selected.Length == 0)
            return new TraceProjection([], diagnostics, "未找到所选追踪。");

        return new TraceProjection(selected, diagnostics, "");
    }

    private async Task<TranscriptProjection> LoadTranscriptAsync(NpcNamespace npcNamespace, string sessionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return new TranscriptProjection(
                [],
                [],
                [],
                "当前 NPC 没有关联会话。",
                "当前记录未包含工具调用。",
                "本次追踪未发现委托。");
        }

        List<Message> messages;
        try
        {
            messages = await _transcriptFactory(npcNamespace).LoadSessionAsync(sessionId, ct);
        }
        catch (SessionNotFoundException)
        {
            return new TranscriptProjection(
                [],
                [],
                [],
                "当前会话还没有保存模型回复。",
                "当前记录未包含工具调用。",
                "本次追踪未发现委托。");
        }

        var modelReplies = messages
            .Where(message => string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            .Select(ToModelReply)
            .Where(reply => !string.IsNullOrWhiteSpace(reply.Content) || !string.IsNullOrWhiteSpace(reply.Reasoning))
            .ToArray();
        var toolResults = messages
            .Where(message => string.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase))
            .ToLookup(message => message.ToolCallId ?? "", StringComparer.OrdinalIgnoreCase);
        var toolCalls = messages
            .Where(message => string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            .SelectMany(message => message.ToolCalls ?? [])
            .Select(call => ToToolCall(call, toolResults[call.Id].LastOrDefault()))
            .ToArray();
        var delegations = toolCalls
            .Where(call => string.Equals(call.Name, "agent", StringComparison.OrdinalIgnoreCase))
            .Select(call => new NpcDeveloperDelegation(call.Id, call.Arguments, call.Result))
            .ToArray();

        return new TranscriptProjection(
            modelReplies,
            toolCalls,
            delegations,
            modelReplies.Length == 0 ? "当前会话还没有保存模型回复。" : "",
            toolCalls.Length == 0 ? "当前记录未包含工具调用。" : "",
            delegations.Length == 0 ? "本次追踪未发现委托。" : "");
    }

    private TodoProjection LoadTodos(NpcNamespace npcNamespace, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return new TodoProjection([], "当前会话没有待办事项。");

        var snapshot = _todoStoreFactory(npcNamespace)?.Read(sessionId) ?? SessionTodoSnapshot.Empty;
        var todos = snapshot.Todos
            .Select(todo => new NpcDeveloperTodo(todo.Id, todo.Content, todo.Status, todo.Reason ?? ""))
            .ToArray();
        return new TodoProjection(todos, todos.Length == 0 ? "当前会话没有待办事项。" : "");
    }

    private static NpcDeveloperTraceEvent ToTraceEvent(NpcRuntimeLogRecord record)
        => new(
            record.TimestampUtc,
            record.TraceId,
            MapTraceKind(record),
            record.ActionType,
            record.Target ?? "",
            record.Stage,
            record.Result,
            record.CommandId ?? "",
            record.Error ?? "",
            record.ExecutorMode ?? "",
            record.TargetSource ?? "");

    private static string MapTraceKind(NpcRuntimeLogRecord record)
    {
        if (string.Equals(record.ActionType, "observation", StringComparison.OrdinalIgnoreCase))
            return "观察事实";

        if (string.Equals(record.ActionType, "tick", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record.Stage, "started", StringComparison.OrdinalIgnoreCase))
        {
            return "模型请求";
        }

        if (string.Equals(record.ActionType, "tick", StringComparison.OrdinalIgnoreCase))
            return "模型回复";

        if (string.Equals(record.ActionType, "diagnostic", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record.Target, "intent_contract", StringComparison.OrdinalIgnoreCase))
        {
            return "意图解析";
        }

        if (string.Equals(record.ActionType, "diagnostic", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record.Target, "local_executor", StringComparison.OrdinalIgnoreCase))
        {
            return "本地执行";
        }

        if (string.Equals(record.ActionType, "local_executor", StringComparison.OrdinalIgnoreCase))
            return "工具调用";

        if (string.Equals(record.ActionType, "host_action", StringComparison.OrdinalIgnoreCase))
            return "游戏桥接";

        if (string.Equals(record.ActionType, "task_continuity", StringComparison.OrdinalIgnoreCase))
            return "执行结果";

        if (string.Equals(record.ActionType, "diagnostic", StringComparison.OrdinalIgnoreCase))
            return "诊断";

        return "原始事件";
    }

    private static NpcDeveloperModelReply ToModelReply(Message message)
        => new(
            message.Timestamp,
            message.Content,
            FirstNonEmpty(
                message.ReasoningContent,
                message.Reasoning,
                message.ReasoningDetails,
                message.CodexReasoningItems) ?? "当前记录未包含推理摘要。");

    private static NpcDeveloperToolCall ToToolCall(ToolCall call, Message? result)
        => new(
            call.Id,
            call.Name,
            call.Arguments,
            result?.Content ?? "当前记录未包含工具结果。",
            result?.Timestamp);

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static async Task<IReadOnlyList<string>> ReadTailLinesAsync(string path, int maxLines, CancellationToken ct)
    {
        var all = await File.ReadAllLinesAsync(path, ct);
        if (all.Length <= maxLines)
            return all;

        return all.Skip(all.Length - maxLines).ToArray();
    }

    private static int CountLines(string path)
    {
        try
        {
            return File.ReadLines(path).Count();
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private static TranscriptStore CreateTranscriptStore(NpcNamespace npcNamespace)
        => npcNamespace.CreateTranscriptStore(NullLogger<SessionSearchIndex>.Instance);

    private int PreviewCharacterLimit => Math.Max(1, _options.PreviewCharacterLimit ?? DefaultPreviewCharacterLimit);

    private int RuntimeLogMaxLines => Math.Max(1, _options.RuntimeLogMaxLines ?? DefaultRuntimeLogMaxLines);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record TraceProjection(
        IReadOnlyList<NpcDeveloperTraceEvent> Events,
        IReadOnlyList<string> Diagnostics,
        string EmptyState);

    private sealed record TranscriptProjection(
        IReadOnlyList<NpcDeveloperModelReply> ModelReplies,
        IReadOnlyList<NpcDeveloperToolCall> ToolCalls,
        IReadOnlyList<NpcDeveloperDelegation> Delegations,
        string ModelReplyEmptyState,
        string ToolCallEmptyState,
        string DelegationEmptyState);

    private sealed record TodoProjection(
        IReadOnlyList<NpcDeveloperTodo> Todos,
        string EmptyState);
}

public sealed record NpcDeveloperInspectorOptions
{
    public int? PreviewCharacterLimit { get; init; }

    public int? RuntimeLogMaxLines { get; init; }
}

public sealed record NpcDeveloperInspectorView(
    string NpcId,
    string DisplayName,
    string GameId,
    string SaveId,
    string ProfileId,
    string SessionId,
    string State,
    string LastTraceId,
    string LastError,
    string MainChannel,
    string DelegationChannel,
    string RuntimePath,
    string RuntimeLogPath,
    IReadOnlyList<NpcDeveloperDocument> Documents,
    IReadOnlyList<NpcDeveloperTraceEvent> TraceEvents,
    IReadOnlyList<string> TraceDiagnostics,
    string TraceEmptyState,
    IReadOnlyList<NpcDeveloperModelReply> ModelReplies,
    IReadOnlyList<NpcDeveloperToolCall> ToolCalls,
    IReadOnlyList<NpcDeveloperDelegation> Delegations,
    string ModelReplyEmptyState,
    string ToolCallEmptyState,
    string DelegationEmptyState,
    IReadOnlyList<NpcDeveloperTodo> Todos,
    string TodoEmptyState);

public sealed record NpcDeveloperDocument(
    string Name,
    string Path,
    bool Exists,
    string Content,
    string Status,
    bool IsTruncated);

public sealed record NpcDeveloperTraceEvent(
    DateTime TimestampUtc,
    string TraceId,
    string Kind,
    string ActionType,
    string Target,
    string Stage,
    string Result,
    string CommandId,
    string Error,
    string ExecutorMode,
    string TargetSource)
{
    public string TimestampText => TimestampUtc.ToLocalTime().ToString("T", System.Globalization.CultureInfo.CurrentCulture);
}

public sealed record NpcDeveloperModelReply(
    DateTime TimestampUtc,
    string Content,
    string Reasoning)
{
    public string TimestampText => TimestampUtc.ToLocalTime().ToString("T", System.Globalization.CultureInfo.CurrentCulture);
}

public sealed record NpcDeveloperToolCall(
    string Id,
    string Name,
    string Arguments,
    string Result,
    DateTime? ResultTimestampUtc)
{
    public string ResultTimestampText => ResultTimestampUtc.HasValue
        ? ResultTimestampUtc.Value.ToLocalTime().ToString("T", System.Globalization.CultureInfo.CurrentCulture)
        : "";
}

public sealed record NpcDeveloperDelegation(
    string ToolCallId,
    string Request,
    string Result);

public sealed record NpcDeveloperTodo(
    string Id,
    string Content,
    string Status,
    string Reason);
