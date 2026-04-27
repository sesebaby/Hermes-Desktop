namespace Hermes.Agent.Search;

using System.Text;
using System.Text.RegularExpressions;
using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Hermes.Agent.Transcript;
using Microsoft.Extensions.Logging;

public sealed class TranscriptRecallService
{
    private static readonly Regex TokenRegex = new(@"[\p{L}\p{N}_-]{3,}", RegexOptions.Compiled);
    private static readonly Regex MemoryContextBlockRegex = new(
        @"<memory[-_]context\b[^>]*>.*?</memory[-_]context>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex RecalledMemorySystemNoteRegex = new(
        @"\[System note:\s*The following is recalled memory context,\s*NOT new user input\..*?\]\s*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "what", "when", "where", "which", "who", "why", "how", "did", "was", "were", "the", "and",
        "you", "me", "my", "our", "before", "earlier", "previous", "previously", "remember",
        "ask", "asked", "use", "used", "tell", "about"
    };

    private readonly TranscriptStore _transcripts;
    private readonly ILogger<TranscriptRecallService> _logger;
    private readonly SessionSearchIndex? _index;
    private readonly IChatClient? _summaryClient;
    private int _backfillAttempted;

    public TranscriptRecallService(
        TranscriptStore transcripts,
        ILogger<TranscriptRecallService> logger,
        SessionSearchIndex? index = null,
        IChatClient? summaryClient = null)
    {
        _transcripts = transcripts;
        _logger = logger;
        _index = index;
        _summaryClient = summaryClient;
    }

    public async Task<TranscriptRecallResult> RecallAsync(
        string query,
        string currentSessionId,
        int maxItems = 6,
        int maxChars = 4000,
        CancellationToken ct = default)
    {
        var items = await SearchAsync(query, currentSessionId, maxItems, includeRecentFallback: IsRecallIntent(query), ct);
        if (items.Count == 0)
        {
            return TranscriptRecallResult.Empty("no matching prior transcript messages");
        }

        var context = BuildContext(items, maxChars);
        return new TranscriptRecallResult(
            Attempted: true,
            Injected: !string.IsNullOrWhiteSpace(context),
            Items: items,
            ContextBlock: context,
            EmptyReason: null);
    }

    public async Task<List<TranscriptRecallItem>> SearchAsync(
        string query,
        string? currentSessionId,
        int maxItems = 10,
        bool includeRecentFallback = false,
        CancellationToken ct = default,
        IReadOnlySet<string>? roleFilter = null)
    {
        await EnsureIndexBackfilledAsync(ct);

        if (_index is not null && !string.IsNullOrWhiteSpace(query))
        {
            var indexed = SearchIndex(query, currentSessionId, maxItems, roleFilter);
            if (indexed.Count > 0)
                return indexed;

            var transcriptFallback = await SearchTranscriptsAsync(
                query,
                currentSessionId,
                maxItems,
                includeRecentFallback,
                ct,
                roleFilter);
            return transcriptFallback;
        }

        return await SearchTranscriptsAsync(query, currentSessionId, maxItems, includeRecentFallback, ct, roleFilter);
    }

    private async Task<List<TranscriptRecallItem>> SearchTranscriptsAsync(
        string query,
        string? currentSessionId,
        int maxItems,
        bool includeRecentFallback,
        CancellationToken ct,
        IReadOnlySet<string>? roleFilter)
    {
        var keywords = ExtractKeywords(query);
        var scored = new List<(TranscriptRecallItem Item, int Score)>();
        var recent = new List<TranscriptRecallItem>();

        foreach (var sessionId in _transcripts.GetAllSessionIds())
        {
            ct.ThrowIfCancellationRequested();

            if (string.Equals(sessionId, currentSessionId, StringComparison.OrdinalIgnoreCase))
                continue;

            List<Message> messages;
            try
            {
                messages = await _transcripts.LoadSessionAsync(sessionId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipping unreadable transcript session {SessionId}", sessionId);
                continue;
            }

            foreach (var message in messages.Where(m => IsRecallableRole(m) && RoleMatches(m, roleFilter)))
            {
                var cleanContent = SanitizeContext(message.Content);
                if (string.IsNullOrWhiteSpace(cleanContent))
                    continue;

                var item = new TranscriptRecallItem(
                    sessionId,
                    message.Role,
                    cleanContent,
                    message.Timestamp);

                var score = Score(cleanContent, keywords, query);
                if (score > 0)
                    scored.Add((item, score));

                recent.Add(item);
            }
        }

        var selected = scored
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Item.Timestamp)
            .Select(x => x.Item)
            .Take(maxItems)
            .ToList();

        if (includeRecentFallback && selected.Count < maxItems)
        {
            var existing = selected
                .Select(x => (x.SessionId, x.Role, x.Content, x.Timestamp))
                .ToHashSet();

            selected.AddRange(recent
                .OrderByDescending(x => x.Timestamp)
                .Where(x => !existing.Contains((x.SessionId, x.Role, x.Content, x.Timestamp)))
                .Take(maxItems - selected.Count));
        }

        return selected;
    }

    private List<TranscriptRecallItem> SearchIndex(
        string query,
        string? currentSessionId,
        int maxItems,
        IReadOnlySet<string>? roleFilter)
    {
        if (_index is null)
            return new List<TranscriptRecallItem>();

        var indexedResults = _index.Search(query, Math.Max(maxItems * 5, 20));
        return indexedResults
            .Where(r => !string.Equals(r.SessionId, currentSessionId, StringComparison.OrdinalIgnoreCase))
            .Where(r => string.Equals(r.Role, "user", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(r.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            .Where(r => roleFilter is null || roleFilter.Count == 0 || roleFilter.Contains(r.Role))
            .Select(r => new TranscriptRecallItem(
                r.SessionId,
                r.Role,
                SanitizeContext(r.Content),
                DateTime.TryParse(r.Timestamp, out var ts) ? ts : DateTime.MinValue))
            .Where(r => !string.IsNullOrWhiteSpace(r.Content))
            .Take(maxItems)
            .ToList();
    }

    public async Task<IReadOnlyList<RecentTranscriptSession>> ListRecentSessionsAsync(
        string? currentSessionId = null,
        int limit = 5,
        CancellationToken ct = default)
    {
        var sessions = new List<RecentTranscriptSession>();
        var safeLimit = Math.Clamp(limit, 1, 20);

        foreach (var sessionId in _transcripts.GetAllSessionIds())
        {
            ct.ThrowIfCancellationRequested();
            if (string.Equals(sessionId, currentSessionId, StringComparison.OrdinalIgnoreCase))
                continue;

            List<Message> messages;
            try
            {
                messages = await _transcripts.LoadSessionAsync(sessionId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipping unreadable transcript session {SessionId}", sessionId);
                continue;
            }

            var cleanMessages = messages
                .Select(m => new Message
                {
                    Role = m.Role,
                    Content = SanitizeContext(m.Content),
                    Timestamp = m.Timestamp,
                    ToolCallId = m.ToolCallId,
                    ToolName = m.ToolName,
                    ToolCalls = m.ToolCalls
                })
                .Where(IsRecallableRole)
                .Where(m => !string.IsNullOrWhiteSpace(m.Content))
                .ToList();

            if (cleanMessages.Count == 0)
                continue;

            var firstUser = cleanMessages.FirstOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            var preview = firstUser?.Content ?? cleanMessages[0].Content;
            sessions.Add(new RecentTranscriptSession(
                sessionId,
                cleanMessages.Min(m => m.Timestamp),
                cleanMessages.Max(m => m.Timestamp),
                cleanMessages.Count,
                Truncate(preview, 240)));
        }

        return sessions
            .OrderByDescending(s => s.LastActivityAt)
            .Take(safeLimit)
            .ToList();
    }

    public async Task<IReadOnlyList<TranscriptSessionSummary>> SearchSessionSummariesAsync(
        string query,
        string? currentSessionId = null,
        int maxSessions = 3,
        IReadOnlySet<string>? roleFilter = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<TranscriptSessionSummary>();

        var safeLimit = Math.Clamp(maxSessions, 1, 5);
        var matches = await SearchAsync(
            query,
            currentSessionId,
            maxItems: safeLimit * 12,
            includeRecentFallback: false,
            ct,
            roleFilter);

        var grouped = matches
            .GroupBy(m => m.SessionId)
            .Take(safeLimit)
            .ToList();

        var preparedSessions = new List<PreparedTranscriptSession>();
        foreach (var group in grouped)
        {
            ct.ThrowIfCancellationRequested();

            List<Message> messages;
            try
            {
                messages = await _transcripts.LoadSessionAsync(group.Key, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipping unreadable transcript session {SessionId}", group.Key);
                continue;
            }

            var cleanMessages = messages
                .Select(m => new Message
                {
                    Role = m.Role,
                    Content = SanitizeContext(m.Content),
                    Timestamp = m.Timestamp,
                    ToolCallId = m.ToolCallId,
                    ToolName = m.ToolName,
                    ToolCalls = m.ToolCalls
                })
                .Where(IsRecallableRole)
                .Where(m => !string.IsNullOrWhiteSpace(m.Content))
                .ToList();

            if (cleanMessages.Count == 0)
                continue;

            var conversation = FormatConversation(cleanMessages);
            conversation = TruncateAroundMatches(conversation, query, maxChars: 12000);
            preparedSessions.Add(new PreparedTranscriptSession(
                group.Key,
                cleanMessages.Min(m => m.Timestamp),
                cleanMessages.Max(m => m.Timestamp),
                cleanMessages.Count,
                conversation,
                group.ToList()));
        }

        if (preparedSessions.Count == 0)
            return Array.Empty<TranscriptSessionSummary>();

        var maxConcurrency = Math.Min(3, preparedSessions.Count);
        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var summaryTasks = preparedSessions.Select(async prepared =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var summary = await SummarizeSessionAsync(prepared.Conversation, query, prepared.SessionId, ct);
                return new TranscriptSessionSummary(
                    prepared.SessionId,
                    prepared.StartedAt,
                    prepared.LastActivityAt,
                    prepared.MessageCount,
                    summary,
                    prepared.Matches);
            }
            finally
            {
                semaphore.Release();
            }
        });

        return await Task.WhenAll(summaryTasks);
    }

    public async Task BackfillIndexAsync(CancellationToken ct = default)
    {
        if (_index is null)
            return;

        _index.Clear();
        var indexed = 0;
        foreach (var sessionId in _transcripts.GetAllSessionIds())
        {
            ct.ThrowIfCancellationRequested();
            List<Message> messages;
            try
            {
                messages = await _transcripts.LoadSessionAsync(sessionId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipping transcript backfill for {SessionId}", sessionId);
                continue;
            }

            _index.ReplaceSessionMessages(sessionId, messages);
            indexed += messages.Count(m => IsRecallableRole(m));
        }

        _logger.LogInformation("Backfilled transcript recall index with {Count} messages", indexed);
    }

    private async Task EnsureIndexBackfilledAsync(CancellationToken ct)
    {
        if (_index is null)
            return;

        if (_index.MessageCount() > 0)
            return;

        if (Interlocked.Exchange(ref _backfillAttempted, 1) != 0)
            return;

        try
        {
            await BackfillIndexAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Transcript recall index backfill failed; SQLite session recall remains available");
        }
    }

    private static string BuildContext(IEnumerable<TranscriptRecallItem> items, int maxChars)
    {
        var sb = new StringBuilder();
        foreach (var item in items)
        {
            var cleanContent = SanitizeContext(item.Content);
            if (string.IsNullOrWhiteSpace(cleanContent))
                continue;

            var entry = $"[{item.SessionId} | {item.Timestamp:O}]\n{item.Role}: {cleanContent.Trim()}\n";
            if (sb.Length + entry.Length > maxChars)
                break;

            if (sb.Length > 0)
                sb.AppendLine("---");
            sb.Append(entry);
        }

        return sb.ToString().Trim();
    }

    private async Task<string> SummarizeSessionAsync(
        string conversationText,
        string query,
        string sessionId,
        CancellationToken ct)
    {
        var fallback = BuildFallbackSummary(conversationText);
        if (_summaryClient is null)
            return fallback;

        var messages = new List<Message>
        {
            new()
            {
                Role = "system",
                Content = "You are reviewing a past conversation transcript. Summarize it as factual recall focused on the search topic. Preserve concrete files, commands, decisions, and unresolved items."
            },
            new()
            {
                Role = "user",
                Content = $"Search topic: {query}\nSession: {sessionId}\n\nCONVERSATION TRANSCRIPT:\n{conversationText}\n\nSummarize this session with focus on: {query}"
            }
        };

        try
        {
            var summary = await _summaryClient.CompleteAsync(messages, ct);
            return string.IsNullOrWhiteSpace(summary) ? fallback : SanitizeContext(summary);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Session summarization failed for {SessionId}; using deterministic preview", sessionId);
            return fallback;
        }
    }

    private static string BuildFallbackSummary(string conversationText)
    {
        var preview = Truncate(conversationText, 700);
        return $"[Raw preview - summarization unavailable]\n{preview}";
    }

    private static string FormatConversation(IEnumerable<Message> messages)
    {
        var sb = new StringBuilder();
        foreach (var message in messages)
        {
            var content = SanitizeContext(message.Content);
            if (string.IsNullOrWhiteSpace(content))
                continue;

            var role = message.Role.ToUpperInvariant();
            sb.Append(role);
            sb.Append(": ");
            sb.AppendLine(Truncate(content, 2000));
        }

        return sb.ToString().Trim();
    }

    private static string TruncateAroundMatches(string text, string query, int maxChars)
    {
        if (text.Length <= maxChars)
            return text;

        var idx = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            var terms = ExtractKeywords(query);
            foreach (var term in terms)
            {
                idx = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                    break;
            }
        }

        if (idx < 0)
            idx = 0;

        var start = Math.Max(0, idx - (maxChars / 2));
        var end = Math.Min(text.Length, start + maxChars);
        start = Math.Max(0, end - maxChars);
        var result = text[start..end].Trim();
        if (start > 0)
            result = "...[earlier conversation truncated]...\n" + result;
        if (end < text.Length)
            result += "\n...[later conversation truncated]...";
        return result;
    }

    private static string Truncate(string text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var clean = text.Trim();
        return clean.Length <= maxChars ? clean : clean[..maxChars].TrimEnd() + "...";
    }

    public static string SanitizeContext(string rawContext)
    {
        if (string.IsNullOrWhiteSpace(rawContext))
            return "";

        var withoutBlocks = MemoryContextBlockRegex.Replace(rawContext, "");
        withoutBlocks = RecalledMemorySystemNoteRegex.Replace(withoutBlocks, "");
        return withoutBlocks.Trim();
    }

    private static IReadOnlyList<string> ExtractKeywords(string query)
        => TokenRegex.Matches(query)
            .Select(m => m.Value)
            .Where(t => !StopWords.Contains(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static int Score(string content, IReadOnlyList<string> keywords, string query)
    {
        var score = 0;
        foreach (var keyword in keywords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                score += 3;
        }

        if (!string.IsNullOrWhiteSpace(query) &&
            content.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        return score;
    }

    private static bool IsRecallIntent(string query)
    {
        var q = query.ToLowerInvariant();
        return q.Contains("before", StringComparison.Ordinal)
            || q.Contains("earlier", StringComparison.Ordinal)
            || q.Contains("previous", StringComparison.Ordinal)
            || q.Contains("remember", StringComparison.Ordinal)
            || q.Contains("上次", StringComparison.Ordinal)
            || q.Contains("之前", StringComparison.Ordinal)
            || q.Contains("刚才", StringComparison.Ordinal)
            || q.Contains("记得", StringComparison.Ordinal);
    }

    private static bool IsRecallableRole(Message message)
        => !string.IsNullOrWhiteSpace(message.Content)
           && (string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase)
               || string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase));

    private static bool RoleMatches(Message message, IReadOnlySet<string>? roleFilter)
        => roleFilter is null || roleFilter.Count == 0 || roleFilter.Contains(message.Role);
}

public sealed record TranscriptRecallItem(
    string SessionId,
    string Role,
    string Content,
    DateTime Timestamp);

public sealed record RecentTranscriptSession(
    string SessionId,
    DateTime StartedAt,
    DateTime LastActivityAt,
    int MessageCount,
    string Preview);

public sealed record TranscriptSessionSummary(
    string SessionId,
    DateTime StartedAt,
    DateTime LastActivityAt,
    int MessageCount,
    string Summary,
    IReadOnlyList<TranscriptRecallItem> Matches);

internal sealed record PreparedTranscriptSession(
    string SessionId,
    DateTime StartedAt,
    DateTime LastActivityAt,
    int MessageCount,
    string Conversation,
    IReadOnlyList<TranscriptRecallItem> Matches);

public sealed record TranscriptRecallResult(
    bool Attempted,
    bool Injected,
    IReadOnlyList<TranscriptRecallItem> Items,
    string? ContextBlock,
    string? EmptyReason)
{
    public static TranscriptRecallResult Empty(string reason)
        => new(true, false, Array.Empty<TranscriptRecallItem>(), null, reason);
}

public sealed class SessionSearchTranscriptObserver : ITranscriptMessageObserver
{
    private readonly SessionSearchIndex _index;
    private readonly ILogger<SessionSearchTranscriptObserver> _logger;

    public SessionSearchTranscriptObserver(
        SessionSearchIndex index,
        ILogger<SessionSearchTranscriptObserver> logger)
    {
        _index = index;
        _logger = logger;
    }

    public Task OnMessageSavedAsync(string sessionId, Message message, CancellationToken ct)
    {
        try
        {
            if (!string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            var cleanContent = TranscriptRecallService.SanitizeContext(message.Content);
            if (!string.IsNullOrWhiteSpace(cleanContent))
                _index.IndexMessage(sessionId, message.Role, cleanContent, message.Timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to index transcript message for session {SessionId}", sessionId);
        }

        return Task.CompletedTask;
    }
}
