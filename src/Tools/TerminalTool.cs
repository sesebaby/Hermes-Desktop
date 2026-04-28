namespace Hermes.Agent.Tools;

using Hermes.Agent.Core;
using Hermes.Agent.Execution;

public sealed class TerminalTool : ITool
{
    public string Name => "terminal";
    public string Description => "Execute shell commands on the local system with deterministic timeout handling";
    public Type ParametersType => typeof(TerminalParameters);
    
    public Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (TerminalParameters)parameters;
        return ExecuteCommandAsync(p.Command, p.WorkingDirectory, p.TimeoutSeconds, ct);
    }
    
    private async Task<ToolResult> ExecuteCommandAsync(string command, string? cwd, int timeout, CancellationToken ct)
    {
        try
        {
            var timeoutMs = Math.Max(1, timeout) * 1000;
            await using var backend = new LocalBackend(new ExecutionConfig
            {
                DefaultTimeoutMs = timeoutMs
            });

            var result = await backend.ExecuteAsync(
                command,
                cwd,
                timeoutMs: timeoutMs,
                background: false,
                ct);

            if (result.Success)
                return ToolResult.Ok(string.IsNullOrWhiteSpace(result.Output) ? "Command completed successfully." : result.Output);

            var message = $"Exit code {result.ExitCode}: {result.Output}";
            if (!string.IsNullOrWhiteSpace(result.ExitCodeMeaning))
                message += $"\n({result.ExitCodeMeaning})";
            return ToolResult.Fail(message);
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Failed to execute command: {ex.Message}", ex);
        }
    }
}

public sealed class TerminalParameters
{
    public required string Command { get; init; }
    public string? WorkingDirectory { get; init; }
    public int TimeoutSeconds { get; init; } = 60;
}
