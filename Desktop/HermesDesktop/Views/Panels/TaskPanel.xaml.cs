using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using Hermes.Agent.Tasks;
using Hermes.Agent.Transcript;
using HermesDesktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;

namespace HermesDesktop.Views.Panels;

public sealed class TaskListItem
{
    public string TaskId { get; set; } = "";
    public string Description { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public string PriorityLabel { get; set; } = "";
    public SolidColorBrush StatusColor { get; set; } = new(Colors.Gray);
    public SolidColorBrush DescriptionBrush { get; set; } = new(ColorHelper.FromArgb(255, 232, 238, 247));
}

public sealed class TaskArchiveListItem
{
    public string Description { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public string TurnLabel { get; set; } = "";
    public string SummaryLabel { get; set; } = "";
    public SolidColorBrush StatusColor { get; set; } = new(Colors.Gray);
    public SolidColorBrush DescriptionBrush { get; set; } = new(ColorHelper.FromArgb(255, 232, 238, 247));
    public SolidColorBrush SummaryBrush { get; set; } = new(ColorHelper.FromArgb(255, 149, 162, 177));
}

public sealed partial class TaskPanel : UserControl
{
    private readonly ResourceLoader _resources = new();
    private readonly SessionTaskPanelModel _model;
    private readonly HermesChatService _chatService;
    private readonly TranscriptStore _transcriptStore;

    public ObservableCollection<TaskListItem> Tasks { get; } = new();
    public ObservableCollection<TaskArchiveListItem> ArchiveTasks { get; } = new();

    public TaskPanel()
    {
        InitializeComponent();
        var taskProjectionService = App.Services.GetRequiredService<SessionTaskProjectionService>();
        _chatService = App.Services.GetRequiredService<HermesChatService>();
        _transcriptStore = App.Services.GetRequiredService<TranscriptStore>();
        _model = new SessionTaskPanelModel(taskProjectionService, () => _chatService.CurrentSessionId);
        _model.Changed += OnModelChanged;
        Loaded += async (_, _) => await RefreshAllAsync();
        Unloaded += (_, _) =>
        {
            _model.Changed -= OnModelChanged;
            _model.Dispose();
        };
    }

    public void Refresh() => _model.Refresh();

    public async Task RefreshAllAsync()
    {
        Refresh();
        await RefreshArchiveAsync();
    }

    public async Task RefreshArchiveAsync()
    {
        var sessionId = _chatService.CurrentSessionId;
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            ApplyArchive([]);
            return;
        }

        try
        {
            var messages = await _transcriptStore.LoadSessionAsync(sessionId, CancellationToken.None);
            ApplyArchive(SessionTodoArchiveService.BuildArchive(sessionId, messages));
        }
        catch (SessionNotFoundException)
        {
            ApplyArchive([]);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TaskPanel archive refresh failed for {sessionId}: {ex}");
            ApplyArchive([]);
        }
    }

    private void ApplyModel()
    {
        Tasks.Clear();
        foreach (var task in _model.Tasks)
        {
            Tasks.Add(new TaskListItem
            {
                TaskId = task.TaskId,
                Description = task.Description,
                StatusLabel = task.StatusLabel,
                PriorityLabel = task.PriorityLabel,
                StatusColor = GetStatusColor(task.Status),
                DescriptionBrush = task.Status is "completed" or "cancelled"
                    ? new SolidColorBrush(ColorHelper.FromArgb(255, 100, 120, 100))
                    : new SolidColorBrush(ColorHelper.FromArgb(255, 232, 238, 247))
            });
        }

        TaskList.ItemsSource = Tasks;
        EmptyState.Visibility = Tasks.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAllAsync();

    private void ApplyArchive(IReadOnlyList<SessionTodoArchiveEntry> entries)
    {
        ArchiveTasks.Clear();
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            ArchiveTasks.Add(new TaskArchiveListItem
            {
                Description = BuildArchiveDescription(entry),
                StatusLabel = BuildArchiveStatusLabel(entry),
                TurnLabel = string.Format(
                    CultureInfo.CurrentCulture,
                    _resources.GetString("TaskPanelArchiveTurnFormat"),
                    i + 1),
                SummaryLabel = $"{entry.Todos.Count - entry.IncompleteCount}/{entry.Todos.Count}",
                StatusColor = entry.HasIncomplete
                    ? new SolidColorBrush(ColorHelper.FromArgb(255, 160, 110, 70))
                    : new SolidColorBrush(ColorHelper.FromArgb(255, 80, 120, 80)),
                DescriptionBrush = entry.IsComplete
                    ? new SolidColorBrush(ColorHelper.FromArgb(255, 100, 120, 100))
                    : new SolidColorBrush(ColorHelper.FromArgb(255, 232, 238, 247)),
                SummaryBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 149, 162, 177))
            });
        }

        ArchiveList.ItemsSource = ArchiveTasks;
        ArchiveEmptyState.Visibility = ArchiveTasks.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnModelChanged(object? sender, EventArgs e)
    {
        if (DispatcherQueue.HasThreadAccess)
            ApplyModel();
        else
            DispatcherQueue.TryEnqueue(ApplyModel);
    }

    private static SolidColorBrush GetStatusColor(string status) => status switch
    {
        "pending" => new SolidColorBrush(ColorHelper.FromArgb(255, 120, 120, 120)),
        "in_progress" => new SolidColorBrush(ColorHelper.FromArgb(255, 80, 140, 220)),
        "completed" => new SolidColorBrush(ColorHelper.FromArgb(255, 80, 180, 80)),
        "cancelled" => new SolidColorBrush(ColorHelper.FromArgb(255, 160, 110, 70)),
        _ => new SolidColorBrush(Colors.Gray)
    };

    private string BuildArchiveStatusLabel(SessionTodoArchiveEntry entry)
        => entry.HasIncomplete
            ? string.Format(
                CultureInfo.CurrentCulture,
                _resources.GetString("TaskPanelArchiveIncompleteFormat"),
                entry.IncompleteCount)
            : _resources.GetString("TaskPanelArchiveComplete");

    private static string BuildArchiveDescription(SessionTodoArchiveEntry entry)
    {
        var parts = entry.Todos
            .Select((todo, index) => $"#{index + 1} {todo.Content}")
            .Take(3)
            .ToList();

        if (entry.Todos.Count > parts.Count)
            parts.Add($"+{entry.Todos.Count - parts.Count}");

        return string.Join(" / ", parts);
    }
}
