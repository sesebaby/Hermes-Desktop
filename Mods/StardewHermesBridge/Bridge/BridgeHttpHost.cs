namespace StardewHermesBridge.Bridge;

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewHermesBridge.Logging;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Quests;

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

    public bool IsRunning => _listener is not null && Port > 0 && !string.IsNullOrWhiteSpace(BridgeToken);

    public void Start(string host, int preferredPort)
    {
        if (_listener is not null)
            return;

        BridgeToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        Port = preferredPort;
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://{host}:{Port}/");
        try
        {
            listener.Start();
        }
        catch (Exception ex)
        {
            listener.Close();
            _listener = null;
            _cts = null;
            BridgeToken = string.Empty;
            Port = 0;
            _logger.Write("bridge_start_failed", null, "bridge", "bridge", null, "failed", ex.Message);
            return;
        }

        _listener = listener;
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
        BridgeToken = string.Empty;
        Port = 0;
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
                case "/debug/npc/reposition":
                    await HandleDebugNpcRepositionAsync(context, ct);
                    return;
                case "/query/status":
                    await HandleQueryStatusAsync(context, ct);
                    return;
                case "/query/world_snapshot":
                    await HandleQueryWorldSnapshotAsync(context, ct);
                    return;
                case "/query/player_status":
                    await HandleQueryPlayerStatusAsync(context, ct);
                    return;
                case "/query/progress_status":
                    await HandleQueryProgressStatusAsync(context, ct);
                    return;
                case "/query/social_status":
                    await HandleQuerySocialStatusAsync(context, ct);
                    return;
                case "/query/quest_status":
                    await HandleQueryQuestStatusAsync(context, ct);
                    return;
                case "/query/farm_status":
                    await HandleQueryFarmStatusAsync(context, ct);
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

    private async Task HandleDebugNpcRepositionAsync(HttpListenerContext context, CancellationToken ct)
    {
        var envelope = await ReadJsonAsync<BridgeEnvelope<DebugRepositionPayload>>(context.Request, ct);
        var response = await _commands.RepositionNpcAsync(envelope, ct);
        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response, ct);
    }

    private async Task HandleQueryStatusAsync(HttpListenerContext context, CancellationToken ct)
    {
        var envelope = await ReadJsonAsync<BridgeEnvelope<StatusQuery>>(context.Request, ct);
        var started = System.Diagnostics.Stopwatch.StartNew();
        var response = BuildStatusResponse(envelope);
        started.Stop();
        LogQueryCompleted("query_status_completed", envelope.NpcId ?? envelope.Payload?.NpcId, envelope.TraceId, response.Data, started.ElapsedMilliseconds);
        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response, ct);
    }

    private async Task HandleQueryPlayerStatusAsync(HttpListenerContext context, CancellationToken ct)
    {
        var envelope = await ReadJsonAsync<BridgeEnvelope<EmptyStatusQuery>>(context.Request, ct);
        var started = System.Diagnostics.Stopwatch.StartNew();
        var response = BuildPlayerStatusResponse(envelope);
        started.Stop();
        LogFactQueryCompleted("player_status_query_completed", envelope.NpcId, envelope.TraceId, response.Data, started.ElapsedMilliseconds);
        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response, ct);
    }

    private async Task HandleQueryProgressStatusAsync(HttpListenerContext context, CancellationToken ct)
    {
        var envelope = await ReadJsonAsync<BridgeEnvelope<EmptyStatusQuery>>(context.Request, ct);
        var started = System.Diagnostics.Stopwatch.StartNew();
        var response = BuildProgressStatusResponse(envelope);
        started.Stop();
        LogFactQueryCompleted("progress_status_query_completed", envelope.NpcId, envelope.TraceId, response.Data, started.ElapsedMilliseconds);
        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response, ct);
    }

    private async Task HandleQuerySocialStatusAsync(HttpListenerContext context, CancellationToken ct)
    {
        var envelope = await ReadJsonAsync<BridgeEnvelope<SocialStatusQuery>>(context.Request, ct);
        var started = System.Diagnostics.Stopwatch.StartNew();
        var response = BuildSocialStatusResponse(envelope);
        started.Stop();
        LogFactQueryCompleted("social_status_query_completed", envelope.NpcId ?? envelope.Payload?.TargetNpcId, envelope.TraceId, response.Data, started.ElapsedMilliseconds);
        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response, ct);
    }

    private async Task HandleQueryQuestStatusAsync(HttpListenerContext context, CancellationToken ct)
    {
        var envelope = await ReadJsonAsync<BridgeEnvelope<EmptyStatusQuery>>(context.Request, ct);
        var started = System.Diagnostics.Stopwatch.StartNew();
        var response = BuildQuestStatusResponse(envelope);
        started.Stop();
        LogFactQueryCompleted("quest_status_query_completed", envelope.NpcId, envelope.TraceId, response.Data, started.ElapsedMilliseconds);
        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response, ct);
    }

    private async Task HandleQueryFarmStatusAsync(HttpListenerContext context, CancellationToken ct)
    {
        var envelope = await ReadJsonAsync<BridgeEnvelope<EmptyStatusQuery>>(context.Request, ct);
        var started = System.Diagnostics.Stopwatch.StartNew();
        var response = BuildFarmStatusResponse(envelope);
        started.Stop();
        LogFactQueryCompleted("farm_status_query_completed", envelope.NpcId, envelope.TraceId, response.Data, started.ElapsedMilliseconds);
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
        var blockedReason = BuildBlockedReason(npc);
        var currentTile = GetCurrentTile(npc);
        var destinations = BuildDestinations(npc, blockedReason, currentTile);
        var nearbyTiles = BuildNearbyTiles(npc, blockedReason, currentTile);
        var playerScene = BuildPlayerScene(npc);
        var data = new NpcStatusData(
            npc.Name.ToLowerInvariant(),
            npc.Name,
            npc.displayName,
            npc.currentLocation?.NameOrUniqueName ?? npc.currentLocation?.Name ?? "unknown",
            currentTile,
            npc.isMoving(),
            isInDialogue,
            blockedReason is null,
            blockedReason,
            null,
            null,
            null,
            null,
            destinations,
            nearbyTiles,
            Game1.timeOfDay,
            Game1.currentSeason,
            Game1.dayOfMonth,
            GetWeatherFact(npc.currentLocation),
            playerScene);

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

    private BridgeResponse<PlayerStatusData> BuildPlayerStatusResponse(BridgeEnvelope<EmptyStatusQuery> envelope)
    {
        if (!Context.IsWorldReady || Game1.player is null)
            return Error<EmptyStatusQuery, PlayerStatusData>(envelope, "world_not_ready", "The Stardew world is not ready.", retryable: true);

        var player = Game1.player;
        var unknownFields = new List<string>();
        var facts = new List<string>
        {
            $"playerName={Safe(player.Name)}",
            $"playerDisplayName={Safe(player.displayName)}",
            $"playerLocation={GetLocationName(player.currentLocation)}",
            $"playerTile={player.TilePoint.X},{player.TilePoint.Y}",
            $"playerHeldItem={FormatItemName(player.ActiveItem)}",
            $"playerCurrentTool={FormatItemName(player.CurrentTool)}",
            $"playerMoney={player.Money}",
            $"playerHat={FormatEquipmentName(player.hat.Value)}",
            $"playerShirt={FormatEquipmentName(player.shirtItem.Value)}",
            $"playerPants={FormatEquipmentName(player.pantsItem.Value)}",
            $"playerBoots={FormatEquipmentName(player.boots.Value)}",
            $"inventorySummary={BuildInventorySummary(player)}"
        };
        var detailFacts = new List<string>
        {
            $"playerStamina={(int)player.Stamina}",
            $"playerMaxStamina={player.MaxStamina}",
            $"playerHealth={player.health}",
            $"playerMaxHealth={player.maxHealth}",
            $"playerSpouse={EmptyToNone(player.spouse)}",
            $"playerFarmName={Safe(player.farmName.Value)}"
        };
        facts.AddRange(detailFacts);

        var summary = $"玩家现在在 {GetLocationName(player.currentLocation)} 的 {player.TilePoint.X},{player.TilePoint.Y}，手里拿着 {FormatItemName(player.ActiveItem)}，有 {player.Money}g，婚姻状态是 {EmptyToNone(player.spouse)}。";
        var data = new PlayerStatusData(summary, facts.Take(12).ToArray(), BuildStatus(unknownFields), unknownFields);
        return Completed(envelope, data);
    }

    private BridgeResponse<ProgressStatusData> BuildProgressStatusResponse(BridgeEnvelope<EmptyStatusQuery> envelope)
    {
        if (!Context.IsWorldReady || Game1.player is null)
            return Error<EmptyStatusQuery, ProgressStatusData>(envelope, "world_not_ready", "The Stardew world is not ready.", retryable: true);

        var player = Game1.player;
        var unknownFields = new List<string>();
        var facts = new List<string>
        {
            $"year={Game1.year}",
            $"season={Game1.currentSeason}",
            $"dayOfMonth={Game1.dayOfMonth}",
            $"gameTime={Game1.timeOfDay}",
            $"gameClock={FormatGameClock(Game1.timeOfDay)}",
            $"farmName={Safe(player.farmName.Value)}",
            $"money={player.Money}",
            $"totalMoneyEarned={player.totalMoneyEarned}",
            $"farmingLevel={player.FarmingLevel}",
            $"miningLevel={player.MiningLevel}",
            $"foragingLevel={player.ForagingLevel}",
            $"fishingLevel={player.FishingLevel}",
            $"combatLevel={player.CombatLevel}",
            $"deepestMineLevel={player.deepestMineLevel}",
            $"houseUpgradeLevel={player.HouseUpgradeLevel}"
        };
        unknownFields.Add("communityCenterOrJojaSummary");

        var summary = $"现在是第 {Game1.year} 年 {Game1.currentSeason} {Game1.dayOfMonth} 日 {FormatGameClock(Game1.timeOfDay)}。玩家农场叫 {Safe(player.farmName.Value)}，最深矿井层数 {player.deepestMineLevel}。";
        var data = new ProgressStatusData(summary, facts.Take(12).ToArray(), BuildStatus(unknownFields), unknownFields);
        return Completed(envelope, data);
    }

    private BridgeResponse<SocialStatusData> BuildSocialStatusResponse(BridgeEnvelope<SocialStatusQuery> envelope)
    {
        if (!Context.IsWorldReady || Game1.player is null)
            return Error<SocialStatusQuery, SocialStatusData>(envelope, "world_not_ready", "The Stardew world is not ready.", retryable: true);

        var targetNpc = envelope.Payload?.TargetNpcId ?? envelope.NpcId;
        var unknownFields = new List<string>();
        if (string.IsNullOrWhiteSpace(targetNpc))
            return Error<SocialStatusQuery, SocialStatusData>(envelope, "invalid_target", "targetNpcId or npcId is required.", retryable: false);

        var npc = BridgeNpcResolver.Resolve(targetNpc);
        if (npc is null)
            return Error<SocialStatusQuery, SocialStatusData>(envelope, "invalid_target", "NPC was not found.", retryable: false);

        var player = Game1.player;
        player.friendshipData.TryGetValue(npc.Name, out var friendship);
        var friendshipPoints = friendship?.Points ?? 0;
        var hearts = friendshipPoints / NPC.friendshipPointsPerHeartLevel;
        var facts = new List<string>
        {
            $"targetNpc={npc.Name}",
            $"targetDisplayName={npc.displayName}",
            $"friendshipPoints={friendshipPoints}",
            $"friendshipHearts={hearts}",
            $"talkedToday={Bool(friendship?.TalkedToToday ?? false)}",
            $"giftsToday={friendship?.GiftsToday ?? 0}",
            $"giftsThisWeek={friendship?.GiftsThisWeek ?? 0}",
            $"spouse={EmptyToNone(player.spouse)}",
            $"isSpouse={Bool(string.Equals(player.spouse, npc.Name, StringComparison.OrdinalIgnoreCase))}",
            $"daysMarried={friendship?.DaysMarried ?? 0}"
        };
        unknownFields.Add("recentGiftItems");
        unknownFields.Add("giftTasteSummary");

        var summary = $"玩家和 {npc.displayName} 目前约 {hearts} 心，今天{(friendship?.TalkedToToday == true ? "已经" : "还没")}说过话，本周送礼 {friendship?.GiftsThisWeek ?? 0} 次。玩家配偶是 {EmptyToNone(player.spouse)}。";
        var data = new SocialStatusData(summary, facts.Take(12).ToArray(), BuildStatus(unknownFields), unknownFields);
        return Completed(envelope, data);
    }

    private BridgeResponse<QuestStatusData> BuildQuestStatusResponse(BridgeEnvelope<EmptyStatusQuery> envelope)
    {
        if (!Context.IsWorldReady || Game1.player is null)
            return Error<EmptyStatusQuery, QuestStatusData>(envelope, "world_not_ready", "The Stardew world is not ready.", retryable: true);

        var quests = Game1.player.questLog?.ToArray() ?? Array.Empty<Quest>();
        var facts = new List<string> { $"questCount={quests.Length}" };
        foreach (var (quest, index) in quests.Take(5).Select((quest, index) => (quest, index)))
        {
            facts.Add($"quest[{index}]=title={Safe(quest.questTitle)},description={TruncateFact(Safe(quest.questDescription), 80)}");
        }

        var summary = quests.Length == 0
            ? "玩家当前没有可见任务。"
            : $"玩家当前有 {quests.Length} 个任务，首个任务是 {Safe(quests[0].questTitle)}。";
        var data = new QuestStatusData(summary, facts.Take(12).ToArray(), "completed", Array.Empty<string>());
        return Completed(envelope, data);
    }

    private BridgeResponse<FarmStatusData> BuildFarmStatusResponse(BridgeEnvelope<EmptyStatusQuery> envelope)
    {
        if (!Context.IsWorldReady || Game1.player is null)
            return Error<EmptyStatusQuery, FarmStatusData>(envelope, "world_not_ready", "The Stardew world is not ready.", retryable: true);

        var player = Game1.player;
        var farm = Game1.getFarm();
        var unknownFields = new List<string>
        {
            "readyCropCount",
            "needsWateringCount",
            "animalCount"
        };
        var facts = new List<string>
        {
            $"farmName={Safe(player.farmName.Value)}",
            $"farmLocation={GetLocationName(farm)}",
            $"season={Game1.currentSeason}",
            $"dayOfMonth={Game1.dayOfMonth}",
            $"weather={GetWeatherFact(farm)}",
            $"money={player.Money}"
        };

        var summary = $"农场叫 {Safe(player.farmName.Value)}，今天 {Game1.currentSeason} {Game1.dayOfMonth} 日，天气是 {GetWeatherFact(farm)}。首版还没有全图作物和动物扫描。";
        var data = new FarmStatusData(summary, facts.Take(12).ToArray(), BuildStatus(unknownFields), unknownFields);
        return Completed(envelope, data);
    }

    private static BridgeResponse<TData> Error<TPayload, TData>(BridgeEnvelope<TPayload> envelope, string code, string message, bool retryable)
        => new(false, envelope.TraceId, envelope.RequestId, null, "failed", default, new BridgeError(code, message, retryable), new { });

    private static BridgeResponse<TData> Completed<TPayload, TData>(BridgeEnvelope<TPayload> envelope, TData data)
        => new(true, envelope.TraceId, envelope.RequestId, null, "completed", data, null, new { });

    private static PlayerSceneData BuildPlayerScene(NPC npc)
    {
        var player = Game1.player;
        var playerLocation = player.currentLocation;
        var sameLocation = playerLocation is not null && ReferenceEquals(playerLocation, npc.currentLocation);
        var distance = sameLocation
            ? Math.Abs(player.TilePoint.X - npc.TilePoint.X) + Math.Abs(player.TilePoint.Y - npc.TilePoint.Y)
            : (int?)null;
        var reachability = !sameLocation
            ? "other_map"
            : distance <= 8
                ? "near_same_map"
                : "same_map_far";
        return new PlayerSceneData(
            GetLocationName(playerLocation),
            new TileDto(player.TilePoint.X, player.TilePoint.Y),
            sameLocation,
            distance,
            reachability,
            BuildPlayerAvailability(),
            FormatItemName(player.ActiveItem));
    }

    private static string BuildPlayerAvailability()
    {
        if (Game1.eventUp)
            return "event_active";
        if (Game1.activeClickableMenu is DialogueBox)
            return "dialogue_open";
        if (Game1.activeClickableMenu is not null)
            return "menu_open";
        return "free";
    }

    private static string? BuildBlockedReason(NPC npc)
    {
        if (Game1.eventUp)
            return "event_active";
        if (Game1.activeClickableMenu is not null)
            return "menu_open";
        if (npc.isMoving())
            return "npc_moving";
        return null;
    }

    private static TileDto GetCurrentTile(NPC npc)
        => new(npc.TilePoint.X, npc.TilePoint.Y);

    private static string GetLocationName(GameLocation? location)
        => location?.NameOrUniqueName ?? location?.Name ?? "unknown";

    private static string FormatItemName(Item? item)
        => string.IsNullOrWhiteSpace(item?.DisplayName)
            ? string.IsNullOrWhiteSpace(item?.Name) ? "empty" : item.Name
            : item.DisplayName;

    private static string FormatEquipmentName(Item? item)
        => item is null ? "none" : FormatItemName(item);

    private static string BuildInventorySummary(Farmer player)
    {
        var items = player.Items?
            .Where(item => item is not null)
            .Take(5)
            .Select(FormatItemName)
            .ToArray() ?? Array.Empty<string>();
        return items.Length == 0 ? "empty" : string.Join("|", items);
    }

    private static string BuildStatus(IReadOnlyList<string> unknownFields)
        => unknownFields.Count > 0 ? "degraded" : "completed";

    private static string Safe(string? value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();

    private static string EmptyToNone(string? value)
        => string.IsNullOrWhiteSpace(value) ? "none" : value.Trim();

    private static string Bool(bool value) => value ? "true" : "false";

    private static string TruncateFact(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static IReadOnlyList<MoveCandidateData> BuildNearbyTiles(NPC npc, string? blockedReason, TileDto currentTile)
    {
        if (!string.IsNullOrWhiteSpace(blockedReason) || npc.currentLocation is null)
            return Array.Empty<MoveCandidateData>();

        var location = npc.currentLocation;
        var locationName = location.NameOrUniqueName ?? location.Name;
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

        return deltas
            .Select(delta => new TileDto(currentTile.X + delta.X, currentTile.Y + delta.Y))
            .Where(tile => IsRouteValidMoveCandidate(npc, location, currentTile, tile))
            .Take(3)
            .Select(tile => new MoveCandidateData(locationName, tile, "same_location_route_valid_reposition"))
            .ToArray();
    }

    private static IReadOnlyList<DestinationData> BuildDestinations(
        NPC npc,
        string? blockedReason,
        TileDto currentTile)
    {
        if (!string.IsNullOrWhiteSpace(blockedReason) || npc.currentLocation is null)
            return Array.Empty<DestinationData>();

        var location = npc.currentLocation;
        var locationName = location.NameOrUniqueName ?? location.Name;
        return BridgeMoveCandidateSelector.SelectDestinations(
            locationName,
            npc.Name,
            BridgeDestinationRegistry.GetForLocation(locationName, npc.Name),
            definition => ResolveDestinationCandidate(npc, location, currentTile, definition));
    }

    private static BridgeResolvedDestinationCandidate? ResolveDestinationCandidate(
        NPC npc,
        GameLocation location,
        TileDto currentTile,
        BridgePlaceCandidateDefinition definition)
    {
        var direct = BridgeMovementPathProbe.Probe(
            currentTile,
            definition.Tile,
            tile => BridgeMovementPathProbe.CheckTargetAffordance(location, tile),
            () => BridgeMovementPathProbe.FindSchedulePath(npc, location, currentTile, definition.Tile),
            tile => BridgeMovementPathProbe.CheckRouteStepSafety(location, tile));
        if (direct.Status == BridgeRouteProbeStatus.RouteValid)
            return new BridgeResolvedDestinationCandidate(definition.Tile, definition.FacingDirection);

        if (direct.Status is not (BridgeRouteProbeStatus.TargetUnsafe or BridgeRouteProbeStatus.PathEmpty))
            return null;

        var fallback = BridgeMovementPathProbe.FindClosestReachableNeighbor(
            npc,
            location,
            definition.Tile,
            currentTile);
        if (fallback is null)
            return null;

        return new BridgeResolvedDestinationCandidate(
            fallback.Value.StandTile,
            fallback.Value.FacingDirection);
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
            $"location={Game1.currentLocation?.NameOrUniqueName ?? Game1.currentLocation?.Name ?? "unknown"}",
            $"gameTime={Game1.timeOfDay}",
            $"gameClock={FormatGameClock(Game1.timeOfDay)}",
            $"season={Game1.currentSeason}",
            $"dayOfMonth={Game1.dayOfMonth}"
        };
        facts.Add($"weather={GetWeatherFact(Game1.currentLocation)}");
        if (Game1.activeClickableMenu is not null)
            facts.Add($"activeMenu={Game1.activeClickableMenu.GetType().Name}");
        if (Game1.eventUp)
            facts.Add("event_active");
        return facts;
    }

    private static string FormatGameClock(int timeOfDay)
        => $"{timeOfDay / 100:00}:{timeOfDay % 100:00}";

    private static string GetWeatherFact(GameLocation? location)
        => location is not null && Game1.IsRainingHere(location) ? "rain" : "sunny";

    private void LogQueryCompleted(string endpoint, string? npcId, string traceId, NpcStatusData? data, long durationMs)
    {
        var detail = data is null
            ? $"status=failed;durationMs={durationMs}"
            : $"status=completed;durationMs={durationMs};gameTime={data.GameTime};npcLocation={data.LocationName};playerReachability={data.Player?.Reachability ?? "unknown"};playerAvailability={data.Player?.Availability ?? "unknown"};heldItem={data.Player?.HeldItem ?? "unknown"}";
        _logger.Write(endpoint, npcId, "query_status", traceId, null, data is null ? "failed" : "completed", detail);
    }

    private void LogFactQueryCompleted<TData>(string endpoint, string? npcId, string traceId, TData? data, long durationMs)
        where TData : class
    {
        var (status, detail) = data switch
        {
            PlayerStatusData value => (value.Status, $"status={value.Status};durationMs={durationMs};payloadChars={EstimatePayloadChars(value)};playerLocation={FindFact(value.Facts, "playerLocation")};playerTile={FindFact(value.Facts, "playerTile")};heldItem={FindFact(value.Facts, "playerHeldItem")};money={FindFact(value.Facts, "playerMoney")};equipmentCount={CountFacts(value.Facts, "playerHat", "playerShirt", "playerPants", "playerBoots")};unknownFields={string.Join("|", value.UnknownFields)}"),
            ProgressStatusData value => (value.Status, $"status={value.Status};durationMs={durationMs};payloadChars={EstimatePayloadChars(value)};year={FindFact(value.Facts, "year")};season={FindFact(value.Facts, "season")};day={FindFact(value.Facts, "dayOfMonth")};mineLevel={FindFact(value.Facts, "deepestMineLevel")};unknownFields={string.Join("|", value.UnknownFields)}"),
            SocialStatusData value => (value.Status, $"status={value.Status};durationMs={durationMs};payloadChars={EstimatePayloadChars(value)};targetNpc={FindFact(value.Facts, "targetNpc")};hearts={FindFact(value.Facts, "friendshipHearts")};talkedToday={FindFact(value.Facts, "talkedToday")};giftsThisWeek={FindFact(value.Facts, "giftsThisWeek")};spouse={FindFact(value.Facts, "spouse")};unknownFields={string.Join("|", value.UnknownFields)}"),
            QuestStatusData value => (value.Status, $"status={value.Status};durationMs={durationMs};payloadChars={EstimatePayloadChars(value)};questCount={FindFact(value.Facts, "questCount")};returnedCount={value.Facts.Count(fact => fact.StartsWith("quest[", StringComparison.OrdinalIgnoreCase))};unknownFields={string.Join("|", value.UnknownFields)}"),
            FarmStatusData value => (value.Status, $"status={value.Status};durationMs={durationMs};payloadChars={EstimatePayloadChars(value)};farmName={FindFact(value.Facts, "farmName")};farmType=unknown;scanTiles=0;readyCropCount={FindFact(value.Facts, "readyCropCount")};needsWateringCount={FindFact(value.Facts, "needsWateringCount")};animalCount={FindFact(value.Facts, "animalCount")};unknownFields={string.Join("|", value.UnknownFields)}"),
            _ => ("failed", $"status=failed;durationMs={durationMs}")
        };
        _logger.Write(endpoint, npcId, "query_status", traceId, null, status, detail);
    }

    private static int EstimatePayloadChars<TData>(TData data)
        => JsonSerializer.Serialize(data, JsonOptions).Length;

    private static string FindFact(IReadOnlyList<string> facts, string key)
    {
        var prefix = key + "=";
        var value = facts.FirstOrDefault(fact => fact.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return value is null ? "unknown" : value[prefix.Length..];
    }

    private static int CountFacts(IReadOnlyList<string> facts, params string[] keys)
        => keys.Count(key => facts.Any(fact => fact.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase)));
}
