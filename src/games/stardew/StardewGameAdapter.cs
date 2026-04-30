namespace Hermes.Agent.Games.Stardew;

using Hermes.Agent.Game;

public sealed class StardewGameAdapter : IGameAdapter
{
    public StardewGameAdapter(
        ISmapiModApiClient client,
        string saveId,
        string? npcId = null,
        Func<DateTime>? nowUtc = null)
    {
        Commands = new StardewCommandService(client, saveId);
        Queries = new StardewQueryService(client, saveId, nowUtc);
        Events = new StardewEventSource(client, saveId, npcId);
    }

    public string AdapterId => "stardew";

    public IGameCommandService Commands { get; }

    public IGameQueryService Queries { get; }

    public IGameEventSource Events { get; }
}
