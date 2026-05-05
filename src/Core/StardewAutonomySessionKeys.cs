namespace Hermes.Agent.Core;

public static class StardewAutonomySessionKeys
{
    public const string IsAutonomyTurn = "stardew.autonomy.isAutonomyTurn";

    public static bool IsAutonomyTurnSession(Session session)
        => session.State.TryGetValue(IsAutonomyTurn, out var value) &&
           value is bool boolValue &&
           boolValue;
}
