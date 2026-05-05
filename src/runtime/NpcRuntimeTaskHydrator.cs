namespace Hermes.Agent.Runtime;

using Hermes.Agent.Search;
using Hermes.Agent.Tasks;
using Microsoft.Extensions.Logging;

public interface INpcRuntimeTaskHydrator
{
    Task HydrateAsync(NpcRuntimeInstance instance, CancellationToken ct);
}

public sealed class NpcRuntimeTaskHydrator : INpcRuntimeTaskHydrator
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<NpcRuntimeTaskHydrator> _logger;

    public NpcRuntimeTaskHydrator(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<NpcRuntimeTaskHydrator>();
    }

    public async Task HydrateAsync(NpcRuntimeInstance instance, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ct.ThrowIfCancellationRequested();

        var sessionId = instance.Descriptor.SessionId;
        var legacyPrivateChatPrefix = $"{sessionId}:private_chat:";
        using var sessionSearchIndex = instance.Namespace.CreateSessionSearchIndex(
            _loggerFactory.CreateLogger<SessionSearchIndex>());
        var transcriptStore = instance.Namespace.CreateTranscriptStore(
            sessionSearchIndex,
            messageObserver: null);
        var messages = await transcriptStore.LoadTodoToolResultsByTaskSessionIdAsync(
            sessionId,
            legacyPrivateChatPrefix,
            ct);
        var projection = new SessionTaskProjectionService(instance.TodoStore);

        foreach (var message in messages)
        {
            ct.ThrowIfCancellationRequested();
            await projection.OnMessageSavedAsync(sessionId, message, ct);
        }

        _logger.LogInformation(
            "NPC runtime task hydration completed; npc={NpcId}; sessionId={SessionId}; todoToolResults={TodoToolResultCount}",
            instance.Descriptor.NpcId,
            sessionId,
            messages.Count);
    }
}
