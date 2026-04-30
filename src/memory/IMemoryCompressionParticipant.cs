namespace Hermes.Agent.Memory;

using Hermes.Agent.Core;

/// <summary>
/// Participates in the memory handoff that runs before context compression.
/// Implementations must be best-effort and must not own prompt injection.
/// </summary>
public interface IMemoryCompressionParticipant
{
    string Name { get; }

    Task OnPreCompressAsync(IReadOnlyList<Message> messages, string sessionId, CancellationToken ct);
}

