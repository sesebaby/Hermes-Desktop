using System.Text.Json;
using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Stardew;

[TestClass]
public sealed class StardewNpcPackSourceLocatorTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-pack-source-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public void Locate_PrefersBundledPersonasDirectoryWhenPresent()
    {
        var repoPersonas = CreateRepoPersonasRoot();
        CreatePack(repoPersonas, "haley", "Haley");

        var baseDirectory = Path.Combine(_tempDir, "app", "bin", "Debug", "net10.0");
        var bundledPersonas = Path.Combine(baseDirectory, "personas");
        CreatePack(bundledPersonas, "penny", "Penny");

        var locator = new StardewNpcPackSourceLocator(
            new FileSystemNpcPackLoader(),
            new StardewNpcPackSourceLocatorOptions(
                BaseDirectory: baseDirectory,
                CurrentDirectory: Path.Combine(_tempDir, "cwd"),
                WorkspaceDirectory: Path.Combine(_tempDir, "workspace"),
                MaxParentDepth: 8));

        var resolution = locator.Locate();

        Assert.AreEqual(Path.GetFullPath(bundledPersonas), resolution.SelectedPath);
        Assert.AreEqual(StardewNpcPackSourceKind.BundledAppPersonas, resolution.SelectedSourceKind);
    }

    [TestMethod]
    public void Locate_IgnoresInvalidWorkspaceAndFallsBackToRepositoryFromBaseDirectory()
    {
        var repoRoot = Path.Combine(_tempDir, "repo");
        var repoPersonas = Path.Combine(repoRoot, "src", "game", "stardew", "personas");
        CreatePack(repoPersonas, "haley", "Haley");

        var workspaceDirectory = Path.Combine(_tempDir, "workspace");
        Directory.CreateDirectory(workspaceDirectory);

        var baseDirectory = Path.Combine(repoRoot, "Desktop", "HermesDesktop", "bin", "x64", "Debug", "net10.0-windows10.0.26100.0");
        Directory.CreateDirectory(baseDirectory);

        var locator = new StardewNpcPackSourceLocator(
            new FileSystemNpcPackLoader(),
            new StardewNpcPackSourceLocatorOptions(
                BaseDirectory: baseDirectory,
                CurrentDirectory: Path.Combine(_tempDir, "cwd"),
                WorkspaceDirectory: workspaceDirectory,
                MaxParentDepth: 8));

        var resolution = locator.Locate();

        Assert.AreEqual(Path.GetFullPath(repoPersonas), resolution.SelectedPath);
        Assert.AreEqual(StardewNpcPackSourceKind.RepositoryFromBaseDirectory, resolution.SelectedSourceKind);
        Assert.IsTrue(
            resolution.RejectedCandidates.Any(candidate =>
                candidate.SourceKind == StardewNpcPackSourceKind.Workspace &&
                candidate.Reason.Contains("not exist", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Locate_WhenNoValidCandidate_ReturnsStructuredRejectedCandidates()
    {
        var baseDirectory = Path.Combine(_tempDir, "app", "bin");
        var currentDirectory = Path.Combine(_tempDir, "cwd");
        var workspaceDirectory = Path.Combine(_tempDir, "workspace");
        Directory.CreateDirectory(baseDirectory);
        Directory.CreateDirectory(currentDirectory);
        Directory.CreateDirectory(workspaceDirectory);

        var locator = new StardewNpcPackSourceLocator(
            new FileSystemNpcPackLoader(),
            new StardewNpcPackSourceLocatorOptions(
                BaseDirectory: baseDirectory,
                CurrentDirectory: currentDirectory,
                WorkspaceDirectory: workspaceDirectory,
                MaxParentDepth: 4));

        var resolution = locator.Locate();

        Assert.IsNull(resolution.SelectedPath);
        Assert.IsNull(resolution.SelectedSourceKind);
        Assert.IsTrue(resolution.RejectedCandidates.Count > 0);
        var ex = Assert.ThrowsException<InvalidOperationException>(() => locator.GetRequiredPackRoot());
        StringAssert.Contains(ex.Message, "No valid Stardew persona source directory was found.");
    }

    [TestMethod]
    public void Resolve_UsesProviderValueAtCallTimeInsteadOfConstructorCachedString()
    {
        var invalidRoot = Path.Combine(_tempDir, "invalid");
        Directory.CreateDirectory(invalidRoot);

        var validRoot = Path.Combine(_tempDir, "packs");
        CreatePack(validRoot, "penny", "Penny");

        var provider = new MutablePackRootProvider(invalidRoot);
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), provider);

        provider.CurrentPackRoot = validRoot;
        var binding = resolver.Resolve("Penny", "save-7");

        Assert.AreEqual("penny", binding.Descriptor.NpcId);
        Assert.IsTrue(binding.Descriptor.PackRoot.EndsWith(Path.Combine("penny", "default"), StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Locate_DeduplicatesEquivalentCandidatePathsAcrossSources()
    {
        var repoRoot = Path.Combine(_tempDir, "repo");
        var repoPersonas = Path.Combine(repoRoot, "src", "game", "stardew", "personas");
        CreatePack(repoPersonas, "haley", "Haley");

        var nestedBaseDirectory = Path.Combine(repoRoot, "Desktop", "HermesDesktop", "bin");
        var nestedCurrentDirectory = Path.Combine(repoRoot, "Desktop", "HermesDesktop");

        var locator = new StardewNpcPackSourceLocator(
            new FileSystemNpcPackLoader(),
            new StardewNpcPackSourceLocatorOptions(
                BaseDirectory: nestedBaseDirectory,
                CurrentDirectory: nestedCurrentDirectory,
                WorkspaceDirectory: repoRoot,
                MaxParentDepth: 8));

        var resolution = locator.Locate();

        Assert.AreEqual(Path.GetFullPath(repoPersonas), resolution.SelectedPath);
        Assert.AreEqual(1, resolution.Candidates.Count(candidate =>
            string.Equals(candidate.Path, Path.GetFullPath(repoPersonas), StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Locate_FallsBackToRepositoryFromCurrentDirectoryWhenBaseDirectoryHasNoRepo()
    {
        var repoRoot = Path.Combine(_tempDir, "repo");
        var repoPersonas = Path.Combine(repoRoot, "src", "game", "stardew", "personas");
        CreatePack(repoPersonas, "penny", "Penny");

        var locator = new StardewNpcPackSourceLocator(
            new FileSystemNpcPackLoader(),
            new StardewNpcPackSourceLocatorOptions(
                BaseDirectory: Path.Combine(_tempDir, "app", "bin"),
                CurrentDirectory: Path.Combine(repoRoot, "Desktop", "HermesDesktop"),
                WorkspaceDirectory: Path.Combine(_tempDir, "workspace"),
                MaxParentDepth: 8));

        var resolution = locator.Locate();

        Assert.AreEqual(Path.GetFullPath(repoPersonas), resolution.SelectedPath);
        Assert.AreEqual(StardewNpcPackSourceKind.RepositoryFromCurrentDirectory, resolution.SelectedSourceKind);
    }

    [TestMethod]
    public void Locate_UsesWorkspaceCandidateOnlyAfterHigherPrioritySourcesMiss()
    {
        var workspacePersonas = Path.Combine(_tempDir, "workspace", "src", "game", "stardew", "personas");
        CreatePack(workspacePersonas, "haley", "Haley");

        var locator = new StardewNpcPackSourceLocator(
            new FileSystemNpcPackLoader(),
            new StardewNpcPackSourceLocatorOptions(
                BaseDirectory: Path.Combine(_tempDir, "app", "bin"),
                CurrentDirectory: Path.Combine(_tempDir, "cwd"),
                WorkspaceDirectory: Path.Combine(_tempDir, "workspace"),
                MaxParentDepth: 4));

        var resolution = locator.Locate();

        Assert.AreEqual(Path.GetFullPath(workspacePersonas), resolution.SelectedPath);
        Assert.AreEqual(StardewNpcPackSourceKind.Workspace, resolution.SelectedSourceKind);
    }

    private string CreateRepoPersonasRoot()
    {
        var repoRoot = Path.Combine(_tempDir, "repo");
        return Path.Combine(repoRoot, "src", "game", "stardew", "personas");
    }

    private static void CreatePack(string root, string npcId, string displayName)
    {
        var packRoot = Path.Combine(root, npcId, "default");
        Directory.CreateDirectory(packRoot);
        foreach (var file in new[] { "SOUL.md", "facts.md", "voice.md", "boundaries.md", "skills.json" })
            File.WriteAllText(Path.Combine(packRoot, file), file == "skills.json" ? """{"required":[],"optional":[]}""" : "ok");

        var manifest = new NpcPackManifest
        {
            SchemaVersion = 1,
            NpcId = npcId,
            GameId = "stardew-valley",
            ProfileId = "default",
            DefaultProfileId = "default",
            DisplayName = displayName,
            SmapiName = displayName,
            Aliases = [npcId, displayName],
            TargetEntityId = npcId,
            AdapterId = "stardew",
            SoulFile = "SOUL.md",
            FactsFile = "facts.md",
            VoiceFile = "voice.md",
            BoundariesFile = "boundaries.md",
            SkillsFile = "skills.json",
            Capabilities = ["move", "speak"]
        };
        File.WriteAllText(Path.Combine(packRoot, FileSystemNpcPackLoader.ManifestFileName), JsonSerializer.Serialize(manifest));
    }

    private sealed class MutablePackRootProvider : IStardewNpcPackRootProvider
    {
        public MutablePackRootProvider(string currentPackRoot)
        {
            CurrentPackRoot = currentPackRoot;
        }

        public string CurrentPackRoot { get; set; }

        public StardewNpcPackSourceResolution Locate()
            => new(
                CurrentPackRoot,
                StardewNpcPackSourceKind.Workspace,
                [new StardewNpcPackSourceCandidate(CurrentPackRoot, StardewNpcPackSourceKind.Workspace)],
                [],
                "mutable test provider");

        public string GetRequiredPackRoot() => CurrentPackRoot;
    }
}
