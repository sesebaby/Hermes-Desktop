namespace Hermes.Agent.Transcript;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hermes.Agent.Core;
using Hermes.Agent.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// SQLite-first session persistence layer backed by Python-style state.db.
/// Session messages and activity logs are not written to JSONL.
/// </summary>
public sealed class TranscriptStore
{
    private readonly string _transcriptsDir;
    private readonly ConcurrentDictionary<string, List<Message>> _cache = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ITranscriptMessageObserver? _messageObserver;
    private readonly SessionSearchIndex _sessionStore;
    private readonly string _sessionSource;

    public TranscriptStore(
        string transcriptsDir,
        bool eagerFlush = false,
        ITranscriptMessageObserver? messageObserver = null,
        SessionSearchIndex? sessionStore = null,
        string sessionSource = "desktop")
    {
        _transcriptsDir = transcriptsDir;
        _messageObserver = messageObserver;
        _sessionSource = string.IsNullOrWhiteSpace(sessionSource) ? "desktop" : sessionSource;
        _sessionStore = sessionStore ?? new SessionSearchIndex(
            Path.Combine(transcriptsDir, "state.db"),
            NullLogger<SessionSearchIndex>.Instance);

        Directory.CreateDirectory(transcriptsDir);
        ImportLegacyJsonlTranscripts();
    }

    /// <summary>
    /// Save message into SQLite state.db before updating in-memory state.
    /// </summary>
    public async Task SaveMessageAsync(string sessionId, Message message, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            _sessionStore.SaveMessage(sessionId, message, _sessionSource);
        }
        finally
        {
            _writeLock.Release();
        }

        // NOW update in-memory cache
        _cache.AddOrUpdate(sessionId,
            _ => new List<Message> { message },
            (_, list) => { list.Add(message); return list; });

        if (_messageObserver is not null)
        {
            try
            {
                await _messageObserver.OnMessageSavedAsync(sessionId, message, ct);
            }
            catch
            {
                // Observer failures must not make session persistence fail.
            }
        }
    }

    /// <summary>Load entire session transcript from SQLite.</summary>
    public Task<List<Message>> LoadSessionAsync(string sessionId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (_cache.TryGetValue(sessionId, out var cached))
            return Task.FromResult(cached.ToList());

        var messages = _sessionStore.LoadMessages(sessionId);
        if (messages.Count == 0 && !_sessionStore.SessionExists(sessionId))
            throw new SessionNotFoundException(sessionId);

        _cache[sessionId] = messages;
        return Task.FromResult(messages.ToList());
    }

    public bool SessionExists(string sessionId)
        => _cache.ContainsKey(sessionId) || _sessionStore.SessionExists(sessionId);

    public SessionMetadata? GetSessionMetadata(string sessionId)
        => _sessionStore.GetSessionMetadata(sessionId);

    public bool IsChildSession(string sessionId)
        => _sessionStore.IsChildSession(sessionId);

    public string ResolveRootSessionId(string sessionId)
        => _sessionStore.ResolveRootSessionId(sessionId);

    public List<string> GetAllSessionIds()
    {
        var ids = _cache.Keys.ToList();
        var seen = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
        ids.AddRange(_sessionStore.ListSessionIds().Where(id => seen.Add(id)));
        return ids;
    }

    public List<string> GetRecentSessionIds(int maxResults)
        => _sessionStore.ListSessionIdsByRecentActivity(maxResults);

    public async Task DeleteSessionAsync(string sessionId, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            _sessionStore.DeleteSession(sessionId);
        }
        finally
        {
            _writeLock.Release();
        }

        _cache.TryRemove(sessionId, out _);
    }

    /// <summary>Clear in-memory cache; SQLite state remains on disk.</summary>
    public void ClearCache() => _cache.Clear();

    /// <summary>Save an activity entry into SQLite state.db.</summary>
    public async Task SaveActivityAsync(string sessionId, ActivityEntry entry, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        await _writeLock.WaitAsync(ct);
        try
        {
            _sessionStore.SaveActivity(sessionId, entry, _sessionSource);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public Task<List<ActivityEntry>> LoadActivityAsync(string sessionId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_sessionStore.LoadActivities(sessionId));
    }

    private void ImportLegacyJsonlTranscripts()
    {
        if (!Directory.Exists(_transcriptsDir))
            return;

        foreach (var path in Directory.EnumerateFiles(_transcriptsDir, "*.jsonl"))
        {
            if (Path.GetFileName(path).EndsWith(".activity.jsonl", StringComparison.OrdinalIgnoreCase))
                ImportLegacyActivity(path);
            else
                ImportLegacyMessages(path);

            DeleteLegacyJsonl(path);
        }
    }

    private bool ImportLegacyMessages(string path)
    {
        var sessionId = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(sessionId))
            return false;

        var messages = new List<Message>();
        var lineNumber = 0;
        try
        {
            foreach (var line in File.ReadLines(path))
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var message = JsonSerializer.Deserialize<Message>(line, JsonOptions);
                    if (message is not null)
                        messages.Add(message);
                    else
                        RecordLegacyImportError(sessionId, path, lineNumber, "message", "deserializer returned null", line);
                }
                catch (Exception ex)
                {
                    RecordLegacyImportError(sessionId, path, lineNumber, "message", ex.GetType().Name, line);
                }
            }
        }
        catch (Exception ex) when (IsRecoverableFileException(ex))
        {
            RecordLegacyImportError(sessionId, path, lineNumber, "message-file", ex.GetType().Name, rawLine: null);
        }

        if (messages.Count > 0 && messages.Count > _sessionStore.GetSessionMessageCount(sessionId))
            _sessionStore.ReplaceSessionMessages(sessionId, messages, _sessionSource);

        return true;
    }

    private bool ImportLegacyActivity(string path)
    {
        var fileName = Path.GetFileName(path);
        var sessionId = fileName[..^".activity.jsonl".Length];
        if (string.IsNullOrWhiteSpace(sessionId))
            return false;

        var lineNumber = 0;
        try
        {
            foreach (var line in File.ReadLines(path))
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var entry = JsonSerializer.Deserialize<ActivityEntry>(line, ActivityJsonOptions);
                    if (entry is not null)
                        _sessionStore.SaveActivity(sessionId, entry, _sessionSource);
                    else
                        RecordLegacyImportError(sessionId, path, lineNumber, "activity", "deserializer returned null", line);
                }
                catch (Exception ex)
                {
                    RecordLegacyImportError(sessionId, path, lineNumber, "activity", ex.GetType().Name, line);
                }
            }
        }
        catch (Exception ex) when (IsRecoverableFileException(ex))
        {
            RecordLegacyImportError(sessionId, path, lineNumber, "activity-file", ex.GetType().Name, rawLine: null);
        }

        return true;
    }

    private void DeleteLegacyJsonl(string path)
    {
        try
        {
            if (!File.Exists(path))
                return;

            File.Delete(path);
        }
        catch
        {
            // Import is authoritative once state.db is updated; a failed cleanup
            // should not block startup or session loading.
        }
    }

    private void RecordLegacyImportError(
        string sessionId,
        string path,
        int lineNumber,
        string kind,
        string error,
        string? rawLine)
    {
        var fileName = Path.GetFileName(path);
        var key = $"legacy_import_error:{sessionId}:{kind}:{lineNumber}:{Guid.NewGuid():N}";
        var value = JsonSerializer.Serialize(new
        {
            file = fileName,
            line = lineNumber,
            kind,
            error,
            sha256 = rawLine is null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawLine))).ToLowerInvariant(),
            length = rawLine?.Length ?? 0,
            importedAt = DateTime.UtcNow
        }, JsonOptions);
        _sessionStore.SetStateMeta(key, value);
    }

    private static bool IsRecoverableFileException(Exception ex)
        => ex is IOException or UnauthorizedAccessException;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions ActivityJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };
}

public interface ITranscriptMessageObserver
{
    Task OnMessageSavedAsync(string sessionId, Message message, CancellationToken ct);
}

/// <summary>
/// Session not found exception.
/// </summary>
public sealed class SessionNotFoundException : Exception
{
    public SessionNotFoundException(string sessionId)
        : base($"Session '{sessionId}' not found. Use 'hermes list' to see available sessions.")
    {
    }
}

/// <summary>
/// Resume manager - loads sessions and restores state.
/// </summary>
public sealed class ResumeManager
{
    private readonly TranscriptStore _transcripts;
    private readonly ILogger<ResumeManager> _logger;

    public ResumeManager(TranscriptStore transcripts, ILogger<ResumeManager> logger)
    {
        _transcripts = transcripts;
        _logger = logger;
    }

    public async Task<Session> ResumeSessionAsync(string sessionId, CancellationToken ct)
    {
        try
        {
            var messages = await _transcripts.LoadSessionAsync(sessionId, ct);

            var session = new Session { Id = sessionId };
            foreach (var msg in messages)
                session.Messages.Add(msg);

            _logger.LogInformation("Resumed session {SessionId} with {Count} messages", sessionId, messages.Count);
            return session;
        }
        catch (SessionNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume session {SessionId}", sessionId);
            throw;
        }
    }

    public List<SessionInfo> ListSessions()
    {
        var sessionIds = _transcripts.GetAllSessionIds();
        var infos = new List<SessionInfo>();

        foreach (var id in sessionIds)
        {
            try
            {
                var messages = _transcripts.LoadSessionAsync(id, CancellationToken.None)
                    .GetAwaiter().GetResult();

                infos.Add(new SessionInfo
                {
                    Id = id,
                    MessageCount = messages.Count,
                    LastMessage = messages.LastOrDefault()?.Content ?? "",
                    LastActivity = messages.LastOrDefault()?.Timestamp ?? DateTime.MinValue
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load session info for {SessionId}", id);
            }
        }

        return infos.OrderByDescending(i => i.LastActivity).ToList();
    }
}

public sealed class SessionInfo
{
    public required string Id { get; init; }
    public int MessageCount { get; init; }
    public string LastMessage { get; init; } = "";
    public DateTime LastActivity { get; init; }
}
