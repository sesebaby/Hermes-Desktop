namespace Hermes.Agent.Runtime;

using System.Text.Json;
using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Hermes.Agent.Search;
using Hermes.Agent.Tasks;
using Hermes.Agent.Transcript;

public sealed class NpcDeveloperInspectorService
{
    private const int DefaultPreviewCharacterLimit = 12000;
    private const int DefaultRuntimeLogMaxLines = 1000;

    private readonly NpcDeveloperInspectorOptions _options;
    private readonly INpcDeveloperInspectorText _text;
    private readonly INpcDeveloperTranscriptReader _transcriptReader;
    private readonly INpcDeveloperTodoReader _todoReader;

    public NpcDeveloperInspectorService(
        NpcDeveloperInspectorOptions? options = null,
        INpcDeveloperInspectorText? text = null,
        INpcDeveloperTranscriptReader? transcriptReader = null,
        INpcDeveloperTodoReader? todoReader = null)
    {
        _options = options ?? new NpcDeveloperInspectorOptions();
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _transcriptReader = transcriptReader ?? new SqliteNpcDeveloperTranscriptReader();
        _todoReader = todoReader ?? EmptyNpcDeveloperTodoReader.Instance;
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
        var trace = await Task.Run(() => LoadTrace(tracePath, snapshot.NpcId, snapshot.LastTraceId, ct), ct);
        var transcript = await LoadTranscriptAsync(npcNamespace, snapshot.SessionId, ct);
        var todos = LoadTodos(snapshot.SessionId);

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
            return new NpcDeveloperDocument(name, path, false, "", _text.FileMissing, false);

        try
        {
            var content = await File.ReadAllTextAsync(path, ct);
            var truncated = content.Length > PreviewCharacterLimit;
            if (truncated)
                content = content[..PreviewCharacterLimit];

            var status = truncated
                ? string.Format(System.Globalization.CultureInfo.CurrentCulture, _text.FileTruncatedFormat, PreviewCharacterLimit)
                : _text.FileLoaded;
            return new NpcDeveloperDocument(name, path, true, content, status, truncated);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new NpcDeveloperDocument(
                name,
                path,
                false,
                "",
                string.Format(System.Globalization.CultureInfo.CurrentCulture, _text.FileReadFailedFormat, ex.Message),
                false);
        }
    }

    private TraceProjection LoadTrace(
        string path,
        string npcId,
        string? preferredTraceId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!File.Exists(path))
            return new TraceProjection([], [], _text.TraceLogMissing);

        var tail = ReadTailLines(path, RuntimeLogMaxLines, ct);
        var records = new List<NpcRuntimeLogRecord>();
        var diagnostics = new List<string>();
        var lineNumberOffset = tail.StartLineNumber - 1;
        for (var i = 0; i < tail.Lines.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var line = tail.Lines[i];
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
                diagnostics.Add(string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    _text.TraceParseFailedFormat,
                    lineNumberOffset + i + 1,
                    ex.Message));
            }
        }

        if (records.Count == 0)
            return new TraceProjection([], diagnostics, _text.TraceEmptyForNpc);

        var traceId = string.IsNullOrWhiteSpace(preferredTraceId)
            ? records.OrderByDescending(record => record.TimestampUtc).First().TraceId
            : preferredTraceId;
        var selected = records
            .Where(record => string.Equals(record.TraceId, traceId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(record => record.TimestampUtc)
            .Select(ToTraceEvent)
            .ToArray();

        if (selected.Length == 0)
            return new TraceProjection([], diagnostics, _text.TraceSelectionMissing);

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
                _text.TranscriptSessionMissing,
                _text.ToolCallEmpty,
                _text.DelegationEmpty);
        }

        List<Message> messages;
        try
        {
            messages = await _transcriptReader.LoadSessionAsync(npcNamespace, sessionId, ct);
        }
        catch (SessionNotFoundException)
        {
            return new TranscriptProjection(
                [],
                [],
                [],
                _text.ModelReplyEmpty,
                _text.ToolCallEmpty,
                _text.DelegationEmpty);
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
            modelReplies.Length == 0 ? _text.ModelReplyEmpty : "",
            toolCalls.Length == 0 ? _text.ToolCallEmpty : "",
            delegations.Length == 0 ? _text.DelegationEmpty : "");
    }

    private TodoProjection LoadTodos(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return new TodoProjection([], _text.TodoEmpty);

        var snapshot = _todoReader.Read(sessionId);
        var todos = snapshot.Todos
            .Select(todo => new NpcDeveloperTodo(todo.Id, todo.Content, todo.Status, todo.Reason ?? ""))
            .ToArray();
        return new TodoProjection(todos, todos.Length == 0 ? _text.TodoEmpty : "");
    }

    private NpcDeveloperTraceEvent ToTraceEvent(NpcRuntimeLogRecord record)
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

    private string MapTraceKind(NpcRuntimeLogRecord record)
    {
        if (string.Equals(record.ActionType, "observation", StringComparison.OrdinalIgnoreCase))
            return _text.TraceKindObservation;

        if (string.Equals(record.ActionType, "tick", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record.Stage, "started", StringComparison.OrdinalIgnoreCase))
        {
            return _text.TraceKindModelRequest;
        }

        if (string.Equals(record.ActionType, "tick", StringComparison.OrdinalIgnoreCase))
            return _text.TraceKindModelReply;

        if (string.Equals(record.ActionType, "diagnostic", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record.Target, "intent_contract", StringComparison.OrdinalIgnoreCase))
        {
            return _text.TraceKindIntent;
        }

        if (string.Equals(record.ActionType, "diagnostic", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record.Target, "local_executor", StringComparison.OrdinalIgnoreCase))
        {
            return _text.TraceKindLocalExecutor;
        }

        if (string.Equals(record.ActionType, "local_executor", StringComparison.OrdinalIgnoreCase))
            return _text.TraceKindToolCall;

        if (string.Equals(record.ActionType, "host_action", StringComparison.OrdinalIgnoreCase))
            return _text.TraceKindBridge;

        if (string.Equals(record.ActionType, "task_continuity", StringComparison.OrdinalIgnoreCase))
            return _text.TraceKindResult;

        if (string.Equals(record.ActionType, "diagnostic", StringComparison.OrdinalIgnoreCase))
            return _text.TraceKindDiagnostic;

        return _text.TraceKindRaw;
    }

    private NpcDeveloperModelReply ToModelReply(Message message)
        => new(
            message.Timestamp,
            message.Content,
            FirstNonEmpty(
                message.ReasoningContent,
                message.Reasoning,
                message.ReasoningDetails,
                message.CodexReasoningItems) ?? _text.ReasoningMissing);

    private NpcDeveloperToolCall ToToolCall(ToolCall call, Message? result)
        => new(
            call.Id,
            call.Name,
            call.Arguments,
            result?.Content ?? _text.ToolResultMissing,
            result?.Timestamp);

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static TailLines ReadTailLines(string path, int maxLines, CancellationToken ct)
    {
        var lines = new Queue<string>(maxLines);
        var total = 0;
        foreach (var line in File.ReadLines(path))
        {
            ct.ThrowIfCancellationRequested();
            total++;
            if (lines.Count == maxLines)
                lines.Dequeue();

            lines.Enqueue(line);
        }

        return new TailLines(lines.ToArray(), Math.Max(1, total - lines.Count + 1));
    }

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

    private sealed record TailLines(
        IReadOnlyList<string> Lines,
        int StartLineNumber);
}

public sealed record NpcDeveloperInspectorOptions
{
    public int? PreviewCharacterLimit { get; init; }

    public int? RuntimeLogMaxLines { get; init; }
}

public interface INpcDeveloperTranscriptReader
{
    Task<List<Message>> LoadSessionAsync(NpcNamespace npcNamespace, string sessionId, CancellationToken ct);
}

public interface INpcDeveloperTodoReader
{
    SessionTodoSnapshot Read(string sessionId);
}

public interface INpcDeveloperInspectorText
{
    string FileMissing { get; }

    string FileLoaded { get; }

    string FileTruncatedFormat { get; }

    string FileReadFailedFormat { get; }

    string TraceLogMissing { get; }

    string TraceParseFailedFormat { get; }

    string TraceEmptyForNpc { get; }

    string TraceSelectionMissing { get; }

    string TranscriptSessionMissing { get; }

    string ModelReplyEmpty { get; }

    string ToolCallEmpty { get; }

    string DelegationEmpty { get; }

    string TodoEmpty { get; }

    string ReasoningMissing { get; }

    string ToolResultMissing { get; }

    string TraceKindObservation { get; }

    string TraceKindModelRequest { get; }

    string TraceKindModelReply { get; }

    string TraceKindIntent { get; }

    string TraceKindLocalExecutor { get; }

    string TraceKindToolCall { get; }

    string TraceKindBridge { get; }

    string TraceKindResult { get; }

    string TraceKindDiagnostic { get; }

    string TraceKindRaw { get; }
}

public sealed class SqliteNpcDeveloperTranscriptReader : INpcDeveloperTranscriptReader
{
    public Task<List<Message>> LoadSessionAsync(NpcNamespace npcNamespace, string sessionId, CancellationToken ct)
        => Task.Run(() => LoadSession(npcNamespace, sessionId, ct), ct);

    private static List<Message> LoadSession(NpcNamespace npcNamespace, string sessionId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(npcNamespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ct.ThrowIfCancellationRequested();

        var dbPath = npcNamespace.TranscriptStateDbPath;
        if (!SessionSearchIndex.TryLoadMessagesReadOnly(dbPath, sessionId, ct, out var messages))
            throw new SessionNotFoundException(sessionId);

        return messages;
    }
}

public sealed class SupervisorNpcDeveloperTodoReader : INpcDeveloperTodoReader
{
    private readonly NpcRuntimeSupervisor _supervisor;

    public SupervisorNpcDeveloperTodoReader(NpcRuntimeSupervisor supervisor)
    {
        _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
    }

    public SessionTodoSnapshot Read(string sessionId)
        => _supervisor.TryGetTaskView(sessionId, out var taskView) && taskView is not null
            ? taskView.ActiveSnapshot
            : SessionTodoSnapshot.Empty;
}

public sealed class EmptyNpcDeveloperTodoReader : INpcDeveloperTodoReader
{
    public static EmptyNpcDeveloperTodoReader Instance { get; } = new();

    public SessionTodoSnapshot Read(string sessionId) => SessionTodoSnapshot.Empty;
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
