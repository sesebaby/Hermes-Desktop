namespace Hermes.Agent.Games.Stardew;

using System.Diagnostics;
using System.Text.Json;
using Hermes.Agent.Game;

public interface IStardewBridgeDiscovery
{
    string DiscoveryFilePath { get; }

    bool TryReadLatest(out StardewBridgeDiscoverySnapshot? snapshot, out string? failureReason);
}

public sealed record StardewBridgeDiscoverySnapshot(
    StardewBridgeOptions Options,
    DateTimeOffset StartedAtUtc,
    int? ProcessId,
    string? SaveId);

public sealed class FileStardewBridgeDiscovery : IStardewBridgeDiscovery
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FileStardewBridgeDiscovery()
        : this(GetDefaultDiscoveryFilePath())
    {
    }

    public FileStardewBridgeDiscovery(string discoveryFilePath)
    {
        DiscoveryFilePath = discoveryFilePath;
    }

    public string DiscoveryFilePath { get; }

    public bool TryReadLatest(out StardewBridgeDiscoverySnapshot? snapshot, out string? failureReason)
    {
        snapshot = null;
        failureReason = null;

        if (!File.Exists(DiscoveryFilePath))
        {
            failureReason = StardewBridgeErrorCodes.BridgeUnavailable;
            return false;
        }

        StardewBridgeDiscoveryDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<StardewBridgeDiscoveryDocument>(
                File.ReadAllText(DiscoveryFilePath),
                JsonOptions);
        }
        catch
        {
            failureReason = StardewBridgeErrorCodes.BridgeStaleDiscovery;
            return false;
        }

        if (document is null ||
            document.Port <= 0 ||
            string.IsNullOrWhiteSpace(document.Host) ||
            string.IsNullOrWhiteSpace(document.BridgeToken))
        {
            failureReason = StardewBridgeErrorCodes.BridgeStaleDiscovery;
            return false;
        }

        var options = new StardewBridgeOptions
        {
            Host = document.Host,
            Port = document.Port,
            BridgeToken = document.BridgeToken
        };

        if (!options.IsLoopbackOnly())
        {
            failureReason = StardewBridgeErrorCodes.BridgeStaleDiscovery;
            return false;
        }

        if (!IsProcessAlive(document.ProcessId))
        {
            failureReason = StardewBridgeErrorCodes.BridgeStaleDiscovery;
            return false;
        }

        snapshot = new StardewBridgeDiscoverySnapshot(
            options,
            document.StartedAtUtc ?? File.GetLastWriteTimeUtc(DiscoveryFilePath),
            document.ProcessId,
            StardewBridgeRuntimeIdentity.NormalizeSaveId(document.SaveId));
        return true;
    }

    public static string GetDefaultDiscoveryFilePath()
        => Path.Combine(GetHermesHomePath(), "hermes-cs", "stardew-bridge.json");

    private static string GetHermesHomePath()
        => Environment.GetEnvironmentVariable("HERMES_HOME") is { Length: > 0 } configuredHome
            ? configuredHome
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "hermes");

    private static bool IsProcessAlive(int? processId)
    {
        if (!processId.HasValue)
            return true;

        if (processId.Value <= 0)
            return false;

        try
        {
            using var process = Process.GetProcessById(processId.Value);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private sealed record StardewBridgeDiscoveryDocument(
        string Host,
        int Port,
        string BridgeToken,
        DateTimeOffset? StartedAtUtc,
        int? ProcessId,
        string? SaveId);
}

public sealed class StardewNpcDebugActionService
{
    private const string HaleyNpcId = "haley";
    private const string HaleyTargetEntityId = "Haley";
    private const string HaleySmapiName = "Haley";
    private const string HaleyDisplayName = "Haley";
    private const string BeachShorelineLocationName = "Beach";
    private const int BeachShorelineTileX = 20;
    private const int BeachShorelineTileY = 35;
    private const int BeachShorelineFacingDirection = 2;

    private readonly IStardewBridgeDiscovery _discovery;
    private readonly Func<StardewBridgeDiscoverySnapshot, IGameCommandService> _commandServiceFactory;

    public StardewNpcDebugActionService(IStardewBridgeDiscovery discovery, HttpClient httpClient)
        : this(discovery, snapshot => new StardewCommandService(
            new SmapiModApiClient(httpClient, snapshot.Options),
            StardewBridgeRuntimeIdentity.RequireSaveId(snapshot)))
    {
    }

    public StardewNpcDebugActionService(
        IStardewBridgeDiscovery discovery,
        Func<StardewBridgeDiscoverySnapshot, IGameCommandService> commandServiceFactory)
    {
        _discovery = discovery;
        _commandServiceFactory = commandServiceFactory;
    }

    public async Task<GameCommandResult> SpeakAsync(string npcId, string text, CancellationToken ct)
        => await SpeakAsync(NpcBodyBinding.FromLogicalId(npcId, "stardew"), text, ct);

    public async Task<GameCommandResult> SpeakAsync(NpcBodyBinding bodyBinding, string text, CancellationToken ct)
    {
        var traceId = $"trace_manual_speak_{Guid.NewGuid():N}";
        ArgumentNullException.ThrowIfNull(bodyBinding);
        if (string.IsNullOrWhiteSpace(bodyBinding.NpcId) || string.IsNullOrWhiteSpace(text))
            return new GameCommandResult(false, "", StardewCommandStatuses.Failed, StardewBridgeErrorCodes.InvalidTarget, traceId);

        if (!_discovery.TryReadLatest(out var snapshot, out var failureReason) || snapshot is null)
            return new GameCommandResult(false, "", StardewCommandStatuses.Failed, failureReason ?? StardewBridgeErrorCodes.BridgeUnavailable, traceId);

        if (!StardewBridgeRuntimeIdentity.TryGetSaveId(snapshot, out _))
            return new GameCommandResult(false, "", StardewCommandStatuses.Failed, StardewBridgeErrorCodes.BridgeStaleDiscovery, traceId);

        var payload = new System.Text.Json.Nodes.JsonObject
        {
            ["text"] = text,
            ["channel"] = "manual_debug"
        };
        var action = new GameAction(
            bodyBinding.NpcId,
            "stardew-valley",
            GameActionType.Speak,
            traceId,
            $"manual_speak_{bodyBinding.NpcId}_{Guid.NewGuid():N}",
            new GameActionTarget("player"),
            "manual desktop debug speak",
            payload,
            BodyBinding: bodyBinding);

        try
        {
            return await _commandServiceFactory(snapshot).SubmitAsync(action, ct);
        }
        catch
        {
            return new GameCommandResult(false, "", StardewCommandStatuses.Failed, StardewBridgeErrorCodes.BridgeUnavailable, traceId);
        }
    }

    public async Task<GameCommandResult> RepositionToTownAsync(string npcId, CancellationToken ct)
        => await RepositionToTownAsync(NpcBodyBinding.FromLogicalId(npcId, "stardew"), ct);

    public async Task<GameCommandResult> RepositionToTownAsync(NpcBodyBinding bodyBinding, CancellationToken ct)
    {
        var traceId = $"trace_manual_reposition_{Guid.NewGuid():N}";
        ArgumentNullException.ThrowIfNull(bodyBinding);
        if (string.IsNullOrWhiteSpace(bodyBinding.NpcId))
            return new GameCommandResult(false, "", StardewCommandStatuses.Failed, StardewBridgeErrorCodes.InvalidTarget, traceId);

        if (!_discovery.TryReadLatest(out var snapshot, out var failureReason) || snapshot is null)
            return new GameCommandResult(false, "", StardewCommandStatuses.Failed, failureReason ?? StardewBridgeErrorCodes.BridgeUnavailable, traceId);

        if (!StardewBridgeRuntimeIdentity.TryGetSaveId(snapshot, out _))
            return new GameCommandResult(false, "", StardewCommandStatuses.Failed, StardewBridgeErrorCodes.BridgeStaleDiscovery, traceId);

        var payload = new System.Text.Json.Nodes.JsonObject
        {
            ["target"] = "town"
        };
        var action = new GameAction(
            bodyBinding.NpcId,
            "stardew-valley",
            GameActionType.DebugReposition,
            traceId,
            $"manual_reposition_{bodyBinding.NpcId}_{Guid.NewGuid():N}",
            new GameActionTarget("debug_reposition"),
            "manual desktop debug reposition to town",
            payload,
            BodyBinding: bodyBinding);

        try
        {
            return await _commandServiceFactory(snapshot).SubmitAsync(action, ct);
        }
        catch
        {
            return new GameCommandResult(false, "", StardewCommandStatuses.Failed, StardewBridgeErrorCodes.BridgeUnavailable, traceId);
        }
    }

    public async Task<GameCommandResult> ProbeBeachRouteAsync(string npcId, CancellationToken ct)
        => await ProbeBeachRouteAsync(NpcBodyBinding.FromLogicalId(npcId, "stardew"), ct);

    public async Task<GameCommandResult> ProbeBeachRouteAsync(NpcBodyBinding bodyBinding, CancellationToken ct)
        => await SendToBeachAsync(bodyBinding, "trace_manual_route_probe", "manual_route_probe", ct);

    public async Task<GameCommandResult> SendHaleyToBeachAsync(CancellationToken ct)
    {
        var bodyBinding = new NpcBodyBinding(
            HaleyNpcId,
            HaleyTargetEntityId,
            HaleySmapiName,
            HaleyDisplayName,
            "stardew");
        return await SendToBeachAsync(bodyBinding, "trace_manual_haley_beach", "manual_haley_beach", ct);
    }

    private async Task<GameCommandResult> SendToBeachAsync(
        NpcBodyBinding bodyBinding,
        string tracePrefix,
        string commandPrefix,
        CancellationToken ct)
    {
        var traceId = $"{tracePrefix}_{Guid.NewGuid():N}";
        ArgumentNullException.ThrowIfNull(bodyBinding);
        if (string.IsNullOrWhiteSpace(bodyBinding.NpcId))
            return new GameCommandResult(false, "", StardewCommandStatuses.Failed, StardewBridgeErrorCodes.InvalidTarget, traceId);

        if (!_discovery.TryReadLatest(out var snapshot, out var failureReason) || snapshot is null)
            return new GameCommandResult(false, "", StardewCommandStatuses.Failed, failureReason ?? StardewBridgeErrorCodes.BridgeUnavailable, traceId);

        if (!StardewBridgeRuntimeIdentity.TryGetSaveId(snapshot, out _))
            return new GameCommandResult(false, "", StardewCommandStatuses.Failed, StardewBridgeErrorCodes.BridgeStaleDiscovery, traceId);

        var payload = new System.Text.Json.Nodes.JsonObject
        {
            ["facingDirection"] = BeachShorelineFacingDirection,
            ["thought"] = "manual desktop debug move to Beach shoreline",
            ["targetSource"] = "map-skill:stardew.navigation.poi.beach-shoreline"
        };
        var action = new GameAction(
            bodyBinding.NpcId,
            "stardew-valley",
            GameActionType.Move,
            traceId,
            $"{commandPrefix}_{bodyBinding.NpcId}_{Guid.NewGuid():N}",
            new GameActionTarget(
                "tile",
                BeachShorelineLocationName,
                new GameTile(BeachShorelineTileX, BeachShorelineTileY)),
            "manual desktop debug move to Beach shoreline",
            payload,
            BodyBinding: bodyBinding);

        try
        {
            return await _commandServiceFactory(snapshot).SubmitAsync(action, ct);
        }
        catch
        {
            return new GameCommandResult(false, "", StardewCommandStatuses.Failed, StardewBridgeErrorCodes.BridgeUnavailable, traceId);
        }
    }
}

internal static class StardewBridgeRuntimeIdentity
{
    public static bool TryGetSaveId(StardewBridgeDiscoverySnapshot snapshot, out string saveId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var normalized = NormalizeSaveId(snapshot.SaveId);
        if (normalized is null)
        {
            saveId = string.Empty;
            return false;
        }

        saveId = normalized;
        return true;
    }

    public static string RequireSaveId(StardewBridgeDiscoverySnapshot snapshot)
        => TryGetSaveId(snapshot, out var saveId)
            ? saveId
            : throw new InvalidOperationException(StardewBridgeErrorCodes.BridgeStaleDiscovery);

    public static string? NormalizeSaveId(string? saveId)
        => string.IsNullOrWhiteSpace(saveId) ? null : saveId.Trim();
}
