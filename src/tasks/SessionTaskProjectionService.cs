namespace Hermes.Agent.Tasks;

using System.Text.Json;
using Hermes.Agent.Core;
using Hermes.Agent.Transcript;

public sealed class SessionTaskProjectionService : ITranscriptMessageObserver
{
    private readonly SessionTodoStore _store;

    public SessionTaskProjectionService(SessionTodoStore store)
    {
        _store = store;
    }

    public event EventHandler<SessionTaskSnapshotChangedEventArgs>? SnapshotChanged;

    public Task OnMessageSavedAsync(string sessionId, Message message, CancellationToken ct)
        => OnMessageSavedAsync(sessionId, message, taskSessionId: null, ct);

    public Task OnMessageSavedAsync(string sessionId, Message message, string? taskSessionId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsTodoToolResult(message))
            return Task.CompletedTask;

        var projectionSessionId = ResolveProjectionSessionId(sessionId, message, taskSessionId);
        if (TryProjectToolResult(projectionSessionId, message.Content, out var snapshot))
            SnapshotChanged?.Invoke(this, new SessionTaskSnapshotChangedEventArgs(projectionSessionId, snapshot));

        return Task.CompletedTask;
    }

    public Task HydrateSessionAsync(string sessionId, IEnumerable<Message> messages, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        SessionTodoSnapshot? latest = null;

        foreach (var message in messages)
        {
            ct.ThrowIfCancellationRequested();
            if (!IsTodoToolResult(message))
                continue;

            if (TryParseTodoInputs(message.Content, out var inputs))
                latest = _store.Write(sessionId, inputs);
        }

        if (latest is null)
        {
            _store.ClearSession(sessionId);
            latest = SessionTodoSnapshot.Empty;
        }

        SnapshotChanged?.Invoke(this, new SessionTaskSnapshotChangedEventArgs(sessionId, latest));
        return Task.CompletedTask;
    }

    public SessionTodoSnapshot GetSnapshot(string sessionId) => _store.Read(sessionId);

    public string? FormatActiveTasksForInjection(string sessionId) => _store.FormatActiveTasksForInjection(sessionId);

    public void ClearSession(string sessionId)
    {
        _store.ClearSession(sessionId);
        SnapshotChanged?.Invoke(this, new SessionTaskSnapshotChangedEventArgs(sessionId, SessionTodoSnapshot.Empty));
    }

    private bool TryProjectToolResult(string sessionId, string content, out SessionTodoSnapshot snapshot)
    {
        if (TryParseTodoInputs(content, out var inputs))
        {
            snapshot = _store.Write(sessionId, inputs);
            return true;
        }

        snapshot = _store.Read(sessionId);
        return false;
    }

    private static bool IsTodoToolResult(Message message)
        => string.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase) &&
           (string.Equals(message.ToolName, "todo", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(message.ToolName, "todo_write", StringComparison.OrdinalIgnoreCase));

    private static string ResolveProjectionSessionId(string sessionId, Message message, string? explicitTaskSessionId)
    {
        if (!string.IsNullOrWhiteSpace(explicitTaskSessionId))
            return explicitTaskSessionId;

        return string.IsNullOrWhiteSpace(message.TaskSessionId) ? sessionId : message.TaskSessionId;
    }

    private static bool TryParseTodoInputs(string content, out IReadOnlyList<SessionTodoInput> inputs)
    {
        inputs = Array.Empty<SessionTodoInput>();
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("todos", out var todosElement) ||
                todosElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var parsed = new List<SessionTodoInput>();
            foreach (var item in todosElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                parsed.Add(new SessionTodoInput(
                    GetString(item, "id"),
                    GetString(item, "content"),
                    GetString(item, "status"),
                    GetString(item, "reason")));
            }

            inputs = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? GetString(JsonElement item, string propertyName)
        => item.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;
}

public sealed class SessionTaskSnapshotChangedEventArgs : EventArgs
{
    public SessionTaskSnapshotChangedEventArgs(string sessionId, SessionTodoSnapshot snapshot)
    {
        SessionId = sessionId;
        Snapshot = snapshot;
    }

    public string SessionId { get; }
    public SessionTodoSnapshot Snapshot { get; }
}
