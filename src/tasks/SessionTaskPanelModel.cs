namespace Hermes.Agent.Tasks;

using System.Collections.ObjectModel;

public sealed class SessionTaskPanelModel : IDisposable
{
    private readonly SessionTaskProjectionService _projectionService;
    private readonly Func<string?> _currentSessionId;
    private bool _disposed;

    public SessionTaskPanelModel(
        SessionTaskProjectionService projectionService,
        Func<string?> currentSessionId)
    {
        _projectionService = projectionService;
        _currentSessionId = currentSessionId;
        _projectionService.SnapshotChanged += OnSnapshotChanged;
    }

    public ObservableCollection<SessionTaskPanelItem> Tasks { get; } = new();

    public event EventHandler? Changed;

    public void Refresh()
    {
        var sessionId = _currentSessionId();
        ApplySnapshot(string.IsNullOrWhiteSpace(sessionId)
            ? SessionTodoSnapshot.Empty
            : _projectionService.GetSnapshot(sessionId));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _projectionService.SnapshotChanged -= OnSnapshotChanged;
        _disposed = true;
    }

    private void OnSnapshotChanged(object? sender, SessionTaskSnapshotChangedEventArgs e)
    {
        if (!string.Equals(e.SessionId, _currentSessionId(), StringComparison.OrdinalIgnoreCase))
            return;

        ApplySnapshot(e.Snapshot);
    }

    private void ApplySnapshot(SessionTodoSnapshot snapshot)
    {
        Tasks.Clear();
        if (snapshot.Todos.Count > 0 &&
            snapshot.Todos.All(t => t.Status is "completed" or "cancelled"))
        {
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        var index = 0;
        foreach (var task in snapshot.Todos)
        {
            index++;
            Tasks.Add(new SessionTaskPanelItem(
                task.Id,
                task.Content,
                task.Status,
                GetStatusLabel(task.Status),
                $"#{index}"));
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string GetStatusLabel(string status) => status switch
    {
        "in_progress" => "In progress",
        "completed" => "Done",
        "cancelled" => "Cancelled",
        _ => "Pending"
    };
}

public sealed record SessionTaskPanelItem(
    string TaskId,
    string Description,
    string Status,
    string StatusLabel,
    string PriorityLabel);
