namespace Hermes.Agent.Games.Stardew;

using System.Text.Json;
using System.Text.Json.Nodes;
using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.Runtime;

public static class StardewNpcToolFactory
{
    public static IReadOnlyList<ITool> CreateDefault(
        IGameAdapter adapter,
        NpcRuntimeDescriptor descriptor,
        Func<string>? traceIdFactory = null,
        Func<string>? idempotencyKeyFactory = null,
        int maxStatusPolls = 3)
    {
        traceIdFactory ??= () => $"trace_{descriptor.NpcId}_{Guid.NewGuid():N}";
        idempotencyKeyFactory ??= () => $"idem_{descriptor.NpcId}_{Guid.NewGuid():N}";

        return
        [
            new StardewStatusTool(adapter.Queries, descriptor),
            new StardewMoveTool(adapter.Commands, descriptor, traceIdFactory, idempotencyKeyFactory, maxStatusPolls),
            new StardewSpeakTool(adapter.Commands, descriptor, traceIdFactory, idempotencyKeyFactory),
            new StardewOpenPrivateChatTool(adapter.Commands, descriptor, traceIdFactory, idempotencyKeyFactory),
            new StardewTaskStatusTool(adapter.Commands)
        ];
    }
}

public sealed class StardewStatusTool : ITool, IToolSchemaProvider
{
    private readonly IGameQueryService _queries;
    private readonly NpcRuntimeDescriptor _descriptor;

    public StardewStatusTool(IGameQueryService queries, NpcRuntimeDescriptor descriptor)
    {
        _queries = queries;
        _descriptor = descriptor;
    }

    public string Name => "stardew_status";

    public string Description => "Read the current Stardew facts for this NPC. This is a passive observation tool.";

    public Type ParametersType => typeof(StardewStatusToolParameters);

    public JsonElement GetParameterSchema() => StardewNpcToolSchemas.Empty();

    public async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
        => ToolResult.Ok(StardewNpcToolJson.Serialize(await _queries.ObserveAsync(_descriptor.NpcId, ct)));
}

public sealed class StardewMoveTool : ITool, IToolSchemaProvider
{
    private readonly IGameCommandService _commands;
    private readonly NpcRuntimeDescriptor _descriptor;
    private readonly Func<string> _traceIdFactory;
    private readonly Func<string> _idempotencyKeyFactory;
    private readonly int _maxStatusPolls;

    public StardewMoveTool(
        IGameCommandService commands,
        NpcRuntimeDescriptor descriptor,
        Func<string> traceIdFactory,
        Func<string> idempotencyKeyFactory,
        int maxStatusPolls)
    {
        _commands = commands;
        _descriptor = descriptor;
        _traceIdFactory = traceIdFactory;
        _idempotencyKeyFactory = idempotencyKeyFactory;
        _maxStatusPolls = Math.Max(0, maxStatusPolls);
    }

    public string Name => "stardew_move";

    public string Description => "Ask this NPC to move to a Stardew tile. The runtime binds npcId, saveId, traceId, and idempotency internally.";

    public Type ParametersType => typeof(StardewMoveToolParameters);

    public JsonElement GetParameterSchema() => StardewNpcToolSchemas.Move();

    public async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (StardewMoveToolParameters)parameters;
        if (string.IsNullOrWhiteSpace(p.LocationName))
            return ToolResult.Fail("locationName is required.");

        var action = new GameAction(
            _descriptor.NpcId,
            _descriptor.GameId,
            GameActionType.Move,
            _traceIdFactory(),
            _idempotencyKeyFactory(),
            new GameActionTarget("tile", p.LocationName, new GameTile(p.X, p.Y)),
            p.Reason);

        var commandResult = await _commands.SubmitAsync(action, ct);
        var statusPolls = await PollUntilTerminalAsync(commandResult, ct);
        var finalStatus = statusPolls.Count > 0 ? statusPolls[^1] : null;

        return ToolResult.Ok(StardewNpcToolJson.Serialize(new StardewNpcActionToolResult(
            commandResult.Accepted,
            commandResult.CommandId,
            commandResult.Status,
            commandResult.FailureReason,
            commandResult.TraceId,
            finalStatus,
            statusPolls)));
    }

    private async Task<IReadOnlyList<GameCommandStatus>> PollUntilTerminalAsync(GameCommandResult commandResult, CancellationToken ct)
    {
        if (!commandResult.Accepted ||
            string.IsNullOrWhiteSpace(commandResult.CommandId) ||
            IsTerminal(commandResult.Status))
        {
            return [];
        }

        var statuses = new List<GameCommandStatus>();
        for (var i = 0; i < _maxStatusPolls; i++)
        {
            var status = await _commands.GetStatusAsync(commandResult.CommandId, ct);
            statuses.Add(status);
            if (IsTerminal(status.Status))
                break;
        }

        return statuses;
    }

    private static bool IsTerminal(string? status)
        => string.Equals(status, StardewCommandStatuses.Completed, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StardewCommandStatuses.Failed, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StardewCommandStatuses.Cancelled, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StardewCommandStatuses.Blocked, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StardewCommandStatuses.Expired, StringComparison.OrdinalIgnoreCase);
}

public sealed class StardewSpeakTool : ITool, IToolSchemaProvider
{
    private readonly IGameCommandService _commands;
    private readonly NpcRuntimeDescriptor _descriptor;
    private readonly Func<string> _traceIdFactory;
    private readonly Func<string> _idempotencyKeyFactory;

    public StardewSpeakTool(
        IGameCommandService commands,
        NpcRuntimeDescriptor descriptor,
        Func<string> traceIdFactory,
        Func<string> idempotencyKeyFactory)
    {
        _commands = commands;
        _descriptor = descriptor;
        _traceIdFactory = traceIdFactory;
        _idempotencyKeyFactory = idempotencyKeyFactory;
    }

    public string Name => "stardew_speak";

    public string Description => "Ask this NPC to say a short line through the Stardew bridge. The runtime binds npcId, saveId, traceId, and idempotency internally.";

    public Type ParametersType => typeof(StardewSpeakToolParameters);

    public JsonElement GetParameterSchema() => StardewNpcToolSchemas.Speak();

    public async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (StardewSpeakToolParameters)parameters;
        if (string.IsNullOrWhiteSpace(p.Text))
            return ToolResult.Fail("text is required.");

        var channel = string.IsNullOrWhiteSpace(p.Channel) ? "player" : p.Channel;
        var payload = new JsonObject
        {
            ["text"] = p.Text,
            ["channel"] = channel
        };
        var action = new GameAction(
            _descriptor.NpcId,
            _descriptor.GameId,
            GameActionType.Speak,
            _traceIdFactory(),
            _idempotencyKeyFactory(),
            new GameActionTarget("player"),
            Payload: payload);

        return ToolResult.Ok(StardewNpcToolJson.Serialize(await _commands.SubmitAsync(action, ct)));
    }
}

public sealed class StardewTaskStatusTool : ITool, IToolSchemaProvider
{
    private readonly IGameCommandService _commands;

    public StardewTaskStatusTool(IGameCommandService commands)
    {
        _commands = commands;
    }

    public string Name => "stardew_task_status";

    public string Description => "Read the status of a Stardew command previously returned by an NPC action tool.";

    public Type ParametersType => typeof(StardewTaskStatusToolParameters);

    public JsonElement GetParameterSchema() => StardewNpcToolSchemas.TaskStatus();

    public async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (StardewTaskStatusToolParameters)parameters;
        if (string.IsNullOrWhiteSpace(p.CommandId))
            return ToolResult.Fail("commandId is required.");

        return ToolResult.Ok(StardewNpcToolJson.Serialize(await _commands.GetStatusAsync(p.CommandId, ct)));
    }
}

public sealed class StardewOpenPrivateChatTool : ITool, IToolSchemaProvider
{
    private readonly IGameCommandService _commands;
    private readonly NpcRuntimeDescriptor _descriptor;
    private readonly Func<string> _traceIdFactory;
    private readonly Func<string> _idempotencyKeyFactory;

    public StardewOpenPrivateChatTool(
        IGameCommandService commands,
        NpcRuntimeDescriptor descriptor,
        Func<string> traceIdFactory,
        Func<string> idempotencyKeyFactory)
    {
        _commands = commands;
        _descriptor = descriptor;
        _traceIdFactory = traceIdFactory;
        _idempotencyKeyFactory = idempotencyKeyFactory;
    }

    public string Name => "stardew_open_private_chat";

    public string Description => "Open an in-game private chat input for this NPC so the player can type a message. Use this only when the observed facts make private chat appropriate.";

    public Type ParametersType => typeof(StardewOpenPrivateChatToolParameters);

    public JsonElement GetParameterSchema() => StardewNpcToolSchemas.OpenPrivateChat();

    public async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (StardewOpenPrivateChatToolParameters)parameters;
        var payload = new JsonObject
        {
            ["prompt"] = p.Prompt
        };
        var action = new GameAction(
            _descriptor.NpcId,
            _descriptor.GameId,
            GameActionType.OpenPrivateChat,
            _traceIdFactory(),
            _idempotencyKeyFactory(),
            new GameActionTarget("player"),
            Payload: payload);

        return ToolResult.Ok(StardewNpcToolJson.Serialize(await _commands.SubmitAsync(action, ct)));
    }
}

public sealed class StardewStatusToolParameters
{
}

public sealed class StardewMoveToolParameters
{
    public required string LocationName { get; init; }

    public int X { get; init; }

    public int Y { get; init; }

    public string? Reason { get; init; }
}

public sealed class StardewSpeakToolParameters
{
    public required string Text { get; init; }

    public string? Channel { get; init; }
}

public sealed class StardewTaskStatusToolParameters
{
    public required string CommandId { get; init; }
}

public sealed class StardewOpenPrivateChatToolParameters
{
    public string? Prompt { get; init; }
}

internal sealed record StardewNpcActionToolResult(
    bool Accepted,
    string CommandId,
    string Status,
    string? FailureReason,
    string TraceId,
    GameCommandStatus? FinalStatus,
    IReadOnlyList<GameCommandStatus> StatusPolls);

internal static class StardewNpcToolJson
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
}

internal static class StardewNpcToolSchemas
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static JsonElement Empty()
        => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        }, JsonOptions);

    public static JsonElement Move()
        => Schema(
            new Dictionary<string, object>
            {
                ["locationName"] = new { type = "string", description = "Stardew location name, for example Town." },
                ["x"] = new { type = "integer", description = "Target tile X coordinate." },
                ["y"] = new { type = "integer", description = "Target tile Y coordinate." },
                ["reason"] = new { type = "string", description = "Short reason for the move." }
            },
            ["locationName", "x", "y"]);

    public static JsonElement Speak()
        => Schema(
            new Dictionary<string, object>
            {
                ["text"] = new { type = "string", description = "Short line for the NPC to say." },
                ["channel"] = new { type = "string", description = "Delivery channel; defaults to player." }
            },
            ["text"]);

    public static JsonElement TaskStatus()
        => Schema(
            new Dictionary<string, object>
            {
                ["commandId"] = new { type = "string", description = "Command id returned by stardew_move or stardew_speak." }
            },
            ["commandId"]);

    public static JsonElement OpenPrivateChat()
        => Schema(
            new Dictionary<string, object>
            {
                ["prompt"] = new { type = "string", description = "Optional short prompt shown or logged with the private chat request." }
            },
            []);

    private static JsonElement Schema(Dictionary<string, object> properties, string[] required)
        => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties,
            required
        }, JsonOptions);
}
