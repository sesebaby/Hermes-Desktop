using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Microsoft.Extensions.Logging;

namespace Hermes.Agent.Runtime;

public sealed class NpcAgentFactory
{
    public Hermes.Agent.Core.Agent Create(
        IChatClient chatClient,
        NpcRuntimeContextBundle context,
        IEnumerable<ITool> tools,
        ILoggerFactory loggerFactory,
        int maxToolIterations = 25)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var agent = new Hermes.Agent.Core.Agent(
            chatClient,
            loggerFactory.CreateLogger<Hermes.Agent.Core.Agent>(),
            transcripts: context.TranscriptStore,
            memories: context.MemoryManager,
            contextManager: context.ContextManager,
            soulService: context.SoulService,
            pluginManager: context.PluginManager,
            turnMemoryCoordinator: context.TurnMemoryCoordinator,
            memoryReviewService: context.MemoryReviewService,
            firstCallContextBudgetPolicy: context.FirstCallContextBudgetPolicy,
            outboundContextCompactionPolicy: context.OutboundContextCompactionPolicy)
        {
            MaxToolIterations = Math.Max(2, maxToolIterations)
        };

        foreach (var tool in tools)
            agent.RegisterTool(tool);

        return agent;
    }
}
