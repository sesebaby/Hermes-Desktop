using Hermes.Agent.Core;
using Hermes.Agent.Tasks;
using Hermes.Agent.Transcript;
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
    public void BuildArchive_IgnoresMalformedTodoPayloadWithoutClearingLatestValidSnapshot()
    {
        var entries = SessionTodoArchiveService.BuildArchive("session-a",
        [
            AssistantToolCall("call-1"),
            TodoResult("call-1", "{\"todos\":[{\"id\":\"1\",\"content\":\"Valid\",\"status\":\"in_progress\"}]}"),
            TodoResult("call-2", "{not json"),
            AssistantFinal("Done")
        ]);

        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("Valid", entries[0].Todos[0].Content);
        Assert.AreEqual("in_progress", entries[0].Todos[0].Status);
    }

    [TestMethod]
    public void BuildArchive_EmptyTodoResultClearsPendingArchiveSnapshot()
    {
        var entries = SessionTodoArchiveService.BuildArchive("session-a",
        [
            AssistantToolCall("call-1"),
            TodoResult("call-1", "{\"todos\":[{\"id\":\"1\",\"content\":\"Valid\",\"status\":\"in_progress\"}]}"),
            TodoResult("call-2", "{\"todos\":[]}"),
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
    public void BuildArchive_MarksIncompleteWhenAnyTodoIsPendingOrInProgress()
    {
        var entries = SessionTodoArchiveService.BuildArchive("session-a",
        [
            AssistantToolCall("call-1"),
            TodoResult("call-1", "{\"todos\":[{\"id\":\"1\",\"content\":\"Done\",\"status\":\"completed\"},{\"id\":\"2\",\"content\":\"Working\",\"status\":\"in_progress\"},{\"id\":\"3\",\"content\":\"Waiting\",\"status\":\"pending\"}]}"),
            AssistantFinal("Done")
        ]);

        Assert.AreEqual(1, entries.Count);
        Assert.IsFalse(entries[0].IsComplete);
        Assert.IsFalse(entries[0].CollapsedByDefault);
        Assert.IsTrue(entries[0].HasIncomplete);
        Assert.AreEqual(2, entries[0].IncompleteCount);
        CollectionAssert.AreEqual(
            new[] { "1", "2", "3" },
            entries[0].Todos.Select(t => t.Id).ToArray());
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

    [TestMethod]
    public void BuildArchive_AcceptsLegacyTodoWriteToolResultAlias()
    {
        var entries = SessionTodoArchiveService.BuildArchive("session-a",
        [
            AssistantToolCall("call-1", "todo_write"),
            TodoResult("call-1", "{\"todos\":[{\"id\":\"1\",\"content\":\"Legacy\",\"status\":\"completed\"}]}", "todo_write"),
            AssistantFinal("Done")
        ]);

        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("Legacy", entries[0].Todos[0].Content);
    }

    [TestMethod]
    public async Task BuildArchive_HydratesFromPersistedTranscriptMessages()
    {
        var sessionId = "archive-hydrate-session";
        var transcripts = new TranscriptStore(
            Path.Combine(Path.GetTempPath(), $"hermes-archive-hydrate-{Guid.NewGuid():N}"));

        await transcripts.SaveMessageAsync(sessionId, AssistantToolCall("call-1"), CancellationToken.None);
        await transcripts.SaveMessageAsync(sessionId, TodoResult("call-1", "{\"todos\":[{\"id\":\"1\",\"content\":\"Hydrated archive task\",\"status\":\"in_progress\"}]}"), CancellationToken.None);
        await transcripts.SaveMessageAsync(sessionId, AssistantFinal("Done"), CancellationToken.None);

        var messages = await transcripts.LoadSessionAsync(sessionId, CancellationToken.None);
        var entries = SessionTodoArchiveService.BuildArchive(sessionId, messages);

        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("Hydrated archive task", entries[0].Todos[0].Content);
        Assert.AreEqual("in_progress", entries[0].Todos[0].Status);
    }

    private static Message AssistantToolCall(string callId, string toolName = "todo")
        => new()
        {
            Role = "assistant",
            Content = "",
            ToolCalls =
            [
                new ToolCall { Id = callId, Name = toolName, Arguments = "{}" }
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

    private static Message TodoResult(string callId, string content, string toolName = "todo")
        => new()
        {
            Role = "tool",
            ToolName = toolName,
            ToolCallId = callId,
            Content = content,
            Timestamp = new DateTime(2026, 4, 30, 1, 0, 0, DateTimeKind.Utc)
        };
}
