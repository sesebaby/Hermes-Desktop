using Hermes.Agent.Core;
using Hermes.Agent.Tasks;
using Hermes.Agent.Transcript;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Services;

[TestClass]
public sealed class SessionTaskPanelModelTests
{
    [TestMethod]
    public async Task TranscriptTodoResult_ForCurrentSession_UpdatesPanelItemsWithoutRefresh()
    {
        var projection = new SessionTaskProjectionService(new SessionTodoStore());
        var currentSessionId = "panel-session";
        var model = new SessionTaskPanelModel(projection, () => currentSessionId);
        var changed = false;
        model.Changed += (_, _) => changed = true;
        var transcripts = new TranscriptStore(
            Path.Combine(Path.GetTempPath(), $"hermes-panel-model-{Guid.NewGuid():N}"),
            messageObserver: projection);

        await transcripts.SaveMessageAsync(currentSessionId, new Message
        {
            Role = "tool",
            ToolName = "todo",
            ToolCallId = "call-1",
            Content = "{\"todos\":[{\"id\":\"1\",\"content\":\"Panel task\",\"status\":\"in_progress\"}]}"
        }, CancellationToken.None);

        Assert.IsTrue(changed);
        Assert.AreEqual(1, model.Tasks.Count);
        Assert.AreEqual("Panel task", model.Tasks[0].Description);
        Assert.AreEqual("In progress", model.Tasks[0].StatusLabel);
    }

    [TestMethod]
    public async Task TranscriptTodoResult_PreservesTodoSourceOrderAsPriorityOrder()
    {
        var projection = new SessionTaskProjectionService(new SessionTodoStore());
        var currentSessionId = "panel-session";
        var model = new SessionTaskPanelModel(projection, () => currentSessionId);
        var transcripts = new TranscriptStore(
            Path.Combine(Path.GetTempPath(), $"hermes-panel-order-{Guid.NewGuid():N}"),
            messageObserver: projection);

        await transcripts.SaveMessageAsync(currentSessionId, new Message
        {
            Role = "tool",
            ToolName = "todo",
            ToolCallId = "call-1",
            Content = "{\"todos\":[{\"id\":\"1\",\"content\":\"First done\",\"status\":\"completed\"},{\"id\":\"2\",\"content\":\"Second active\",\"status\":\"in_progress\"},{\"id\":\"3\",\"content\":\"Third pending\",\"status\":\"pending\"}]}"
        }, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "1", "2", "3" },
            model.Tasks.Select(t => t.TaskId).ToArray());
        CollectionAssert.AreEqual(
            new[] { "#1", "#2", "#3" },
            model.Tasks.Select(t => t.PriorityLabel).ToArray());
    }
}
