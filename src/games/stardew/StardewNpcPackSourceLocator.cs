namespace Hermes.Agent.Games.Stardew;

using Hermes.Agent.Game;

public enum StardewNpcPackSourceKind
{
    BundledAppPersonas,
    RepositoryFromBaseDirectory,
    RepositoryFromCurrentDirectory,
    Workspace
}

public sealed record StardewNpcPackSourceCandidate(
    string Path,
    StardewNpcPackSourceKind SourceKind);

public sealed record StardewNpcPackRejectedCandidate(
    string Path,
    StardewNpcPackSourceKind SourceKind,
    string Reason);

public sealed record StardewNpcPackSourceResolution(
    string? SelectedPath,
    StardewNpcPackSourceKind? SelectedSourceKind,
    IReadOnlyList<StardewNpcPackSourceCandidate> Candidates,
    IReadOnlyList<StardewNpcPackRejectedCandidate> RejectedCandidates,
    string ValidationSummary)
{
    public string GetRequiredPackRoot()
    {
        if (!string.IsNullOrWhiteSpace(SelectedPath))
            return SelectedPath;

        throw new InvalidOperationException(BuildFailureMessage());
    }

    public string BuildFailureMessage()
    {
        if (RejectedCandidates.Count == 0)
            return "No valid Stardew persona source directory was found.";

        var details = string.Join(
            "; ",
            RejectedCandidates.Select(candidate =>
                $"{candidate.SourceKind} '{candidate.Path}': {candidate.Reason}"));
        return $"No valid Stardew persona source directory was found. Rejected candidates: {details}";
    }
}

public sealed record StardewNpcPackSourceLocatorOptions(
    string? BaseDirectory,
    string? CurrentDirectory,
    string? WorkspaceDirectory,
    int MaxParentDepth = 8);

public interface IStardewNpcPackRootProvider
{
    StardewNpcPackSourceResolution Locate();

    string GetRequiredPackRoot();
}

public sealed class StardewNpcPackSourceLocator : IStardewNpcPackRootProvider
{
    private static readonly string[] RepoPersonasSegments = ["src", "game", "stardew", "personas"];
    private readonly INpcPackLoader _packLoader;
    private readonly StardewNpcPackSourceLocatorOptions _options;

    public StardewNpcPackSourceLocator(
        INpcPackLoader packLoader,
        StardewNpcPackSourceLocatorOptions options)
    {
        _packLoader = packLoader;
        _options = options;
    }

    public StardewNpcPackSourceResolution Locate()
    {
        var candidates = new List<StardewNpcPackSourceCandidate>();
        var rejected = new List<StardewNpcPackRejectedCandidate>();
        string? selectedPath = null;
        StardewNpcPackSourceKind? selectedSourceKind = null;

        foreach (var candidate in EnumerateCandidates(rejected))
        {
            candidates.Add(candidate);
            if (TryValidate(candidate.Path, out var reason))
            {
                selectedPath ??= candidate.Path;
                selectedSourceKind ??= candidate.SourceKind;
                continue;
            }

            rejected.Add(new StardewNpcPackRejectedCandidate(candidate.Path, candidate.SourceKind, reason));
        }

        return new StardewNpcPackSourceResolution(
            selectedPath,
            selectedSourceKind,
            candidates,
            rejected,
            selectedPath is null
                ? "No valid Stardew persona source directory was accepted."
                : $"Selected {selectedSourceKind} '{selectedPath}'.");
    }

    public string GetRequiredPackRoot() => Locate().GetRequiredPackRoot();

    private IEnumerable<StardewNpcPackSourceCandidate> EnumerateCandidates(List<StardewNpcPackRejectedCandidate> rejected)
    {
        var seenCandidatePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (TryNormalizeChild(_options.BaseDirectory, ["personas"], out var bundledPath) &&
            seenCandidatePaths.Add(bundledPath))
            yield return new StardewNpcPackSourceCandidate(bundledPath, StardewNpcPackSourceKind.BundledAppPersonas);

        if (TryFindRepoPersonas(_options.BaseDirectory, out var baseRepoPath, out var baseRepoReason) &&
            seenCandidatePaths.Add(baseRepoPath))
            yield return new StardewNpcPackSourceCandidate(baseRepoPath, StardewNpcPackSourceKind.RepositoryFromBaseDirectory);
        else if (!string.IsNullOrWhiteSpace(baseRepoReason) && !string.IsNullOrWhiteSpace(_options.BaseDirectory))
            rejected.Add(new StardewNpcPackRejectedCandidate(
                NormalizePath(_options.BaseDirectory),
                StardewNpcPackSourceKind.RepositoryFromBaseDirectory,
                baseRepoReason));

        if (TryFindRepoPersonas(_options.CurrentDirectory, out var currentRepoPath, out var currentRepoReason) &&
            seenCandidatePaths.Add(currentRepoPath))
            yield return new StardewNpcPackSourceCandidate(currentRepoPath, StardewNpcPackSourceKind.RepositoryFromCurrentDirectory);
        else if (!string.IsNullOrWhiteSpace(currentRepoReason) && !string.IsNullOrWhiteSpace(_options.CurrentDirectory))
            rejected.Add(new StardewNpcPackRejectedCandidate(
                NormalizePath(_options.CurrentDirectory),
                StardewNpcPackSourceKind.RepositoryFromCurrentDirectory,
                currentRepoReason));

        if (TryNormalizeChild(_options.WorkspaceDirectory, RepoPersonasSegments, out var workspacePath) &&
            seenCandidatePaths.Add(workspacePath))
            yield return new StardewNpcPackSourceCandidate(workspacePath, StardewNpcPackSourceKind.Workspace);
    }

    private bool TryValidate(string candidatePath, out string reason)
    {
        if (!Directory.Exists(candidatePath))
        {
            reason = "Directory does not exist.";
            return false;
        }

        try
        {
            var packs = _packLoader.LoadPacks(candidatePath);
            if (packs.Count == 0)
            {
                reason = "Directory does not contain any loadable NPC packs.";
                return false;
            }

            if (!packs.Any(IsValidStardewPack))
            {
                reason = "Directory does not contain any valid Stardew NPC packs.";
                return false;
            }

            reason = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            return false;
        }
    }

    private static bool IsValidStardewPack(NpcPack pack)
        => string.Equals(pack.Manifest.GameId, "stardew-valley", StringComparison.OrdinalIgnoreCase) &&
           string.Equals(pack.Manifest.AdapterId, "stardew", StringComparison.OrdinalIgnoreCase);

    private bool TryFindRepoPersonas(string? anchorDirectory, out string candidatePath, out string reason)
    {
        candidatePath = string.Empty;
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(anchorDirectory))
            return false;

        var current = NormalizePath(anchorDirectory);
        for (var depth = 0; depth <= Math.Max(0, _options.MaxParentDepth); depth++)
        {
            var repoCandidate = NormalizePath(Path.Combine(current, Path.Combine(RepoPersonasSegments)));
            if (Directory.Exists(repoCandidate))
            {
                candidatePath = repoCandidate;
                return true;
            }

            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrWhiteSpace(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                break;

            current = parent;
        }

        reason = $"No repository personas directory was found while walking up from '{NormalizePath(anchorDirectory)}'.";
        return false;
    }

    private static bool TryNormalizeChild(string? root, IReadOnlyList<string> segments, out string candidatePath)
    {
        candidatePath = string.Empty;
        if (string.IsNullOrWhiteSpace(root))
            return false;

        candidatePath = NormalizePath(Path.Combine([root, .. segments]));
        return true;
    }

    private static string NormalizePath(string path)
        => Path.GetFullPath(path);
}
