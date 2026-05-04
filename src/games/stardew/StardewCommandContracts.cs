namespace Hermes.Agent.Games.Stardew;

public static class StardewBridgeRoutes
{
    public const string Health = "/health";
    public const string QueryStatus = "/query/status";
    public const string QueryWorldSnapshot = "/query/world_snapshot";
    public const string EventsPoll = "/events/poll";
    public const string TaskMove = "/task/move";
    public const string TaskStatus = "/task/status";
    public const string TaskLookup = "/task/lookup";
    public const string TaskCancel = "/task/cancel";
    public const string ActionSpeak = "/action/speak";
    public const string ActionOpenPrivateChat = "/action/open_private_chat";
}

public static class StardewCommandStatuses
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string Blocked = "blocked";
    public const string Expired = "expired";
}

public static class StardewBridgeErrorCodes
{
    public const string BridgeUnavailable = "bridge_unavailable";
    public const string WorldNotReady = "world_not_ready";
    public const string PlayerNotFree = "player_not_free";
    public const string FestivalBlocked = "festival_blocked";
    public const string CutsceneBlocked = "cutscene_blocked";
    public const string MenuBlocked = "menu_blocked";
    public const string DayTransition = "day_transition";
    public const string InvalidTarget = "invalid_target";
    public const string PathBlocked = "path_blocked";
    public const string PathUnreachable = "path_unreachable";
    public const string InvalidState = "invalid_state";
    public const string CommandConflict = "command_conflict";
    public const string CommandNotFound = "command_not_found";
    public const string CommandExpired = "command_expired";
    public const string CommandStuck = "command_stuck";
    public const string ActionSlotBusy = "action_slot_busy";
    public const string ActionSlotTimeout = "action_slot_timeout";
    public const string IdempotencyConflict = "idempotency_conflict";
    public const string BridgeUnauthorized = "bridge_unauthorized";
    public const string BridgeStaleDiscovery = "bridge_stale_discovery";
}
