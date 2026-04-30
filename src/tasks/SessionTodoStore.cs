namespace Hermes.Agent.Tasks;

using System.Collections.Concurrent;
using System.Text.Json.Serialization;

public sealed class SessionTodoStore
{
    private const string DefaultSessionId = "__default__";
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.Ordinal)
    {
        "pending",
        "in_progress",
        "completed",
        "cancelled"
    };

    private readonly ConcurrentDictionary<string, List<SessionTodoItem>> _todosBySession = new(StringComparer.OrdinalIgnoreCase);

    public SessionTodoSnapshot Read(string? sessionId)
    {
        var key = NormalizeSessionId(sessionId);
        if (!_todosBySession.TryGetValue(key, out var items))
            return SessionTodoSnapshot.Empty;

        lock (items)
        {
            return BuildSnapshot(items);
        }
    }

    public SessionTodoSnapshot Write(string? sessionId, IEnumerable<SessionTodoInput> todos, bool merge = false)
    {
        var key = NormalizeSessionId(sessionId);
        var inputs = DeduplicateById(todos).ToList();

        var items = _todosBySession.GetOrAdd(key, _ => new List<SessionTodoItem>());
        lock (items)
        {
            if (!merge)
            {
                items.Clear();
                items.AddRange(inputs.Select(Validate));
                return BuildSnapshot(items);
            }

            var byId = items.ToDictionary(t => t.Id, StringComparer.Ordinal);
            foreach (var input in inputs)
            {
                var itemId = (input.Id ?? "").Trim();
                if (string.IsNullOrWhiteSpace(itemId))
                    continue;

                if (byId.TryGetValue(itemId, out var existing))
                {
                    byId[itemId] = existing with
                    {
                        Content = string.IsNullOrWhiteSpace(input.Content) ? existing.Content : input.Content.Trim(),
                        Status = NormalizeStatus(input.Status, existing.Status)
                    };
                }
                else
                {
                    var validated = Validate(input);
                    byId[validated.Id] = validated;
                    items.Add(validated);
                }
            }

            for (var i = 0; i < items.Count; i++)
                items[i] = byId[items[i].Id];

            return BuildSnapshot(items);
        }
    }

    public void ClearSession(string? sessionId)
        => _todosBySession.TryRemove(NormalizeSessionId(sessionId), out _);

    public string? FormatActiveTasksForInjection(string? sessionId)
    {
        var snapshot = Read(sessionId);
        var active = snapshot.Todos
            .Where(t => t.Status is "pending" or "in_progress")
            .ToList();
        if (active.Count == 0)
            return null;

        var lines = new List<string> { "[Your active task list was preserved across context compression]" };
        foreach (var item in active)
        {
            var marker = item.Status == "in_progress" ? "[>]" : "[ ]";
            lines.Add($"- {marker} {item.Id}. {item.Content} ({item.Status})");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static IEnumerable<SessionTodoInput> DeduplicateById(IEnumerable<SessionTodoInput> todos)
    {
        var list = todos.ToList();
        var lastIndexById = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < list.Count; i++)
        {
            var itemId = (list[i].Id ?? "").Trim();
            if (string.IsNullOrEmpty(itemId))
                itemId = "?";
            lastIndexById[itemId] = i;
        }

        foreach (var index in lastIndexById.Values.OrderBy(i => i))
            yield return list[index];
    }

    private static SessionTodoItem Validate(SessionTodoInput input)
    {
        var id = (input.Id ?? "").Trim();
        if (string.IsNullOrWhiteSpace(id))
            id = "?";

        var content = (input.Content ?? "").Trim();
        if (string.IsNullOrWhiteSpace(content))
            content = "(no description)";

        return new SessionTodoItem(id, content, NormalizeStatus(input.Status, "pending"));
    }

    private static string NormalizeStatus(string? status, string fallback)
    {
        var normalized = (status ?? "").Trim().ToLowerInvariant();
        return ValidStatuses.Contains(normalized) ? normalized : fallback;
    }

    private static SessionTodoSnapshot BuildSnapshot(IReadOnlyList<SessionTodoItem> items)
    {
        var copy = items.Select(t => t with { }).ToList();
        var summary = new SessionTodoSummary(
            copy.Count,
            copy.Count(t => t.Status == "pending"),
            copy.Count(t => t.Status == "in_progress"),
            copy.Count(t => t.Status == "completed"),
            copy.Count(t => t.Status == "cancelled"));
        return new SessionTodoSnapshot(copy, summary);
    }

    private static string NormalizeSessionId(string? sessionId)
        => string.IsNullOrWhiteSpace(sessionId) ? DefaultSessionId : sessionId.Trim();
}

public sealed record SessionTodoInput(string? Id, string? Content, string? Status);

public sealed record SessionTodoItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("status")] string Status);

public sealed record SessionTodoSummary(
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("pending")] int Pending,
    [property: JsonPropertyName("in_progress")] int InProgress,
    [property: JsonPropertyName("completed")] int Completed,
    [property: JsonPropertyName("cancelled")] int Cancelled);

public sealed record SessionTodoSnapshot(
    [property: JsonPropertyName("todos")] IReadOnlyList<SessionTodoItem> Todos,
    [property: JsonPropertyName("summary")] SessionTodoSummary Summary)
{
    public static SessionTodoSnapshot Empty { get; } =
        new(Array.Empty<SessionTodoItem>(), new SessionTodoSummary(0, 0, 0, 0, 0));
}
