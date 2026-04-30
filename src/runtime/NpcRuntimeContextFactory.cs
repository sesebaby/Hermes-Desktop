using Hermes.Agent.Context;
using Hermes.Agent.LLM;
using Hermes.Agent.Memory;
using Hermes.Agent.Search;
using Hermes.Agent.Soul;
using Hermes.Agent.Transcript;
using Microsoft.Extensions.Logging;

namespace Hermes.Agent.Runtime;

public sealed class NpcRuntimeContextFactory
{
    private const string DefaultSystemPrompt =
        "You are an autonomous NPC runtime. Observe facts, decide only from NPC-local context, " +
        "and use only the tools explicitly registered for this NPC.";

    public NpcRuntimeContextBundle Create(
        NpcNamespace npcNamespace,
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        string? systemPrompt = null,
        int maxTokens = 8000,
        int recentTurnWindow = 6)
    {
        ArgumentNullException.ThrowIfNull(npcNamespace);
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        npcNamespace.EnsureDirectories();

        var soulService = npcNamespace.CreateSoulService(loggerFactory.CreateLogger<SoulService>());
        var memoryManager = npcNamespace.CreateMemoryManager(chatClient, loggerFactory.CreateLogger<MemoryManager>());
        var transcriptStore = npcNamespace.CreateTranscriptStore(loggerFactory.CreateLogger<SessionSearchIndex>());
        var promptBuilder = new PromptBuilder(systemPrompt ?? DefaultSystemPrompt);
        var contextManager = new ContextManager(
            transcriptStore,
            chatClient,
            new TokenBudget(maxTokens, recentTurnWindow),
            promptBuilder,
            loggerFactory.CreateLogger<ContextManager>(),
            soulService: soulService);

        return new NpcRuntimeContextBundle(
            soulService,
            memoryManager,
            transcriptStore,
            promptBuilder,
            contextManager);
    }
}

public sealed record NpcRuntimeContextBundle(
    SoulService SoulService,
    MemoryManager MemoryManager,
    TranscriptStore TranscriptStore,
    PromptBuilder PromptBuilder,
    ContextManager ContextManager);
