namespace Hermes.Agent.Tools;

using Hermes.Agent.Core;
using Hermes.Agent.Search;
using Hermes.Agent.Transcript;
using Microsoft.Extensions.Logging.Abstractions;
using System.ComponentModel;
using System.Text.Json.Serialization;

/// <summary>
/// Search long-term conversation memory or browse recent sessions.
/// </summary>
public sealed class SessionSearchTool : ITool
{
    private readonly TranscriptRecallService _recallService;

    public string Name => "session_search";
    public string Description =>
        "Search long-term memory of past user/assistant conversation turns, or call with no query to browse recent sessions. " +
        "Keyword search returns session-level summaries rather than raw per-message snippets. Tool and system messages are not searched.";
    public Type ParametersType => typeof(SessionSearchParameters);

    public SessionSearchTool(string transcriptDir)
        : this(new TranscriptRecallService(
            new TranscriptStore(transcriptDir),
            NullLogger<TranscriptRecallService>.Instance))
    {
    }

    public SessionSearchTool(TranscriptRecallService recallService)
    {
        _recallService = recallService;
    }

    public async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (SessionSearchParameters)parameters;
        var limit = NormalizeLimit(p.Limit ?? 0);

        try
        {
            if (string.IsNullOrWhiteSpace(p.Query))
            {
                var recent = await _recallService.ListRecentSessionsAsync(
                    p.CurrentSessionId,
                    limit,
                    ct);
                return ToolResult.Ok(FormatRecentSessions(recent));
            }

            var roleFilter = ParseRoleFilter(p.RoleFilter);
            var results = await _recallService.SearchSessionSummariesAsync(
                p.Query,
                currentSessionId: p.CurrentSessionId,
                maxSessions: limit,
                roleFilter,
                ct);

            if (results.Count == 0)
                return ToolResult.Ok($"No sessions found matching '{p.Query}'.");

            return ToolResult.Ok(FormatSessionSummaries(p.Query, results));
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Search failed: {ex.Message}", ex);
        }
    }

    private static int NormalizeLimit(int requested)
        => Math.Clamp(requested > 0 ? requested : 3, 1, 5);

    private static IReadOnlySet<string>? ParseRoleFilter(string? roleFilter)
    {
        if (string.IsNullOrWhiteSpace(roleFilter))
            return null;

        return roleFilter
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string FormatRecentSessions(IReadOnlyList<RecentTranscriptSession> sessions)
    {
        if (sessions.Count == 0)
            return "Mode: recent\nNo prior sessions found.";

        var lines = new List<string>
        {
            "Mode: recent",
            $"Count: {sessions.Count}",
            "Use a keyword query to search specific topics across these sessions.",
            ""
        };

        foreach (var session in sessions)
        {
            lines.Add($"Session: {session.SessionId}");
            lines.Add($"When: {session.StartedAt:O} - {session.LastActivityAt:O}");
            lines.Add($"Messages: {session.MessageCount}");
            lines.Add($"Preview: {session.Preview}");
            lines.Add("");
        }

        return string.Join("\n", lines).TrimEnd();
    }

    private static string FormatSessionSummaries(
        string query,
        IReadOnlyList<TranscriptSessionSummary> summaries)
    {
        var lines = new List<string>
        {
            "Mode: search",
            $"Query: {query}",
            $"Count: {summaries.Count}",
            ""
        };

        foreach (var summary in summaries)
        {
            lines.Add($"Session: {summary.SessionId}");
            lines.Add($"When: {summary.StartedAt:O} - {summary.LastActivityAt:O}");
            lines.Add($"Messages: {summary.MessageCount}");
            lines.Add($"Matches: {summary.Matches.Count}");
            lines.Add("Summary:");
            lines.Add(summary.Summary);
            lines.Add("");
        }

        return string.Join("\n", lines).TrimEnd();
    }
}

public sealed class SessionSearchParameters : ISessionAwareToolParameters
{
    [JsonPropertyName("query")]
    [Description("Search query - keywords, phrases, boolean syntax, or prefix syntax. Omit this to browse recent sessions.")]
    public string? Query { get; init; }

    [JsonPropertyName("role_filter")]
    [Description("Optional role filter within the searchable recall corpus. Supported values: user,assistant. Tool and system messages are not searched.")]
    public string? RoleFilter { get; init; }

    [JsonPropertyName("limit")]
    [Description("Max sessions to summarize or list (default: 3, max: 5).")]
    public int? Limit { get; init; }

    [JsonIgnore]
    public string? CurrentSessionId { get; set; }
}
