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
        "Manage your task list for the current session. Use for complex tasks with 3+ steps or when the user provides multiple tasks. " +
        "Call with no parameters to read the current list. Provide a todos array to create/update items. " +
        "merge=false replaces the entire list with a fresh plan; merge=true updates existing items by id and adds new ones. " +
        "Each item has id, content, and status: pending, in_progress, completed, or cancelled. " +
        "List order is priority. Only ONE item in_progress at a time. " +
        "Mark items completed immediately when done. If something fails, cancel it and add a revised item. " +
        "Always returns the full current list.";

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
