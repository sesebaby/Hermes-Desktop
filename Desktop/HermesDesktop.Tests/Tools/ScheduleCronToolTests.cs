using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace HermesDesktop.Tests.Tools;

[TestClass]
public class ScheduleCronToolTests
{
    [TestMethod]
    public async Task ExecuteAsync_WithAgentInjectedSessionContext_StoresSessionIdOnScheduledTask()
    {
        var scheduler = new CapturingCronScheduler();
        var chatClient = new Mock<IChatClient>(MockBehavior.Strict);
        chatClient
            .SetupSequence(client => client.CompleteWithToolsAsync(
                It.IsAny<IEnumerable<Message>>(),
                It.IsAny<IEnumerable<ToolDefinition>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse
            {
                ToolCalls =
                [
                    new ToolCall
                    {
                        Id = "call_schedule",
                        Name = "schedule_cron",
                        Arguments = """
                            {
                              "name": "drink_water",
                              "cronExpression": "*/1 * * * *",
                              "prompt": "该喝水了",
                              "recurring": true,
                              "durable": true
                            }
                            """
                    }
                ],
                FinishReason = "tool_calls"
            })
            .ReturnsAsync(new ChatResponse
            {
                Content = "已设置提醒。",
                FinishReason = "stop"
            });

        var agent = new Agent(chatClient.Object, NullLogger<Agent>.Instance);
        agent.RegisterTool(new ScheduleCronTool(scheduler));
        var session = new Session { Id = "current-session", Platform = "desktop" };

        await agent.ChatAsync("每 1 分钟提醒我喝水", session, CancellationToken.None);

        Assert.IsNotNull(scheduler.ScheduledTask);
        var sessionIdProperty = scheduler.ScheduledTask.GetType().GetProperty("SessionId");
        Assert.IsNotNull(sessionIdProperty, "Scheduled cron tasks must retain the source session id.");
        Assert.AreEqual("current-session", sessionIdProperty.GetValue(scheduler.ScheduledTask));
    }

    private sealed class CapturingCronScheduler : ICronScheduler
    {
        public CronTask? ScheduledTask { get; private set; }

        public event EventHandler<CronTaskDueEventArgs>? TaskDue
        {
            add { }
            remove { }
        }

        public void Schedule(CronTask task) => ScheduledTask = task;

        public void Cancel(string taskId)
        {
        }

        public CronTask? GetTask(string taskId) => ScheduledTask?.Id == taskId ? ScheduledTask : null;

        public IReadOnlyList<CronTask> GetAllTasks() =>
            ScheduledTask is null ? [] : [ScheduledTask];

        public DateTimeOffset? GetNextRun(string taskId) => DateTimeOffset.UtcNow.AddMinutes(1);
    }
}
