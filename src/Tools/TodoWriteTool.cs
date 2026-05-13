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
        "管理当前会话的任务列表。适合记录以后要兑现的承诺、3 步以上的复杂任务，或玩家一次给出的多个任务。 " +
        "如果玩家要求现在就执行会改变外部世界或游戏世界的动作，不要只写 todo；应先使用当前场景提供的执行或委托工具，例如 stardew_submit_host_task。 " +
        "不传参数表示读取当前列表；传 todos 数组表示创建或更新任务。 " +
        "merge=false 表示用新列表替换全部任务；merge=true 表示按 id 更新已有任务并追加新任务。 " +
        "每个任务包含 id、content、status，可选 reason。status 可为 pending、in_progress、completed、cancelled、blocked 或 failed。 " +
        "列表顺序代表优先级；同一时间只能有一个 in_progress。完成后立刻标 completed；无法继续时用 blocked 或 failed 并写短 reason。 " +
        "工具总是返回完整的当前任务列表。";

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
                                @enum = new[] { "pending", "in_progress", "completed", "cancelled", "blocked", "failed" },
                                description = "Current status."
                            },
                            ["reason"] = new
                            {
                                type = "string",
                                description = "Optional short reason for blocked, failed, or cancelled items."
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
                    p.Todos.Select(t => new SessionTodoInput(t.Id, t.Content, t.Status, t.Reason)),
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

    public string? Reason { get; init; }
}
