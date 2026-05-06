namespace Hermes.Agent.Runtime;

using System.Text.Json;

public enum NpcLocalActionKind
{
    Move,
    Observe,
    Wait,
    TaskStatus,
    Escalate
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
    bool Escalate = false)
{
    private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "move",
        "observe",
        "wait",
        "task_status",
        "escalate"
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

        if (!ActionAppearsInAllowedActions(root, actionText))
        {
            error = "action_not_allowed";
            return false;
        }

        var destinationId = ReadString(root, "destinationId");
        if (action is NpcLocalActionKind.Move && string.IsNullOrWhiteSpace(destinationId))
        {
            error = "destinationId_required";
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

        intent = new NpcLocalActionIntent(
            action,
            ReadString(root, "reason") ?? "",
            destinationId,
            commandId,
            ReadString(root, "observeTarget"),
            ReadString(root, "waitReason"),
            speech,
            taskUpdate,
            escalate);
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

    private static bool ActionAppearsInAllowedActions(JsonElement root, string actionText)
    {
        if (!root.TryGetProperty("allowedActions", out var allowedActions) ||
            allowedActions.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var allowedAction in allowedActions.EnumerateArray())
        {
            if (allowedAction.ValueKind == JsonValueKind.String &&
                string.Equals(allowedAction.GetString(), actionText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
            default:
                action = default;
                return false;
        }
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

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
