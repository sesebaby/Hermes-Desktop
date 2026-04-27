namespace Hermes.Agent.Tools;

using Hermes.Agent.Core;
using Hermes.Agent.Search;
using Hermes.Agent.Transcript;
using Microsoft.Extensions.Logging.Abstractions;
using System.ComponentModel;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Search long-term conversation memory or browse recent sessions.
/// </summary>
public sealed class SessionSearchTool : ITool
{
    private readonly TranscriptRecallService _recallService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string Name => "session_search";
    public string Description => MemoryReferenceText.SessionSearchToolDescription;
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
                return ToolResult.Ok(Serialize(new SessionSearchNoResultsResponse(
                    Success: true,
                    Query: p.Query.Trim(),
                    Results: Array.Empty<SessionSearchResultDto>(),
                    Count: 0,
                    Message: "No matching sessions found.")));

            return ToolResult.Ok(FormatSessionSummaries(p.Query, results));
        }
        catch (Exception ex)
        {
            return ToolResult.Fail(Serialize(new SessionSearchErrorResponse(
                Success: false,
                Error: $"Search failed: {ex.Message}")), ex);
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
        => Serialize(new RecentSessionsResponse(
            Success: true,
            Mode: "recent",
            Results: sessions
                .Select(session => new RecentSessionDto(
                    SessionId: session.SessionId,
                    Title: session.Title,
                    Source: session.Source,
                    StartedAt: session.StartedAt.ToString("O", CultureInfo.InvariantCulture),
                    LastActive: session.LastActivityAt.ToString("O", CultureInfo.InvariantCulture),
                    MessageCount: session.MessageCount,
                    Preview: session.Preview))
                .ToArray(),
            Count: sessions.Count,
            Message: $"Showing {sessions.Count} most recent sessions. Use a keyword query to search specific topics."));

    private static string FormatSessionSummaries(
        string query,
        IReadOnlyList<TranscriptSessionSummary> summaries)
        => Serialize(new SessionSearchResponse(
            Success: true,
            Query: query,
            Results: summaries
                .Select(summary => new SessionSearchResultDto(
                    SessionId: summary.SessionId,
                    When: summary.StartedAt.ToString("O", CultureInfo.InvariantCulture),
                    Source: summary.Source,
                    Model: summary.Model,
                    Summary: summary.Summary))
                .ToArray(),
            Count: summaries.Count,
            SessionsSearched: summaries.Count));

    private static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, JsonOptions);

    private sealed record RecentSessionsResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("mode")] string Mode,
        [property: JsonPropertyName("results")] IReadOnlyList<RecentSessionDto> Results,
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("message")] string Message);

    private sealed record RecentSessionDto(
        [property: JsonPropertyName("session_id")] string SessionId,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("source")] string Source,
        [property: JsonPropertyName("started_at")] string StartedAt,
        [property: JsonPropertyName("last_active")] string LastActive,
        [property: JsonPropertyName("message_count")] int MessageCount,
        [property: JsonPropertyName("preview")] string Preview);

    private sealed record SessionSearchResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("query")] string Query,
        [property: JsonPropertyName("results")] IReadOnlyList<SessionSearchResultDto> Results,
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("sessions_searched")] int SessionsSearched);

    private sealed record SessionSearchNoResultsResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("query")] string Query,
        [property: JsonPropertyName("results")] IReadOnlyList<SessionSearchResultDto> Results,
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("message")] string? Message);

    private sealed record SessionSearchResultDto(
        [property: JsonPropertyName("session_id")] string SessionId,
        [property: JsonPropertyName("when")] string When,
        [property: JsonPropertyName("source")] string Source,
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("summary")] string Summary);

    private sealed record SessionSearchErrorResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("error")] string Error);
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
    [JsonConverter(typeof(FlexibleNullableIntConverter))]
    [Description("Max sessions to summarize or list (default: 3, max: 5).")]
    public int? Limit { get; init; }

    [JsonIgnore]
    public string? CurrentSessionId { get; set; }
}

public sealed class FlexibleNullableIntConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                if (reader.TryGetInt32(out var intValue))
                    return intValue;
                if (reader.TryGetDouble(out var doubleValue))
                    return (int)doubleValue;
                return null;
            case JsonTokenType.String:
                var value = reader.GetString();
                return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : null;
            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                using (JsonDocument.ParseValue(ref reader))
                {
                    return null;
                }
            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
    }
}
