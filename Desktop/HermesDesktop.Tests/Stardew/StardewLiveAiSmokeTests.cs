using System.Text.Json;
using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Hermes.Agent.LLM;
using Hermes.Agent.Runtime;
using Hermes.Agent.Skills;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Stardew;

[TestClass]
public sealed class StardewLiveAiSmokeTests
{
    // 真实 AI smoke 默认跳过。启用后优先读取 %LOCALAPPDATA%\hermes\config.yaml，
    // 环境变量只作为临时覆盖。
    // $env:HERMES_STARDEW_LIVE_AI_SMOKE='1'
    // dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj -c Debug --filter "FullyQualifiedName~StardewLiveAiSmokeTests"
    private const string EnableEnv = "HERMES_STARDEW_LIVE_AI_SMOKE";
    private const string BaseUrlEnv = "HERMES_STARDEW_LIVE_AI_BASE_URL";
    private const string ParentModelEnv = "HERMES_STARDEW_LIVE_AI_PARENT_MODEL";
    private const string ExecutorBaseUrlEnv = "HERMES_STARDEW_LIVE_AI_EXECUTOR_BASE_URL";
    private const string ExecutorModelEnv = "HERMES_STARDEW_LIVE_AI_EXECUTOR_MODEL";
    private const string ApiKeyEnv = "HERMES_STARDEW_LIVE_AI_API_KEY";
    private const string AuthModeEnv = "HERMES_STARDEW_LIVE_AI_AUTH_MODE";
    private const string ExecutorApiKeyEnv = "HERMES_STARDEW_LIVE_AI_EXECUTOR_API_KEY";
    private const string ExecutorAuthModeEnv = "HERMES_STARDEW_LIVE_AI_EXECUTOR_AUTH_MODE";

    private static readonly JsonSerializerOptions ToolArgumentJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private string _tempDir = null!;
    private SkillManager _skillManager = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-live-ai-smoke", Guid.NewGuid().ToString("N"));
        var skillsRoot = Path.Combine(_tempDir, "skills");
        CopyDirectory(FindRepositoryPath("skills", "gaming", "stardew-navigation"), Path.Combine(skillsRoot, "stardew-navigation"));
        _skillManager = new SkillManager(skillsRoot, NullLogger<SkillManager>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public async Task PrivateChatParentLiveAi_WhenPlayerAsksHaleyToGoBeachNow_CallsNpcDelegateAction()
    {
        var config = RequireLiveConfig();
        using var httpClient = CreateHttpClient();
        var parentClient = new OpenAiClient(config.Parent, httpClient);
        var delegateTool = new CaptureTool(
            "npc_delegate_action",
            "仅限私聊父 agent 使用。玩家要求现在就做现实世界动作且你决定答应时，先调用本工具把行动意图委托给宿主执行，再自然回复玩家。只口头答应不会发生动作。action=move 时必须先用 skill_view 读取 stardew-navigation 分层资料，并把已加载 POI 给出的 target(locationName,x,y,source) 原样传入 target；不要使用 destinationId 或编造坐标。",
            typeof(DelegateActionParameters));
        var skillViewTool = new SkillViewTool(_skillManager);
        var messages = new List<Message>
        {
            new()
            {
                Role = "system",
                Content =
                    "你是海莉，正在和玩家私聊。所有提示词和回答都用中文。" +
                    "如果玩家现在就请你做一件会改变游戏世界的事，而你决定答应，必须先调用 npc_delegate_action，再自然回复玩家。" +
                    "action=move 时，必须先用 skill_view 读取 stardew-navigation、references/index.md、相关 region 和最具体的 POI 文件。" +
                    "只有已加载 POI/reference 明确给出 target(locationName,x,y,source) 后，才调用 npc_delegate_action；不要使用 destinationId，不要编造坐标。" +
                    "如果只是以后才兑现的约定，用 todo；如果是现在就执行，必须委托。"
            },
            new()
            {
                Role = "user",
                Content = "海莉，我们现在去海边吧。"
            }
        };
        var tools = new[] { BuildToolDefinition(skillViewTool), BuildToolDefinition(delegateTool) };

        var response = await RunLiveToolLoopAsync(
            parentClient,
            messages,
            tools,
            (toolCall, token) => ExecuteLiveSmokeToolAsync(toolCall, skillViewTool, delegateTool, token),
            maxIterations: 5,
            new CancellationTokenSource(TimeSpan.FromSeconds(120)).Token);

        var toolCall = response.ToolCalls?.FirstOrDefault(call => call.Name == "npc_delegate_action");
        Assert.IsNotNull(toolCall, BuildFailure("真实父层模型没有调用 npc_delegate_action", response));
        using var args = JsonDocument.Parse(toolCall.Arguments);
        Assert.AreEqual("move", args.RootElement.GetProperty("action").GetString());
        var target = args.RootElement.GetProperty("target");
        Assert.AreEqual("Beach", ReadStringProperty(target, "locationName"), $"父层应传入已加载 POI target。实际参数：{toolCall.Arguments}");
        Assert.AreEqual(32, ReadIntProperty(target, "x"), $"父层应传入已加载 POI target。实际参数：{toolCall.Arguments}");
        Assert.AreEqual(34, ReadIntProperty(target, "y"), $"父层应传入已加载 POI target。实际参数：{toolCall.Arguments}");
        Assert.AreEqual("map-skill:stardew.navigation.poi.beach-shoreline", ReadStringProperty(target, "source"), $"父层应传入已加载 POI target。实际参数：{toolCall.Arguments}");
        Assert.IsFalse(args.RootElement.TryGetProperty("destinationText", out _), $"私聊 move 不应再传 destinationText。实际参数：{toolCall.Arguments}");
    }

    [TestMethod]
    public async Task PrivateChatFullRunnerLiveAi_WhenPlayerAsksHaleyToGoBeachNow_QueuesNpcDelegateAction()
    {
        var config = RequireLiveConfig();
        using var httpClient = CreateHttpClient();
        var parentClient = new OpenAiClient(config.Parent, httpClient);
        var runtimeSupervisor = new NpcRuntimeSupervisor();
        var packRoot = Path.Combine(_tempDir, "packs");
        CreatePack(packRoot, "haley", "海莉");
        var runner = new StardewNpcPrivateChatAgentRunner(
            parentClient,
            NullLoggerFactory.Instance,
            _tempDir,
            runtimeSupervisor,
            _skillManager,
            new NoopCronScheduler(),
            new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), packRoot),
            includeMemory: true,
            includeUser: true,
            discoveredToolProvider: () => Enumerable.Empty<ITool>(),
            maxToolIterations: 4,
            delegationChatClient: new OpenAiClient(config.Executor, httpClient));

        var reply = await runner.ReplyAsync(
            new NpcPrivateChatRequest("haley", "save-live-ai-smoke", "conversation-beach", "海莉，我们现在去海边吧。"),
            new CancellationTokenSource(TimeSpan.FromSeconds(120)).Token);

        Assert.IsFalse(string.IsNullOrWhiteSpace(reply.Text), "完整私聊 runner 应该给玩家一个自然回复。");
        var snapshot = runtimeSupervisor.Snapshot().Single(item => item.NpcId == "haley");
        var ingress = snapshot.Controller.IngressWorkItems.SingleOrDefault(item =>
            string.Equals(item.WorkType, "npc_delegated_action", StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(ingress, $"完整私聊 runner 使用真实 AI 时必须把即时移动意图交给 npc_delegate_action。reply={reply.Text}");
        Assert.AreEqual("move", ingress.Payload?["action"]?.GetValue<string>());
        Assert.IsTrue(
            !ingress.Payload!.TryGetPropertyValue("conversationId", out var conversationNode) ||
            string.Equals(conversationNode?.GetValue<string>(), "conversation-beach", StringComparison.Ordinal),
            $"conversationId 可以由工具从 session 推断；如果模型显式填写，必须正确。payload={ingress.Payload.ToJsonString()}");
        var target = ingress.Payload?["target"]?.AsObject();
        Assert.IsNotNull(target, $"委托参数应携带父层解析出的机械 target。payload={ingress.Payload?.ToJsonString()}");
        Assert.AreEqual("Beach", target["locationName"]?.GetValue<string>());
        Assert.AreEqual(32, target["x"]?.GetValue<int>());
        Assert.AreEqual(34, target["y"]?.GetValue<int>());
        Assert.AreEqual("map-skill:stardew.navigation.poi.beach-shoreline", target["source"]?.GetValue<string>());
        Assert.IsNull(ingress.Payload?["destinationText"]);
    }

    [TestMethod]
    public async Task LocalExecutorLiveAi_WhenMoveIntentSaysBeach_UsesSkillViewThenNavigateToBeachTile()
    {
        var config = RequireLiveConfig();
        using var httpClient = CreateHttpClient();
        var executorClient = new OpenAiClient(config.Executor, httpClient);
        var navigateTool = new CaptureTool(
            "stardew_navigate_to_tile",
            "移动到已经由 stardew-navigation skill 资料披露的具体星露谷地图 tile；真实移动由宿主和 Stardew bridge 执行。",
            typeof(NavigateToTileParameters));
        var runner = new NpcLocalExecutorRunner(
            executorClient,
            [
                new SkillViewTool(_skillManager),
                navigateTool
            ]);
        var descriptor = CreateDescriptor();
        var intent = new NpcLocalActionIntent(
            NpcLocalActionKind.Move,
            "玩家现在邀请海莉去海边",
            DestinationText: "海边");

        var result = await runner.ExecuteAsync(
            descriptor,
            intent,
            [new NpcObservationFact("haley", "stardew-valley", "save-live-ai-smoke", "default", "sdv_save-live-ai-smoke_haley_default", "private_chat", "conversation-live-ai-smoke", DateTime.UtcNow, "玩家邀请海莉现在去海边", ["intentText=海莉，我们现在去海边吧"])],
            $"trace_live_{Guid.NewGuid():N}",
            new CancellationTokenSource(TimeSpan.FromSeconds(120)).Token);

        Assert.AreEqual("completed", result.Stage, $"本地执行层没有完成导航：{result.Result}; {result.Error}; diagnostics={string.Join(" | ", result.Diagnostics)}");
        Assert.AreEqual("stardew_navigate_to_tile", result.Target);
        Assert.IsNotNull(navigateTool.LastArguments, "导航工具应该收到参数。");
        var navigateArgs = navigateTool.LastArguments.RootElement;
        Assert.AreEqual("Beach", ReadStringProperty(navigateArgs, "locationName"), $"导航参数错误：{navigateArgs.GetRawText()}");
        Assert.AreEqual(32, ReadIntProperty(navigateArgs, "x"), $"导航参数错误：{navigateArgs.GetRawText()}");
        Assert.AreEqual(34, ReadIntProperty(navigateArgs, "y"), $"导航参数错误：{navigateArgs.GetRawText()}");
        Assert.AreEqual("map-skill:stardew.navigation.poi.beach-shoreline", ReadStringProperty(navigateArgs, "source"), $"导航参数错误：{navigateArgs.GetRawText()}");
        CollectionAssert.Contains(result.Diagnostics.ToArray(), "target=skill_view stage=completed result=skill_source:stardew-navigation");
        Assert.IsTrue(
            result.Diagnostics.Any(item => item.Contains("stardew-navigation/references/poi/beach-shoreline.md", StringComparison.Ordinal)),
            $"本地执行层应读取海边 POI 文件。diagnostics={string.Join(" | ", result.Diagnostics)}");
    }

    private static LiveConfig RequireLiveConfig()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnableEnv), "1", StringComparison.Ordinal))
            Assert.Inconclusive($"真实 AI smoke 默认跳过。设置 {EnableEnv}=1 后运行。");

        var configPath = ResolveHermesConfigPath();
        var configValues = File.Exists(configPath)
            ? ReadFlatConfig(configPath)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var baseUrl = OverrideOrConfig(BaseUrlEnv, configValues, "model", "base_url");
        var parentModel = OverrideOrConfig(ParentModelEnv, configValues, "model", "default");
        var executorBaseUrl = OverrideOrConfig(ExecutorBaseUrlEnv, configValues, "delegation", "base_url");
        var executorModel = OverrideOrConfig(ExecutorModelEnv, configValues, "delegation", "model");
        if (string.IsNullOrWhiteSpace(baseUrl) ||
            string.IsNullOrWhiteSpace(parentModel))
        {
            Assert.Inconclusive($"需要在 {configPath} 配置 model.base_url 和 model.default，或设置 {BaseUrlEnv}/{ParentModelEnv}。");
        }

        var authMode = OverrideOrConfig(AuthModeEnv, configValues, "model", "auth_mode");
        var apiKey = OverrideOrConfig(ApiKeyEnv, configValues, "model", "api_key");
        var executorAuthMode = OverrideOrConfig(ExecutorAuthModeEnv, configValues, "delegation", "auth_mode");
        var executorApiKey = OverrideOrConfig(ExecutorApiKeyEnv, configValues, "delegation", "api_key");
        var parent = CreateConfig(baseUrl!, parentModel!, authMode, apiKey);
        var executor = CreateConfig(
            string.IsNullOrWhiteSpace(executorBaseUrl) ? baseUrl! : executorBaseUrl!,
            string.IsNullOrWhiteSpace(executorModel) ? parentModel! : executorModel!,
            string.IsNullOrWhiteSpace(executorAuthMode) ? authMode : executorAuthMode,
            string.IsNullOrWhiteSpace(executorApiKey) ? apiKey : executorApiKey);
        return new LiveConfig(parent, executor);
    }

    private static string ResolveHermesConfigPath()
    {
        var hermesHome = Environment.GetEnvironmentVariable("HERMES_HOME");
        if (string.IsNullOrWhiteSpace(hermesHome))
            hermesHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "hermes");
        return Path.Combine(hermesHome, "config.yaml");
    }

    private static string? OverrideOrConfig(
        string envName,
        IReadOnlyDictionary<string, string> configValues,
        string section,
        string key)
    {
        var envValue = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrWhiteSpace(envValue))
            return envValue.Trim();

        return configValues.TryGetValue($"{section}.{key}", out var configValue)
            ? configValue
            : null;
    }

    private static Dictionary<string, string> ReadFlatConfig(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? currentSection = null;
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                continue;

            if (!char.IsWhiteSpace(rawLine, 0))
            {
                currentSection = line.EndsWith(":", StringComparison.Ordinal) ? line[..^1].Trim() : null;
                continue;
            }

            if (string.IsNullOrWhiteSpace(currentSection))
                continue;

            var trimmed = line.Trim();
            var separator = trimmed.IndexOf(":", StringComparison.Ordinal);
            if (separator <= 0)
                continue;

            var key = trimmed[..separator].Trim();
            var value = trimmed[(separator + 1)..].Trim().Trim('"', '\'');
            values[$"{currentSection}.{key}"] = value;
        }

        return values;
    }

    private static LlmConfig CreateConfig(string baseUrl, string model, string? authMode, string? apiKey)
        => new()
        {
            Provider = "openai-compatible-live-smoke",
            Model = model,
            BaseUrl = baseUrl.TrimEnd('/'),
            AuthMode = string.IsNullOrWhiteSpace(authMode) ? "none" : authMode,
            ApiKey = apiKey,
            Temperature = 0,
            MaxTokens = 1024
        };

    private static HttpClient CreateHttpClient()
        => new() { Timeout = TimeSpan.FromSeconds(120) };

    private static ToolDefinition BuildToolDefinition(ITool tool)
    {
        var schema = tool is IToolSchemaProvider schemaProvider
            ? schemaProvider.GetParameterSchema()
            : JsonSerializer.SerializeToElement(new { type = "object", properties = new { } });
        return new ToolDefinition
        {
            Name = tool.Name,
            Description = tool.Description,
            Parameters = schema
        };
    }

    private static async Task<ChatResponse> RunLiveToolLoopAsync(
        IChatClient client,
        List<Message> messages,
        IReadOnlyList<ToolDefinition> tools,
        Func<ToolCall, CancellationToken, Task<ToolResult>> executeTool,
        int maxIterations,
        CancellationToken ct)
    {
        ChatResponse response = new();
        for (var i = 0; i < maxIterations; i++)
        {
            response = await client.CompleteWithToolsAsync(messages, tools, ct);
            if (!response.HasToolCalls)
                return response;

            messages.Add(new Message
            {
                Role = "assistant",
                Content = response.Content ?? "",
                ToolCalls = response.ToolCalls,
                Reasoning = response.Reasoning,
                ReasoningContent = response.ReasoningContent,
                ReasoningDetails = response.ReasoningDetails,
                CodexReasoningItems = response.CodexReasoningItems
            });

            foreach (var toolCall in response.ToolCalls!)
            {
                var result = await executeTool(toolCall, ct);
                messages.Add(new Message
                {
                    Role = "tool",
                    Content = result.Content,
                    ToolCallId = toolCall.Id,
                    ToolName = toolCall.Name
                });
            }

            if (response.ToolCalls.Any(call => string.Equals(call.Name, "npc_delegate_action", StringComparison.OrdinalIgnoreCase)))
                return response;
        }

        return response;
    }

    private static async Task<ToolResult> ExecuteLiveSmokeToolAsync(
        ToolCall toolCall,
        SkillViewTool skillViewTool,
        CaptureTool delegateTool,
        CancellationToken ct)
    {
        if (string.Equals(toolCall.Name, "skill_view", StringComparison.OrdinalIgnoreCase))
        {
            var parameters = JsonSerializer.Deserialize<SkillViewParameters>(toolCall.Arguments, ToolArgumentJsonOptions)
                ?? new SkillViewParameters { Name = "stardew-navigation" };
            return await skillViewTool.ExecuteAsync(parameters, ct);
        }

        if (string.Equals(toolCall.Name, "npc_delegate_action", StringComparison.OrdinalIgnoreCase))
        {
            var parameters = JsonSerializer.Deserialize<DelegateActionParameters>(toolCall.Arguments, ToolArgumentJsonOptions)
                ?? new DelegateActionParameters();
            return await delegateTool.ExecuteAsync(parameters, ct);
        }

        return ToolResult.Fail($"unexpected tool: {toolCall.Name}");
    }

    private static string BuildFailure(string prefix, ChatResponse response)
        => $"{prefix}。finish={response.FinishReason}; content={response.Content}; toolCalls={string.Join(",", response.ToolCalls?.Select(call => call.Name) ?? [])}";

    private static bool ContainsAny(string? value, params string[] needles)
        => !string.IsNullOrWhiteSpace(value) &&
           needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));

    private static string? ReadStringProperty(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            return value.GetString();

        var pascalName = char.ToUpperInvariant(name[0]) + name[1..];
        return element.TryGetProperty(pascalName, out value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? ReadIntProperty(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var value) && value.TryGetInt32(out var intValue))
            return intValue;

        var pascalName = char.ToUpperInvariant(name[0]) + name[1..];
        return element.TryGetProperty(pascalName, out value) && value.TryGetInt32(out intValue)
            ? intValue
            : null;
    }

    private static NpcRuntimeDescriptor CreateDescriptor()
        => new(
            NpcId: "haley",
            DisplayName: "Haley",
            GameId: "stardew-valley",
            SaveId: "save-live-ai-smoke",
            ProfileId: "default",
            AdapterId: "stardew",
            PackRoot: Path.Combine(Path.GetTempPath(), "hermes-stardew-live-ai-smoke-pack"),
            SessionId: "sdv_save-live-ai-smoke_haley_default",
            BodyBinding: new NpcBodyBinding("haley", "Haley", "Haley", "Haley", "stardew"));

    private static string FindRepositoryPath(params string[] segments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (Directory.Exists(candidate) || File.Exists(candidate))
                return candidate;
        }

        throw new DirectoryNotFoundException($"Could not locate repository path: {Path.Combine(segments)}");
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void CreatePack(string packRoot, string npcId, string displayName)
    {
        var root = Path.Combine(packRoot, npcId, "default");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "SOUL.md"), $"# {displayName}\n\n你是星露谷里的{displayName}。");
        File.WriteAllText(Path.Combine(root, "facts.md"), $"{displayName} lives in Stardew Valley.");
        File.WriteAllText(Path.Combine(root, "voice.md"), $"{displayName} speaks naturally in Chinese.");
        File.WriteAllText(Path.Combine(root, "boundaries.md"), "不要扮演系统或工具。");
        File.WriteAllText(Path.Combine(root, "skills.json"), """{"required":[],"optional":[]}""");

        var manifest = new NpcPackManifest
        {
            SchemaVersion = 1,
            NpcId = npcId,
            GameId = "stardew-valley",
            ProfileId = "default",
            DefaultProfileId = "default",
            DisplayName = displayName,
            SmapiName = "Haley",
            Aliases = [npcId, displayName, "Haley"],
            TargetEntityId = "Haley",
            AdapterId = "stardew",
            SoulFile = "SOUL.md",
            FactsFile = "facts.md",
            VoiceFile = "voice.md",
            BoundariesFile = "boundaries.md",
            SkillsFile = "skills.json",
            Capabilities = ["move", "speak"]
        };
        File.WriteAllText(
            Path.Combine(root, FileSystemNpcPackLoader.ManifestFileName),
            JsonSerializer.Serialize(manifest));
    }

    private sealed record LiveConfig(LlmConfig Parent, LlmConfig Executor);

    private sealed class NoopCronScheduler : ICronScheduler
    {
        public event EventHandler<CronTaskDueEventArgs>? TaskDue;

        public void Schedule(CronTask task) => TaskDue?.Invoke(this, new CronTaskDueEventArgs { Task = task, FiredAt = DateTimeOffset.UtcNow });

        public void Cancel(string taskId)
        {
        }

        public CronTask? GetTask(string taskId) => null;

        public IReadOnlyList<CronTask> GetAllTasks() => Array.Empty<CronTask>();

        public DateTimeOffset? GetNextRun(string taskId) => null;
    }

    private sealed class CaptureTool(string name, string description, Type parametersType) : ITool, IToolSchemaProvider
    {
        public JsonDocument? LastArguments { get; private set; }

        public string Name { get; } = name;

        public string Description { get; } = description;

        public Type ParametersType { get; } = parametersType;

        public JsonElement GetParameterSchema()
        {
            if (ParametersType == typeof(DelegateActionParameters))
            {
                return JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new
                    {
                        action = new { type = "string", description = "行动类型，移动时填 move。" },
                        reason = new { type = "string", description = "你为什么接受这个立即行动的简短原因。" },
                        target = new
                        {
                            type = "object",
                            description = "移动时必填。来自已加载 stardew-navigation POI/reference 的机械 target；不要编造。",
                            properties = new
                            {
                                locationName = new { type = "string", description = "已加载 POI/reference 给出的地图名。" },
                                x = new { type = "integer", description = "已加载 POI/reference 给出的 tile X。" },
                                y = new { type = "integer", description = "已加载 POI/reference 给出的 tile Y。" },
                                source = new { type = "string", description = "披露该坐标的已加载 skill reference。" }
                            },
                            required = new[] { "locationName", "x", "y", "source" }
                        },
                        intentText = new { type = "string", description = "可选兼容字段；action=move 的执行目标以 target 为准。" },
                        conversationId = new { type = "string", description = "可选。私聊 conversation id。" }
                    },
                    required = new[] { "action", "reason", "target" }
                });
            }

            return StardewNpcToolSchemas.NavigateToTile();
        }

        public Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
        {
            LastArguments?.Dispose();
            LastArguments = JsonDocument.Parse(JsonSerializer.Serialize(parameters));
            return Task.FromResult(ToolResult.Ok("""{"accepted":true,"commandId":"cmd-live-smoke","status":"completed","traceId":"trace-live-smoke"}"""));
        }
    }

    private sealed class DelegateActionParameters
    {
        public string Action { get; init; } = "";

        public string Reason { get; init; } = "";

        public DelegateMoveTargetParameters? Target { get; init; }

        public string? IntentText { get; init; }

        public string? ConversationId { get; init; }
    }

    private sealed class DelegateMoveTargetParameters
    {
        public string LocationName { get; init; } = "";

        public int X { get; init; }

        public int Y { get; init; }

        public string Source { get; init; } = "";
    }

    private sealed class NavigateToTileParameters
    {
        public required string LocationName { get; init; }

        public int X { get; init; }

        public int Y { get; init; }

        public string? Source { get; init; }

        public int? FacingDirection { get; init; }

        public string? Reason { get; init; }

        public string? Thought { get; init; }
    }
}
