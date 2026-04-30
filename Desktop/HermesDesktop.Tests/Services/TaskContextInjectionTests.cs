using Hermes.Agent.Context;
using Hermes.Agent.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Services;

[TestClass]
public sealed class TaskContextInjectionTests
{
    [TestMethod]
    public void PromptBuilder_IncludesActiveTaskContextAsSystemLayer()
    {
        var builder = new PromptBuilder("stable system");
        var packet = builder.Build(new BuildRequest
        {
            State = new SessionState(),
            CurrentUserMessage = "continue",
            ActiveTaskContext = "[Your active task list was preserved across context compression]\n- [>] 1. Build task loop (in_progress)"
        });

        var messages = builder.ToOpenAiMessages(packet);

        Assert.IsTrue(messages.Any(m =>
            m.Role == "system" &&
            m.Content.Contains("active task list", StringComparison.Ordinal) &&
            m.Content.Contains("Build task loop", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void PromptBuilder_OmitsTaskContextWhenNoActiveTasksExist()
    {
        var builder = new PromptBuilder("stable system");
        var packet = builder.Build(new BuildRequest
        {
            State = new SessionState(),
            CurrentUserMessage = "continue",
            ActiveTaskContext = null
        });

        var messages = builder.ToOpenAiMessages(packet);

        Assert.IsFalse(messages.Any(m => m.Content.Contains("active task list", StringComparison.Ordinal)));
    }
}
