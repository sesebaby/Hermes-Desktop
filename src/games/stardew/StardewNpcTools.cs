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
        Func<string>? idempotencyKeyFactory = null)
    {
        traceIdFactory ??= () => $"trace_{descriptor.NpcId}_{Guid.NewGuid():N}";
        idempotencyKeyFactory ??= () => $"idem_{descriptor.NpcId}_{Guid.NewGuid():N}";

        return
        [
            new StardewStatusTool(adapter.Queries, descriptor),
            new StardewMoveTool(adapter.Commands, descriptor, traceIdFactory, idempotencyKeyFactory),
            new StardewSpeakTool(adapter.Commands, descriptor, traceIdFactory, idempotencyKeyFactory),
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

    public StardewMoveTool(
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

        return ToolResult.Ok(StardewNpcToolJson.Serialize(await _commands.SubmitAsync(action, ct)));
    }
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

    private static JsonElement Schema(Dictionary<string, object> properties, string[] required)
        => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties,
            required
        }, JsonOptions);
}
