namespace Hermes.Agent.Tasks;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hermes.Agent.Core;

public static class SessionTodoArchiveService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IReadOnlyList<SessionTodoArchiveEntry> BuildArchive(
        string sessionId,
        IEnumerable<Message> messages)
    {
        var entries = new List<SessionTodoArchiveEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        SessionTodoSnapshot? pendingSnapshot = null;
        string? pendingSnapshotHash = null;

        foreach (var message in messages)
        {
            if (IsTodoToolResult(message))
            {
                if (TryParseSnapshot(message.Content, out var snapshot))
                {
                    pendingSnapshot = snapshot;
                    pendingSnapshotHash = HashParts(JsonSerializer.Serialize(snapshot, JsonOptions));
                }

                continue;
            }

            if (!IsAssistantBoundary(message) ||
                pendingSnapshot is null ||
                string.IsNullOrEmpty(pendingSnapshotHash))
            {
                continue;
            }

            if (pendingSnapshot.Todos.Count == 0)
            {
                pendingSnapshot = null;
                pendingSnapshotHash = null;
                continue;
            }

            var key = HashParts(
                sessionId,
                pendingSnapshotHash,
                message.Content,
                message.Timestamp.ToUniversalTime().ToString("O"));

            if (seen.Add(key))
                entries.Add(SessionTodoArchiveEntry.FromSnapshot(key, message.Timestamp, pendingSnapshot));

            pendingSnapshot = null;
            pendingSnapshotHash = null;
        }

        return entries;
    }

    private static bool IsTodoToolResult(Message message)
        => string.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase) &&
           (string.Equals(message.ToolName, "todo", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(message.ToolName, "todo_write", StringComparison.OrdinalIgnoreCase));

    private static bool IsAssistantBoundary(Message message)
        => string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase) &&
           message.ToolCalls is not { Count: > 0 };

    private static bool TryParseSnapshot(string content, out SessionTodoSnapshot snapshot)
    {
        snapshot = SessionTodoSnapshot.Empty;
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("todos", out var todosElement) ||
                todosElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var inputs = new List<SessionTodoInput>();
            foreach (var item in todosElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                inputs.Add(new SessionTodoInput(
                    GetString(item, "id"),
                    GetString(item, "content"),
                    GetString(item, "status")));
            }

            snapshot = new SessionTodoStore().Write("__archive__", inputs);
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

    private static string HashParts(params string[] parts)
    {
        var joined = string.Join('\u001f', parts);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(joined))).ToLowerInvariant();
    }
}

public sealed record SessionTodoArchiveEntry(
    string IdempotencyKey,
    DateTime Timestamp,
    IReadOnlyList<SessionTodoItem> Todos,
    bool IsComplete,
    bool HasIncomplete,
    int IncompleteCount,
    bool CollapsedByDefault)
{
    public static SessionTodoArchiveEntry FromSnapshot(
        string idempotencyKey,
        DateTime timestamp,
        SessionTodoSnapshot snapshot)
    {
        var todos = snapshot.Todos.Select(t => t with { }).ToList();
        var incompleteCount = todos.Count(t => t.Status is "pending" or "in_progress");
        var isComplete = todos.Count > 0 && incompleteCount == 0;
        return new SessionTodoArchiveEntry(
            idempotencyKey,
            timestamp,
            todos,
            isComplete,
            incompleteCount > 0,
            incompleteCount,
            isComplete);
    }
}
