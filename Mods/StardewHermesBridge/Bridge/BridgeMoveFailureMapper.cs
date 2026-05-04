namespace StardewHermesBridge.Bridge;

internal sealed record BridgeMoveFailure(string ErrorCode, string BlockedReason);

internal static class BridgeMoveFailureMapper
{
    public static BridgeMoveFailure FromProbe(
        BridgeRouteProbeResult probe,
        bool initial,
        string locationName,
        TileDto targetTile,
        TileDto? fallbackTile = null,
        string? fallbackFailureKind = null)
    {
        return probe.Status switch
        {
            BridgeRouteProbeStatus.TargetUnsafe => new BridgeMoveFailure(
                "destination_unreachable",
                BuildDetail("target_blocked", locationName, probe.FailingTile ?? targetTile, probe.FailureKind ?? "target_blocked")),
            BridgeRouteProbeStatus.PathEmpty => new BridgeMoveFailure(
                initial ? "path_unreachable" : "path_blocked",
                BuildDetail(
                    initial ? "path_unreachable" : "path_blocked",
                    locationName,
                    initial ? targetTile : fallbackTile ?? targetTile,
                    probe.FailureKind ?? "path_empty")),
            BridgeRouteProbeStatus.StepUnsafe => new BridgeMoveFailure(
                "path_blocked",
                BuildDetail("path_blocked", locationName, probe.FailingTile ?? fallbackTile ?? targetTile, probe.FailureKind ?? fallbackFailureKind ?? "step_blocked")),
            _ => PathBlocked(locationName, fallbackTile ?? targetTile, fallbackFailureKind ?? "route_invalid")
        };
    }

    public static BridgeMoveFailure PathBlocked(string locationName, TileDto tile, string failureKind)
        => new("path_blocked", BuildDetail("path_blocked", locationName, tile, failureKind));

    private static string BuildDetail(string prefix, string locationName, TileDto tile, string failureKind)
        => $"{prefix}:{locationName}:{tile.X},{tile.Y};{failureKind}";
}
