using Hermes.Agent.Core;
using Hermes.Agent.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Services;

[TestClass]
public sealed class SessionTodoArchiveServiceTests
{
    [TestMethod]
    public void BuildArchive_DerivesEntryAtAssistantBoundaryFromLatestTodoSnapshot()
    {
        var entries = SessionTodoArchiveService.BuildArchive("session-a",
        [
            AssistantToolCall("call-1"),
            TodoResult("call-1", "{\"todos\":[{\"id\":\"1\",\"content\":\"Plan\",\"status\":\"completed\"},{\"id\":\"2\",\"content\":\"Build\",\"status\":\"in_progress\"}]}"),
            AssistantFinal("Working on it")
        ]);

        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual(2, entries[0].Todos.Count);
        Assert.AreEqual("Plan", entries[0].Todos[0].Content);
        Assert.IsTrue(entries[0].HasIncomplete);
        Assert.AreEqual(1, entries[0].IncompleteCount);
    }

    [TestMethod]
    public void BuildArchive_DeduplicatesDuplicateToolAndAssistantPersistence()
    {
        var assistant = AssistantFinal("Done");
        var todo = TodoResult("call-1", "{\"todos\":[{\"id\":\"1\",\"content\":\"Done\",\"status\":\"completed\"}]}");

        var entries = SessionTodoArchiveService.BuildArchive("session-a",
        [
            AssistantToolCall("call-1"),
            todo,
            assistant,
            todo,
            assistant
        ]);

        Assert.AreEqual(1, entries.Count);
    }

    [TestMethod]
    public void BuildArchive_IgnoresMalformedTodoPayloads()
    {
        var entries = SessionTodoArchiveService.BuildArchive("session-a",
        [
            AssistantToolCall("call-1"),
            TodoResult("call-1", "{not json"),
            AssistantFinal("Done")
        ]);

        Assert.AreEqual(0, entries.Count);
    }

    [TestMethod]
    public void BuildArchive_MarksCompleteWhenAllTodosAreCompletedOrCancelled()
    {
        var entries = SessionTodoArchiveService.BuildArchive("session-a",
        [
            AssistantToolCall("call-1"),
            TodoResult("call-1", "{\"todos\":[{\"id\":\"1\",\"content\":\"Done\",\"status\":\"completed\"},{\"id\":\"2\",\"content\":\"Dropped\",\"status\":\"cancelled\"}]}"),
            AssistantFinal("Done")
        ]);

        Assert.AreEqual(1, entries.Count);
        Assert.IsTrue(entries[0].IsComplete);
        Assert.IsTrue(entries[0].CollapsedByDefault);
        Assert.IsFalse(entries[0].HasIncomplete);
    }

    [TestMethod]
    public void BuildArchive_PreservesSourceOrder()
    {
        var entries = SessionTodoArchiveService.BuildArchive("session-a",
        [
            AssistantToolCall("call-1"),
            TodoResult("call-1", "{\"todos\":[{\"id\":\"1\",\"content\":\"First done\",\"status\":\"completed\"},{\"id\":\"2\",\"content\":\"Second active\",\"status\":\"in_progress\"},{\"id\":\"3\",\"content\":\"Third pending\",\"status\":\"pending\"}]}"),
            AssistantFinal("Done")
        ]);

        CollectionAssert.AreEqual(
            new[] { "1", "2", "3" },
            entries[0].Todos.Select(t => t.Id).ToArray());
    }

    private static Message AssistantToolCall(string callId)
        => new()
        {
            Role = "assistant",
            Content = "",
            ToolCalls =
            [
                new ToolCall { Id = callId, Name = "todo", Arguments = "{}" }
            ],
            Timestamp = new DateTime(2026, 4, 30, 1, 0, 0, DateTimeKind.Utc)
        };

    private static Message AssistantFinal(string content)
        => new()
        {
            Role = "assistant",
            Content = content,
            Timestamp = new DateTime(2026, 4, 30, 1, 0, 1, DateTimeKind.Utc)
        };

    private static Message TodoResult(string callId, string content)
        => new()
        {
            Role = "tool",
            ToolName = "todo",
            ToolCallId = callId,
            Content = content,
            Timestamp = new DateTime(2026, 4, 30, 1, 0, 0, DateTimeKind.Utc)
        };
}
