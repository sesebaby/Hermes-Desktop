namespace Hermes.Agent.Execution;

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

// ══════════════════════════════════════════════
// Local Execution Backend
// ══════════════════════════════════════════════
//
// Upstream ref: tools/environments/local.py
// Executes commands directly on the host machine.

public sealed partial class LocalBackend : IExecutionBackend
{
    private readonly ExecutionConfig _config;

    public LocalBackend(ExecutionConfig config) => _config = config;
    public ExecutionBackendType Type => ExecutionBackendType.Local;

    public async Task<ExecutionResult> ExecuteAsync(
        string command, string? workingDirectory, int? timeoutMs,
        bool background, CancellationToken ct)
    {
        var timeout = timeoutMs ?? _config.DefaultTimeoutMs;
        var sw = Stopwatch.StartNew();

        var psi = CreateShellStartInfo(command, workingDirectory ?? Directory.GetCurrentDirectory());

        using var process = new Process { StartInfo = psi };
        process.Start();

        if (background)
        {
            sw.Stop();
            return new ExecutionResult
            {
                Output = $"Command started in background (PID: {process.Id})",
                ExitCode = 0,
                DurationMs = sw.ElapsedMilliseconds,
                BackgroundProcessId = process.Id.ToString()
            };
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            sw.Stop();

            // Combine and strip ANSI
            var output = string.IsNullOrEmpty(stderr)
                ? stdout
                : $"{stdout}\n{stderr}";
            output = StripAnsi(output);
            output = OutputTruncator.Truncate(output, _config.MaxOutputChars);

            return new ExecutionResult
            {
                Output = string.IsNullOrWhiteSpace(output) ? "(no output)" : output,
                ExitCode = process.ExitCode,
                ExitCodeMeaning = ExitCodeInterpreter.Interpret(command, process.ExitCode),
                DurationMs = sw.ElapsedMilliseconds
            };
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LocalBackend timed-out process kill failed: {ex}");
            }
            sw.Stop();
            return new ExecutionResult
            {
                Output = $"Command timed out after {timeout}ms",
                ExitCode = 124,
                ExitCodeMeaning = "Timed out",
                DurationMs = sw.ElapsedMilliseconds
            };
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static string StripAnsi(string text) =>
        AnsiRegex().Replace(text, "");

    private static ProcessStartInfo CreateShellStartInfo(string command, string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (!OperatingSystem.IsWindows())
        {
            psi.FileName = "/bin/bash";
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(command);
            return psi;
        }

        var effectiveCommand = TranslateWindowsCommand(command);
        if (LooksLikePowerShell(effectiveCommand))
        {
            psi.FileName = "powershell.exe";
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add(effectiveCommand);
            return psi;
        }

        psi.FileName = "cmd.exe";
        psi.ArgumentList.Add("/d");
        psi.ArgumentList.Add("/s");
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add(effectiveCommand);
        return psi;
    }

    private static string TranslateWindowsCommand(string command)
    {
        var trimmed = command.Trim();

        if (string.Equals(trimmed, "date", StringComparison.OrdinalIgnoreCase))
            return "Get-Date -Format \"yyyy-MM-dd HH:mm:ss K\"";

        var exactWeekdayDate = PosixWeekdayDateRegex().Match(trimmed);
        if (exactWeekdayDate.Success)
            return "$d=Get-Date; \"{0:yyyy-MM-dd dddd} {1}\" -f $d, [int]$d.DayOfWeek";

        var dateFormat = PosixDateFormatRegex().Match(trimmed);
        if (dateFormat.Success)
        {
            var powerShellCommand = ConvertSimplePosixDateFormat(dateFormat.Groups["format"].Value)
                ?? "Get-Date -Format o";
            return powerShellCommand;
        }

        return command;
    }

    private static string? ConvertSimplePosixDateFormat(string format)
    {
        if (format.Contains("%w", StringComparison.Ordinal))
            return null;

        if (format.Contains("%z", StringComparison.Ordinal) ||
            format.Contains("%Z", StringComparison.Ordinal))
        {
            return BuildPowerShellDateExpression(format);
        }

        var converted = ConvertPosixDateFormatToDotNet(format);
        return converted is null ? null : $"Get-Date -Format \"{EscapePowerShellDoubleQuoted(converted)}\"";
    }

    private static string? BuildPowerShellDateExpression(string format)
    {
        var parts = new List<string>();
        var dotNetBuffer = new StringBuilder();
        var dotNetBufferHasDateToken = false;

        void FlushDotNetBuffer()
        {
            if (dotNetBuffer.Length == 0)
                return;

            var segment = EscapePowerShellSingleQuoted(dotNetBuffer.ToString());
            parts.Add(dotNetBufferHasDateToken
                ? $"$d.ToString('{segment}')"
                : $"'{segment}'");
            dotNetBuffer.Clear();
            dotNetBufferHasDateToken = false;
        }

        for (var i = 0; i < format.Length; i++)
        {
            if (format[i] != '%')
            {
                dotNetBuffer.Append(format[i]);
                continue;
            }

            if (i + 1 >= format.Length)
                return null;

            var token = format[++i];
            switch (token)
            {
                case 'Y':
                    dotNetBuffer.Append("yyyy");
                    dotNetBufferHasDateToken = true;
                    break;
                case 'y':
                    dotNetBuffer.Append("yy");
                    dotNetBufferHasDateToken = true;
                    break;
                case 'm':
                    dotNetBuffer.Append("MM");
                    dotNetBufferHasDateToken = true;
                    break;
                case 'd':
                    dotNetBuffer.Append("dd");
                    dotNetBufferHasDateToken = true;
                    break;
                case 'H':
                    dotNetBuffer.Append("HH");
                    dotNetBufferHasDateToken = true;
                    break;
                case 'M':
                    dotNetBuffer.Append("mm");
                    dotNetBufferHasDateToken = true;
                    break;
                case 'S':
                    dotNetBuffer.Append("ss");
                    dotNetBufferHasDateToken = true;
                    break;
                case 'A':
                    dotNetBuffer.Append("dddd");
                    dotNetBufferHasDateToken = true;
                    break;
                case 'a':
                    dotNetBuffer.Append("ddd");
                    dotNetBufferHasDateToken = true;
                    break;
                case 'z':
                    FlushDotNetBuffer();
                    parts.Add("$offset");
                    break;
                case 'Z':
                    FlushDotNetBuffer();
                    parts.Add("$zone");
                    break;
                case '%':
                    dotNetBuffer.Append('%');
                    break;
                default:
                    return null;
            }
        }

        FlushDotNetBuffer();
        if (parts.Count == 0)
            return null;

        return "$d=Get-Date; " +
               "$offset=$d.ToString('zzz').Replace(':',''); " +
               "$zone=[TimeZoneInfo]::Local.StandardName; " +
               "if([TimeZoneInfo]::Local.IsDaylightSavingTime($d)){$zone=[TimeZoneInfo]::Local.DaylightName}; " +
               $"[string]::Concat({string.Join(", ", parts)})";
    }

    private static string? ConvertPosixDateFormatToDotNet(string format)
    {
        var converted = format
            .Replace("%%", "%", StringComparison.Ordinal)
            .Replace("%Y", "yyyy", StringComparison.Ordinal)
            .Replace("%y", "yy", StringComparison.Ordinal)
            .Replace("%m", "MM", StringComparison.Ordinal)
            .Replace("%d", "dd", StringComparison.Ordinal)
            .Replace("%H", "HH", StringComparison.Ordinal)
            .Replace("%M", "mm", StringComparison.Ordinal)
            .Replace("%S", "ss", StringComparison.Ordinal)
            .Replace("%A", "dddd", StringComparison.Ordinal)
            .Replace("%a", "ddd", StringComparison.Ordinal);

        return converted.Contains('%', StringComparison.Ordinal) ? null : converted;
    }

    private static string EscapePowerShellDoubleQuoted(string value)
        => value.Replace("`", "``", StringComparison.Ordinal)
            .Replace("\"", "`\"", StringComparison.Ordinal);

    private static string EscapePowerShellSingleQuoted(string value)
        => value.Replace("'", "''", StringComparison.Ordinal);

    private static bool LooksLikePowerShell(string command)
    {
        var trimmed = command.TrimStart();
        return trimmed.StartsWith("$", StringComparison.Ordinal) ||
               trimmed.Contains("$env:", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("pwsh", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("powershell", StringComparison.OrdinalIgnoreCase) ||
               PowerShellVerbRegex().IsMatch(trimmed);
    }

    [GeneratedRegex(@"\x1B\[[0-9;]*[a-zA-Z]|\x1B\].*?\x07|\x1B[()][AB012]")]
    private static partial Regex AnsiRegex();

    [GeneratedRegex(@"^date\s+\+[""']?(?<format>%Y-%m-%d\s+%A\s+%w)[""']?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex PosixWeekdayDateRegex();

    [GeneratedRegex(@"^date\s+\+[""']?(?<format>[^""']+)[""']?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex PosixDateFormatRegex();

    [GeneratedRegex(@"^(Get|Set|New|Remove|Test|Select|Where|ForEach|Measure|ConvertTo|ConvertFrom|Invoke|Start|Stop|Restart|Write|Read|Copy|Move|Clear|Join|Split|Resolve)-[A-Za-z]", RegexOptions.IgnoreCase)]
    private static partial Regex PowerShellVerbRegex();
}
