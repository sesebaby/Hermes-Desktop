using Hermes.Agent.Runtime;
using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
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
    private readonly StardewNpcDebugActionService? _stardewNpcDebugActions = App.Services.GetService<StardewNpcDebugActionService>();
    private readonly StardewAutonomyTickDebugService? _stardewAutonomyTickDebug = App.Services.GetService<StardewAutonomyTickDebugService>();

    private NpcDeveloperInspectorView? _currentView;
    private NpcRuntimeItem? _currentItem;
    private NpcRuntimeSnapshot? _currentSnapshot;
    private NpcDeveloperInspectorRequest _currentRequest = NpcDeveloperInspectorRequest.Empty;
    private string _activeDetailTab = "model";
    private bool _operationInProgress;

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
            ClearSelection(ResourceLoader.GetString("DeveloperRuntimeUnavailable"));
            RuntimeCountText.Text = "0";
            BridgeHealthText.Text = ResourceLoader.GetString("DeveloperRuntimeUnavailable");
            LastErrorText.Text = "-";
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

        _currentItem = item;
        var snapshot = _supervisor.Snapshot()
            .FirstOrDefault(candidate =>
                string.Equals(candidate.NpcId, item.NpcId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.SessionId, item.SessionId, StringComparison.OrdinalIgnoreCase));
        if (snapshot is null)
        {
            _currentSnapshot = null;
            ClearSelection(ResourceLoader.GetString("DeveloperRuntimeSnapshotMissing"));
            SelectedNpcText.Text = string.IsNullOrWhiteSpace(item.DisplayName) ? item.NpcId : item.DisplayName;
            SessionIdText.Text = string.IsNullOrWhiteSpace(item.SessionId) ? "-" : item.SessionId;
            TraceIdText.Text = string.IsNullOrWhiteSpace(item.LastTraceId) ? "-" : item.LastTraceId;
            NpcStateText.Text = item.State;
            UpdateActionControls();
            return;
        }

        try
        {
            var runtimeRoot = _workspaceService.RuntimeRoot;
            _currentSnapshot = snapshot;
            _currentView = await _inspectorService.InspectAsync(snapshot, runtimeRoot, _currentRequest, CancellationToken.None);
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
        ContextBlockList.ItemsSource = view.ContextBlocks;
        RuntimePathText.Text = string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetString("DeveloperRuntimePathFormat"), view.RuntimePath);
        RuntimeLogPathText.Text = string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetString("DeveloperRuntimeLogPathFormat"), view.RuntimeLogPath);
        TraceDiagnosticsText.Text = view.TraceDiagnostics.Count == 0
            ? ResourceLoader.GetString("DeveloperNoTraceDiagnostics")
            : string.Join(Environment.NewLine, view.TraceDiagnostics);
        DocumentList.SelectedIndex = view.Documents.Count > 0 ? 0 : -1;
        UpdateActionControls();
        ApplyDetailTab(_activeDetailTab);
    }

    private void ClearSelection(string message)
    {
        _currentItem = null;
        _currentSnapshot = null;
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
        ContextBlockList.ItemsSource = Array.Empty<NpcDeveloperContextBlock>();
        DocumentPreviewText.Text = message;
        DocumentPathText.Text = "";
        TraceEmptyText.Text = message;
        DetailEmptyText.Text = message;
        DetailEmptyText.Visibility = Visibility.Visible;
        RuntimePathText.Text = "";
        RuntimeLogPathText.Text = "";
        TraceDiagnosticsText.Text = "";
        DebugActionStatusText.Text = message;
        DebugActionResultText.Text = ResourceLoader.GetString("DeveloperDebugActionResultInitial/Text");
        DiagnosticsExportText.Text = ResourceLoader.GetString("DeveloperDiagnosticsExportInitial/Text");
        UpdateActionControls();
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

    private async void DebugReposition_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteDebugCommandAsync(
            ResourceLoader.GetString("DeveloperDebugRepositionActionName"),
            ct => _stardewNpcDebugActions!.RepositionToTownAsync(_currentSnapshot!.BodyBinding!, ct));
    }

    private async void DebugSpeak_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteDebugCommandAsync(
            ResourceLoader.GetString("DeveloperDebugSpeakActionName"),
            ct => _stardewNpcDebugActions!.SpeakAsync(
                _currentSnapshot!.BodyBinding!,
                ResourceLoader.GetString("DeveloperDebugSpeakText"),
                ct));
    }

    private async void DebugTick_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteDebugTickAsync(ResourceLoader.GetString("DeveloperDebugTickActionName"));
    }

    private async void ApplyTraceFilter_Click(object sender, RoutedEventArgs e)
    {
        _currentRequest = BuildInspectorRequest();
        await ReloadCurrentNpcAsync();
    }

    private async void ClearTraceFilter_Click(object sender, RoutedEventArgs e)
    {
        TraceIdFilterBox.Text = "";
        EventTypeFilterBox.Text = "";
        CommandIdFilterBox.Text = "";
        ToolNameFilterBox.Text = "";
        ErrorCodeFilterBox.Text = "";
        KeywordFilterBox.Text = "";
        _currentRequest = NpcDeveloperInspectorRequest.Empty;
        await ReloadCurrentNpcAsync();
    }

    private async void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        if (_workspaceService is null || _inspectorService is null || _currentSnapshot is null)
        {
            SetStatusText(DiagnosticsExportText, ResourceLoader.GetString("DeveloperDiagnosticsExportUnavailable"), success: false);
            return;
        }

        _operationInProgress = true;
        UpdateActionControls();
        SetStatusText(DiagnosticsExportText, ResourceLoader.GetString("DeveloperDiagnosticsExportRunning"), success: null);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var export = await _inspectorService.ExportDiagnosticsAsync(
                _currentSnapshot,
                _workspaceService.RuntimeRoot,
                _currentRequest,
                cts.Token);
            SetStatusText(
                DiagnosticsExportText,
                string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetString("DeveloperDiagnosticsExportSuccessFormat"), export.ZipPath),
                success: true);
        }
        catch (Exception ex)
        {
            SetStatusText(
                DiagnosticsExportText,
                string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetString("DeveloperDiagnosticsExportFailedFormat"), ex.Message),
                success: false);
        }
        finally
        {
            _operationInProgress = false;
            UpdateActionControls();
        }
    }

    private void ApplyDetailTab(string tag)
    {
        _activeDetailTab = tag;
        SetButtonActive(DetailModelButton, tag == "model");
        SetButtonActive(DetailToolButton, tag == "tools");
        SetButtonActive(DetailDelegationButton, tag == "delegation");
        SetButtonActive(DetailTodoButton, tag == "todos");
        SetButtonActive(DetailLogsButton, tag == "logs");
        SetButtonActive(DetailContextButton, tag == "context");

        ModelReplyList.Visibility = tag == "model" ? Visibility.Visible : Visibility.Collapsed;
        ToolCallList.Visibility = tag == "tools" ? Visibility.Visible : Visibility.Collapsed;
        DelegationList.Visibility = tag == "delegation" ? Visibility.Visible : Visibility.Collapsed;
        TodoList.Visibility = tag == "todos" ? Visibility.Visible : Visibility.Collapsed;
        LogsPanel.Visibility = tag == "logs" ? Visibility.Visible : Visibility.Collapsed;
        ContextBlockList.Visibility = tag == "context" ? Visibility.Visible : Visibility.Collapsed;

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
            "context" => _currentView.ContextBlocks.Count == 0 ? ResourceLoader.GetString("DeveloperContextBlockEmptyState") : "",
            _ => ""
        };
    }

    private NpcDeveloperInspectorRequest BuildInspectorRequest()
        => new(new NpcDeveloperTraceFilter(
            TraceId: NormalizeFilterText(TraceIdFilterBox.Text),
            EventType: NormalizeFilterText(EventTypeFilterBox.Text),
            CommandId: NormalizeFilterText(CommandIdFilterBox.Text),
            ToolName: NormalizeFilterText(ToolNameFilterBox.Text),
            ErrorCode: NormalizeFilterText(ErrorCodeFilterBox.Text),
            Keyword: NormalizeFilterText(KeywordFilterBox.Text)));

    private async Task ReloadCurrentNpcAsync()
    {
        var item = _currentItem ?? NpcList.SelectedItem as NpcRuntimeItem;
        if (item is not null)
            await LoadNpcAsync(item);
    }

    private async Task ExecuteDebugCommandAsync(string actionName, Func<CancellationToken, Task<GameCommandResult>> action)
    {
        if (!CanRunDebugAction(_stardewNpcDebugActions is not null))
            return;

        if (!await ConfirmDebugActionAsync(actionName))
            return;

        _operationInProgress = true;
        UpdateActionControls();
        SetStatusText(
            DebugActionResultText,
            string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetString("DeveloperDebugActionRunningFormat"), actionName, _currentSnapshot!.DisplayName),
            success: null);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var result = await action(cts.Token);
            if (result.Accepted)
            {
                SetStatusText(
                    DebugActionResultText,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        ResourceLoader.GetString("DeveloperDebugActionSuccessFormat"),
                        actionName,
                        result.CommandId,
                        result.TraceId),
                    success: true);
            }
            else
            {
                SetStatusText(
                    DebugActionResultText,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        ResourceLoader.GetString("DeveloperDebugActionFailedFormat"),
                        actionName,
                        result.FailureReason ?? result.Status),
                    success: false);
            }

            await ReloadCurrentNpcAsync();
        }
        catch (Exception ex)
        {
            SetStatusText(
                DebugActionResultText,
                string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetString("DeveloperDebugActionFailedFormat"), actionName, ex.Message),
                success: false);
        }
        finally
        {
            _operationInProgress = false;
            UpdateActionControls();
        }
    }

    private async Task ExecuteDebugTickAsync(string actionName)
    {
        if (!CanRunDebugAction(_stardewAutonomyTickDebug is not null))
            return;

        if (!await ConfirmDebugActionAsync(actionName))
            return;

        _operationInProgress = true;
        UpdateActionControls();
        SetStatusText(
            DebugActionResultText,
            string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetString("DeveloperDebugActionRunningFormat"), actionName, _currentSnapshot!.DisplayName),
            success: null);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var result = await _stardewAutonomyTickDebug!.RunOneTickAsync(_currentSnapshot!.NpcId, cts.Token);
            if (result.Success)
            {
                SetStatusText(
                    DebugActionResultText,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        ResourceLoader.GetString("DeveloperDebugTickSuccessFormat"),
                        result.TraceId,
                        result.ObservationFacts,
                        result.EventFacts),
                    success: true);
            }
            else
            {
                SetStatusText(
                    DebugActionResultText,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        ResourceLoader.GetString("DeveloperDebugActionFailedFormat"),
                        actionName,
                        result.FailureReason ?? "unknown_error"),
                    success: false);
            }

            await ReloadCurrentNpcAsync();
        }
        catch (Exception ex)
        {
            SetStatusText(
                DebugActionResultText,
                string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetString("DeveloperDebugActionFailedFormat"), actionName, ex.Message),
                success: false);
        }
        finally
        {
            _operationInProgress = false;
            UpdateActionControls();
        }
    }

    private bool CanRunDebugAction(bool serviceAvailable)
    {
        if (_currentSnapshot is null)
        {
            SetStatusText(DebugActionResultText, ResourceLoader.GetString("DeveloperDebugNoSelection"), success: false);
            return false;
        }

        if (!IsBridgeReady(_currentSnapshot))
        {
            SetStatusText(DebugActionResultText, ResourceLoader.GetString("DeveloperDebugBridgeUnavailable"), success: false);
            return false;
        }

        if (!serviceAvailable)
        {
            SetStatusText(DebugActionResultText, ResourceLoader.GetString("DeveloperDebugServiceUnavailable"), success: false);
            return false;
        }

        return true;
    }

    private async Task<bool> ConfirmDebugActionAsync(string actionName)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = ResourceLoader.GetString("DeveloperDebugConfirmTitle"),
            Content = string.Format(
                CultureInfo.CurrentCulture,
                ResourceLoader.GetString("DeveloperDebugConfirmContentFormat"),
                actionName,
                _currentSnapshot?.DisplayName ?? _currentSnapshot?.NpcId ?? "-"),
            PrimaryButtonText = ResourceLoader.GetString("DeveloperDebugConfirmPrimaryButton"),
            CloseButtonText = ResourceLoader.GetString("DeveloperDebugConfirmCancelButton"),
            DefaultButton = ContentDialogButton.Close
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void UpdateActionControls()
    {
        var hasSelection = _currentSnapshot is not null;
        var bridgeReady = hasSelection && IsBridgeReady(_currentSnapshot!);
        var hasDebugActionService = _stardewNpcDebugActions is not null;
        var hasTickService = _stardewAutonomyTickDebug is not null;
        var canUseDebugCommands = !_operationInProgress && hasSelection && bridgeReady && hasDebugActionService;
        DebugRepositionButton.IsEnabled = canUseDebugCommands;
        DebugSpeakButton.IsEnabled = canUseDebugCommands;
        DebugTickButton.IsEnabled = !_operationInProgress && hasSelection && bridgeReady && hasTickService;
        ApplyTraceFilterButton.IsEnabled = !_operationInProgress && hasSelection;
        ClearTraceFilterButton.IsEnabled = !_operationInProgress && hasSelection;
        ExportDiagnosticsButton.IsEnabled = !_operationInProgress && hasSelection && _inspectorService is not null;

        DebugActionStatusText.Text = _operationInProgress
            ? ResourceLoader.GetString("DeveloperDebugActionInProgress")
            : !hasSelection
                ? ResourceLoader.GetString("DeveloperDebugNoSelection")
                : !bridgeReady
                    ? ResourceLoader.GetString("DeveloperDebugBridgeUnavailable")
                    : !hasDebugActionService && !hasTickService
                        ? ResourceLoader.GetString("DeveloperDebugServiceUnavailable")
                        : !hasDebugActionService
                            ? ResourceLoader.GetString("DeveloperDebugActionServiceUnavailable")
                            : !hasTickService
                                ? ResourceLoader.GetString("DeveloperDebugTickServiceUnavailable")
                                : ResourceLoader.GetString("DeveloperDebugActionReady");
    }

    private static bool IsBridgeReady(NpcRuntimeSnapshot snapshot)
        => !string.IsNullOrWhiteSpace(snapshot.CurrentBridgeKey) &&
           !string.Equals(snapshot.PauseReason, StardewBridgeErrorCodes.BridgeUnavailable, StringComparison.OrdinalIgnoreCase) &&
           !string.Equals(snapshot.PauseReason, StardewBridgeErrorCodes.BridgeStaleDiscovery, StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeFilterText(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void SetStatusText(TextBlock target, string message, bool? success)
    {
        target.Text = message;
        target.Foreground = success switch
        {
            true => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 34, 197, 94)),
            false => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68)),
            _ => Application.Current.Resources["AppTextSecondaryBrush"] as Brush
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
