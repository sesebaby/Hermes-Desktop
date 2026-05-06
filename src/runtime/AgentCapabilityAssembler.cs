namespace Hermes.Agent.Runtime;

using Hermes.Agent.Core;
using Hermes.Agent.Context;
using Hermes.Agent.LLM;
using Hermes.Agent.Memory;
using Hermes.Agent.Plugins;
using Hermes.Agent.Search;
using Hermes.Agent.Skills;
using Hermes.Agent.Tasks;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging;

/// <summary>
/// Shared registration source for desktop and NPC agent capabilities.
/// </summary>
public static class AgentCapabilityAssembler
{
    public static readonly IReadOnlyList<string> BuiltInToolNames =
    [
        "todo",
        "todo_write",
        "schedule_cron",
        "agent",
        "memory",
        "session_search",
        "skills_list",
        "skill_view",
        "skill_manage",
        "skill_invoke",
        "checkpoint"
    ];

    public static void RegisterBuiltInTools(Agent agent, AgentCapabilityServices services)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(services);

        services.Validate();

        RegisterAndTrack(agent, services.ToolRegistry, new TodoTool(services.TodoStore));
        RegisterAndTrack(agent, services.ToolRegistry, new TodoWriteTool(services.TodoStore));
        RegisterAndTrack(agent, services.ToolRegistry, new ScheduleCronTool(services.CronScheduler));
        RegisterAndTrack(
            agent,
            services.ToolRegistry,
            new AgentTool(
                services.DelegationChatClient ?? services.ChatClient,
                services.ToolRegistry,
                new AgentToolConfig { MaxSubagentDepth = 1 },
                logger: services.LoggerFactory?.CreateLogger<AgentTool>()));
        RegisterAndTrack(agent, services.ToolRegistry, new MemoryTool(
            services.MemoryManager,
            services.PluginManager,
            services.MemoryAvailable));
        RegisterAndTrack(agent, services.ToolRegistry, new SessionSearchTool(services.TranscriptRecallService));
        RegisterAndTrack(agent, services.ToolRegistry, new SkillsListTool(services.SkillManager));
        RegisterAndTrack(agent, services.ToolRegistry, new SkillViewTool(services.SkillManager));
        RegisterAndTrack(agent, services.ToolRegistry, new SkillManageTool(services.SkillManager));
        RegisterAndTrack(agent, services.ToolRegistry, new SkillInvokeTool(services.SkillManager));
        RegisterAndTrack(agent, services.ToolRegistry, new CheckpointTool(services.CheckpointDirectory));
    }

    public static void RegisterDiscoveredTools(
        Agent agent,
        IToolRegistry registry,
        IEnumerable<ITool> tools)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(tools);

        foreach (var tool in tools)
            RegisterAndTrack(agent, registry, tool);
    }

    public static void RegisterAllTools(
        Agent agent,
        AgentCapabilityServices services,
        IEnumerable<ITool> discoveredTools)
    {
        RegisterBuiltInTools(agent, services);
        RegisterDiscoveredTools(agent, services.ToolRegistry, discoveredTools);
    }

    public static PromptBuilder CreatePromptBuilder(AgentPromptServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Validate();

        return new PromptBuilder(() =>
        {
            var prompt = services.UseStardewNpcRuntimePrompt
                ? SystemPrompts.StardewNpcRuntime
                : SystemPrompts.BuildFromBase(
                    services.BaseSystemPrompt,
                    includeMemoryGuidance: services.IncludeMemoryGuidance && services.MemoryAvailable,
                    includeSessionSearchGuidance: services.IncludeSessionSearchGuidance,
                    includeSkillsGuidance: services.IncludeSkillsGuidance,
                    skillsMandatoryPrompt: services.IncludeSkillsMandatoryCatalog
                        ? services.SkillManager.BuildSkillsMandatoryPrompt()
                        : null,
                    includeRuntimeFactsGuidance: services.IncludeRuntimeFactsGuidance);

            if (!string.IsNullOrWhiteSpace(services.SupplementalSystemPrompt))
                prompt += "\n\n" + services.SupplementalSystemPrompt.Trim();

            return prompt;
        });
    }

    private static void RegisterAndTrack(Agent agent, IToolRegistry registry, ITool tool)
    {
        agent.RegisterTool(tool);
        registry.RegisterTool(tool);
    }
}

public sealed class AgentCapabilityServices
{
    public required IChatClient ChatClient { get; init; }
    public IChatClient? DelegationChatClient { get; init; }
    public ILoggerFactory? LoggerFactory { get; init; }
    public required IToolRegistry ToolRegistry { get; init; }
    public required SessionTodoStore TodoStore { get; init; }
    public required ICronScheduler CronScheduler { get; init; }
    public required MemoryManager MemoryManager { get; init; }
    public required PluginManager PluginManager { get; init; }
    public required TranscriptRecallService TranscriptRecallService { get; init; }
    public required SkillManager SkillManager { get; init; }
    public required string CheckpointDirectory { get; init; }
    public bool MemoryAvailable { get; init; } = true;

    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(ChatClient);
        ArgumentNullException.ThrowIfNull(ToolRegistry);
        ArgumentNullException.ThrowIfNull(TodoStore);
        ArgumentNullException.ThrowIfNull(CronScheduler);
        ArgumentNullException.ThrowIfNull(MemoryManager);
        ArgumentNullException.ThrowIfNull(PluginManager);
        ArgumentNullException.ThrowIfNull(TranscriptRecallService);
        ArgumentNullException.ThrowIfNull(SkillManager);
        if (string.IsNullOrWhiteSpace(CheckpointDirectory))
            throw new ArgumentException("Checkpoint directory is required.", nameof(CheckpointDirectory));
    }
}

public sealed class AgentPromptServices
{
    public required SkillManager SkillManager { get; init; }
    public string BaseSystemPrompt { get; init; } = SystemPrompts.Default;
    public string? SupplementalSystemPrompt { get; init; }
    public bool UseStardewNpcRuntimePrompt { get; init; }
    public bool IncludeMemoryGuidance { get; init; } = true;
    public bool IncludeSessionSearchGuidance { get; init; } = true;
    public bool IncludeSkillsGuidance { get; init; } = true;
    public bool IncludeSkillsMandatoryCatalog { get; init; } = true;
    public bool IncludeRuntimeFactsGuidance { get; init; } = true;
    public bool MemoryAvailable { get; init; } = true;

    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(SkillManager);
    }
}
