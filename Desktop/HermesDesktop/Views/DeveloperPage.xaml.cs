using Hermes.Agent.Runtime;
using HermesDesktop.Models;
using HermesDesktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using System.Globalization;

namespace HermesDesktop.Views;

public sealed partial class DeveloperPage : Page
{
    private static readonly ResourceLoader ResourceLoader = new();
    private readonly NpcRuntimeWorkspaceService? _workspaceService = App.Services.GetService<NpcRuntimeWorkspaceService>();
    private readonly NpcRuntimeSupervisor? _supervisor = App.Services.GetService<NpcRuntimeSupervisor>();
    private readonly NpcDeveloperInspectorService? _inspectorService = App.Services.GetService<NpcDeveloperInspectorService>();

    private NpcDeveloperInspectorView? _currentView;
    private string _activeDetailTab = "model";

    public DeveloperPage()
    {
        InitializeComponent();
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        Refresh();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        Refresh();
    }

    private void OpenRuntimeDirectory_Click(object sender, RoutedEventArgs e)
    {
        _workspaceService?.OpenRuntimeDirectory();
    }

    private void Refresh()
    {
        if (_workspaceService is null)
        {
            BridgeHealthText.Text = ResourceLoader.GetString("DeveloperRuntimeUnavailable");
            NpcList.ItemsSource = Array.Empty<NpcRuntimeItem>();
            return;
        }

        var workspaceSnapshot = _workspaceService.GetSnapshot();
        RuntimeCountText.Text = workspaceSnapshot.Items.Count.ToString(CultureInfo.CurrentCulture);
        BridgeHealthText.Text = workspaceSnapshot.BridgeHealth;
        LastErrorText.Text = string.IsNullOrWhiteSpace(workspaceSnapshot.LastError) ? "-" : workspaceSnapshot.LastError;
        NpcList.ItemsSource = workspaceSnapshot.Items;

        if (workspaceSnapshot.Items.Count == 0)
        {
            ClearSelection(ResourceLoader.GetString("DeveloperNoNpcEmptyState"));
            return;
        }

        var selected = NpcList.SelectedItem as NpcRuntimeItem;
        selected ??= workspaceSnapshot.Items[0];
        NpcList.SelectedItem = selected;
        _ = LoadNpcAsync(selected);
    }

    private async void NpcList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is NpcRuntimeItem item)
            await LoadNpcAsync(item);
    }

    private async Task LoadNpcAsync(NpcRuntimeItem item)
    {
        if (_workspaceService is null || _supervisor is null || _inspectorService is null)
        {
            ClearSelection(ResourceLoader.GetString("DeveloperRuntimeUnavailable"));
            return;
        }

        var snapshot = _supervisor.Snapshot()
            .FirstOrDefault(candidate =>
                string.Equals(candidate.NpcId, item.NpcId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.SessionId, item.SessionId, StringComparison.OrdinalIgnoreCase));
        if (snapshot is null)
        {
            ClearSelection(ResourceLoader.GetString("DeveloperRuntimeSnapshotMissing"));
            SelectedNpcText.Text = string.IsNullOrWhiteSpace(item.DisplayName) ? item.NpcId : item.DisplayName;
            SessionIdText.Text = string.IsNullOrWhiteSpace(item.SessionId) ? "-" : item.SessionId;
            TraceIdText.Text = string.IsNullOrWhiteSpace(item.LastTraceId) ? "-" : item.LastTraceId;
            NpcStateText.Text = item.State;
            return;
        }

        try
        {
            var runtimeRoot = _workspaceService.RuntimeRoot;
            _currentView = await _inspectorService.InspectAsync(snapshot, runtimeRoot, CancellationToken.None);
            ApplyView(_currentView);
        }
        catch (Exception ex)
        {
            ClearSelection(string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetString("DeveloperLoadFailedFormat"), ex.Message));
        }
    }

    private void ApplyView(NpcDeveloperInspectorView view)
    {
        SelectedNpcText.Text = $"{view.DisplayName} ({view.NpcId})";
        SessionIdText.Text = string.IsNullOrWhiteSpace(view.SessionId) ? "-" : view.SessionId;
        TraceIdText.Text = string.IsNullOrWhiteSpace(view.LastTraceId) ? "-" : view.LastTraceId;
        NpcStateText.Text = view.State;
        ChannelText.Text = $"{view.MainChannel} / {view.DelegationChannel}";
        DocumentList.ItemsSource = view.Documents;
        TraceList.ItemsSource = view.TraceEvents;
        TraceEmptyText.Text = view.TraceEmptyState;
        ModelReplyList.ItemsSource = view.ModelReplies;
        ToolCallList.ItemsSource = view.ToolCalls;
        DelegationList.ItemsSource = view.Delegations;
        TodoList.ItemsSource = view.Todos;
        RuntimePathText.Text = string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetString("DeveloperRuntimePathFormat"), view.RuntimePath);
        RuntimeLogPathText.Text = string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetString("DeveloperRuntimeLogPathFormat"), view.RuntimeLogPath);
        TraceDiagnosticsText.Text = view.TraceDiagnostics.Count == 0
            ? ResourceLoader.GetString("DeveloperNoTraceDiagnostics")
            : string.Join(Environment.NewLine, view.TraceDiagnostics);
        DocumentList.SelectedIndex = view.Documents.Count > 0 ? 0 : -1;
        ApplyDetailTab(_activeDetailTab);
    }

    private void ClearSelection(string message)
    {
        _currentView = null;
        SelectedNpcText.Text = "-";
        SessionIdText.Text = "-";
        TraceIdText.Text = "-";
        NpcStateText.Text = message;
        ChannelText.Text = "-";
        DocumentList.ItemsSource = Array.Empty<NpcDeveloperDocument>();
        TraceList.ItemsSource = Array.Empty<NpcDeveloperTraceEvent>();
        ModelReplyList.ItemsSource = Array.Empty<NpcDeveloperModelReply>();
        ToolCallList.ItemsSource = Array.Empty<NpcDeveloperToolCall>();
        DelegationList.ItemsSource = Array.Empty<NpcDeveloperDelegation>();
        TodoList.ItemsSource = Array.Empty<NpcDeveloperTodo>();
        DocumentPreviewText.Text = message;
        DocumentPathText.Text = "";
        TraceEmptyText.Text = message;
        DetailEmptyText.Text = message;
        DetailEmptyText.Visibility = Visibility.Visible;
        RuntimePathText.Text = "";
        RuntimeLogPathText.Text = "";
        TraceDiagnosticsText.Text = "";
    }

    private void DocumentList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DocumentList.SelectedItem is not NpcDeveloperDocument document)
        {
            DocumentPathText.Text = "";
            DocumentPreviewText.Text = ResourceLoader.GetString("DeveloperNoDocumentSelected");
            return;
        }

        DocumentPathText.Text = document.Path;
        DocumentPreviewText.Text = string.IsNullOrWhiteSpace(document.Content)
            ? document.Status
            : document.Content;
    }

    private void DetailTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tag)
            ApplyDetailTab(tag);
    }

    private void ApplyDetailTab(string tag)
    {
        _activeDetailTab = tag;
        SetButtonActive(DetailModelButton, tag == "model");
        SetButtonActive(DetailToolButton, tag == "tools");
        SetButtonActive(DetailDelegationButton, tag == "delegation");
        SetButtonActive(DetailTodoButton, tag == "todos");
        SetButtonActive(DetailLogsButton, tag == "logs");

        ModelReplyList.Visibility = tag == "model" ? Visibility.Visible : Visibility.Collapsed;
        ToolCallList.Visibility = tag == "tools" ? Visibility.Visible : Visibility.Collapsed;
        DelegationList.Visibility = tag == "delegation" ? Visibility.Visible : Visibility.Collapsed;
        TodoList.Visibility = tag == "todos" ? Visibility.Visible : Visibility.Collapsed;
        LogsPanel.Visibility = tag == "logs" ? Visibility.Visible : Visibility.Collapsed;

        DetailEmptyText.Text = GetActiveEmptyState(tag);
        DetailEmptyText.Visibility = string.IsNullOrWhiteSpace(DetailEmptyText.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private string GetActiveEmptyState(string tag)
    {
        if (_currentView is null)
            return ResourceLoader.GetString("DeveloperNoNpcEmptyState");

        return tag switch
        {
            "model" => _currentView.ModelReplyEmptyState,
            "tools" => _currentView.ToolCallEmptyState,
            "delegation" => _currentView.DelegationEmptyState,
            "todos" => _currentView.TodoEmptyState,
            _ => ""
        };
    }

    private static void SetButtonActive(Button button, bool active)
    {
        button.Background = active
            ? Application.Current.Resources["AppAccentGradientBrush"] as Brush
            : Application.Current.Resources["AppInsetBrush"] as Brush;
        button.Foreground = active
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 22, 17, 13))
            : Application.Current.Resources["AppTextSecondaryBrush"] as Brush;
        button.BorderBrush = active
            ? null
            : Application.Current.Resources["AppStrokeBrush"] as Brush;
    }
}
