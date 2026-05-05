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
    ICronScheduler CronScheduler)
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

        var toolList = tools.ToArray();
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
    Func<IGameAdapter, IEnumerable<ITool>> GameToolFactory,
    NpcRuntimeCompositionServices Services,
    NpcToolSurface ToolSurface,
    long ToolSurfaceSnapshotVersion = 0,
    string? SystemPromptSupplement = null)
{
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
