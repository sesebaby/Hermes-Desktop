using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Hermes.Agent.Permissions;
using Hermes.Agent.Tools;
using Hermes.Agent.Transcript;
using Microsoft.Extensions.Logging;

namespace HermesDesktop.Services;

/// <summary>
/// Pure C# chat service — bridges the WinUI frontend to the Hermes Agent core.
/// No Python sidecar. Direct in-process agent execution.
/// </summary>
internal sealed class HermesChatService : IDisposable
{
    private readonly Agent _agent;
    private readonly IChatClient _chatClient;
    private readonly TranscriptStore _transcriptStore;
    private readonly PermissionManager _permissionManager;
    private readonly WorkspacePermissionRuleStore _permissionRuleStore;
    private readonly ICronScheduler _cronScheduler;
    private readonly ILogger<HermesChatService> _logger;

    private Session? _currentSession;
    private CancellationTokenSource? _streamCts;
    private bool _disposed;

    public HermesChatService(
        Agent agent,
        IChatClient chatClient,
        TranscriptStore transcriptStore,
        PermissionManager permissionManager,
        WorkspacePermissionRuleStore permissionRuleStore,
        ICronScheduler cronScheduler,
        ILogger<HermesChatService> logger)
    {
        _agent = agent;
        _chatClient = chatClient;
        _transcriptStore = transcriptStore;
        _permissionManager = permissionManager;
        _permissionRuleStore = permissionRuleStore;
        _cronScheduler = cronScheduler;
        _logger = logger;
        CurrentPermissionMode = _permissionManager.Mode;
        _cronScheduler.TaskDue += OnCronTaskDue;
    }

    public string? CurrentSessionId => _currentSession?.Id;
    public Session? CurrentSession => _currentSession;
    public PermissionMode CurrentPermissionMode { get; private set; } = PermissionMode.Default;
    public event Action<ScheduledChatMessage>? ScheduledMessageReceived;

    // ── Health Check ──

    public async Task<(bool IsHealthy, string Detail)> CheckHealthAsync(CancellationToken ct)
    {
        try
        {
            var messages = new[] { new Message { Role = "user", Content = "Respond with only: OK" } };
            var response = await _chatClient.CompleteAsync(messages, ct);
            return !string.IsNullOrEmpty(response)
                ? (true, "Connected to LLM")
                : (false, "Empty response from LLM");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ── Send (blocking, full response) ──

    public async Task<HermesChatReply> SendAsync(string message, CancellationToken ct)
    {
        EnsureSession();
        var messageCountBefore = _currentSession!.Messages.Count;

        try
        {
            var response = await _agent.ChatAsync(message, _currentSession, ct);

            // Persist all new messages (user + tool calls + assistant)
            await PersistNewMessagesAsync(messageCountBefore);

            _logger.LogInformation("Chat reply for session {SessionId}: {Length} chars", _currentSession.Id, response.Length);
            return new HermesChatReply(response, _currentSession.Id);
        }
        catch (OperationCanceledException)
        {
            await PersistNewMessagesAsync(messageCountBefore);
            throw;
        }
        catch (Exception ex)
        {
            // Persist whatever was added before the failure (at minimum the user message)
            await PersistNewMessagesAsync(messageCountBefore);
            _logger.LogWarning(ex, "Chat send failed for session {SessionId}", _currentSession.Id);
            throw;
        }
    }

    private async Task PersistNewMessagesAsync(int fromIndex)
    {
        for (var i = fromIndex; i < _currentSession!.Messages.Count; i++)
        {
            await _transcriptStore.SaveMessageAsync(_currentSession.Id, _currentSession.Messages[i], CancellationToken.None);
        }
    }

    // ── Stream (structured events: tokens + thinking) ──

    public async IAsyncEnumerable<ChatStreamEvent> StreamStructuredAsync(
        string message,
        [EnumeratorCancellation] CancellationToken ct)
    {
        EnsureSession();
        _streamCts?.Dispose();
        _streamCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var fullResponse = new System.Text.StringBuilder();
        try
        {
            await foreach (var evt in _agent.StreamChatAsync(message, _currentSession!, _streamCts.Token))
            {
                switch (evt)
                {
                    case Hermes.Agent.LLM.StreamEvent.TokenDelta td:
                        // Tool-calling status messages (e.g. "[Calling tool: bash]") are
                        // informational — show in UI but don't accumulate into the saved response
                        if (td.Text.StartsWith("\n[Calling tool:") && td.Text.TrimEnd().EndsWith("]"))
                        {
                            yield return new ChatStreamEvent(ChatStreamEventType.Thinking, td.Text.Trim());
                        }
                        else
                        {
                            fullResponse.Append(td.Text);
                            yield return new ChatStreamEvent(ChatStreamEventType.Token, td.Text);
                        }
                        break;

                    case Hermes.Agent.LLM.StreamEvent.ThinkingDelta tk:
                        yield return new ChatStreamEvent(ChatStreamEventType.Thinking, tk.Text);
                        break;

                    case Hermes.Agent.LLM.StreamEvent.StreamError err:
                        yield return new ChatStreamEvent(ChatStreamEventType.Error, err.Error.Message);
                        break;
                }
            }
        }
        finally
        {
            // Save response (partial or complete) — handles normal completion and cancellation.
            // Always save to avoid dangling user messages in the session, even if response is empty.
            // Guard against null — session may have been deleted/reset during streaming.
            if (_currentSession is not null &&
                _currentSession.Messages.LastOrDefault()?.Role != "assistant")
            {
                var assistantMsg = new Message { Role = "assistant", Content = fullResponse.ToString() };
                _currentSession.AddMessage(assistantMsg);
                await _transcriptStore.SaveMessageAsync(_currentSession.Id, assistantMsg, CancellationToken.None);
            }
        }
    }

    // ── Legacy string streaming (kept for backwards compatibility) ──

    public async IAsyncEnumerable<string> StreamAsync(
        string message,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var evt in StreamStructuredAsync(message, ct))
        {
            if (evt.Type == ChatStreamEventType.Token)
                yield return evt.Text;
        }
    }

    // ── Cancel ──

    public void CancelStream()
    {
        _streamCts?.Cancel();
        _logger.LogInformation("Stream cancelled for session {SessionId}", _currentSession?.Id);
    }

    // ── Session Management ──

    public void EnsureSession()
    {
        if (_currentSession is not null) return;
        _currentSession = new Session
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Platform = "desktop"
        };
        _logger.LogInformation("Created new session {SessionId}", _currentSession.Id);
    }

    public async Task LoadSessionAsync(string sessionId, CancellationToken ct)
    {
        if (_currentSession is not null &&
            !string.Equals(_currentSession.Id, sessionId, StringComparison.OrdinalIgnoreCase))
        {
            await EndCurrentSessionAsync(ct);
        }

        var messages = await _transcriptStore.LoadSessionAsync(sessionId, ct);
        _currentSession = new Session
        {
            Id = sessionId,
            Platform = "desktop"
        };
        foreach (var msg in messages)
            _currentSession.AddMessage(msg);

        _logger.LogInformation("Loaded session {SessionId} with {Count} messages", sessionId, messages.Count);
    }

    public async Task ResetConversationAsync(CancellationToken ct = default)
    {
        await EndCurrentSessionAsync(ct);
        _currentSession = null;
        _streamCts?.Cancel();
        _streamCts?.Dispose();
        _streamCts = null;
    }

    public void ResetConversation()
        => ResetConversationAsync(CancellationToken.None).GetAwaiter().GetResult();

    // ── Permission Mode ──

    public void SetPermissionMode(PermissionMode mode)
    {
        _permissionManager.Mode = mode;
        CurrentPermissionMode = mode;
    }

    public void ClearRememberedPermissionsForWorkspace()
    {
        _permissionManager.ClearAlwaysAllowRules();
        _permissionRuleStore.ClearAlwaysAllowRules();
    }

    public void ClearRememberedWorkspacePermissions() => ClearRememberedPermissionsForWorkspace();

    // ── Tool Registration ──

    public void RegisterTool(ITool tool) => _agent.RegisterTool(tool);

    // ── Scheduled reminders ──

    private void OnCronTaskDue(object? sender, CronTaskDueEventArgs e)
    {
        _ = HandleCronTaskDueAsync(e.Task);
    }

    private async Task HandleCronTaskDueAsync(CronTask task)
    {
        try
        {
            var sessionId = task.SessionId;
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                if (_currentSession is null)
                {
                    _logger.LogWarning("Cron task {TaskId} fired without a target session.", task.Id);
                    return;
                }

                sessionId = _currentSession.Id;
            }

            var content = FormatScheduledReminder(task.Prompt);
            var message = new Message { Role = "assistant", Content = content };
            var isCurrentSession = _currentSession is not null &&
                string.Equals(_currentSession.Id, sessionId, StringComparison.OrdinalIgnoreCase);

            if (isCurrentSession)
                _currentSession!.AddMessage(message);

            await _transcriptStore.SaveMessageAsync(sessionId, message, CancellationToken.None);
            ScheduledMessageReceived?.Invoke(new ScheduledChatMessage(sessionId, content, isCurrentSession));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deliver scheduled cron task {TaskId}", task.Id);
        }
    }

    private static string FormatScheduledReminder(string prompt)
    {
        var trimmed = prompt.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return "计划提醒已触发。";

        return trimmed.StartsWith("提醒", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"提醒：{trimmed}";
    }

    // ── Dispose ──

    public void Dispose()
    {
        if (_disposed) return;
        _cronScheduler.TaskDue -= OnCronTaskDue;
        try
        {
            Task.Run(() => EndCurrentSessionAsync(CancellationToken.None))
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to end session during chat service disposal");
        }
        _streamCts?.Dispose();
        _disposed = true;
    }

    private async Task EndCurrentSessionAsync(CancellationToken ct)
    {
        if (_currentSession is null)
            return;

        try
        {
            await _agent.EndSessionAsync(_currentSession, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to end session {SessionId}", _currentSession.Id);
        }
    }

    internal sealed record HermesChatReply(string Response, string SessionId);
    internal sealed record ScheduledChatMessage(string SessionId, string Content, bool IsCurrentSession);
}

// ── Structured stream events for UI consumption ──

internal enum ChatStreamEventType
{
    Token,
    Thinking,
    Error
}

internal sealed record ChatStreamEvent(ChatStreamEventType Type, string Text);
