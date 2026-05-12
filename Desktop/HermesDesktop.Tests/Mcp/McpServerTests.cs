using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Hermes.Agent.Mcp;
using Hermes.Agent.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Mcp;

[TestClass]
[DoNotParallelize]
public class McpServerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [TestMethod]
    public async Task PostMcp_Initialize_ReturnsNegotiatedProtocolAndCapabilities()
    {
        await using var server = await StartServerAsync(new ITool[] { new EchoTool() });
        using var client = CreateClient(server);

        using var response = await PostMcpAsync(client, server, new
        {
            jsonrpc = "2.0",
            id = "init-1",
            method = "initialize",
            @params = new
            {
                protocolVersion = "2025-11-25",
                capabilities = new { },
                clientInfo = new { name = "test-client", version = "1.0.0" }
            }
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;
        Assert.AreEqual("2.0", root.GetProperty("jsonrpc").GetString());
        Assert.AreEqual("init-1", root.GetProperty("id").GetString());
        var result = root.GetProperty("result");
        Assert.AreEqual("2025-11-25", result.GetProperty("protocolVersion").GetString());
        Assert.IsFalse(result.GetProperty("capabilities").GetProperty("tools").GetProperty("listChanged").GetBoolean());
        Assert.AreEqual("hermes-desktop", result.GetProperty("serverInfo").GetProperty("name").GetString());
    }

    [TestMethod]
    public async Task PostMcp_ToolsList_ReturnsToolSchemaFromSchemaProvider()
    {
        await using var server = await StartServerAsync(new ITool[] { new EchoTool() });
        using var client = CreateClient(server);

        using var response = await PostMcpAsync(client, server, new
        {
            jsonrpc = "2.0",
            id = "tools-1",
            method = "tools/list"
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        var tools = document.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToArray();
        Assert.AreEqual(1, tools.Length);
        Assert.AreEqual("echo", tools[0].GetProperty("name").GetString());
        Assert.AreEqual("Echoes text.", tools[0].GetProperty("description").GetString());
        Assert.AreEqual("object", tools[0].GetProperty("inputSchema").GetProperty("type").GetString());
        Assert.IsTrue(tools[0].GetProperty("inputSchema").GetProperty("required").EnumerateArray().Any(value => value.GetString() == "text"));
    }

    [TestMethod]
    public async Task PostMcp_ToolsCall_ReturnsStandardTextContentBlock()
    {
        await using var server = await StartServerAsync(new ITool[] { new EchoTool() });
        using var client = CreateClient(server);

        using var response = await PostMcpAsync(client, server, new
        {
            jsonrpc = "2.0",
            id = "call-1",
            method = "tools/call",
            @params = new
            {
                name = "echo",
                arguments = new
                {
                    text = "hello"
                }
            }
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        var result = document.RootElement.GetProperty("result");
        Assert.IsFalse(result.GetProperty("isError").GetBoolean());
        var content = result.GetProperty("content").EnumerateArray().Single();
        Assert.AreEqual("text", content.GetProperty("type").GetString());
        Assert.AreEqual("echo:hello", content.GetProperty("text").GetString());
        Assert.IsFalse(content.TryGetProperty("value", out _));
    }

    [TestMethod]
    public void McpToolResult_DeserializeStandardTextBlock_MapsTextProperty()
    {
        var result = JsonSerializer.Deserialize<McpToolResult>(
            """
            {
              "content": [
                {
                  "type": "text",
                  "text": "standard content"
                }
              ],
              "isError": false
            }
            """,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

        Assert.IsNotNull(result);
        var text = result.Content.Single() as McpContentBlock.TextBlock;
        Assert.IsNotNull(text);
        Assert.AreEqual("standard content", text.Text);
    }

    [TestMethod]
    public async Task PostMcp_Notification_ReturnsAcceptedWithNoBody()
    {
        await using var server = await StartServerAsync(new ITool[] { new EchoTool() });
        using var client = CreateClient(server);

        using var response = await PostMcpAsync(client, server, new
        {
            jsonrpc = "2.0",
            method = "notifications/initialized"
        });

        Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
        Assert.AreEqual(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task PostMcp_NotificationWithoutJsonRpc_ReturnsInvalidRequest()
    {
        await using var server = await StartServerAsync(new ITool[] { new EchoTool() });
        using var client = CreateClient(server);

        using var response = await PostMcpAsync(client, server, new
        {
            method = "notifications/initialized"
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        Assert.AreEqual(-32600, document.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    }

    [TestMethod]
    public async Task PostMcp_ToolFailure_ReturnsCallToolErrorResult()
    {
        await using var server = await StartServerAsync(new ITool[] { new FailingTool() });
        using var client = CreateClient(server);

        using var response = await PostMcpAsync(client, server, new
        {
            jsonrpc = "2.0",
            id = "call-fail-1",
            method = "tools/call",
            @params = new
            {
                name = "fail",
                arguments = new { }
            }
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        var result = document.RootElement.GetProperty("result");
        Assert.IsTrue(result.GetProperty("isError").GetBoolean());
        Assert.AreEqual("tool failed", result.GetProperty("content")[0].GetProperty("text").GetString());
        Assert.IsFalse(document.RootElement.TryGetProperty("error", out _));
    }

    [TestMethod]
    public async Task PostMcp_UnknownMethod_ReturnsJsonRpcMethodNotFound()
    {
        await using var server = await StartServerAsync(new ITool[] { new EchoTool() });
        using var client = CreateClient(server);

        using var response = await PostMcpAsync(client, server, new
        {
            jsonrpc = "2.0",
            id = "unknown-1",
            method = "unknown/method"
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonAsync(response);
        Assert.AreEqual("unknown-1", document.RootElement.GetProperty("id").GetString());
        Assert.AreEqual(-32601, document.RootElement.GetProperty("error").GetProperty("code").GetInt32());
    }

    [TestMethod]
    public async Task PostMcp_WithNonLocalOrigin_ReturnsForbidden()
    {
        await using var server = await StartServerAsync(new ITool[] { new EchoTool() });
        using var client = CreateClient(server);
        client.DefaultRequestHeaders.Add("Origin", "https://example.com");

        using var response = await PostMcpAsync(client, server, new
        {
            jsonrpc = "2.0",
            id = "origin-1",
            method = "tools/list"
        });

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task GetMcp_ReturnsMethodNotAllowedUntilSseIsImplemented()
    {
        await using var server = await StartServerAsync(new ITool[] { new EchoTool() });
        using var client = CreateClient(server);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://localhost:{server.Port}/mcp");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", server.AuthToken);

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [TestMethod]
    public async Task PostMcp_StardewNavigateToTile_RecordsTerminalStatusForNextAutonomyWake()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-mcp-stardew-completion-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var descriptor = CreateDescriptor("haley");
            var supervisor = new NpcRuntimeSupervisor();
            var driver = await supervisor.GetOrCreateDriverAsync(descriptor, tempDir, CancellationToken.None);
            var commands = new CapturingCommandService(
                [
                    new GameCommandStatus(
                        "cmd-mcp-move",
                        "haley",
                        "move",
                        StardewCommandStatuses.Completed,
                        1,
                        null,
                        null)
                ],
                new GameCommandResult(true, "cmd-mcp-move", StardewCommandStatuses.Queued, null, "trace-mcp-move"));
            var tools = StardewNpcToolFactory.CreateDefault(
                new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
                descriptor,
                traceIdFactory: () => "trace-mcp-move",
                idempotencyKeyFactory: () => "idem-mcp-move",
                maxStatusPolls: 1,
                runtimeDriver: driver);
            await using var server = await StartServerAsync(tools);
            using var client = CreateClient(server);

            using var response = await PostMcpAsync(client, server, new
            {
                jsonrpc = "2.0",
                id = "stardew-move-1",
                method = "tools/call",
                @params = new
                {
                    name = "stardew_navigate_to_tile",
                    arguments = new
                    {
                        locationName = "Beach",
                        x = 32,
                        y = 34,
                        source = "map-skill:stardew.navigation.poi.beach-shoreline",
                        facingDirection = 2,
                        reason = "meet the player at the shoreline",
                        thought = "I should actually walk there."
                    }
                }
            });

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            using var document = await ReadJsonAsync(response);
            var result = document.RootElement.GetProperty("result");
            Assert.IsFalse(result.GetProperty("isError").GetBoolean());
            Assert.AreEqual("text", result.GetProperty("content")[0].GetProperty("type").GetString());
            StringAssert.Contains(result.GetProperty("content")[0].GetProperty("text").GetString(), "cmd-mcp-move");
            Assert.IsNotNull(commands.LastAction);
            Assert.AreEqual("haley", commands.LastAction.NpcId);
            Assert.AreEqual("Beach", commands.LastAction.Target.LocationName);
            Assert.AreEqual(32, commands.LastAction.Target.Tile?.X);
            Assert.AreEqual(34, commands.LastAction.Target.Tile?.Y);
            Assert.AreEqual("map-skill:stardew.navigation.poi.beach-shoreline", commands.LastAction.Payload?["targetSource"]?.ToString());

            var snapshot = driver.Snapshot();
            Assert.AreEqual(StardewCommandStatuses.Completed, snapshot.LastTerminalCommandStatus?.Status);
            Assert.AreEqual("cmd-mcp-move", snapshot.LastTerminalCommandStatus?.CommandId);

            var instance = new NpcRuntimeInstance(descriptor, new NpcNamespace(tempDir, descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId));
            await instance.StartAsync(CancellationToken.None);
            instance.SetLastTerminalCommandStatus(snapshot.LastTerminalCommandStatus);
            var agent = new CapturingAgent();
            var loop = new NpcAutonomyLoop(
                new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
                new NpcObservationFactStore(),
                agent);

            await loop.RunOneTickAsync(instance, new GameEventCursor(null), CancellationToken.None);

            Assert.IsNotNull(agent.LastMessage);
            StringAssert.Contains(agent.LastMessage, "last_action_result");
            StringAssert.Contains(agent.LastMessage, "commandId=cmd-mcp-move");
            StringAssert.Contains(agent.LastMessage, "status=completed");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task PostMcp_StardewIdleMicroAction_RecordsTerminalStatusForNextAutonomyWake()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-mcp-stardew-idle-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var descriptor = CreateDescriptor("haley");
            var supervisor = new NpcRuntimeSupervisor();
            var driver = await supervisor.GetOrCreateDriverAsync(descriptor, tempDir, CancellationToken.None);
            var commands = new CapturingCommandService(
                [
                    new GameCommandStatus(
                        "cmd-mcp-idle",
                        "haley",
                        "idle_micro_action",
                        StardewCommandStatuses.Completed,
                        1,
                        null,
                        null)
                ],
                new GameCommandResult(true, "cmd-mcp-idle", StardewCommandStatuses.Queued, null, "trace-mcp-idle"));
            var tools = StardewNpcToolFactory.CreateDefault(
                new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
                descriptor,
                traceIdFactory: () => "trace-mcp-idle",
                idempotencyKeyFactory: () => "idem-mcp-idle",
                maxStatusPolls: 1,
                runtimeDriver: driver);
            await using var server = await StartServerAsync(tools);
            using var client = CreateClient(server);

            using var response = await PostMcpAsync(client, server, new
            {
                jsonrpc = "2.0",
                id = "stardew-idle-1",
                method = "tools/call",
                @params = new
                {
                    name = "stardew_idle_micro_action",
                    arguments = new
                    {
                        kind = "look_around",
                        intensity = "light",
                        ttlSeconds = 4
                    }
                }
            });

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            using var document = await ReadJsonAsync(response);
            var result = document.RootElement.GetProperty("result");
            Assert.IsFalse(result.GetProperty("isError").GetBoolean());
            Assert.AreEqual("text", result.GetProperty("content")[0].GetProperty("type").GetString());
            StringAssert.Contains(result.GetProperty("content")[0].GetProperty("text").GetString(), "cmd-mcp-idle");
            Assert.IsNotNull(commands.LastAction);
            Assert.AreEqual("haley", commands.LastAction.NpcId);
            Assert.AreEqual(GameActionType.IdleMicroAction, commands.LastAction.Type);
            Assert.AreEqual("self", commands.LastAction.Target.Kind);
            Assert.AreEqual("look_around", commands.LastAction.Payload?["kind"]?.ToString());
            Assert.AreEqual("light", commands.LastAction.Payload?["intensity"]?.ToString());
            Assert.AreEqual(4, (int?)commands.LastAction.Payload?["ttlSeconds"]);

            var snapshot = driver.Snapshot();
            Assert.AreEqual(StardewCommandStatuses.Completed, snapshot.LastTerminalCommandStatus?.Status);
            Assert.AreEqual("cmd-mcp-idle", snapshot.LastTerminalCommandStatus?.CommandId);
            Assert.AreEqual("idle_micro_action", snapshot.LastTerminalCommandStatus?.Action);

            var instance = new NpcRuntimeInstance(descriptor, new NpcNamespace(tempDir, descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId));
            await instance.StartAsync(CancellationToken.None);
            instance.SetLastTerminalCommandStatus(snapshot.LastTerminalCommandStatus);
            var agent = new CapturingAgent();
            var loop = new NpcAutonomyLoop(
                new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
                new NpcObservationFactStore(),
                agent);

            await loop.RunOneTickAsync(instance, new GameEventCursor(null), CancellationToken.None);

            Assert.IsNotNull(agent.LastMessage);
            StringAssert.Contains(agent.LastMessage, "last_action_result");
            StringAssert.Contains(agent.LastMessage, "commandId=cmd-mcp-idle");
            StringAssert.Contains(agent.LastMessage, "action=idle_micro_action");
            StringAssert.Contains(agent.LastMessage, "status=completed");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task PostMcp_StardewSpeak_RecordsTerminalStatusForNextAutonomyWake()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-mcp-stardew-speak-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var descriptor = CreateDescriptor("haley");
            var supervisor = new NpcRuntimeSupervisor();
            var driver = await supervisor.GetOrCreateDriverAsync(descriptor, tempDir, CancellationToken.None);
            var commands = new CapturingCommandService(
                [
                    new GameCommandStatus(
                        "cmd-mcp-speak",
                        "haley",
                        "speak",
                        StardewCommandStatuses.Completed,
                        1,
                        null,
                        null)
                ],
                new GameCommandResult(true, "cmd-mcp-speak", StardewCommandStatuses.Queued, null, "trace-mcp-speak"));
            var tools = StardewNpcToolFactory.CreateDefault(
                new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
                descriptor,
                traceIdFactory: () => "trace-mcp-speak",
                idempotencyKeyFactory: () => "idem-mcp-speak",
                maxStatusPolls: 1,
                runtimeDriver: driver);
            await using var server = await StartServerAsync(tools);
            using var client = CreateClient(server);

            using var response = await PostMcpAsync(client, server, new
            {
                jsonrpc = "2.0",
                id = "stardew-speak-1",
                method = "tools/call",
                @params = new
                {
                    name = "stardew_speak",
                    arguments = new
                    {
                        text = "I'll be there soon.",
                        channel = "player"
                    }
                }
            });

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            using var document = await ReadJsonAsync(response);
            var result = document.RootElement.GetProperty("result");
            Assert.IsFalse(result.GetProperty("isError").GetBoolean());
            StringAssert.Contains(result.GetProperty("content")[0].GetProperty("text").GetString(), "cmd-mcp-speak");
            Assert.IsNotNull(commands.LastAction);
            Assert.AreEqual("haley", commands.LastAction.NpcId);
            Assert.AreEqual(GameActionType.Speak, commands.LastAction.Type);
            Assert.AreEqual("player", commands.LastAction.Target.Kind);
            Assert.AreEqual("I'll be there soon.", commands.LastAction.Payload?["text"]?.ToString());
            Assert.AreEqual("player", commands.LastAction.Payload?["channel"]?.ToString());

            var snapshot = driver.Snapshot();
            Assert.AreEqual(StardewCommandStatuses.Completed, snapshot.LastTerminalCommandStatus?.Status);
            Assert.AreEqual("cmd-mcp-speak", snapshot.LastTerminalCommandStatus?.CommandId);
            Assert.AreEqual("speak", snapshot.LastTerminalCommandStatus?.Action);

            var instance = new NpcRuntimeInstance(descriptor, new NpcNamespace(tempDir, descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId));
            await instance.StartAsync(CancellationToken.None);
            instance.SetLastTerminalCommandStatus(snapshot.LastTerminalCommandStatus);
            var agent = new CapturingAgent();
            var loop = new NpcAutonomyLoop(
                new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
                new NpcObservationFactStore(),
                agent);

            await loop.RunOneTickAsync(instance, new GameEventCursor(null), CancellationToken.None);

            Assert.IsNotNull(agent.LastMessage);
            StringAssert.Contains(agent.LastMessage, "last_action_result");
            StringAssert.Contains(agent.LastMessage, "commandId=cmd-mcp-speak");
            StringAssert.Contains(agent.LastMessage, "action=speak");
            StringAssert.Contains(agent.LastMessage, "status=completed");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task PostMcp_StardewOpenPrivateChat_RecordsTerminalStatusForNextAutonomyWake()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-mcp-stardew-open-chat-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var descriptor = CreateDescriptor("haley");
            var supervisor = new NpcRuntimeSupervisor();
            var driver = await supervisor.GetOrCreateDriverAsync(descriptor, tempDir, CancellationToken.None);
            var commands = new CapturingCommandService(
                [
                    new GameCommandStatus(
                        "cmd-mcp-open-chat",
                        "haley",
                        "open_private_chat",
                        StardewCommandStatuses.Completed,
                        1,
                        null,
                        null)
                ],
                new GameCommandResult(true, "cmd-mcp-open-chat", StardewCommandStatuses.Queued, null, "trace-mcp-open-chat"));
            var tools = StardewNpcToolFactory.CreateDefault(
                new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
                descriptor,
                traceIdFactory: () => "trace-mcp-open-chat",
                idempotencyKeyFactory: () => "idem-mcp-open-chat",
                maxStatusPolls: 1,
                runtimeDriver: driver);
            await using var server = await StartServerAsync(tools);
            using var client = CreateClient(server);

            using var response = await PostMcpAsync(client, server, new
            {
                jsonrpc = "2.0",
                id = "stardew-open-chat-1",
                method = "tools/call",
                @params = new
                {
                    name = "stardew_open_private_chat",
                    arguments = new
                    {
                        prompt = "Ask me in private."
                    }
                }
            });

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            using var document = await ReadJsonAsync(response);
            var result = document.RootElement.GetProperty("result");
            Assert.IsFalse(result.GetProperty("isError").GetBoolean());
            StringAssert.Contains(result.GetProperty("content")[0].GetProperty("text").GetString(), "cmd-mcp-open-chat");
            Assert.IsNotNull(commands.LastAction);
            Assert.AreEqual("haley", commands.LastAction.NpcId);
            Assert.AreEqual(GameActionType.OpenPrivateChat, commands.LastAction.Type);
            Assert.AreEqual("player", commands.LastAction.Target.Kind);
            Assert.AreEqual("Ask me in private.", commands.LastAction.Payload?["prompt"]?.ToString());

            var snapshot = driver.Snapshot();
            Assert.AreEqual(StardewCommandStatuses.Completed, snapshot.LastTerminalCommandStatus?.Status);
            Assert.AreEqual("cmd-mcp-open-chat", snapshot.LastTerminalCommandStatus?.CommandId);
            Assert.AreEqual("open_private_chat", snapshot.LastTerminalCommandStatus?.Action);

            var instance = new NpcRuntimeInstance(descriptor, new NpcNamespace(tempDir, descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId));
            await instance.StartAsync(CancellationToken.None);
            instance.SetLastTerminalCommandStatus(snapshot.LastTerminalCommandStatus);
            var agent = new CapturingAgent();
            var loop = new NpcAutonomyLoop(
                new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
                new NpcObservationFactStore(),
                agent);

            await loop.RunOneTickAsync(instance, new GameEventCursor(null), CancellationToken.None);

            Assert.IsNotNull(agent.LastMessage);
            StringAssert.Contains(agent.LastMessage, "interaction_session");
            StringAssert.Contains(agent.LastMessage, "commandId=cmd-mcp-open-chat");
            StringAssert.Contains(agent.LastMessage, "action=open_private_chat");
            StringAssert.Contains(agent.LastMessage, "status=completed");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task PostMcp_StardewNavigateToTile_WithBlockedStatus_WakesAgentWithFailureFact()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-mcp-stardew-blocked-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var descriptor = CreateDescriptor("haley");
            var supervisor = new NpcRuntimeSupervisor();
            var driver = await supervisor.GetOrCreateDriverAsync(descriptor, tempDir, CancellationToken.None);
            var commands = new CapturingCommandService(
                [
                    new GameCommandStatus(
                        "cmd-mcp-blocked",
                        "haley",
                        "move",
                        StardewCommandStatuses.Blocked,
                        1,
                        "path_blocked",
                        "path_blocked")
                ],
                new GameCommandResult(true, "cmd-mcp-blocked", StardewCommandStatuses.Queued, null, "trace-mcp-blocked"));
            var tools = StardewNpcToolFactory.CreateDefault(
                new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
                descriptor,
                traceIdFactory: () => "trace-mcp-blocked",
                idempotencyKeyFactory: () => "idem-mcp-blocked",
                maxStatusPolls: 1,
                runtimeDriver: driver);
            await using var server = await StartServerAsync(tools);
            using var client = CreateClient(server);

            using var response = await PostMcpAsync(client, server, new
            {
                jsonrpc = "2.0",
                id = "stardew-move-blocked",
                method = "tools/call",
                @params = new
                {
                    name = "stardew_navigate_to_tile",
                    arguments = new
                    {
                        locationName = "Beach",
                        x = 32,
                        y = 34,
                        source = "map-skill:stardew.navigation.poi.beach-shoreline",
                        reason = "try to meet the player"
                    }
                }
            });

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            using var document = await ReadJsonAsync(response);
            Assert.IsFalse(document.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
            var snapshot = driver.Snapshot();
            Assert.AreEqual(StardewCommandStatuses.Blocked, snapshot.LastTerminalCommandStatus?.Status);
            Assert.AreEqual("path_blocked", snapshot.LastTerminalCommandStatus?.ErrorCode);

            var instance = new NpcRuntimeInstance(descriptor, new NpcNamespace(tempDir, descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId));
            await instance.StartAsync(CancellationToken.None);
            instance.SetLastTerminalCommandStatus(snapshot.LastTerminalCommandStatus);
            var agent = new CapturingAgent();
            var loop = new NpcAutonomyLoop(
                new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
                new NpcObservationFactStore(),
                agent);

            await loop.RunOneTickAsync(instance, new GameEventCursor(null), CancellationToken.None);

            Assert.IsNotNull(agent.LastMessage);
            StringAssert.Contains(agent.LastMessage, "last_action_result");
            StringAssert.Contains(agent.LastMessage, "commandId=cmd-mcp-blocked");
            StringAssert.Contains(agent.LastMessage, "status=blocked");
            StringAssert.Contains(agent.LastMessage, "reason=path_blocked");
            StringAssert.Contains(agent.LastMessage, "不是下一步指令");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    private static async Task<McpServer> StartServerAsync(IEnumerable<ITool> tools)
    {
        var toolRegistry = tools.ToDictionary(tool => tool.Name);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var server = new McpServer(toolRegistry, NullLogger<McpServer>.Instance, "test-token");
            try
            {
                await server.StartAsync(GetFreePort(), CancellationToken.None);
                return server;
            }
            catch (HttpListenerException) when (attempt < 9)
            {
                await server.DisposeAsync();
            }
        }

        throw new InvalidOperationException("Failed to allocate a test port for MCP server.");
    }

    private static HttpClient CreateClient(McpServer server)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", server.AuthToken);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return client;
    }

    private static async Task<HttpResponseMessage> PostMcpAsync(HttpClient client, McpServer server, object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"http://localhost:{server.Port}/mcp")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        return await client.SendAsync(request);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body);
    }

    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class EchoTool : ITool, IToolSchemaProvider
    {
        public string Name => "echo";

        public string Description => "Echoes text.";

        public Type ParametersType => typeof(EchoParameters);

        public JsonElement GetParameterSchema()
            => JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new
                {
                    text = new { type = "string" }
                },
                required = new[] { "text" }
            }, JsonOptions);

        public Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
        {
            var p = (EchoParameters)parameters;
            return Task.FromResult(ToolResult.Ok($"echo:{p.Text}"));
        }
    }

    private sealed class FailingTool : ITool
    {
        public string Name => "fail";

        public string Description => "Fails.";

        public Type ParametersType => typeof(NoParameters);

        public Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
            => Task.FromResult(ToolResult.Fail("tool failed"));
    }

    private sealed class EchoParameters
    {
        public string? Text { get; init; }
    }

    private sealed class NoParameters
    {
    }

    private static NpcRuntimeDescriptor CreateDescriptor(string npcId)
        => new(
            npcId,
            npcId,
            "stardew-valley",
            "save-1",
            "default",
            "stardew",
            "pack-root",
            $"sdv_save-1_{npcId}_default",
            new NpcBodyBinding(npcId, "Haley", "Haley", "Haley", "stardew"));

    private sealed class FakeGameAdapter : IGameAdapter
    {
        public FakeGameAdapter(IGameCommandService commands, IGameQueryService queries, IGameEventSource events)
        {
            Commands = commands;
            Queries = queries;
            Events = events;
        }

        public string AdapterId => "stardew";

        public IGameCommandService Commands { get; }

        public IGameQueryService Queries { get; }

        public IGameEventSource Events { get; }
    }

    private sealed class CapturingCommandService : IGameCommandService
    {
        private readonly Queue<GameCommandStatus> _statusSequence;
        private readonly GameCommandResult _submitResult;

        public CapturingCommandService(
            IReadOnlyList<GameCommandStatus>? statusSequence = null,
            GameCommandResult? submitResult = null)
        {
            _statusSequence = new Queue<GameCommandStatus>(statusSequence ?? []);
            _submitResult = submitResult ?? new GameCommandResult(true, "cmd-1", StardewCommandStatuses.Queued, null, "trace-1");
        }

        public GameAction? LastAction { get; private set; }

        public Task<GameCommandResult> SubmitAsync(GameAction action, CancellationToken ct)
        {
            LastAction = action;
            return Task.FromResult(_submitResult);
        }

        public Task<GameCommandStatus> GetStatusAsync(string commandId, CancellationToken ct)
            => Task.FromResult(_statusSequence.Count > 0
                ? _statusSequence.Dequeue()
                : new GameCommandStatus(commandId, "haley", "move", StardewCommandStatuses.Running, 0.5, null, null));

        public Task<GameCommandStatus> CancelAsync(string commandId, string reason, CancellationToken ct)
            => Task.FromResult(new GameCommandStatus(commandId, "haley", "move", StardewCommandStatuses.Cancelled, 0, reason, null));
    }

    private sealed class FakeQueryService : IGameQueryService
    {
        public Task<GameObservation> ObserveAsync(string npcId, CancellationToken ct)
            => Task.FromResult(new GameObservation(npcId, "stardew-valley", DateTime.UtcNow, "status", []));

        public Task<WorldSnapshot> GetWorldSnapshotAsync(string npcId, CancellationToken ct)
            => Task.FromResult(new WorldSnapshot("stardew-valley", "save-1", DateTime.UtcNow, [], []));
    }

    private sealed class FakeEventSource : IGameEventSource
    {
        public Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<GameEventRecord>>([]);

        public Task<GameEventBatch> PollBatchAsync(GameEventCursor cursor, CancellationToken ct)
            => Task.FromResult(new GameEventBatch([], cursor));
    }

    private sealed class CapturingAgent : Hermes.Agent.Core.IAgent
    {
        public string? LastMessage { get; private set; }

        public Task<string> ChatAsync(string message, Session session, CancellationToken ct)
        {
            LastMessage = message;
            return Task.FromResult("ok");
        }

        public IAsyncEnumerable<Hermes.Agent.LLM.StreamEvent> StreamChatAsync(string message, Session session, CancellationToken ct)
            => AsyncEnumerable.Empty<Hermes.Agent.LLM.StreamEvent>();

        public void RegisterTool(ITool tool)
        {
        }
    }
}
