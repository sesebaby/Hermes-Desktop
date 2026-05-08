using Hermes.Agent.Context;
using Hermes.Agent.LLM;
using Hermes.Agent.Memory;
using Hermes.Agent.Plugins;
using Hermes.Agent.Search;
using Hermes.Agent.Skills;
using Hermes.Agent.Soul;
using Hermes.Agent.Tasks;
using Hermes.Agent.Transcript;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging;

namespace Hermes.Agent.Runtime;

public sealed class NpcRuntimeContextFactory
{
    private const string DefaultInteractiveSystemPromptSupplement =
        "You are acting as a Stardew Valley NPC runtime. Use NPC-local context and explicit tool results, " +
        "keep continuity inside this NPC namespace, and use the registered tools available in this session.";

    private const string DefaultAutonomySystemPromptSupplement =
        "You are acting as a Stardew Valley NPC autonomy parent runtime. The host only wakes you; it does not " +
        "choose for you or preload world facts. You are a person living in Stardew Valley, so decide your own " +
        "next action from your own perspective, keep continuity inside this NPC namespace, and return one JSON " +
        "intent contract only. Mechanical actions are executed by the host and local executor.";

    public NpcRuntimeContextBundle Create(
        NpcNamespace npcNamespace,
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        SkillManager skillManager,
        string? channelKey = null,
        string? systemPromptSupplement = null,
        int maxTokens = 8000,
        int recentTurnWindow = 6,
        bool includeMemory = true,
        bool includeUser = true,
        SessionTodoStore? sharedTodoStore = null)
    {
        ArgumentNullException.ThrowIfNull(npcNamespace);
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(skillManager);

        npcNamespace.EnsureDirectories();

        var soulService = npcNamespace.CreateSoulService(loggerFactory.CreateLogger<SoulService>());
        var memoryManager = npcNamespace.CreateMemoryManager(chatClient, loggerFactory.CreateLogger<MemoryManager>());
        var todoStore = sharedTodoStore ?? new SessionTodoStore();
        var taskProjectionService = new SessionTaskProjectionService(todoStore);
        var sessionSearchIndex = npcNamespace.CreateSessionSearchIndex(loggerFactory.CreateLogger<SessionSearchIndex>());
        var transcriptStore = npcNamespace.CreateTranscriptStore(sessionSearchIndex, taskProjectionService);
        var transcriptRecallService = new TranscriptRecallService(
            transcriptStore,
            loggerFactory.CreateLogger<TranscriptRecallService>(),
            sessionSearchIndex,
            chatClient);
        var curatedMemoryProvider = new CuratedMemoryLifecycleProvider(
            memoryManager,
            includeMemory: includeMemory,
            includeUser: includeUser);
        var memoryOrchestrator = new HermesMemoryOrchestrator(
            new IMemoryProvider[]
            {
                curatedMemoryProvider,
                new TranscriptMemoryProvider(transcriptRecallService)
            },
            loggerFactory.CreateLogger<HermesMemoryOrchestrator>(),
            new IMemoryCompressionParticipant[] { curatedMemoryProvider });
        var pluginManager = new PluginManager(loggerFactory.CreateLogger<PluginManager>());
        pluginManager.Register(new BuiltinMemoryPlugin(
            memoryManager,
            includeMemory: includeMemory,
            includeUser: includeUser));
        var isAutonomyChannel = string.Equals(channelKey, "autonomy", StringComparison.OrdinalIgnoreCase);
        var promptBuilder = AgentCapabilityAssembler.CreatePromptBuilder(new AgentPromptServices
        {
            SkillManager = skillManager,
            MemoryAvailable = includeMemory || includeUser,
            IncludeSkillsMandatoryCatalog = !isAutonomyChannel,
            UseStardewNpcRuntimePrompt = isAutonomyChannel,
            IncludeMemoryGuidance = !isAutonomyChannel,
            IncludeSessionSearchGuidance = !isAutonomyChannel,
            IncludeSkillsGuidance = !isAutonomyChannel,
            IncludeRuntimeFactsGuidance = !isAutonomyChannel,
            SupplementalSystemPrompt = BuildSystemPromptSupplement(isAutonomyChannel, systemPromptSupplement)
        });
        var contextManager = new ContextManager(
            transcriptStore,
            chatClient,
            new TokenBudget(maxTokens, recentTurnWindow),
            promptBuilder,
            loggerFactory.CreateLogger<ContextManager>(),
            soulService: soulService,
            pluginManager: pluginManager,
            memoryOrchestrator: memoryOrchestrator,
            taskProjectionService: taskProjectionService);
        var turnMemoryCoordinator = new TurnMemoryCoordinator(
            contextManager,
            memoryOrchestrator,
            loggerFactory.CreateLogger<TurnMemoryCoordinator>());
        var memoryReviewService = new MemoryReviewService(
            chatClient,
            memoryManager,
            loggerFactory.CreateLogger<MemoryReviewService>(),
            pluginManager,
            MemoryReviewDefaults.NudgeInterval,
            skillManager,
            MemoryReviewDefaults.SkillCreationNudgeInterval);
        var firstCallContextBudgetPolicy = new StardewAutonomyFirstCallContextBudgetPolicy(
            loggerFactory.CreateLogger<StardewAutonomyFirstCallContextBudgetPolicy>());

        return new NpcRuntimeContextBundle(
            soulService,
            memoryManager,
            transcriptStore,
            promptBuilder,
            contextManager,
            pluginManager,
            transcriptRecallService,
            memoryOrchestrator,
            turnMemoryCoordinator,
            memoryReviewService,
            taskProjectionService,
            todoStore,
            new ToolRegistry(),
            firstCallContextBudgetPolicy,
            firstCallContextBudgetPolicy);
    }

    private static string BuildSystemPromptSupplement(bool isAutonomyChannel, string? systemPromptSupplement)
    {
        var defaultSupplement = isAutonomyChannel
            ? DefaultAutonomySystemPromptSupplement
            : DefaultInteractiveSystemPromptSupplement;
        return string.IsNullOrWhiteSpace(systemPromptSupplement)
            ? defaultSupplement
            : $"{defaultSupplement}\n\n{systemPromptSupplement.Trim()}";
    }
}

public sealed record NpcRuntimeContextBundle(
    SoulService SoulService,
    MemoryManager MemoryManager,
    TranscriptStore TranscriptStore,
    PromptBuilder PromptBuilder,
    ContextManager ContextManager,
    PluginManager PluginManager,
    TranscriptRecallService TranscriptRecallService,
    HermesMemoryOrchestrator MemoryOrchestrator,
    TurnMemoryCoordinator TurnMemoryCoordinator,
    MemoryReviewService MemoryReviewService,
    SessionTaskProjectionService TaskProjectionService,
    SessionTodoStore TodoStore,
    IToolRegistry ToolRegistry,
    Hermes.Agent.Core.IFirstCallContextBudgetPolicy? FirstCallContextBudgetPolicy = null,
    Hermes.Agent.Core.IOutboundContextCompactionPolicy? OutboundContextCompactionPolicy = null);
