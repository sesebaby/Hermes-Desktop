namespace Hermes.Agent.Runtime;

using System.Text.Json;

public enum NpcLocalActionKind
{
    Move,
    Observe,
    Wait,
    TaskStatus,
    Escalate,
    IdleMicroAction
}

public sealed record NpcLocalActionIntent(
    NpcLocalActionKind Action,
    string Reason,
    string? DestinationId = null,
    string? CommandId = null,
    string? ObserveTarget = null,
    string? WaitReason = null,
    NpcLocalSpeechIntent? Speech = null,
    NpcLocalTaskUpdateIntent? TaskUpdate = null,
    NpcLocalMoveTargetIntent? Target = null,
    bool Escalate = false,
    NpcLocalIdleMicroActionIntent? IdleMicroAction = null)
{
    private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "move",
        "observe",
        "wait",
        "task_status",
        "escalate",
        "idle_micro_action"
    };

    private static readonly HashSet<string> AllowedIdleMicroActionKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "emote_happy",
        "emote_question",
        "emote_sleepy",
        "emote_music",
        "look_left",
        "look_right",
        "look_up",
        "look_down",
        "look_around",
        "tiny_hop",
        "tiny_shake",
        "idle_pose",
        "idle_animation_once"
    };

    private static readonly HashSet<string> AllowedTaskStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "pending",
        "in_progress",
        "completed",
        "cancelled",
        "blocked",
        "failed"
    };

    public static bool TryParse(string? value, out NpcLocalActionIntent? intent, out string error)
    {
        intent = null;
        error = "";
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "intent_contract_invalid";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "intent_contract_invalid";
                return false;
            }

            return TryParseObject(document.RootElement, out intent, out error);
        }
        catch (JsonException)
        {
            error = "intent_contract_invalid";
            return false;
        }
    }

    private static bool TryParseObject(JsonElement root, out NpcLocalActionIntent? intent, out string error)
    {
        intent = null;
        error = "";
        var actionText = ReadString(root, "action");
        if (string.IsNullOrWhiteSpace(actionText) ||
            !TryReadActionKind(actionText, out var action))
        {
            error = "action_not_allowed";
            return false;
        }

        var escalate = ReadBool(root, "escalate");
        if (!AllowedActions.Contains(actionText))
        {
            error = "action_not_allowed";
            return false;
        }

        if (action is NpcLocalActionKind.IdleMicroAction &&
            HasIdleMicroActionForbiddenField(root))
        {
            error = "idle_micro_action_forbidden_field";
            return false;
        }

        var destinationId = ReadString(root, "destinationId");
        if (!TryReadMoveTarget(root, out var target, out error))
            return false;

        if (action is NpcLocalActionKind.Move &&
            string.IsNullOrWhiteSpace(destinationId) &&
            target is null)
        {
            error = "move_target_required";
            return false;
        }

        var commandId = ReadString(root, "commandId");
        if (action is NpcLocalActionKind.TaskStatus && string.IsNullOrWhiteSpace(commandId))
        {
            error = "commandId_required";
            return false;
        }

        if (!TryReadSpeech(root, out var speech, out error))
            return false;

        if (!TryReadTaskUpdate(root, out var taskUpdate, out error))
            return false;

        if (!TryReadIdleMicroAction(root, action, out var idleMicroAction, out error))
            return false;

        intent = new NpcLocalActionIntent(
            action,
            ReadString(root, "reason") ?? "",
            destinationId,
            commandId,
            ReadString(root, "observeTarget"),
            ReadString(root, "waitReason"),
            speech,
            taskUpdate,
            target,
            escalate,
            idleMicroAction);
        return true;
    }

    private static bool TryReadIdleMicroAction(
        JsonElement root,
        NpcLocalActionKind action,
        out NpcLocalIdleMicroActionIntent? idleMicroAction,
        out string error)
    {
        idleMicroAction = null;
        error = "";
        if (action is not NpcLocalActionKind.IdleMicroAction)
            return true;

        if (!root.TryGetProperty("idleMicroAction", out var element) ||
            element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            error = "idle_micro_action_required";
            return false;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            error = "idle_micro_action_contract_invalid";
            return false;
        }

        var kind = ReadString(element, "kind");
        if (string.IsNullOrWhiteSpace(kind) || !AllowedIdleMicroActionKinds.Contains(kind))
        {
            error = "idle_micro_action_kind_not_allowed";
            return false;
        }

        if (!TryReadOptionalInt(element, "ttlSeconds", out var ttlSeconds))
        {
            error = "idle_micro_action_ttl_invalid";
            return false;
        }

        idleMicroAction = new NpcLocalIdleMicroActionIntent(
            kind.Trim(),
            ReadString(element, "animationAlias"),
            ReadString(element, "intensity"),
            ttlSeconds);
        return true;
    }

    private static bool HasIdleMicroActionForbiddenField(JsonElement root)
    {
        string[] forbiddenRootFields = ["speech", "destinationId", "target", "text", "message", "dialogue", "locationName", "x", "y"];
        foreach (var field in forbiddenRootFields)
        {
            if (root.TryGetProperty(field, out var value) &&
                value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                return true;
            }
        }

        if (root.TryGetProperty("idleMicroAction", out var idleMicroAction) &&
            idleMicroAction.ValueKind == JsonValueKind.Object)
        {
            string[] forbiddenIdleFields = ["frame", "frameIndex", "rawAnimationId"];
            foreach (var field in forbiddenIdleFields)
            {
                if (idleMicroAction.TryGetProperty(field, out var value) &&
                    value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryReadMoveTarget(JsonElement root, out NpcLocalMoveTargetIntent? target, out string error)
    {
        target = null;
        error = "";
        if (!root.TryGetProperty("target", out var element) ||
            element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            error = "target_contract_invalid";
            return false;
        }

        var locationName = ReadString(element, "locationName");
        if (string.IsNullOrWhiteSpace(locationName))
        {
            error = "target_location_required";
            return false;
        }

        if (!TryReadInt(element, "x", out var x))
        {
            error = "target_x_required";
            return false;
        }

        if (!TryReadInt(element, "y", out var y))
        {
            error = "target_y_required";
            return false;
        }

        var source = ReadString(element, "source");
        if (string.IsNullOrWhiteSpace(source))
        {
            error = "target_source_required";
            return false;
        }

        var facingDirection = TryReadInt(element, "facingDirection", out var facing)
            ? facing
            : (int?)null;
        target = new NpcLocalMoveTargetIntent(
            locationName.Trim(),
            x,
            y,
            source.Trim(),
            facingDirection);
        return true;
    }

    private static bool TryReadSpeech(JsonElement root, out NpcLocalSpeechIntent? speech, out string error)
    {
        speech = null;
        error = "";
        if (!root.TryGetProperty("speech", out var element) ||
            element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            error = "speech_contract_invalid";
            return false;
        }

        var shouldSpeak = ReadBool(element, "shouldSpeak");
        if (!shouldSpeak)
        {
            speech = new NpcLocalSpeechIntent(false, null, null);
            return true;
        }

        var text = ReadString(element, "text");
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "speech_text_required";
            return false;
        }

        speech = new NpcLocalSpeechIntent(
            true,
            ReadString(element, "channel"),
            text.Trim());
        return true;
    }

    private static bool TryReadTaskUpdate(JsonElement root, out NpcLocalTaskUpdateIntent? taskUpdate, out string error)
    {
        taskUpdate = null;
        error = "";
        if (!root.TryGetProperty("taskUpdate", out var element) ||
            element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            error = "task_update_contract_invalid";
            return false;
        }

        var taskId = ReadString(element, "taskId");
        if (string.IsNullOrWhiteSpace(taskId))
        {
            error = "task_update_taskId_required";
            return false;
        }

        var status = ReadString(element, "status");
        if (string.IsNullOrWhiteSpace(status) ||
            !AllowedTaskStatuses.Contains(status))
        {
            error = "task_update_status_not_allowed";
            return false;
        }

        taskUpdate = new NpcLocalTaskUpdateIntent(
            taskId.Trim(),
            status.Trim().ToLowerInvariant(),
            ReadString(element, "reason"));
        return true;
    }

    private static bool TryReadActionKind(string value, out NpcLocalActionKind action)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "move":
                action = NpcLocalActionKind.Move;
                return true;
            case "observe":
                action = NpcLocalActionKind.Observe;
                return true;
            case "wait":
                action = NpcLocalActionKind.Wait;
                return true;
            case "task_status":
                action = NpcLocalActionKind.TaskStatus;
                return true;
            case "escalate":
                action = NpcLocalActionKind.Escalate;
                return true;
            case "idle_micro_action":
                action = NpcLocalActionKind.IdleMicroAction;
                return true;
            default:
                action = default;
                return false;
        }
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool TryReadInt(JsonElement element, string propertyName, out int value)
    {
        value = default;
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt32(out value);
    }

    private static bool TryReadOptionalInt(JsonElement element, string propertyName, out int? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var parsed))
            return false;

        value = parsed;
        return true;
    }

    private static bool ReadBool(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) &&
           value.ValueKind == JsonValueKind.True;
}

public sealed record NpcLocalSpeechIntent(
    bool ShouldSpeak,
    string? Channel,
    string? Text);

public sealed record NpcLocalTaskUpdateIntent(
    string TaskId,
    string Status,
    string? Reason);

public sealed record NpcLocalMoveTargetIntent(
    string LocationName,
    int X,
    int Y,
    string Source,
    int? FacingDirection = null);

public sealed record NpcLocalIdleMicroActionIntent(
    string Kind,
    string? AnimationAlias,
    string? Intensity,
    int? TtlSeconds);
