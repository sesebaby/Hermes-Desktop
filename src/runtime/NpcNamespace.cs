namespace Hermes.Agent.Runtime;

using Hermes.Agent.Memory;
using Hermes.Agent.Search;
using Hermes.Agent.Soul;
using Hermes.Agent.Transcript;
using Microsoft.Extensions.Logging;

public sealed record NpcNamespace(
    string RootPath,
    string GameId,
    string SaveId,
    string NpcId,
    string ProfileId)
{
    public string RuntimeRoot => Path.Combine(
        RootPath,
        "runtime",
        "stardew",
        "games",
        Sanitize(GameId),
        "saves",
        Sanitize(SaveId),
        "npc",
        Sanitize(NpcId),
        "profiles",
        Sanitize(ProfileId));

    public string SoulHomePath => RuntimeRoot;

    public string MemoryPath => Path.Combine(RuntimeRoot, "memory");

    public string TranscriptPath => Path.Combine(RuntimeRoot, "transcripts");

    public string TracePath => Path.Combine(RuntimeRoot, "traces");

    public string ActivityPath => Path.Combine(RuntimeRoot, "activity");

    public string SessionSource => $"stardew:{GameId}:{SaveId}:{NpcId}:{ProfileId}";

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RuntimeRoot);
        Directory.CreateDirectory(MemoryPath);
        Directory.CreateDirectory(TranscriptPath);
        Directory.CreateDirectory(TracePath);
        Directory.CreateDirectory(ActivityPath);
    }

    public SoulService CreateSoulService(ILogger<SoulService> logger)
    {
        EnsureDirectories();
        return new SoulService(SoulHomePath, logger);
    }

    public MemoryManager CreateMemoryManager(Hermes.Agent.LLM.IChatClient chatClient, ILogger<MemoryManager> logger)
    {
        EnsureDirectories();
        return new MemoryManager(MemoryPath, chatClient, logger);
    }

    public TranscriptStore CreateTranscriptStore(ILogger<SessionSearchIndex> logger)
    {
        EnsureDirectories();
        var dbPath = Path.Combine(TranscriptPath, "state.db");
        return new TranscriptStore(
            TranscriptPath,
            eagerFlush: true,
            sessionStore: new SessionSearchIndex(dbPath, logger),
            sessionSource: SessionSource);
    }

    public static string Sanitize(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            throw new ArgumentException("Namespace segment cannot be empty.", nameof(segment));

        var safe = segment.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
            safe = safe.Replace(c, '_');

        safe = safe.Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_');
        if (safe is "." or "..")
            throw new ArgumentException($"Invalid namespace segment '{segment}'.", nameof(segment));

        return safe;
    }
}
