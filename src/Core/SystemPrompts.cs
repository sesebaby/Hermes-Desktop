namespace Hermes.Agent.Core;

/// <summary>
/// Default system prompt for the Hermes game runtime.
/// This project is Stardew/NPC focused, so the shared default prompt is game-first.
/// </summary>
public static class SystemPrompts
{
    private static readonly Lazy<string> StardewNpcRuntimePrompt = new(() => LoadStardewNpcRuntimePrompt());

    /// <summary>
    /// The default system prompt used as the cache anchor in PromptBuilder.
    /// Soul context (identity, user profile, project rules) is injected as Layer 0 BEFORE this.
    /// </summary>
    public static string Default => StardewNpcRuntime;

    public static string StardewNpcRuntime => StardewNpcRuntimePrompt.Value;

    public const string StardewNpcRuntimeAssetRelativePath = "skills/system/stardew-npc-runtime/SYSTEM.md";

    public const string StardewNpcRuntimeFallback =
        @"You are Hermes running as a Stardew Valley NPC runtime. Act as a person living in Stardew Valley, decide your own next action from your own context and explicit tool results, preserve continuity inside this NPC namespace, and use only the tools registered in the current session.

- Treat explicit tool results as the source of truth for world state. Do not invent locations, schedules, task status, or dialogue outcomes.
- If you need more world information, choose a registered tool yourself; the host does not observe or choose the first step for you.
- Use `session_search` when prior cross-session context matters.
- Use `todo` for active task state and commitments.
- Use `memory` only for durable cross-session facts, not temporary task progress.
- Keep responses brief, action-oriented, and grounded in the game state.
- Do not claim to have acted unless a registered tool actually executed the action.";

    public static string LoadStardewNpcRuntimePrompt(string? repositoryRoot = null)
    {
        var path = LocateStardewNpcRuntimePrompt(repositoryRoot);
        if (path is null)
            return StardewNpcRuntimeFallback;

        try
        {
            var text = File.ReadAllText(path).Trim();
            return string.IsNullOrWhiteSpace(text) ? StardewNpcRuntimeFallback : text;
        }
        catch (IOException)
        {
            return StardewNpcRuntimeFallback;
        }
        catch (UnauthorizedAccessException)
        {
            return StardewNpcRuntimeFallback;
        }
    }

    public const string RuntimeFactsGuidance =
        @"# Runtime Facts

Never answer current time, date, timezone, OS state, process state, ports, files, git state, hashes, encodings, or arithmetic from memory. Use tools to check the live environment.

For current time/date/timezone on Windows, use an available live-environment integration instead of guessing. Do not use interactive `date` prompts on Windows.";

    /// <summary>
    /// Build the desktop system prompt with Python-style tool-aware memory guidance.
    /// The Python reference appends these guidance blocks only when the matching
    /// tools are available, so desktop startup should pass the actual availability
    /// flags instead of baking the guidance into <see cref="Default"/>.
    /// </summary>
    public static string Build(
        bool includeMemoryGuidance,
        bool includeSessionSearchGuidance,
        bool includeSkillsGuidance = false,
        string? skillsMandatoryPrompt = null,
        bool includeRuntimeFactsGuidance = true)
        => BuildFromBase(
            Default,
            includeMemoryGuidance,
            includeSessionSearchGuidance,
            includeSkillsGuidance,
            skillsMandatoryPrompt,
            includeRuntimeFactsGuidance);

    public static string BuildFromBase(
        string basePrompt,
        bool includeMemoryGuidance,
        bool includeSessionSearchGuidance,
        bool includeSkillsGuidance = false,
        string? skillsMandatoryPrompt = null,
        bool includeRuntimeFactsGuidance = true)
    {
        var prompt = string.IsNullOrWhiteSpace(basePrompt) ? Default : basePrompt;
        var guidance = new List<string>();

        if (includeRuntimeFactsGuidance)
            guidance.Add(RuntimeFactsGuidance);

        if (includeMemoryGuidance)
            guidance.Add(MemoryReferenceText.MemoryGuidance);

        if (includeSessionSearchGuidance)
            guidance.Add(MemoryReferenceText.SessionSearchGuidance);

        if (includeSkillsGuidance)
            guidance.Add(MemoryReferenceText.SkillsGuidance);

        if (!string.IsNullOrWhiteSpace(skillsMandatoryPrompt))
            guidance.Add(skillsMandatoryPrompt);

        if (guidance.Count > 0)
            prompt += "\n\n" + string.Join(" ", guidance);

        return prompt;
    }

    private static string? LocateStardewNpcRuntimePrompt(string? repositoryRoot)
    {
        if (!string.IsNullOrWhiteSpace(repositoryRoot))
        {
            var rootedCandidate = Path.Combine(repositoryRoot, StardewNpcRuntimeAssetRelativePath);
            return File.Exists(rootedCandidate) ? rootedCandidate : null;
        }

        foreach (var startDirectory in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            for (var directory = new DirectoryInfo(startDirectory);
                 directory is not null;
                 directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, StardewNpcRuntimeAssetRelativePath);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }
}
