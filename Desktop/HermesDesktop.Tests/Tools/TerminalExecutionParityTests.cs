using System.Diagnostics;
using Hermes.Agent.Core;
using Hermes.Agent.Execution;
using Hermes.Agent.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Tools;

[TestClass]
public class TerminalExecutionParityTests
{
    [TestMethod]
    public async Task LocalBackend_WindowsPowerShellDateCommand_Completes()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("Windows-specific shell selection test.");

        await using var backend = new LocalBackend(new ExecutionConfig { DefaultTimeoutMs = 5000 });

        var result = await backend.ExecuteAsync(
            "Get-Date -Format yyyy",
            workingDirectory: null,
            timeoutMs: 5000,
            background: false,
            ct: CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode, result.Output);
        StringAssert.Contains(result.Output, DateTime.Now.Year.ToString());
    }

    [TestMethod]
    public async Task LocalBackend_WindowsPosixDateFormat_IsTranslated()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("Windows-specific shell selection test.");

        await using var backend = new LocalBackend(new ExecutionConfig { DefaultTimeoutMs = 5000 });

        var result = await backend.ExecuteAsync(
            "date +\"%Y-%m-%d %A %w\"",
            workingDirectory: null,
            timeoutMs: 5000,
            background: false,
            ct: CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode, result.Output);
        StringAssert.Contains(result.Output, DateTime.Now.ToString("yyyy-MM-dd"));
        StringAssert.Contains(result.Output, ((int)DateTime.Now.DayOfWeek).ToString());
    }

    [TestMethod]
    public async Task LocalBackend_WindowsPosixTimezoneOffset_IsTranslated()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("Windows-specific shell selection test.");

        await using var backend = new LocalBackend(new ExecutionConfig { DefaultTimeoutMs = 5000 });

        var result = await backend.ExecuteAsync(
            "date +\"%z\"",
            workingDirectory: null,
            timeoutMs: 5000,
            background: false,
            ct: CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode, result.Output);
        StringAssert.Matches(result.Output.Trim(), new System.Text.RegularExpressions.Regex(@"^[+-]\d{4}$"));
    }

    [TestMethod]
    public async Task LocalBackend_WindowsPosixTimezoneName_IsTranslated()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("Windows-specific shell selection test.");

        await using var backend = new LocalBackend(new ExecutionConfig { DefaultTimeoutMs = 5000 });

        var result = await backend.ExecuteAsync(
            "date +\"%Z\"",
            workingDirectory: null,
            timeoutMs: 5000,
            background: false,
            ct: CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode, result.Output);
        Assert.IsTrue(result.Output.Trim().Length > 1, result.Output);
        Assert.IsFalse(result.Output.Contains("%Z", StringComparison.Ordinal), result.Output);
    }

    [TestMethod]
    public async Task LocalBackend_WindowsMixedPosixTimezoneFormat_IsTranslated()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("Windows-specific shell selection test.");

        await using var backend = new LocalBackend(new ExecutionConfig { DefaultTimeoutMs = 5000 });

        var result = await backend.ExecuteAsync(
            "date +\"%Y-%m-%d %H:%M:%S %z %Z\"",
            workingDirectory: null,
            timeoutMs: 5000,
            background: false,
            ct: CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode, result.Output);
        StringAssert.Contains(result.Output, DateTime.Now.ToString("yyyy-MM-dd"));
        StringAssert.Matches(result.Output, new System.Text.RegularExpressions.Regex(@"[+-]\d{4}"));
        Assert.IsFalse(result.Output.Contains("%Z", StringComparison.Ordinal), result.Output);
    }

    [TestMethod]
    public async Task TerminalTool_TimeoutKillsProcessAndReturnsFailure()
    {
        var command = OperatingSystem.IsWindows()
            ? "powershell -NoProfile -Command \"Start-Sleep -Seconds 5\""
            : "sleep 5";
        var tool = new TerminalTool();
        var sw = Stopwatch.StartNew();

        var result = await tool.ExecuteAsync(new TerminalParameters
        {
            Command = command,
            TimeoutSeconds = 1
        }, CancellationToken.None);

        sw.Stop();
        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Content, "timed out");
        Assert.IsTrue(sw.Elapsed < TimeSpan.FromSeconds(4), $"Timeout returned too late: {sw.Elapsed}");
    }
}
