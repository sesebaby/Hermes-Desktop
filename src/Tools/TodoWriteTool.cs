namespace Hermes.Agent.Tools;

using System.Text.Json;
using System.Text.Json.Serialization;
using Hermes.Agent.Core;
using Hermes.Agent.Tasks;

/// <summary>
/// Reference-faithful session todo tool. `todo_write` remains as a compatibility alias.
/// </summary>
public class TodoTool : ITool, IToolSchemaProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SessionTodoStore _store;

    public TodoTool(SessionTodoStore store)
    {
        _store = store;
    }

    public virtual string Name => "todo";

    public string Description =>
        "Manage your task list for the current session. Call with no parameters to read. " +
        "Provide todos to write; merge=false replaces the list, merge=true updates by id and appends new items. " +
        "Statuses: pending, in_progress, completed, cancelled. Always returns the full current list.";

    public Type ParametersType => typeof(TodoToolParameters);

    public JsonElement GetParameterSchema()
    {
        var schema = new
        {
            type = "object",
            properties = new Dictionary<string, object>
            {
                ["todos"] = new
                {
                    type = "array",
                    description = "Task items to write. Omit to read current list.",
                    items = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["id"] = new { type = "string", description = "Unique item identifier." },
                            ["content"] = new { type = "string", description = "Task description." },
                            ["status"] = new
                            {
                                type = "string",
                                @enum = new[] { "pending", "in_progress", "completed", "cancelled" },
                                description = "Current status."
                            }
                        },
                        required = new[] { "id", "content", "status" }
                    }
                },
                ["merge"] = new
                {
                    type = "boolean",
                    description = "true: update existing items by id and add new ones. false: replace the entire list.",
                    @default = false
                }
            },
            required = Array.Empty<string>()
        };

        return JsonDocument.Parse(JsonSerializer.Serialize(schema, JsonOptions)).RootElement.Clone();
    }

    public Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var p = (TodoToolParameters)parameters;

        try
        {
            var snapshot = p.Todos is null
                ? _store.Read(p.CurrentSessionId)
                : _store.Write(
                    p.CurrentSessionId,
                    p.Todos.Select(t => new SessionTodoInput(t.Id, t.Content, t.Status)),
                    p.Merge);

            return Task.FromResult(ToolResult.Ok(JsonSerializer.Serialize(snapshot, JsonOptions)));
        }
        catch (Exception ex)
        {
            var error = JsonSerializer.Serialize(new { error = $"Failed to update todos: {ex.Message}" }, JsonOptions);
            return Task.FromResult(ToolResult.Fail(error, ex));
        }
    }
}

public sealed class TodoWriteTool : TodoTool, ILegacyToolAlias
{
    public TodoWriteTool(SessionTodoStore store) : base(store)
    {
    }

    public override string Name => "todo_write";

    public string CanonicalName => "todo";
}

public sealed class TodoToolParameters : ISessionAwareToolParameters
{
    [JsonIgnore]
    public string? CurrentSessionId { get; set; }

    public IReadOnlyList<TodoItemInput>? Todos { get; init; }

    public bool Merge { get; init; }
}

public sealed class TodoItemInput
{
    public string? Id { get; init; }
    public string? Content { get; init; }
    public string? Status { get; init; }
}
