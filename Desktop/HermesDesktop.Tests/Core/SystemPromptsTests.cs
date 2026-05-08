using Hermes.Agent.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Core;

[TestClass]
public sealed class SystemPromptsTests
{
    [TestMethod]
    public void LoadStardewNpcRuntimePrompt_UsesRepositoryAsset()
    {
        var repositoryRoot = FindRepositoryRoot();
        var assetPath = Path.Combine(repositoryRoot, "skills", "system", "stardew-npc-runtime", "SYSTEM.md");

        var prompt = SystemPrompts.LoadStardewNpcRuntimePrompt(repositoryRoot);

        Assert.AreEqual(File.ReadAllText(assetPath).Trim(), prompt);
        StringAssert.Contains(prompt, "Stardew Valley NPC runtime");
        StringAssert.Contains(prompt, "explicit tool results");
    }

    [TestMethod]
    public void LoadStardewNpcRuntimePrompt_FallsBackDeterministicallyWhenAssetIsMissing()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "hermes-system-prompt-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var prompt = SystemPrompts.LoadStardewNpcRuntimePrompt(tempRoot);

            Assert.AreEqual(SystemPrompts.StardewNpcRuntimeFallback, prompt);
            StringAssert.Contains(prompt, "Stardew Valley NPC runtime");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [TestMethod]
    public void Build_CanOmitRuntimeFactsGuidance()
    {
        var prompt = SystemPrompts.Build(
            includeMemoryGuidance: false,
            includeSessionSearchGuidance: false,
            includeRuntimeFactsGuidance: false);

        Assert.IsFalse(prompt.Contains(SystemPrompts.RuntimeFactsGuidance, StringComparison.Ordinal));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "skills", "system", "stardew-npc-runtime", "SYSTEM.md");
            if (File.Exists(candidate))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from AppContext.BaseDirectory.");
    }
}
