using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StardewHermesBridge.Tests;

[TestClass]
public class RawDialogueDisplayRegressionTests
{
    [TestMethod]
    public void RawNpcDialogueText_IsNotDisplayedThroughTranslationKeyOverload()
    {
        var modEntry = ReadRepositoryFile("Mods", "StardewHermesBridge", "ModEntry.cs");
        var commandQueue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");

        Assert.IsFalse(
            modEntry.Contains("Game1.DrawDialogue(npc,", StringComparison.Ordinal),
            "Raw custom follow-up text must not use Game1.DrawDialogue(NPC,string), because that overload treats the string as a translation key.");
        Assert.IsFalse(
            commandQueue.Contains("Game1.DrawDialogue(npc,", StringComparison.Ordinal),
            "Raw debug speak text must not use Game1.DrawDialogue(NPC,string), because that overload treats the string as a translation key.");
    }

    [TestMethod]
    public void NpcClickPathObservesVanillaDialogueInsteadOfManuallyStartingOriginalDialogue()
    {
        var modEntry = ReadRepositoryFile("Mods", "StardewHermesBridge", "ModEntry.cs");

        StringAssert.Contains(
            modEntry,
            "helper.Events.Display.MenuChanged += OnMenuChanged",
            "The formal NPC dialogue path must observe Stardew's DialogueBox lifecycle instead of relying only on ButtonPressed timing.");
        Assert.IsFalse(
            modEntry.Contains("_originalDialogueStarter.TryStart(clickedNpc, e.Button)", StringComparison.Ordinal),
            "The formal NPC dialogue path must not manually replay npc.checkAction directly from ButtonPressed.");
        StringAssert.Contains(
            modEntry,
            "TryStartOriginalDialogueIfNeeded()",
            "If vanilla does not naturally open the DialogueBox after an accepted click, the bridge may perform a narrow delayed original-start retry.");
        StringAssert.Contains(
            modEntry,
            "_menuGuard.ConsumeMenuChange(oldDialogueNpcName, newDialogueNpcName)",
            "MenuChanged must consume Hermes custom-dialogue menu transitions before deciding whether a vanilla follow-up is pending.");
        StringAssert.Contains(
            modEntry,
            "if (_menuGuard.IsCustomDialogue(activeDialogueNpcName))",
            "ButtonPressed must ignore Hermes custom dialogue boxes so their close click cannot start another follow-up loop.");
    }

    [TestMethod]
    public void NpcClickPathHasTimeoutFallbackWhenVanillaDialogueIsNotObserved()
    {
        var modEntry = ReadRepositoryFile("Mods", "StardewHermesBridge", "ModEntry.cs");

        StringAssert.Contains(
            modEntry,
            "original_not_observed_timeout",
            "If Stardew never opens the original DialogueBox after an accepted NPC click, Hermes should expose a fallback instead of waiting forever.");
    }

    private static string ReadRepositoryFile(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(relativePath).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            directory = directory.Parent;
        }

        Assert.Fail($"Could not find repository file: {Path.Combine(relativePath)}");
        return string.Empty;
    }
}
