namespace Hermes.Agent.Core;

public enum ActivityStatus { Running, Success, Failed, Denied }

public sealed class ActivityEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public long DurationMs { get; set; }
    public required string ToolName { get; init; }
    public string? ToolCallId { get; init; }
    public string InputSummary { get; set; } = "";
    public string OutputSummary { get; set; } = "";
    public ActivityStatus Status { get; set; } = ActivityStatus.Running;
    public string? DiffPreview { get; set; }
    public string? ScreenshotPath { get; set; }
}
