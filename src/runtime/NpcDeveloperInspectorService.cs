namespace Hermes.Agent.Runtime;

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    public Task<NpcDeveloperInspectorView> InspectAsync(
        NpcRuntimeSnapshot snapshot,
        string runtimeRoot,
        CancellationToken ct)
        => InspectAsync(snapshot, runtimeRoot, null, ct);

    public async Task<NpcDeveloperInspectorView> InspectAsync(
        NpcRuntimeSnapshot snapshot,
        string runtimeRoot,
        NpcDeveloperInspectorRequest? request,
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
        request ??= NpcDeveloperInspectorRequest.Empty;
        var documents = await LoadDocumentsAsync(npcNamespace, ct);
        var tracePath = Path.Combine(npcNamespace.ActivityPath, "runtime.jsonl");
        var trace = await Task.Run(() => LoadTrace(tracePath, snapshot.NpcId, snapshot.LastTraceId, request.TraceFilter, ct), ct);
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
            trace.ContextBlocks,
            transcript.ModelReplies,
            transcript.ToolCalls,
            transcript.Delegations,
            transcript.ModelReplyEmptyState,
            transcript.ToolCallEmptyState,
            transcript.DelegationEmptyState,
            todos.Todos,
            todos.EmptyState);
    }

    public async Task<NpcDeveloperDiagnosticsExport> ExportDiagnosticsAsync(
        NpcRuntimeSnapshot snapshot,
        string runtimeRoot,
        NpcDeveloperInspectorRequest? request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeRoot);

        var view = await InspectAsync(snapshot, runtimeRoot, request, ct);
        var diagnosticsRoot = Path.Combine(runtimeRoot, "diagnostics");
        Directory.CreateDirectory(diagnosticsRoot);
        var fileName = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "npc-diagnostics-{0}-{1:yyyyMMdd-HHmmss}.zip",
            SanitizeFileName(snapshot.NpcId),
            DateTime.Now);
        var zipPath = Path.Combine(diagnosticsRoot, fileName);
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        AddTextEntry(archive, "README.txt", BuildDiagnosticsReadme(view));
        AddJsonEntry(archive, "runtime-summary.json", new
        {
            view.NpcId,
            view.DisplayName,
            view.GameId,
            view.SaveId,
            view.ProfileId,
            view.SessionId,
            view.State,
            view.LastTraceId,
            view.LastError,
            view.RuntimePath,
            view.RuntimeLogPath
        });
        AddJsonEntry(archive, "documents-summary.json", view.Documents.Select(document => new
        {
            document.Name,
            document.Path,
            document.Exists,
            document.Status,
            document.IsTruncated,
            ContentExported = false
        }));
        AddJsonEntry(archive, "trace-events.json", view.TraceEvents);
        AddJsonEntry(archive, "context-blocks.json", view.ContextBlocks);
        AddJsonEntry(archive, "transcript-summary.json", new
        {
            ModelReplies = view.ModelReplies.Select(reply => new
            {
                reply.TimestampUtc,
                ContentPreview = Truncate(RedactSensitiveText(reply.Content), 500),
                ReasoningPreview = Truncate(RedactSensitiveText(reply.Reasoning), 500)
            }),
            ToolCalls = view.ToolCalls.Select(call => new
            {
                call.Id,
                call.Name,
                ArgumentsPreview = Truncate(RedactSensitiveText(call.Arguments), 500),
                ResultPreview = Truncate(RedactSensitiveText(call.Result), 500),
                call.ResultTimestampUtc
            }),
            Delegations = view.Delegations.Select(delegation => new
            {
                delegation.ToolCallId,
                RequestPreview = Truncate(RedactSensitiveText(delegation.Request), 500),
                ResultPreview = Truncate(RedactSensitiveText(delegation.Result), 500)
            }),
            Todos = view.Todos
        });

        AddFileTailEntry(archive, "logs/hermes-tail.log", Path.Combine(runtimeRoot, "logs", "hermes.log"), 240, ct);
        AddFileTailEntry(
            archive,
            "logs/smapi-tail.log",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StardewValley", "ErrorLogs", "SMAPI-latest.txt"),
            240,
            ct);
        AddFileEntryIfExists(
            archive,
            "bridge-discovery.redacted.json",
            Path.Combine(runtimeRoot, "stardew-bridge.json"),
            ct);

        return new NpcDeveloperDiagnosticsExport(zipPath);
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
        NpcDeveloperTraceFilter? filter,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!File.Exists(path))
            return new TraceProjection([], [], [], _text.TraceLogMissing);

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
            return new TraceProjection([], diagnostics, [], _text.TraceEmptyForNpc);

        var hasFilter = HasTraceFilter(filter);
        var traceId = !string.IsNullOrWhiteSpace(filter?.TraceId)
            ? filter.TraceId
            : string.IsNullOrWhiteSpace(preferredTraceId)
            ? records.OrderByDescending(record => record.TimestampUtc).First().TraceId
            : preferredTraceId;
        var selectedRecords = records
            .Where(record => hasFilter && string.IsNullOrWhiteSpace(filter?.TraceId)
                ? true
                : string.Equals(record.TraceId, traceId, StringComparison.OrdinalIgnoreCase))
            .Where(record => MatchesTraceFilter(record, filter))
            .OrderBy(record => record.TimestampUtc)
            .ToArray();
        var selected = selectedRecords
            .Select(ToTraceEvent)
            .ToArray();
        var contextBlocks = selectedRecords
            .Select(TryProjectContextBlock)
            .Where(block => block is not null)
            .Cast<NpcDeveloperContextBlock>()
            .ToArray();

        if (selected.Length == 0)
            return new TraceProjection([], diagnostics, contextBlocks, _text.TraceSelectionMissing);

        return new TraceProjection(selected, diagnostics, contextBlocks, "");
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

    private static bool HasTraceFilter(NpcDeveloperTraceFilter? filter)
        => filter is not null &&
           (!string.IsNullOrWhiteSpace(filter.NpcId) ||
            !string.IsNullOrWhiteSpace(filter.TraceId) ||
            !string.IsNullOrWhiteSpace(filter.EventType) ||
            !string.IsNullOrWhiteSpace(filter.CommandId) ||
            !string.IsNullOrWhiteSpace(filter.ToolName) ||
            !string.IsNullOrWhiteSpace(filter.ErrorCode) ||
            !string.IsNullOrWhiteSpace(filter.Keyword));

    private static bool MatchesTraceFilter(NpcRuntimeLogRecord record, NpcDeveloperTraceFilter? filter)
    {
        if (filter is null)
            return true;

        if (!MatchesText(record.NpcId, filter.NpcId))
            return false;
        if (!MatchesText(record.TraceId, filter.TraceId))
            return false;
        if (!MatchesText(record.ActionType, filter.EventType))
            return false;
        if (!MatchesText(record.CommandId, filter.CommandId))
            return false;
        if (!MatchesText(record.Target, filter.ToolName))
            return false;
        if (!MatchesText(record.Error, filter.ErrorCode))
            return false;

        if (string.IsNullOrWhiteSpace(filter.Keyword))
            return true;

        var keyword = filter.Keyword.Trim();
        return Contains(record.Result, keyword) ||
               Contains(record.Target, keyword) ||
               Contains(record.Stage, keyword) ||
               Contains(record.CommandId, keyword) ||
               Contains(record.Error, keyword) ||
               Contains(record.ExecutorMode, keyword) ||
               Contains(record.TargetSource, keyword);
    }

    private static bool MatchesText(string? value, string? expected)
        => string.IsNullOrWhiteSpace(expected) ||
           (!string.IsNullOrWhiteSpace(value) &&
            value.Contains(expected.Trim(), StringComparison.OrdinalIgnoreCase));

    private static bool Contains(string? value, string keyword)
        => !string.IsNullOrWhiteSpace(value) &&
           value.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private static NpcDeveloperContextBlock? TryProjectContextBlock(NpcRuntimeLogRecord record)
    {
        if (!string.Equals(record.ActionType, "diagnostic", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(record.Target, "context_injection", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var fields = ParseKeyValueResult(record.Result);
        var name = GetField(fields, "block", "name");
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return new NpcDeveloperContextBlock(
            name,
            ReadInt(fields, "charsBefore"),
            ReadInt(fields, "charsAfter"),
            ReadInt(fields, "tokens"),
            string.Equals(GetField(fields, "trimmed", "isTrimmed"), "true", StringComparison.OrdinalIgnoreCase),
            GetField(fields, "hash") ?? "",
            GetField(fields, "preview") ?? "");
    }

    private static Dictionary<string, string> ParseKeyValueResult(string value)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
                continue;

            result[part[..separator].Trim()] = part[(separator + 1)..].Trim();
        }

        return result;
    }

    private static string? GetField(IReadOnlyDictionary<string, string> fields, params string[] names)
    {
        foreach (var name in names)
        {
            if (fields.TryGetValue(name, out var value))
                return value;
        }

        return null;
    }

    private static int ReadInt(IReadOnlyDictionary<string, string> fields, string name)
        => fields.TryGetValue(name, out var value) &&
           int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;

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

    private static void AddJsonEntry(ZipArchive archive, string entryName, object value)
        => AddTextEntry(
            archive,
            entryName,
            RedactSensitiveText(JsonSerializer.Serialize(value, DiagnosticsJsonOptions)));

    private static void AddTextEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(RedactSensitiveText(content));
    }

    private static void AddFileTailEntry(
        ZipArchive archive,
        string entryName,
        string path,
        int maxLines,
        CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            AddTextEntry(archive, entryName, $"missing: {path}");
            return;
        }

        var tail = ReadTailLines(path, maxLines, ct);
        AddTextEntry(archive, entryName, string.Join(Environment.NewLine, tail.Lines));
    }

    private static void AddFileEntryIfExists(ZipArchive archive, string entryName, string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!File.Exists(path))
        {
            AddTextEntry(archive, entryName, $"missing: {path}");
            return;
        }

        AddTextEntry(archive, entryName, File.ReadAllText(path));
    }

    private static string BuildDiagnosticsReadme(NpcDeveloperInspectorView view)
        => $"""
           Hermes NPC diagnostics package

           NPC: {view.DisplayName} ({view.NpcId})
           Session: {view.SessionId}
           Trace: {view.LastTraceId}

           This package contains runtime summaries, selected trace rows, transcript previews, log tails, and redacted bridge discovery.
           Full SOUL.md, MEMORY.md, and USER.md contents are intentionally excluded by default.
           """;

    private static string RedactSensitiveText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var redacted = SensitiveKeyRegex.Replace(value, match =>
        {
            var prefix = match.Groups["prefix"].Value;
            var quote = match.Groups["quote"].Value;
            return $"{prefix}{quote}[redacted]{quote}";
        });
        redacted = AuthorizationRegex.Replace(redacted, "${prefix}[redacted]");
        return redacted;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
            builder.Append(invalid.Contains(ch) ? '-' : ch);

        return builder.Length == 0 ? "npc" : builder.ToString();
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private int PreviewCharacterLimit => Math.Max(1, _options.PreviewCharacterLimit ?? DefaultPreviewCharacterLimit);

    private int RuntimeLogMaxLines => Math.Max(1, _options.RuntimeLogMaxLines ?? DefaultRuntimeLogMaxLines);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions DiagnosticsJsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly Regex SensitiveKeyRegex = new(
        @"(?<prefix>(?:""?(?:bridgeToken|authorization|apiKey|apikey|secret|password|token|connectionString)""?)\s*[:=]\s*)(?<quote>""?)(?<value>[^"",}\r\n]+)(?<quote2>""?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AuthorizationRegex = new(
        @"(?<prefix>Authorization\s*:\s*)(?:Bearer\s+)?[^\r\n]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private sealed record TraceProjection(
        IReadOnlyList<NpcDeveloperTraceEvent> Events,
        IReadOnlyList<string> Diagnostics,
        IReadOnlyList<NpcDeveloperContextBlock> ContextBlocks,
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

public sealed record NpcDeveloperInspectorRequest(
    NpcDeveloperTraceFilter? TraceFilter = null)
{
    public static NpcDeveloperInspectorRequest Empty { get; } = new();
}

public sealed record NpcDeveloperTraceFilter(
    string? NpcId = null,
    string? TraceId = null,
    string? EventType = null,
    string? CommandId = null,
    string? ToolName = null,
    string? ErrorCode = null,
    string? Keyword = null);

public sealed record NpcDeveloperDiagnosticsExport(string ZipPath);

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
    IReadOnlyList<NpcDeveloperContextBlock> ContextBlocks,
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

public sealed record NpcDeveloperContextBlock(
    string Name,
    int CharactersBefore,
    int CharactersAfter,
    int EstimatedTokens,
    bool Trimmed,
    string Hash,
    string Preview);

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
