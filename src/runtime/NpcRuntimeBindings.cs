namespace Hermes.Agent.Runtime;

using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.LLM;
using Hermes.Agent.Skills;
using Hermes.Agent.Tasks;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging;

public sealed record NpcRuntimeCompositionServices(
    IChatClient ChatClient,
    ILoggerFactory LoggerFactory,
    SkillManager SkillManager,
    ICronScheduler CronScheduler,
    IChatClient? DelegationChatClient = null)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(ChatClient);
        ArgumentNullException.ThrowIfNull(LoggerFactory);
        ArgumentNullException.ThrowIfNull(SkillManager);
        ArgumentNullException.ThrowIfNull(CronScheduler);
    }
}

public sealed record NpcToolSurface(IReadOnlyList<ITool> Tools, string Fingerprint)
{
    public static NpcToolSurface FromTools(IEnumerable<ITool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var toolList = tools
            .GroupBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        var fingerprint = string.Join(
            "|",
            toolList
                .Select(tool => $"{tool.Name}:{tool.ParametersType.FullName}")
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase));
        return new NpcToolSurface(toolList, fingerprint);
    }
}

public sealed record NpcToolSurfaceSnapshot(
    NpcToolSurface ToolSurface,
    long SnapshotVersion,
    DateTime CapturedAtUtc);

public interface INpcToolSurfaceSnapshotProvider
{
    NpcToolSurfaceSnapshot Capture();
}

public sealed record NpcRuntimeAgentBindingRequest(
    string ChannelKey,
    string? SystemPromptSupplement,
    bool IncludeMemory,
    bool IncludeUser,
    int MaxToolIterations,
    NpcRuntimeCompositionServices Services,
    NpcToolSurface ToolSurface,
    long ToolSurfaceSnapshotVersion = 0)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ChannelKey))
            throw new ArgumentException("Channel key is required.", nameof(ChannelKey));

        Services.Validate();
        ArgumentNullException.ThrowIfNull(ToolSurface);
    }
}

public sealed record NpcRuntimeAutonomyBindingRequest(
    string ChannelKey,
    string AdapterKey,
    bool IncludeMemory,
    bool IncludeUser,
    int MaxToolIterations,
    Func<IGameAdapter> AdapterFactory,
    Func<IGameAdapter, NpcObservationFactStore, IEnumerable<ITool>> GameToolFactory,
    NpcRuntimeCompositionServices Services,
    NpcToolSurface ToolSurface,
    long ToolSurfaceSnapshotVersion = 0,
    string? SystemPromptSupplement = null,
    Func<IGameAdapter, NpcObservationFactStore, IEnumerable<ITool>>? LocalExecutorGameToolFactory = null,
    Func<NpcRuntimeCompositionServices, IEnumerable<ITool>>? LocalExecutorRuntimeToolFactory = null,
    string? LocalExecutorToolFingerprint = null,
    NpcActionChainGuardOptions? ActionChainGuardOptions = null)
{
    public NpcRuntimeAutonomyBindingRequest(
        string ChannelKey,
        string AdapterKey,
        bool IncludeMemory,
        bool IncludeUser,
        int MaxToolIterations,
        Func<IGameAdapter> AdapterFactory,
        Func<IGameAdapter, IEnumerable<ITool>> GameToolFactory,
        NpcRuntimeCompositionServices Services,
        NpcToolSurface ToolSurface,
        long ToolSurfaceSnapshotVersion = 0,
        string? SystemPromptSupplement = null)
        : this(
            ChannelKey,
            AdapterKey,
            IncludeMemory,
            IncludeUser,
            MaxToolIterations,
            AdapterFactory,
            (adapter, _) => GameToolFactory(adapter),
            Services,
            ToolSurface,
            ToolSurfaceSnapshotVersion,
            SystemPromptSupplement,
            null,
            null,
            null,
            null)
    {
        ArgumentNullException.ThrowIfNull(GameToolFactory);
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ChannelKey))
            throw new ArgumentException("Channel key is required.", nameof(ChannelKey));

        if (string.IsNullOrWhiteSpace(AdapterKey))
            throw new ArgumentException("Adapter key is required.", nameof(AdapterKey));

        ArgumentNullException.ThrowIfNull(AdapterFactory);
        ArgumentNullException.ThrowIfNull(GameToolFactory);
        Services.Validate();
        ArgumentNullException.ThrowIfNull(ToolSurface);
        if ((LocalExecutorGameToolFactory is not null || LocalExecutorRuntimeToolFactory is not null) &&
            string.IsNullOrWhiteSpace(LocalExecutorToolFingerprint))
        {
            throw new ArgumentException(
                "Local executor tool fingerprint is required when local executor tools are provided.",
                nameof(LocalExecutorToolFingerprint));
        }
    }
}

public sealed record NpcRuntimeAgentHandle(
    NpcRuntimeInstance Instance,
    NpcRuntimeContextBundle Context,
    Hermes.Agent.Core.Agent Agent,
    string ChannelKey,
    string RebindKey,
    int RebindGeneration,
    string ToolFingerprint);

public sealed record NpcRuntimeAutonomyHandle(
    NpcRuntimeInstance Instance,
    NpcRuntimeAgentHandle AgentHandle,
    IGameAdapter Adapter,
    NpcAutonomyLoop Loop,
    string ChannelKey,
    string RebindKey,
    int RebindGeneration,
    string ToolFingerprint);

public sealed record NpcRuntimeTaskView(
    string SessionId,
    SessionTodoSnapshot ActiveSnapshot);
