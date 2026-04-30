using System.Text.Json;
using Hermes.Agent.Core;
using Hermes.Agent.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Services;

[TestClass]
public sealed class SessionTaskProjectionServiceTests
{
    [TestMethod]
    public async Task OnMessageSavedAsync_ProjectsCompletedTodoToolResultIntoSessionSnapshot()
    {
        var store = new SessionTodoStore();
        var projection = new SessionTaskProjectionService(store);
        var content = JsonSerializer.Serialize(new
        {
            todos = new[]
            {
                new { id = "1", content = "Plan", status = "completed" },
                new { id = "2", content = "Build", status = "in_progress" }
            },
            summary = new { total = 2, completed = 1, in_progress = 1, pending = 0, cancelled = 0 }
        });

        await projection.OnMessageSavedAsync("session-a", new Message
        {
            Role = "tool",
            ToolName = "todo",
            ToolCallId = "call-1",
            Content = content
        }, CancellationToken.None);

        var snapshot = projection.GetSnapshot("session-a");
        Assert.AreEqual(2, snapshot.Todos.Count);
        Assert.AreEqual("Build", snapshot.Todos[1].Content);
        Assert.AreEqual("in_progress", snapshot.Todos[1].Status);
        Assert.AreEqual(1, snapshot.Summary.InProgress);
    }

    [TestMethod]
    public async Task OnMessageSavedAsync_ProjectsTodoWriteAliasIntoSessionSnapshot()
    {
        var store = new SessionTodoStore();
        var projection = new SessionTaskProjectionService(store);

        await projection.OnMessageSavedAsync("session-a", new Message
        {
            Role = "tool",
            ToolName = "todo_write",
            ToolCallId = "call-legacy",
            Content = "{\"todos\":[{\"id\":\"1\",\"content\":\"Legacy alias\",\"status\":\"pending\"}]}"
        }, CancellationToken.None);

        var snapshot = projection.GetSnapshot("session-a");
        Assert.AreEqual(1, snapshot.Todos.Count);
        Assert.AreEqual("Legacy alias", snapshot.Todos[0].Content);
    }

    [TestMethod]
    public async Task OnMessageSavedAsync_IgnoresMalformedTodoPayloadWithoutClearingPriorSnapshot()
    {
        var store = new SessionTodoStore();
        var projection = new SessionTaskProjectionService(store);
        await projection.OnMessageSavedAsync("session-a", new Message
        {
            Role = "tool",
            ToolName = "todo",
            Content = "{\"todos\":[{\"id\":\"1\",\"content\":\"Keep\",\"status\":\"pending\"}]}"
        }, CancellationToken.None);

        await projection.OnMessageSavedAsync("session-a", new Message
        {
            Role = "tool",
            ToolName = "todo",
            Content = "{not json"
        }, CancellationToken.None);

        var snapshot = projection.GetSnapshot("session-a");
        Assert.AreEqual(1, snapshot.Todos.Count);
        Assert.AreEqual("Keep", snapshot.Todos[0].Content);
    }

    [TestMethod]
    public async Task HydrateSessionAsync_UsesLatestValidTodoToolResultOnly()
    {
        var store = new SessionTodoStore();
        var projection = new SessionTaskProjectionService(store);
        var messages = new[]
        {
            new Message
            {
                Role = "tool",
                ToolName = "todo",
                Content = "{\"todos\":[{\"id\":\"old\",\"content\":\"Old\",\"status\":\"pending\"}]}"
            },
            new Message { Role = "tool", ToolName = "session_search", Content = "{\"todos\":[]}" },
            new Message
            {
                Role = "tool",
                ToolName = "todo",
                Content = "{\"todos\":[{\"id\":\"new\",\"content\":\"New\",\"status\":\"completed\"}]}"
            }
        };

        await projection.HydrateSessionAsync("session-a", messages, CancellationToken.None);

        var snapshot = projection.GetSnapshot("session-a");
        Assert.AreEqual(1, snapshot.Todos.Count);
        Assert.AreEqual("new", snapshot.Todos[0].Id);
        Assert.AreEqual("completed", snapshot.Todos[0].Status);
    }
}
