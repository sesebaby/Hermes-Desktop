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
                ? Game1.getCharacterFromName(npcId, mustBeVillager: false, includeEventActors: false)
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
        var events = _events.Poll(envelope.Payload.Since, envelope.Payload.NpcId ?? envelope.NpcId);
        var response = new BridgeResponse<EventPollData>(
            true,
            envelope.TraceId,
            envelope.RequestId,
            null,
            "completed",
            new EventPollData(events),
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
        var requestedNpc = envelope.Payload.NpcId ?? envelope.NpcId;
        if (!Context.IsWorldReady || Game1.player is null)
            return Error<StatusQuery, NpcStatusData>(envelope, "world_not_ready", "The Stardew world is not ready.", retryable: true);

        if (string.IsNullOrWhiteSpace(requestedNpc))
            return Error<StatusQuery, NpcStatusData>(envelope, "invalid_target", "npcId is required.", retryable: false);

        var npc = Game1.getCharacterFromName(requestedNpc, mustBeVillager: false, includeEventActors: false);
        if (npc is null)
            return Error<StatusQuery, NpcStatusData>(envelope, "invalid_target", "NPC was not found.", retryable: false);

        var isInDialogue = Game1.activeClickableMenu is DialogueBox dialogueBox &&
                           string.Equals(dialogueBox.characterDialogue?.speaker?.Name ?? Game1.currentSpeaker?.Name, npc.Name, StringComparison.OrdinalIgnoreCase);
        var blockedReason = BuildBlockedReason();
        var data = new NpcStatusData(
            npc.Name.ToLowerInvariant(),
            npc.Name,
            npc.displayName,
            npc.currentLocation?.NameOrUniqueName ?? npc.currentLocation?.Name ?? "unknown",
            new TileDto((int)npc.Tile.X, (int)npc.Tile.Y),
            npc.isMoving(),
            isInDialogue,
            blockedReason is null,
            blockedReason,
            null,
            null);

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
