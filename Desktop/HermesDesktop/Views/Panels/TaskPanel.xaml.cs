using System.Collections.ObjectModel;
using System.Linq;
using Hermes.Agent.Tasks;
using HermesDesktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace HermesDesktop.Views.Panels;

public sealed class TaskListItem
{
    public string TaskId { get; set; } = "";
    public string Description { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public string PriorityLabel { get; set; } = "";
    public string DueLabel { get; set; } = "";
    public SolidColorBrush StatusColor { get; set; } = new(Colors.Gray);
    public SolidColorBrush DescriptionBrush { get; set; } = new(ColorHelper.FromArgb(255, 232, 238, 247));
    public SolidColorBrush DueBrush { get; set; } = new(ColorHelper.FromArgb(255, 149, 162, 177));
}

public sealed partial class TaskPanel : UserControl
{
    private readonly SessionTaskPanelModel _model;
    public ObservableCollection<TaskListItem> Tasks { get; } = new();

    public TaskPanel()
    {
        InitializeComponent();
        var taskProjectionService = App.Services.GetRequiredService<SessionTaskProjectionService>();
        var chatService = App.Services.GetRequiredService<HermesChatService>();
        _model = new SessionTaskPanelModel(taskProjectionService, () => chatService.CurrentSessionId);
        _model.Changed += OnModelChanged;
        Loaded += (_, _) => Refresh();
        Unloaded += (_, _) =>
        {
            _model.Changed -= OnModelChanged;
            _model.Dispose();
        };
    }

    public void Refresh() => _model.Refresh();

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
                DueLabel = "",
                StatusColor = GetStatusColor(task.Status),
                DescriptionBrush = task.Status is "completed" or "cancelled"
                    ? new SolidColorBrush(ColorHelper.FromArgb(255, 100, 120, 100))
                    : new SolidColorBrush(ColorHelper.FromArgb(255, 232, 238, 247)),
                DueBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 149, 162, 177))
            });
        }

        TaskList.ItemsSource = Tasks;
        EmptyState.Visibility = Tasks.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

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

}
