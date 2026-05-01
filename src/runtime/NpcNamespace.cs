namespace Hermes.Agent.Runtime;

using Hermes.Agent.Game;
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

    public string SoulFilePath => Path.Combine(SoulHomePath, "SOUL.md");

    public string PersonaPath => Path.Combine(RuntimeRoot, "persona");

    public string MemoryPath => Path.Combine(RuntimeRoot, "memory");

    public string TranscriptPath => Path.Combine(RuntimeRoot, "transcripts");

    public string TranscriptStateDbPath => Path.Combine(TranscriptPath, "state.db");

    public string TracePath => Path.Combine(RuntimeRoot, "traces");

    public string ActivityPath => Path.Combine(RuntimeRoot, "activity");

    public string SessionSource => $"stardew:{GameId}:{SaveId}:{NpcId}:{ProfileId}";

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RuntimeRoot);
        Directory.CreateDirectory(PersonaPath);
        Directory.CreateDirectory(MemoryPath);
        Directory.CreateDirectory(TranscriptPath);
        Directory.CreateDirectory(TracePath);
        Directory.CreateDirectory(ActivityPath);
    }

    public void SeedPersonaPack(NpcPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        EnsureDirectories();

        var sourceRoot = Path.GetFullPath(pack.RootPath);
        CopyDirectoryIfMissing(sourceRoot, PersonaPath);

        if (!File.Exists(SoulFilePath))
        {
            var soulSource = ResolvePackFile(sourceRoot, pack.Manifest.SoulFile);
            File.Copy(soulSource, SoulFilePath, overwrite: false);
        }
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

    public SessionSearchIndex CreateSessionSearchIndex(ILogger<SessionSearchIndex> logger)
    {
        EnsureDirectories();
        return new SessionSearchIndex(TranscriptStateDbPath, logger);
    }

    public TranscriptStore CreateTranscriptStore(
        ILogger<SessionSearchIndex> logger,
        ITranscriptMessageObserver? messageObserver = null)
    {
        EnsureDirectories();
        return CreateTranscriptStore(CreateSessionSearchIndex(logger), messageObserver);
    }

    public TranscriptStore CreateTranscriptStore(
        SessionSearchIndex sessionStore,
        ITranscriptMessageObserver? messageObserver = null)
    {
        EnsureDirectories();
        return new TranscriptStore(
            TranscriptPath,
            eagerFlush: true,
            messageObserver: messageObserver,
            sessionStore: sessionStore,
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

    private static string ResolvePackFile(string sourceRoot, string relativePath)
    {
        var fullSourceRoot = Path.GetFullPath(sourceRoot);
        var fullPath = Path.GetFullPath(Path.Combine(fullSourceRoot, relativePath));
        if (!fullPath.StartsWith(fullSourceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, fullSourceRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Pack file '{relativePath}' must stay inside '{sourceRoot}'.");
        }

        return fullPath;
    }

    private static void CopyDirectoryIfMissing(string sourceRoot, string destinationRoot)
    {
        Directory.CreateDirectory(destinationRoot);

        foreach (var sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, sourcePath);
            var destinationPath = Path.Combine(destinationRoot, relativePath);
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (destinationDirectory is not null)
                Directory.CreateDirectory(destinationDirectory);

            if (!File.Exists(destinationPath))
                File.Copy(sourcePath, destinationPath, overwrite: false);
        }
    }
}
