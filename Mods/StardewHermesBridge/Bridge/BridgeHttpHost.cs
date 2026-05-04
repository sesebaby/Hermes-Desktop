namespace StardewHermesBridge.Bridge;

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewHermesBridge.Logging;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

public sealed class BridgeHttpHost
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly BridgeCommandQueue _commands;
    private readonly BridgeEventBuffer _events;
    private readonly SmapiBridgeLogger _logger;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;

    public BridgeHttpHost(BridgeCommandQueue commands, BridgeEventBuffer events, SmapiBridgeLogger logger)
    {
        _commands = commands;
        _events = events;
        _logger = logger;
    }

    public int Port { get; private set; }

    public string BridgeToken { get; private set; } = "";

    public void Start(string host, int preferredPort)
    {
        if (_listener is not null)
            return;

        BridgeToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        Port = preferredPort;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://{host}:{Port}/");
        _listener.Start();
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => RunAsync(_cts.Token));
        _logger.Write("bridge_started", null, "bridge", "bridge", null, "online", $"127.0.0.1:{Port}");
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Close();
        _listener = null;
        _cts = null;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is not null)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch when (ct.IsCancellationRequested)
            {
                return;
            }

            _ = Task.Run(() => HandleAsync(context, ct), ct);
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken ct)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            if (path != "/health" && !IsAuthorized(context.Request))
            {
                await WriteJsonAsync(context.Response, HttpStatusCode.Unauthorized, new
                {
                    ok = false,
                    error = new { code = "bridge_unauthorized", message = "Missing or invalid bearer token.", retryable = false }
                }, ct);
                return;
            }

            switch (path)
            {
                case "/health":
                    await WriteJsonAsync(context.Response, HttpStatusCode.OK, new { ok = true, online = true }, ct);
                    return;
                case "/task/move":
                    await HandleMoveAsync(context, ct);
                    return;
                case "/task/status":
                    await HandleStatusAsync(context, ct);
                    return;
                case "/task/lookup":
                    await HandleLookupAsync(context, ct);
                    return;
                case "/task/cancel":
                    await HandleCancelAsync(context, ct);
                    return;
                case "/action/speak":
                    await HandleSpeakAsync(context, ct);
                    return;
                case "/action/open_private_chat":
                    await HandleOpenPrivateChatAsync(context, ct);
                    return;
                case "/query/status":
                    await HandleQueryStatusAsync(context, ct);
                    return;
                case "/query/world_snapshot":
                    await HandleQueryWorldSnapshotAsync(context, ct);
                    return;
                case "/events/poll":
                    await HandleEventsPollAsync(context, ct);
                    return;
                default:
                    await WriteJsonAsync(context.Response, HttpStatusCode.NotFound, new { ok = false }, ct);
                    return;
            }
        }
        catch (Exception ex)
        {
            _logger.Write("bridge_error", null, "bridge", "bridge", null, "failed", ex.Message);
            await WriteJsonAsync(context.Response, HttpStatusCode.InternalServerError, new { ok = false, error = ex.Message }, ct);
        }
    }

    private async Task HandleMoveAsync(HttpListenerContext context, CancellationToken ct)
    {
        var envelope = await ReadJsonAsync<BridgeEnvelope<MovePayload>>(context.Request, ct);
        var response = _commands.EnqueueMove(envelope);
        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response, ct);
    }

    private async Task HandleStatusAsync(HttpListenerContext context, CancellationToken ct)
    {
        var envelope = await ReadJsonAsync<BridgeEnvelope<TaskStatusRequest>>(context.Request, ct);
        var response = _commands.GetStatus(envelope);
        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response, ct);
    }

    private async Task HandleLookupAsync(HttpListenerContext context, CancellationToken ct)
    {
        var envelope = await ReadJsonAsync<BridgeEnvelope<TaskLookupRequest>>(context.Request, ct);
        var response = _commands.LookupByIdempotency(envelope);
        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response, ct);
    }

    private async Task HandleCancelAsync(HttpListenerContext context, CancellationToken ct)
    {
        var envelope = await ReadJsonAsync<BridgeEnvelope<TaskCancelRequest>>(context.Request, ct);
        var response = _commands.Cancel(envelope);
        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response, ct);
    }

    private async Task HandleSpeakAsync(HttpListenerContext context, CancellationToken ct)
    {
        var envelope = await ReadJsonAsync<BridgeEnvelope<SpeakPayload>>(context.Request, ct);
        var response = await _commands.SpeakAsync(envelope, ct);
        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response, ct);
    }

    private async Task HandleOpenPrivateChatAsync(HttpListenerContext context, CancellationToken ct)
    {
        var envelope = await ReadJsonAsync<BridgeEnvelope<OpenPrivateChatPayload>>(context.Request, ct);
        var response = await _commands.OpenPrivateChatAsync(envelope, ct);
        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response, ct);
    }

    private async Task HandleQueryStatusAsync(HttpListenerContext context, CancellationToken ct)
    {
        var envelope = await ReadJsonAsync<BridgeEnvelope<StatusQuery>>(context.Request, ct);
        var response = BuildStatusResponse(envelope);
        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response, ct);
    }

    private async Task HandleQueryWorldSnapshotAsync(HttpListenerContext context, CancellationToken ct)
    {
        var envelope = await ReadJsonAsync<BridgeEnvelope<WorldSnapshotQuery>>(context.Request, ct);
        var npcId = envelope.Payload.NpcId ?? envelope.NpcId;
        var entities = new List<WorldEntityData>();
        if (!string.IsNullOrWhiteSpace(npcId))
        {
            var npc = Context.IsWorldReady
                ? BridgeNpcResolver.Resolve(npcId)
                : null;
            if (npc is not null)
                entities.Add(new WorldEntityData(npc.Name.ToLowerInvariant(), npc.Name, npc.displayName, "stardew"));
        }

        var response = new BridgeResponse<WorldSnapshotData>(
            true,
            envelope.TraceId,
            envelope.RequestId,
            null,
            "completed",
            new WorldSnapshotData(
                "stardew-valley",
                string.IsNullOrWhiteSpace(envelope.SaveId) ? "unknown-save" : envelope.SaveId!,
                DateTime.UtcNow,
                entities,
                BuildWorldFacts()),
            null,
            new { });
        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response, ct);
    }

    private async Task HandleEventsPollAsync(HttpListenerContext context, CancellationToken ct)
    {
        var envelope = await ReadJsonAsync<BridgeEnvelope<EventPollQuery>>(context.Request, ct);
        var events = _events.PollBatch(envelope.Payload.Since, envelope.Payload.NpcId ?? envelope.NpcId, envelope.Payload.Sequence);
        var response = new BridgeResponse<EventPollData>(
            true,
            envelope.TraceId,
            envelope.RequestId,
            null,
            "completed",
            events,
            null,
            new { });
        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response, ct);
    }

    private bool IsAuthorized(HttpListenerRequest request)
    {
        var header = request.Headers["Authorization"];
        return string.Equals(header, $"Bearer {BridgeToken}", StringComparison.Ordinal);
    }

    private static async Task<T> ReadJsonAsync<T>(HttpListenerRequest request, CancellationToken ct)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        ct.ThrowIfCancellationRequested();
        var raw = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<T>(raw, JsonOptions)
            ?? throw new InvalidOperationException("Invalid JSON request.");
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, HttpStatusCode status, object value, CancellationToken ct)
    {
        response.StatusCode = (int)status;
        response.ContentType = "application/json";
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions));
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, 0, bytes.Length, ct);
        response.OutputStream.Close();
    }

    private BridgeResponse<NpcStatusData> BuildStatusResponse(BridgeEnvelope<StatusQuery> envelope)
    {
        var requestedNpc = envelope.Payload?.NpcId ?? envelope.NpcId;
        if (!Context.IsWorldReady || Game1.player is null)
            return Error<StatusQuery, NpcStatusData>(envelope, "world_not_ready", "The Stardew world is not ready.", retryable: true);

        if (string.IsNullOrWhiteSpace(requestedNpc))
            return Error<StatusQuery, NpcStatusData>(envelope, "invalid_target", "npcId is required.", retryable: false);

        var npc = BridgeNpcResolver.Resolve(requestedNpc);
        if (npc is null)
            return Error<StatusQuery, NpcStatusData>(envelope, "invalid_target", "NPC was not found.", retryable: false);

        var isInDialogue = Game1.activeClickableMenu is DialogueBox dialogueBox &&
                           string.Equals(dialogueBox.characterDialogue?.speaker?.Name ?? Game1.currentSpeaker?.Name, npc.Name, StringComparison.OrdinalIgnoreCase);
        var blockedReason = BuildBlockedReason();
        var destinations = BuildDestinations(npc, blockedReason);
        var nearbyTiles = BuildNearbyTiles(npc, blockedReason);
        var data = new NpcStatusData(
            npc.Name.ToLowerInvariant(),
            npc.Name,
            npc.displayName,
            npc.currentLocation?.NameOrUniqueName ?? npc.currentLocation?.Name ?? "unknown",
            new TileDto((int)(npc.Position.X / Game1.tileSize), (int)(npc.Position.Y / Game1.tileSize)),
            npc.isMoving(),
            isInDialogue,
            blockedReason is null,
            blockedReason,
            null,
            null,
            null,
            null,
            destinations,
            nearbyTiles);

        return new BridgeResponse<NpcStatusData>(
            true,
            envelope.TraceId,
            envelope.RequestId,
            null,
            "completed",
            data,
            null,
            new { });
    }

    private static BridgeResponse<TData> Error<TPayload, TData>(BridgeEnvelope<TPayload> envelope, string code, string message, bool retryable)
        => new(false, envelope.TraceId, envelope.RequestId, null, "failed", default, new BridgeError(code, message, retryable), new { });

    private static string? BuildBlockedReason()
    {
        if (Game1.eventUp)
            return "event_active";
        if (Game1.activeClickableMenu is not null)
            return "menu_open";
        return null;
    }

    private static IReadOnlyList<MoveCandidateData> BuildNearbyTiles(NPC npc, string? blockedReason)
    {
        if (!string.IsNullOrWhiteSpace(blockedReason) || npc.currentLocation is null)
            return Array.Empty<MoveCandidateData>();

        var location = npc.currentLocation;
        var locationName = location.NameOrUniqueName ?? location.Name;
        var currentX = (int)(npc.Position.X / Game1.tileSize);
        var currentY = (int)(npc.Position.Y / Game1.tileSize);
        var deltas = new (int X, int Y)[]
        {
            (1, 0),
            (-1, 0),
            (0, 1),
            (0, -1),
            (1, 1),
            (-1, 1),
            (1, -1),
            (-1, -1),
            (2, 0),
            (-2, 0),
            (0, 2),
            (0, -2)
        };

        var currentTile = new TileDto(currentX, currentY);
        return deltas
            .Select(delta => new TileDto(currentX + delta.X, currentY + delta.Y))
            .Where(tile => IsRouteValidMoveCandidate(npc, location, currentTile, tile))
            .Take(3)
            .Select(tile => new MoveCandidateData(locationName, tile, "same_location_route_valid_reposition"))
            .ToArray();
    }

    private static IReadOnlyList<DestinationData> BuildDestinations(
        NPC npc,
        string? blockedReason)
    {
        if (!string.IsNullOrWhiteSpace(blockedReason) || npc.currentLocation is null)
            return Array.Empty<DestinationData>();

        var location = npc.currentLocation;
        var locationName = location.NameOrUniqueName ?? location.Name;
        var currentTile = new TileDto((int)(npc.Position.X / Game1.tileSize), (int)(npc.Position.Y / Game1.tileSize));
        return BridgeMoveCandidateSelector.SelectDestinations(
            locationName,
            npc.Name,
            BuildPlaceCandidateDefinitions(locationName, npc.Name));
    }

    private static IEnumerable<BridgePlaceCandidateDefinition> BuildPlaceCandidateDefinitions(string locationName, string npcName)
    {
        if (string.Equals(locationName, "HaleyHouse", StringComparison.OrdinalIgnoreCase))
        {
            yield return new BridgePlaceCandidateDefinition(
                "Bedroom mirror",
                new TileDto(6, 4),
                new[] { "home", "photogenic", "Haley" },
                "check her look before deciding whether to go out",
                2,
                "Haley_Mirror");
            yield return new BridgePlaceCandidateDefinition(
                "Living room",
                new TileDto(10, 12),
                new[] { "home", "social" },
                "see what is happening downstairs",
                2,
                null);
            yield return new BridgePlaceCandidateDefinition(
                "Front door",
                new TileDto(15, 8),
                new[] { "transition", "outdoor" },
                "consider stepping outside",
                2,
                null);
            yield break;
        }

        if (string.Equals(locationName, "Town", StringComparison.OrdinalIgnoreCase))
        {
            yield return new BridgePlaceCandidateDefinition(
                "Town fountain",
                new TileDto(47, 56),
                new[] { "public", "photogenic", "social" },
                "stand somewhere bright and visible in town",
                2,
                null);
            yield return new BridgePlaceCandidateDefinition(
                "Town square",
                new TileDto(52, 68),
                new[] { "public", "social" },
                "notice who is passing through town",
                2,
                null);
            yield return new BridgePlaceCandidateDefinition(
                "Clinic path",
                new TileDto(30, 55),
                new[] { "public", "errands" },
                "walk near the town services without committing to a visit",
                2,
                null);
            yield break;
        }

        if (string.Equals(locationName, "Beach", StringComparison.OrdinalIgnoreCase))
        {
            yield return new BridgePlaceCandidateDefinition(
                "Shore photo spot",
                new TileDto(32, 34),
                new[] { "outdoor", "photogenic", "water" },
                "look for good light near the water",
                2,
                null);
            yield return new BridgePlaceCandidateDefinition(
                "Beach bridge",
                new TileDto(55, 14),
                new[] { "outdoor", "landmark" },
                "check the beach crossing and horizon",
                2,
                null);
            yield break;
        }

        if (string.Equals(locationName, "Forest", StringComparison.OrdinalIgnoreCase))
        {
            yield return new BridgePlaceCandidateDefinition(
                "Forest path",
                new TileDto(34, 48),
                new[] { "outdoor", "quiet" },
                "walk somewhere quieter and greener",
                2,
                null);
            yield break;
        }

        if (string.Equals(locationName, "Mountain", StringComparison.OrdinalIgnoreCase))
        {
            yield return new BridgePlaceCandidateDefinition(
                "Lake overlook",
                new TileDto(32, 20),
                new[] { "outdoor", "water", "photogenic" },
                "look toward the mountain lake",
                2,
                null);
            yield break;
        }

        yield return new BridgePlaceCandidateDefinition(
            $"{npcName} nearby spot",
            new TileDto(-1, -1),
            new[] { "nearby", "current-location" },
            "fallback nearby place",
            null,
            null);
    }

    private static bool IsRouteValidMoveCandidate(NPC npc, GameLocation location, TileDto currentTile, TileDto targetTile)
    {
        if (targetTile.X == currentTile.X && targetTile.Y == currentTile.Y)
            return false;

        var probe = BridgeMovementPathProbe.Probe(
            currentTile,
            targetTile,
            tile => BridgeMovementPathProbe.CheckTargetAffordance(location, tile),
            () => BridgeMovementPathProbe.FindSchedulePath(npc, location, currentTile, targetTile),
            tile => BridgeMovementPathProbe.CheckRouteStepSafety(location, tile));
        return probe.Status == BridgeRouteProbeStatus.RouteValid;
    }

    private static IReadOnlyList<string> BuildWorldFacts()
    {
        if (!Context.IsWorldReady)
            return new[] { "world_not_ready" };

        var facts = new List<string>
        {
            $"location={Game1.currentLocation?.NameOrUniqueName ?? Game1.currentLocation?.Name ?? "unknown"}"
        };
        if (Game1.activeClickableMenu is not null)
            facts.Add($"activeMenu={Game1.activeClickableMenu.GetType().Name}");
        if (Game1.eventUp)
            facts.Add("event_active");
        return facts;
    }
}
