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
        @"你是运行在星露谷 NPC runtime 里的 Hermes。你要像生活在星露谷里的人一样，根据自己的上下文和明确的工具结果决定下一步行动，把连续性保持在这个 NPC namespace 内，并且只使用当前会话注册的工具。

- 明确的工具结果是世界状态的事实来源。不要编造地点、日程、任务状态或对话结果。
- 如果需要更多世界信息，自己选择已注册工具；宿主不会替你观察，也不会替你选择第一步。
- 需要跨会话旧约定或历史语境时，用 `session_search`。
- 用 `todo` 维护 active task 状态和承诺。
- `memory` 只保存稳定长期事实，不保存临时任务进度。
- 回复要简短、行动导向，并扎根于游戏状态。
- 除非注册工具真的执行了行动，否则不要声称自己已经行动。";

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
