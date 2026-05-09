namespace Hermes.Agent.Skills;

public static class BundledSkillRootDiscovery
{
    public static string? FindBundledSkillsDir(
        string? appBaseDirectory,
        string? currentDirectory,
        string? workspaceDirectory,
        int maxParentDepth = 10)
    {
        foreach (var seed in BuildSearchSeeds(appBaseDirectory, currentDirectory, workspaceDirectory))
        {
            var found = FindFromSeed(seed, maxParentDepth);
            if (found is not null)
                return found;
        }

        return null;
    }

    public static void ReconcileRequiredSkillDirectories(
        string activeSkillsDir,
        IEnumerable<string> sourceRootDirectories,
        string relativeCategory,
        IEnumerable<string> requiredSkillIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activeSkillsDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeCategory);
        ArgumentNullException.ThrowIfNull(sourceRootDirectories);
        ArgumentNullException.ThrowIfNull(requiredSkillIds);

        var sourceRoots = sourceRootDirectories
            .Where(root => !string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sourceRoots.Length == 0)
            return;

        foreach (var skillId in requiredSkillIds)
        {
            if (string.IsNullOrWhiteSpace(skillId) ||
                skillId.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
            {
                continue;
            }

            var source = sourceRoots
                .Select(root => Path.Combine(root, skillId))
                .FirstOrDefault(path => Directory.Exists(path) && File.Exists(Path.Combine(path, "SKILL.md")));
            if (source is null)
                continue;

            var destination = Path.Combine(activeSkillsDir, relativeCategory, skillId);
            if (Directory.Exists(destination))
                Directory.Delete(destination, recursive: true);

            CopyDirectoryRecursive(source, destination);
        }
    }

    private static IEnumerable<string> BuildSearchSeeds(
        string? appBaseDirectory,
        string? currentDirectory,
        string? workspaceDirectory)
    {
        foreach (var seed in new[] { workspaceDirectory, currentDirectory, appBaseDirectory })
        {
            if (string.IsNullOrWhiteSpace(seed))
                continue;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(seed);
            }
            catch
            {
                continue;
            }

            if (Directory.Exists(fullPath))
                yield return fullPath;
        }
    }

    private static string? FindFromSeed(string seed, int maxParentDepth)
    {
        var dir = seed;
        for (var i = 0; i <= maxParentDepth && !string.IsNullOrWhiteSpace(dir); i++)
        {
            var skillsCandidate = Path.Combine(dir, "skills");
            if (IsBundledSkillsDir(skillsCandidate))
                return skillsCandidate;

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    private static bool IsBundledSkillsDir(string path)
        => Directory.Exists(path) &&
           File.Exists(BundledSkillCatalogService.GetManifestPath(path));

    private static void CopyDirectoryRecursive(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);

        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyDirectoryRecursive(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
}
