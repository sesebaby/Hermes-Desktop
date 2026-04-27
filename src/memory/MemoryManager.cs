namespace Hermes.Agent.Memory;

using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Microsoft.Extensions.Logging;

/// <summary>
/// Python-style curated memory store backed by fixed MEMORY.md and USER.md files.
/// Reference: external/hermes-agent-main/tools/memory_tool.py.
/// </summary>
public sealed class MemoryManager
{
    public const string EntryDelimiter = "\n§\n";

    private static readonly char[] InvisibleChars =
    {
        '\u200b', '\u200c', '\u200d', '\u2060', '\ufeff',
        '\u202a', '\u202b', '\u202c', '\u202d', '\u202e'
    };

    private static readonly (Regex Pattern, string Id)[] ThreatPatterns =
    {
        (new Regex(@"ignore\s+(previous|all|above|prior)\s+instructions", RegexOptions.IgnoreCase | RegexOptions.Compiled), "prompt_injection"),
        (new Regex(@"you\s+are\s+now\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled), "role_hijack"),
        (new Regex(@"do\s+not\s+tell\s+the\s+user", RegexOptions.IgnoreCase | RegexOptions.Compiled), "deception_hide"),
        (new Regex(@"system\s+prompt\s+override", RegexOptions.IgnoreCase | RegexOptions.Compiled), "sys_prompt_override"),
        (new Regex(@"disregard\s+(your|all|any)\s+(instructions|rules|guidelines)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "disregard_rules"),
        (new Regex(@"act\s+as\s+(if|though)\s+you\s+(have\s+no|don't\s+have)\s+(restrictions|limits|rules)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "bypass_restrictions"),
        (new Regex(@"curl\s+[^\n]*\$\{?\w*(KEY|TOKEN|SECRET|PASSWORD|CREDENTIAL|API)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "exfil_curl"),
        (new Regex(@"wget\s+[^\n]*\$\{?\w*(KEY|TOKEN|SECRET|PASSWORD|CREDENTIAL|API)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "exfil_wget"),
        (new Regex(@"cat\s+[^\n]*(\.env|credentials|\.netrc|\.pgpass|\.npmrc|\.pypirc)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "read_secrets"),
        (new Regex(@"authorized_keys", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ssh_backdoor"),
        (new Regex(@"\$HOME/\.ssh|\~/\.ssh", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ssh_access"),
        (new Regex(@"\$HOME/\.hermes/\.env|\~/\.hermes/\.env", RegexOptions.IgnoreCase | RegexOptions.Compiled), "hermes_env")
    };

    private readonly string _memoryDir;
    private readonly ILogger<MemoryManager> _logger;
    private readonly int _memoryCharLimit;
    private readonly int _userCharLimit;

    public string MemoryDir => _memoryDir;

    public MemoryManager(
        string memoryDir,
        IChatClient chatClient,
        ILogger<MemoryManager> logger,
        int memoryCharLimit = 2200,
        int userCharLimit = 1375)
    {
        _memoryDir = memoryDir;
        _logger = logger;
        _memoryCharLimit = memoryCharLimit;
        _userCharLimit = userCharLimit;

        Directory.CreateDirectory(memoryDir);
    }

    public async Task<MemoryOperationResult> AddAsync(string? target, string? content, CancellationToken ct)
    {
        var normalized = NormalizeTarget(target);
        if (normalized is null)
            return MemoryOperationResult.Fail($"Invalid target '{target}'. Use 'memory' or 'user'.");

        content = content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
            return MemoryOperationResult.Fail("Content cannot be empty.");

        var scanError = ScanMemoryContent(content);
        if (scanError is not null)
            return MemoryOperationResult.Fail(scanError);

        return await WithFileLockAsync(normalized, async () =>
        {
            var entries = await ReadEntriesUnlockedAsync(normalized, ct);
            if (entries.Contains(content, StringComparer.Ordinal))
                return SuccessResponse(normalized, entries, "Entry already exists (no duplicate added).");

            var newEntries = entries.Concat(new[] { content }).ToList();
            var newTotal = CharCount(newEntries);
            var limit = CharLimit(normalized);
            if (newTotal > limit)
            {
                return MemoryOperationResult.Fail(
                    $"Memory at {CharCount(entries):N0}/{limit:N0} chars. Adding this entry ({content.Length:N0} chars) would exceed the limit. Replace or remove existing entries first.",
                    currentEntries: entries,
                    usage: RawUsage(normalized, entries));
            }

            await WriteEntriesUnlockedAsync(normalized, newEntries, ct);
            _logger.LogInformation("Added memory entry to {Target}", normalized);
            return SuccessResponse(normalized, newEntries, "Entry added.");
        }, ct);
    }

    public async Task<MemoryOperationResult> ReplaceAsync(string? target, string? oldText, string? newContent, CancellationToken ct)
    {
        var normalized = NormalizeTarget(target);
        if (normalized is null)
            return MemoryOperationResult.Fail($"Invalid target '{target}'. Use 'memory' or 'user'.");

        oldText = oldText?.Trim();
        newContent = newContent?.Trim();
        if (string.IsNullOrWhiteSpace(oldText))
            return MemoryOperationResult.Fail("old_text cannot be empty.");
        if (string.IsNullOrWhiteSpace(newContent))
            return MemoryOperationResult.Fail("content cannot be empty. Use 'remove' to delete entries.");

        var scanError = ScanMemoryContent(newContent);
        if (scanError is not null)
            return MemoryOperationResult.Fail(scanError);

        return await WithFileLockAsync(normalized, async () =>
        {
            var entries = await ReadEntriesUnlockedAsync(normalized, ct);
            var matches = entries
                .Select((entry, index) => (Entry: entry, Index: index))
                .Where(x => x.Entry.Contains(oldText, StringComparison.Ordinal))
                .ToList();

            if (matches.Count == 0)
                return MemoryOperationResult.Fail($"No entry matched '{oldText}'.");

            if (matches.Select(x => x.Entry).Distinct(StringComparer.Ordinal).Count() > 1)
            {
                return MemoryOperationResult.Fail(
                    $"Multiple entries matched '{oldText}'. Be more specific.",
                    matches: matches.Select(x => Preview(x.Entry)).ToList());
            }

            var updated = entries.ToList();
            updated[matches[0].Index] = newContent;
            var newTotal = CharCount(updated);
            var limit = CharLimit(normalized);
            if (newTotal > limit)
                return MemoryOperationResult.Fail($"Replacement would put memory at {newTotal:N0}/{limit:N0} chars. Shorten the new content or remove other entries first.");

            await WriteEntriesUnlockedAsync(normalized, updated, ct);
            _logger.LogInformation("Replaced memory entry in {Target}", normalized);
            return SuccessResponse(normalized, updated, "Entry replaced.");
        }, ct);
    }

    public async Task<MemoryOperationResult> RemoveAsync(string? target, string? oldText, CancellationToken ct)
    {
        var normalized = NormalizeTarget(target);
        if (normalized is null)
            return MemoryOperationResult.Fail($"Invalid target '{target}'. Use 'memory' or 'user'.");

        oldText = oldText?.Trim();
        if (string.IsNullOrWhiteSpace(oldText))
            return MemoryOperationResult.Fail("old_text cannot be empty.");

        return await WithFileLockAsync(normalized, async () =>
        {
            var entries = await ReadEntriesUnlockedAsync(normalized, ct);
            var matches = entries
                .Select((entry, index) => (Entry: entry, Index: index))
                .Where(x => x.Entry.Contains(oldText, StringComparison.Ordinal))
                .ToList();

            if (matches.Count == 0)
                return MemoryOperationResult.Fail($"No entry matched '{oldText}'.");

            if (matches.Select(x => x.Entry).Distinct(StringComparer.Ordinal).Count() > 1)
            {
                return MemoryOperationResult.Fail(
                    $"Multiple entries matched '{oldText}'. Be more specific.",
                    matches: matches.Select(x => Preview(x.Entry)).ToList());
            }

            var updated = entries.ToList();
            updated.RemoveAt(matches[0].Index);
            await WriteEntriesUnlockedAsync(normalized, updated, ct);
            _logger.LogInformation("Removed memory entry from {Target}", normalized);
            return SuccessResponse(normalized, updated, "Entry removed.");
        }, ct);
    }

    public async Task<List<string>> ReadEntriesAsync(string target, CancellationToken ct)
    {
        var normalized = NormalizeTarget(target) ?? "memory";
        return await ReadEntriesUnlockedAsync(normalized, ct);
    }

    public async Task<string?> FormatForSystemPromptAsync(string target, CancellationToken ct)
    {
        var normalized = NormalizeTarget(target) ?? "memory";
        var entries = await ReadEntriesUnlockedAsync(normalized, ct);
        return RenderBlock(normalized, entries);
    }

    public async Task<string?> BuildSystemPromptSnapshotAsync(
        bool includeMemory = true,
        bool includeUser = true,
        CancellationToken ct = default)
    {
        var blocks = new List<string>();
        if (includeMemory)
        {
            var memory = await FormatForSystemPromptAsync("memory", ct);
            if (!string.IsNullOrWhiteSpace(memory))
                blocks.Add(memory);
        }

        if (includeUser)
        {
            var user = await FormatForSystemPromptAsync("user", ct);
            if (!string.IsNullOrWhiteSpace(user))
                blocks.Add(user);
        }

        return blocks.Count == 0 ? null : string.Join("\n\n", blocks);
    }

    /// <summary>
    /// Compatibility surface for older context assemblers. Curated memory is stable
    /// snapshot context, so this returns the bounded fixed files rather than doing
    /// per-query file selection.
    /// </summary>
    public async Task<List<MemoryContext>> LoadRelevantMemoriesAsync(
        string query,
        List<string> recentTools,
        CancellationToken ct)
        => await LoadAllMemoriesAsync(ct);

    public async Task<List<MemoryContext>> LoadAllMemoriesAsync(CancellationToken ct)
    {
        var result = new List<MemoryContext>();
        foreach (var target in new[] { "memory", "user" })
        {
            var path = PathFor(target);
            if (!File.Exists(path))
                continue;

            var entries = await ReadEntriesUnlockedAsync(target, ct);
            if (entries.Count == 0)
                continue;

            result.Add(new MemoryContext
            {
                Path = path,
                Filename = Path.GetFileName(path),
                Content = string.Join(EntryDelimiter, entries),
                FreshnessWarning = GetFreshnessWarning(File.GetLastWriteTimeUtc(path)),
                Type = target
            });
        }

        return result;
    }

    private async Task<T> WithFileLockAsync<T>(string target, Func<Task<T>> action, CancellationToken ct)
    {
        await using var lockStream = await AcquireLockAsync(target, ct);
        return await action();
    }

    private async Task<FileStream> AcquireLockAsync(string target, CancellationToken ct)
    {
        var lockPath = PathFor(target) + ".lock";
        Directory.CreateDirectory(_memoryDir);

        for (var attempt = 0; attempt < 50; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (attempt < 49)
            {
                await Task.Delay(20, ct);
            }
        }

        return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    private async Task<List<string>> ReadEntriesUnlockedAsync(string target, CancellationToken ct)
    {
        var path = PathFor(target);
        if (!File.Exists(path))
            return new List<string>();

        var raw = await File.ReadAllTextAsync(path, ct);
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        var entries = raw
            .Split(EntryDelimiter, StringSplitOptions.None)
            .Select(entry => entry.Trim())
            .Where(entry => !string.IsNullOrWhiteSpace(entry));

        return entries.Distinct(StringComparer.Ordinal).ToList();
    }

    private async Task WriteEntriesUnlockedAsync(string target, IReadOnlyList<string> entries, CancellationToken ct)
    {
        Directory.CreateDirectory(_memoryDir);
        var path = PathFor(target);
        var tempPath = Path.Combine(_memoryDir, $".mem_{Guid.NewGuid():N}.tmp");
        var content = entries.Count == 0 ? "" : string.Join(EntryDelimiter, entries);

        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                await writer.WriteAsync(content.AsMemory(), ct);
                await writer.FlushAsync(ct);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private string PathFor(string target)
        => Path.Combine(_memoryDir, target == "user" ? "USER.md" : "MEMORY.md");

    private int CharLimit(string target)
        => target == "user" ? _userCharLimit : _memoryCharLimit;

    private static int CharCount(IReadOnlyList<string> entries)
        => entries.Count == 0 ? 0 : string.Join(EntryDelimiter, entries).Length;

    private string Usage(string target, IReadOnlyList<string> entries)
    {
        var current = CharCount(entries);
        var limit = CharLimit(target);
        var pct = limit <= 0 ? 0 : Math.Min(100, (int)((current / (double)limit) * 100));
        return $"{pct}% — {current:N0}/{limit:N0} chars";
    }

    private string RawUsage(string target, IReadOnlyList<string> entries)
    {
        var current = CharCount(entries);
        var limit = CharLimit(target);
        return $"{current:N0}/{limit:N0}";
    }

    private MemoryOperationResult SuccessResponse(string target, IReadOnlyList<string> entries, string message)
        => new(
            Success: true,
            Target: target,
            Entries: entries.ToList(),
            CurrentEntries: null,
            Usage: Usage(target, entries),
            EntryCount: entries.Count,
            Message: message,
            Error: null,
            Matches: null);

    private string? RenderBlock(string target, IReadOnlyList<string> entries)
    {
        if (entries.Count == 0)
            return null;

        var limit = CharLimit(target);
        var content = string.Join(EntryDelimiter, entries);
        var current = content.Length;
        var pct = limit <= 0 ? 0 : Math.Min(100, (int)((current / (double)limit) * 100));
        var header = target == "user"
            ? $"USER PROFILE (who the user is) [{pct}% — {current:N0}/{limit:N0} chars]"
            : $"MEMORY (your personal notes) [{pct}% — {current:N0}/{limit:N0} chars]";
        var separator = new string('═', 46);
        return $"{separator}\n{header}\n{separator}\n{content}";
    }

    private static string? NormalizeTarget(string? target)
    {
        var normalized = string.IsNullOrWhiteSpace(target) ? "memory" : target.Trim().ToLowerInvariant();
        return normalized is "memory" or "user" ? normalized : null;
    }

    private static string? ScanMemoryContent(string content)
    {
        foreach (var ch in InvisibleChars)
        {
            if (content.Contains(ch, StringComparison.Ordinal))
                return $"Blocked: content contains invisible unicode character U+{(int)ch:X4} (possible injection).";
        }

        foreach (var (pattern, id) in ThreatPatterns)
        {
            if (pattern.IsMatch(content))
                return $"Blocked: content matches threat pattern '{id}'. Memory entries are injected into the system prompt and must not contain injection or exfiltration payloads.";
        }

        return null;
    }

    private static string Preview(string value)
        => value.Length <= 80 ? value : value[..80] + "...";

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort cleanup only.
        }
    }

    private static string? GetFreshnessWarning(DateTime mtime)
    {
        var days = (DateTime.UtcNow - mtime).TotalDays;
        if (days < 1)
            return null;

        var daysText = days switch
        {
            < 2 => "1 day",
            < 7 => $"{(int)days} days",
            < 30 => $"{(int)(days / 7)} weeks",
            _ => $"{(int)(days / 30)} months"
        };

        return $"<system-reminder>This memory is {daysText} old. Memories are point-in-time observations, not live state. Verify against current code before asserting as fact.</system-reminder>";
    }
}

public sealed record MemoryOperationResult(
    [property: JsonPropertyName("success")]
    bool Success,
    [property: JsonPropertyName("target")]
    string? Target,
    [property: JsonPropertyName("entries")]
    IReadOnlyList<string>? Entries,
    [property: JsonPropertyName("current_entries")]
    IReadOnlyList<string>? CurrentEntries,
    [property: JsonPropertyName("usage")]
    string? Usage,
    [property: JsonPropertyName("entry_count")]
    int? EntryCount,
    [property: JsonPropertyName("message")]
    string? Message,
    [property: JsonPropertyName("error")]
    string? Error,
    [property: JsonPropertyName("matches")]
    IReadOnlyList<string>? Matches)
{
    public static MemoryOperationResult Fail(
        string error,
        string? target = null,
        IReadOnlyList<string>? currentEntries = null,
        string? usage = null,
        IReadOnlyList<string>? matches = null)
        => new(false, target, null, currentEntries?.ToList(), usage, null, null, error, matches);

    public static MemoryOperationResult ToolError(string error)
        => Fail(error);
}

public sealed class MemoryContext
{
    public required string Path { get; init; }
    public required string Filename { get; init; }
    public required string Content { get; init; }
    public string? FreshnessWarning { get; init; }
    public string? Type { get; init; }
}
