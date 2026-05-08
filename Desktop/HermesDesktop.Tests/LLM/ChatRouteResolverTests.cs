using Hermes.Agent.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.LLM;

[TestClass]
public class ChatRouteResolverTests
{
    [TestMethod]
    public void Resolve_RootModelLane_ReturnsRootConfig()
    {
        var root = CreateRootConfig();
        var resolver = new ChatRouteResolver(root, (_, _) => null);

        var route = resolver.Resolve(ChatRouteNames.Model);

        Assert.AreEqual("openai", route.Config.Provider);
        Assert.AreEqual("gpt-root", route.Config.Model);
        Assert.AreEqual("https://root.example/v1", route.Config.BaseUrl);
        Assert.AreEqual("model.provider", route.Sources.Provider);
        Assert.AreEqual("model.default", route.Sources.Model);
        Assert.AreEqual("model.base_url", route.Sources.BaseUrl);
    }

    [TestMethod]
    public void Resolve_LaneModelOverride_InheritsBaseUrlAndAuth()
    {
        var root = CreateRootConfig();
        var values = new Dictionary<(string Section, string Key), string>(StringTupleComparer.OrdinalIgnoreCase)
        {
            [("stardew_autonomy", "model")] = "qwen-local"
        };
        var resolver = new ChatRouteResolver(root, Read(values));

        var route = resolver.Resolve(ChatRouteNames.StardewAutonomy);

        Assert.AreEqual("openai", route.Config.Provider);
        Assert.AreEqual("qwen-local", route.Config.Model);
        Assert.AreEqual("https://root.example/v1", route.Config.BaseUrl);
        Assert.AreEqual("root-secret", route.Config.ApiKey);
        Assert.AreEqual("model.provider", route.Sources.Provider);
        Assert.AreEqual("stardew_autonomy.model", route.Sources.Model);
        Assert.AreEqual("model.base_url", route.Sources.BaseUrl);
    }

    [TestMethod]
    public void Resolve_AutonomyResponseFormatOverride_UsesLaneResponseFormat()
    {
        var root = CreateRootConfig();
        var values = new Dictionary<(string Section, string Key), string>(StringTupleComparer.OrdinalIgnoreCase)
        {
            [("stardew_autonomy", "response_format")] = "json_object"
        };
        var resolver = new ChatRouteResolver(root, Read(values));

        var route = resolver.Resolve(ChatRouteNames.StardewAutonomy);

        Assert.AreEqual("json_object", route.Config.ResponseFormat);
        Assert.AreEqual("stardew_autonomy.response_format", route.Sources.ResponseFormat);
    }

    [TestMethod]
    public void Resolve_DelegationLane_UsesLaneSpecificProviderModelAndBaseUrl()
    {
        var root = CreateRootConfig();
        var values = new Dictionary<(string Section, string Key), string>(StringTupleComparer.OrdinalIgnoreCase)
        {
            [("delegation", "provider")] = "openai",
            [("delegation", "base_url")] = "http://127.0.0.1:1234/v1",
            [("delegation", "model")] = "qwen3-4b",
            [("delegation", "api_key")] = "lm-studio"
        };
        var resolver = new ChatRouteResolver(root, Read(values));

        var route = resolver.Resolve(ChatRouteNames.Delegation);

        Assert.AreEqual("openai", route.Config.Provider);
        Assert.AreEqual("qwen3-4b", route.Config.Model);
        Assert.AreEqual("http://127.0.0.1:1234/v1", route.Config.BaseUrl);
        Assert.AreEqual("lm-studio", route.Config.ApiKey);
        Assert.AreEqual("delegation.provider", route.Sources.Provider);
        Assert.AreEqual("delegation.model", route.Sources.Model);
        Assert.AreEqual("delegation.base_url", route.Sources.BaseUrl);
        Assert.AreEqual("delegation.api_key", route.Sources.ApiKey);
    }

    [TestMethod]
    public void ChatLaneClientProvider_LogsMaxSpawnDepthAsReservedFlatOnlySetting()
    {
        var root = CreateRootConfig();
        var values = new Dictionary<(string Section, string Key), string>(StringTupleComparer.OrdinalIgnoreCase)
        {
            [("delegation", "provider")] = "openai",
            [("delegation", "base_url")] = "http://127.0.0.1:1234/v1",
            [("delegation", "model")] = "qwen3-4b",
            [("delegation", "max_spawn_depth")] = "3",
            [("delegation", "max_concurrent_children")] = "2"
        };
        var logger = new CapturingLogger<ChatLaneClientProvider>();
        var provider = new ChatLaneClientProvider(
            new ChatClientFactory(
                root,
                new HttpClient(),
                NullLogger<ChatClientFactory>.Instance),
            new ChatRouteResolver(root, Read(values)),
            logger,
            Read(values));

        _ = provider;

        var logText = string.Join(Environment.NewLine, logger.Messages);
        StringAssert.Contains(logText, "flat-only in v1");
        StringAssert.Contains(logText, "max_spawn_depth=3");
        StringAssert.Contains(logText, "reserved_not_enforced");
        Assert.IsFalse(logText.Contains("effectiveMaxSpawnDepth", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Resolve_LaneBaseUrlOverrideWithoutProvider_UsesOpenAiCompatibleProvider()
    {
        var root = new LlmConfig
        {
            Provider = "anthropic",
            Model = "claude-root",
            BaseUrl = null,
            ApiKey = "root-secret",
            AuthMode = "api_key"
        };
        var values = new Dictionary<(string Section, string Key), string>(StringTupleComparer.OrdinalIgnoreCase)
        {
            [("delegation", "base_url")] = "http://127.0.0.1:1234/v1",
            [("delegation", "model")] = "qwen3-4b"
        };
        var resolver = new ChatRouteResolver(root, Read(values));

        var route = resolver.Resolve(ChatRouteNames.Delegation);
        var factory = new ChatClientFactory(
            root,
            new HttpClient(),
            NullLogger<ChatClientFactory>.Instance);
        var client = factory.CreateClientForConfig(route.Config);

        Assert.AreEqual("openai", route.Config.Provider);
        Assert.AreEqual("delegation.base_url:openai-compatible", route.Sources.Provider);
        Assert.AreEqual("http://127.0.0.1:1234/v1", route.Config.BaseUrl);
        Assert.IsInstanceOfType<OpenAiClient>(client);
    }

    [TestMethod]
    public void Resolve_MissingLane_FallsBackToRoot()
    {
        var root = CreateRootConfig();
        var resolver = new ChatRouteResolver(root, (_, _) => null);

        var route = resolver.Resolve(ChatRouteNames.StardewPrivateChat);

        Assert.AreEqual(root.Provider, route.Config.Provider);
        Assert.AreEqual(root.Model, route.Config.Model);
        Assert.AreEqual(root.BaseUrl, route.Config.BaseUrl);
        Assert.AreEqual("model.provider", route.Sources.Provider);
        Assert.AreEqual("model.default", route.Sources.Model);
    }

    [TestMethod]
    public void SyncStardewNpcConfigScript_LabelsMaxSpawnDepthAsReservedFlatOnlySetting()
    {
        var script = ReadRepositoryFile("scripts", "sync-stardew-npc-config.ps1");

        StringAssert.Contains(script, "max_spawn_depth is emitted only as a reserved flat-only v1 marker");
        StringAssert.Contains(script, "max_spawn_depth is reserved for future nested delegation; flat-only v1 ignores it.");
        StringAssert.Contains(script, "max_concurrent_children is reserved for future fan-out policy; flat-only v1 ignores it.");
        StringAssert.Contains(script, "Delegation depth: max_spawn_depth is reserved and ignored by flat-only v1.");
        Assert.IsFalse(script.Contains("effectiveMaxSpawnDepth", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Resolve_EmptyLaneValues_AreIgnored()
    {
        var root = CreateRootConfig();
        var values = new Dictionary<(string Section, string Key), string>(StringTupleComparer.OrdinalIgnoreCase)
        {
            [("delegation", "provider")] = "  ",
            [("delegation", "model")] = "",
            [("delegation", "base_url")] = "  "
        };
        var resolver = new ChatRouteResolver(root, Read(values));

        var route = resolver.Resolve(ChatRouteNames.Delegation);

        Assert.AreEqual(root.Provider, route.Config.Provider);
        Assert.AreEqual(root.Model, route.Config.Model);
        Assert.AreEqual(root.BaseUrl, route.Config.BaseUrl);
    }

    [TestMethod]
    public void GetClient_WithSecretLaneConfig_LogsSourcesButNotSecretValues()
    {
        var root = new LlmConfig
        {
            Provider = "openai",
            Model = "gpt-root",
            BaseUrl = "https://root.example/v1",
            ApiKey = "root-secret-value",
            AuthMode = "api_key",
            AuthHeader = "Authorization",
            AuthScheme = "Bearer",
            ApiKeyEnv = "ROOT_KEY",
            AuthTokenEnv = "ROOT_TOKEN",
            AuthTokenCommand = "root-token-command --secret"
        };
        var values = new Dictionary<(string Section, string Key), string>(StringTupleComparer.OrdinalIgnoreCase)
        {
            [("delegation", "provider")] = "openai",
            [("delegation", "model")] = "local-model",
            [("delegation", "base_url")] = "http://127.0.0.1:1234/v1",
            [("delegation", "api_key")] = "lane-secret-value",
            [("delegation", "auth_token_command")] = "lane-token-command --secret"
        };
        var logger = new CapturingLogger<ChatLaneClientProvider>();
        var factory = new ChatClientFactory(
            root,
            new HttpClient(),
            NullLogger<ChatClientFactory>.Instance);
        var provider = new ChatLaneClientProvider(
            factory,
            new ChatRouteResolver(root, Read(values)),
            logger,
            Read(values));

        provider.GetClient(ChatRouteNames.Delegation);

        var logText = string.Join(Environment.NewLine, logger.Messages);
        StringAssert.Contains(logText, "lane=delegation");
        StringAssert.Contains(logText, "provider=openai");
        StringAssert.Contains(logText, "model=local-model");
        StringAssert.Contains(logText, "apiKeySource=delegation.api_key");
        Assert.IsFalse(logText.Contains("lane-secret-value", StringComparison.Ordinal));
        Assert.IsFalse(logText.Contains("root-secret-value", StringComparison.Ordinal));
        Assert.IsFalse(logText.Contains("lane-token-command", StringComparison.Ordinal));
        Assert.IsFalse(logText.Contains("root-token-command", StringComparison.Ordinal));
    }

    private static LlmConfig CreateRootConfig()
        => new()
        {
            Provider = "openai",
            Model = "gpt-root",
            BaseUrl = "https://root.example/v1",
            ApiKey = "root-secret",
            AuthMode = "api_key",
            AuthHeader = "Authorization",
            AuthScheme = "Bearer",
            ApiKeyEnv = "ROOT_KEY",
            AuthTokenEnv = "ROOT_TOKEN",
            AuthTokenCommand = "root-token-command"
        };

    private static Func<string, string, string?> Read(Dictionary<(string Section, string Key), string> values)
        => (section, key) => values.TryGetValue((section, key), out var value) ? value : null;

    private static string ReadRepositoryFile(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. relativePath]);
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate repository file: {string.Join(Path.DirectorySeparatorChar, relativePath)}");
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string Section, string Key)>
    {
        public static StringTupleComparer OrdinalIgnoreCase { get; } = new();

        public bool Equals((string Section, string Key) x, (string Section, string Key) y)
            => StringComparer.OrdinalIgnoreCase.Equals(x.Section, y.Section) &&
               StringComparer.OrdinalIgnoreCase.Equals(x.Key, y.Key);

        public int GetHashCode((string Section, string Key) obj)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Section),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Key));
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
