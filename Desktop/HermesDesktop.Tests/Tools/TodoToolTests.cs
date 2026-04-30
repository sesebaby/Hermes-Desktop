using System.Text.Json;
using Hermes.Agent.Tasks;
using Hermes.Agent.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Tools;

[TestClass]
public sealed class TodoToolTests
{
    [TestMethod]
    public async Task ExecuteAsync_WriteThenRead_ReturnsFullReferenceShapedJsonForCurrentSession()
    {
        var store = new SessionTodoStore();
        var tool = new TodoTool(store);

        var write = await tool.ExecuteAsync(new TodoToolParameters
        {
            CurrentSessionId = "session-a",
            Todos =
            [
                new TodoItemInput { Id = "1", Content = "First task", Status = "pending" },
                new TodoItemInput { Id = "2", Content = "Second task", Status = "in_progress" }
            ]
        }, CancellationToken.None);

        Assert.IsTrue(write.Success, write.Content);
        using var writeJson = JsonDocument.Parse(write.Content);
        Assert.AreEqual(2, writeJson.RootElement.GetProperty("summary").GetProperty("total").GetInt32());
        Assert.AreEqual(1, writeJson.RootElement.GetProperty("summary").GetProperty("pending").GetInt32());
        Assert.AreEqual(1, writeJson.RootElement.GetProperty("summary").GetProperty("in_progress").GetInt32());
        Assert.AreEqual("1", writeJson.RootElement.GetProperty("todos")[0].GetProperty("id").GetString());
        Assert.AreEqual("pending", writeJson.RootElement.GetProperty("todos")[0].GetProperty("status").GetString());

        var read = await tool.ExecuteAsync(new TodoToolParameters
        {
            CurrentSessionId = "session-a"
        }, CancellationToken.None);

        Assert.IsTrue(read.Success, read.Content);
        using var readJson = JsonDocument.Parse(read.Content);
        Assert.AreEqual(2, readJson.RootElement.GetProperty("todos").GetArrayLength());
        Assert.AreEqual("Second task", readJson.RootElement.GetProperty("todos")[1].GetProperty("content").GetString());
    }

    [TestMethod]
    public async Task ExecuteAsync_MergeMode_UpdatesExistingByIdAndAppendsNewItems()
    {
        var store = new SessionTodoStore();
        var tool = new TodoTool(store);

        await tool.ExecuteAsync(new TodoToolParameters
        {
            CurrentSessionId = "session-a",
            Todos =
            [
                new TodoItemInput { Id = "1", Content = "Original", Status = "pending" }
            ]
        }, CancellationToken.None);

        var result = await tool.ExecuteAsync(new TodoToolParameters
        {
            CurrentSessionId = "session-a",
            Merge = true,
            Todos =
            [
                new TodoItemInput { Id = "1", Status = "completed" },
                new TodoItemInput { Id = "2", Content = "Next", Status = "pending" }
            ]
        }, CancellationToken.None);

        Assert.IsTrue(result.Success, result.Content);
        using var json = JsonDocument.Parse(result.Content);
        var todos = json.RootElement.GetProperty("todos");
        Assert.AreEqual(2, todos.GetArrayLength());
        Assert.AreEqual("Original", todos[0].GetProperty("content").GetString());
        Assert.AreEqual("completed", todos[0].GetProperty("status").GetString());
        Assert.AreEqual("2", todos[1].GetProperty("id").GetString());
    }

    [TestMethod]
    public async Task ExecuteAsync_KeepsTodoStateScopedByCurrentSessionId()
    {
        var store = new SessionTodoStore();
        var tool = new TodoTool(store);

        await tool.ExecuteAsync(new TodoToolParameters
        {
            CurrentSessionId = "session-a",
            Todos =
            [
                new TodoItemInput { Id = "a", Content = "Session A item", Status = "pending" }
            ]
        }, CancellationToken.None);

        var sessionB = await tool.ExecuteAsync(new TodoToolParameters
        {
            CurrentSessionId = "session-b"
        }, CancellationToken.None);

        using var json = JsonDocument.Parse(sessionB.Content);
        Assert.AreEqual(0, json.RootElement.GetProperty("summary").GetProperty("total").GetInt32());
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplaceModeDeduplicatesDuplicateIdsKeepingLastOccurrence()
    {
        var store = new SessionTodoStore();
        var tool = new TodoTool(store);

        var result = await tool.ExecuteAsync(new TodoToolParameters
        {
            CurrentSessionId = "session-a",
            Todos =
            [
                new TodoItemInput { Id = "1", Content = "First version", Status = "pending" },
                new TodoItemInput { Id = "2", Content = "Other task", Status = "pending" },
                new TodoItemInput { Id = "1", Content = "Latest version", Status = "in_progress" }
            ]
        }, CancellationToken.None);

        using var json = JsonDocument.Parse(result.Content);
        var todos = json.RootElement.GetProperty("todos");
        Assert.AreEqual(2, todos.GetArrayLength());
        Assert.AreEqual("2", todos[0].GetProperty("id").GetString());
        Assert.AreEqual("1", todos[1].GetProperty("id").GetString());
        Assert.AreEqual("Latest version", todos[1].GetProperty("content").GetString());
    }

    [TestMethod]
    public async Task ExecuteAsync_InvalidStatusNormalizesToPendingLikeReferenceTool()
    {
        var store = new SessionTodoStore();
        var tool = new TodoTool(store);

        var result = await tool.ExecuteAsync(new TodoToolParameters
        {
            CurrentSessionId = "session-a",
            Todos =
            [
                new TodoItemInput { Id = "1", Content = "Unknown status", Status = "not_real" }
            ]
        }, CancellationToken.None);

        using var json = JsonDocument.Parse(result.Content);
        Assert.AreEqual("pending", json.RootElement.GetProperty("todos")[0].GetProperty("status").GetString());
    }

    [TestMethod]
    public void GetParameterSchema_DescribesReferenceTodoItemStatusEnum()
    {
        var schema = new TodoTool(new SessionTodoStore()).GetParameterSchema();

        var status = schema
            .GetProperty("properties")
            .GetProperty("todos")
            .GetProperty("items")
            .GetProperty("properties")
            .GetProperty("status");

        var values = status.GetProperty("enum").EnumerateArray().Select(v => v.GetString()).ToArray();
        CollectionAssert.AreEqual(
            new[] { "pending", "in_progress", "completed", "cancelled" },
            values);
    }

    [TestMethod]
    public void SessionTodoStore_FormatActiveTasksForInjection_FiltersCompletedAndCancelled()
    {
        var store = new SessionTodoStore();
        store.Write("session-a",
        [
            new SessionTodoInput("1", "Done", "completed"),
            new SessionTodoInput("2", "Working", "in_progress"),
            new SessionTodoInput("3", "Waiting", "pending"),
            new SessionTodoInput("4", "Abandoned", "cancelled")
        ]);

        var text = store.FormatActiveTasksForInjection("session-a");

        Assert.IsNotNull(text);
        StringAssert.Contains(text, "Working");
        StringAssert.Contains(text, "Waiting");
        Assert.IsFalse(text!.Contains("Done", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("Abandoned", StringComparison.Ordinal));
    }
}
