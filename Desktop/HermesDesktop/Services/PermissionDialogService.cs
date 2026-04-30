using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace HermesDesktop.Services;

public enum PermissionPromptDecision
{
    Deny = 0,
    AllowOnce = 1,
    AlwaysAllowTool = 2,
}

/// <summary>
/// Renders the WinUI permission prompt that fires when the agent's
/// PermissionManager returns <c>PermissionBehavior.Ask</c>. Extracted from
/// <c>App.xaml.cs</c> so the code-behind stays focused on lifecycle / DI
/// orchestration and the dialog body / argument formatting / dispatcher
/// safety live in one auditable place.
///
/// Responsibilities:
/// <list type="number">
///   <item>Marshal to the UI thread via the owning window's DispatcherQueue.</item>
///   <item>Format the raw tool arguments for human review.</item>
///   <item>Build the ContentDialog body with a selectable monospace command
///         block so technical users can audit (and Ctrl-C) what the agent
///         is about to run before approving.</item>
///   <item>Defend against dispatcher-shutdown races so the agent loop
///         never hangs awaiting a TaskCompletionSource that nobody is going
///         to complete.</item>
/// </list>
///
/// This class is stateless beyond the captured window + logger references
/// and is safe to instantiate per call, but it can also be reused across
/// many permission prompts within the same window's lifetime.
/// </summary>
public sealed class PermissionDialogService
{
    private readonly Window _window;
    private readonly ILogger? _logger;

    public PermissionDialogService(Window window, ILogger? logger = null)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _logger = logger;
    }

    /// <summary>
    /// Backward-compatible allow/deny API. Prefer <see cref="ShowPermissionDecisionAsync"/>
    /// for callers that need richer decisions.
    /// </summary>
    public async Task<bool> ShowPermissionDialogAsync(string message, string toolName, string? toolArguments)
    {
        var decision = await ShowPermissionDecisionAsync(message, toolName, toolArguments);
        return decision is PermissionPromptDecision.AllowOnce or PermissionPromptDecision.AlwaysAllowTool;
    }

    /// <summary>
    /// Show a modal permission prompt for the given tool and return a rich
    /// decision (deny / allow once / always allow this tool). Returns deny
    /// on any failure (window gone, dispatcher shutting down, dialog throws)
    /// so the agent loop always gets a definite answer instead of hanging on
    /// an uncompleted task.
    /// </summary>
    /// <param name="message">
    /// Human-readable explanation of why permission is being requested.
    /// Comes from <c>PermissionDecision.Message</c> on the agent side.
    /// </param>
    /// <param name="toolName">
    /// The tool the agent wants to invoke (e.g. <c>"todo"</c>, <c>"session_search"</c>).
    /// Surfaced in both the dialog title and passed to
    /// <see cref="FormatToolArgumentsForPrompt"/> for formatting.
    /// </param>
    /// <param name="toolArguments">
    /// The raw JSON arguments string from the model's tool call. May be
    /// null/empty if the host doesn't have it, in which case the Arguments
    /// section is omitted entirely.
    /// </param>
    public Task<PermissionPromptDecision> ShowPermissionDecisionAsync(
        string message,
        string toolName,
        string? toolArguments)
    {
        // RunContinuationsAsynchronously: when the dialog completes on the UI
        // thread, the awaiting agent loop must NOT pick up its continuation
        // synchronously on that same UI thread — otherwise the next iteration
        // of the agent loop would run inside the dispatcher callback and
        // create a re-entrancy hazard for subsequent permission prompts.
        var tcs = new TaskCompletionSource<PermissionPromptDecision>(TaskCreationOptions.RunContinuationsAsynchronously);

        // TryEnqueue can fail (return false) if the dispatcher has already
        // started shutting down — e.g. the user closed the window while the
        // agent loop was mid-iteration. If we ignore the return value the
        // queued lambda never runs, tcs is never completed, and the agent
        // loop hangs forever on `await tcs.Task`. Capture it and deny by
        // default if dispatch failed.
        var enqueued = _window.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                var dialog = BuildDialog(message, toolName, toolArguments);
                var result = await dialog.ShowAsync();
                var decision = result switch
                {
                    ContentDialogResult.Primary => PermissionPromptDecision.AllowOnce,
                    ContentDialogResult.Secondary => PermissionPromptDecision.AlwaysAllowTool,
                    _ => PermissionPromptDecision.Deny
                };
                tcs.TrySetResult(decision);
            }
            catch (Exception ex)
            {
                if (_logger is not null)
                    _logger.LogWarning(ex,
                        "Permission prompt dialog failed for tool {Tool}; denying",
                        toolName);
                else
                    Debug.WriteLine($"Permission prompt dialog failed for tool {toolName}: {ex}");
                tcs.TrySetResult(PermissionPromptDecision.Deny);
            }
        });

        if (!enqueued)
        {
            if (_logger is not null)
                _logger.LogWarning(
                    "DispatcherQueue.TryEnqueue refused permission prompt for tool {Tool}; denying by default",
                    toolName);
            else
                Debug.WriteLine(
                    $"DispatcherQueue.TryEnqueue refused permission prompt for tool {toolName}; denying by default");
            tcs.TrySetResult(PermissionPromptDecision.Deny);
        }

        return tcs.Task;
    }

    /// <summary>
    /// Build the ContentDialog body. Vertical StackPanel containing the
    /// human-readable message followed (when args are present) by a
    /// "Arguments" header and a read-only monospace TextBox the user can
    /// Ctrl-A / Ctrl-C from. DefaultButton is Close so an accidental Enter
    /// keypress on the dialog denies rather than approves — never let
    /// muscle memory grant permissions.
    /// </summary>
    private ContentDialog BuildDialog(string message, string toolName, string? toolArguments)
    {
        var body = new StackPanel
        {
            Spacing = 12,
            Orientation = Orientation.Vertical
        };
        body.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.WrapWholeWords,
            IsTextSelectionEnabled = true
        });

        var formattedArguments = FormatToolArgumentsForPrompt(toolName, toolArguments);
        if (!string.IsNullOrEmpty(formattedArguments))
        {
            body.Children.Add(new TextBlock
            {
                Text = "Arguments",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Opacity = 0.7,
                Margin = new Thickness(0, 4, 0, 0)
            });
            // Read-only multiline TextBox rather than another TextBlock: the
            // TextBox gives the user a familiar Ctrl+A / Ctrl+C affordance,
            // shows a caret on focus so it's obvious the field is selectable,
            // and scrolls gracefully when the command is long.
            body.Children.Add(new TextBox
            {
                Text = formattedArguments,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                MinHeight = 60,
                MaxHeight = 240
            });
        }

        return new ContentDialog
        {
            Title = $"Permission Required: {toolName}",
            Content = body,
            PrimaryButtonText = "Allow once",
            SecondaryButtonText = "Always allow tool",
            CloseButtonText = "Deny",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = _window.Content.XamlRoot
        };
    }

    /// <summary>
    /// Format raw tool arguments for display in the permission dialog. Parse
    /// JSON when possible and pretty-print it for readability. Returns an
    /// empty string when there is nothing useful to display so the caller can
    /// omit the Arguments section entirely.
    /// </summary>
    /// <remarks>
    /// Internal so it can be unit-tested. Static so it has no instance state
    /// to mock.
    /// </remarks>
    internal static string FormatToolArgumentsForPrompt(string toolName, string? toolArguments)
    {
        if (string.IsNullOrWhiteSpace(toolArguments))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(toolArguments);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return toolArguments;
        }
    }
}
