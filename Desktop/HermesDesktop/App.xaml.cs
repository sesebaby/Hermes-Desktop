using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Hermes.Agent.Core;
using Hermes.Agent.Diagnostics;
using Hermes.Agent.LLM;
using Hermes.Agent.Transcript;
using Hermes.Agent.Memory;
using Hermes.Agent.Skills;
using Hermes.Agent.Permissions;
using Hermes.Agent.Tasks;
using Hermes.Agent.Buddy;
using Hermes.Agent.Context;
using Hermes.Agent.Search;
using Hermes.Agent.Agents;
using Hermes.Agent.Coordinator;
using Hermes.Agent.Mcp;
using Hermes.Agent.Analytics;
using Hermes.Agent.Plugins;
using Hermes.Agent.Soul;
using Hermes.Agent.Tools;
using Hermes.Agent.Dreamer;
using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Hermes.Agent.Runtime;
using HermesDesktop.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;

namespace HermesDesktop;

public partial class App : Application
{
    private Window? _window;
    private static readonly object _dreamerCtsLock = new();
    private static readonly object _dreamerHttpClientsLock = new();
    private static System.Threading.CancellationTokenSource? _dreamerCts;
    private static DreamerHttpClients? _dreamerHttpClients;

    /// <summary>Global service provider for DI — accessed by pages via App.Services.</summary>
    /// <remarks>
    /// Starts as <see cref="UninitializedAppServiceProvider"/> so <see cref="TryGetAppLogger"/> can call
    /// <see cref="IServiceProvider.GetService"/> without throwing before <see cref="OnLaunched"/> builds the real provider.
    /// <see cref="Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService"/> throws if invoked before services are registered.
    /// </remarks>
    public static IServiceProvider Services { get; private set; } = UninitializedAppServiceProvider.Instance;

    /// <summary>
    /// Initializes application components and wires up application-level unhandled exception handling.
    /// </summary>
    /// <remarks>
    /// Registers OnAppUnhandledException to run when the app encounters an unhandled exception (used to cancel background workers such as the Dreamer loop).
    /// </remarks>
    public App()
    {
        InitializeComponent();
        this.UnhandledException += OnAppUnhandledException;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    /// <summary>
    /// Cancels the Dreamer background loop when the application encounters an unhandled exception.
    /// </summary>
    private void OnAppUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        var logger = TryGetAppLogger();
        var exception = e.Exception;

        try
        {
            if (logger is not null)
                logger.LogError(exception, "Unhandled UI exception");
            else
                BestEffort.LogFailure(null, exception, "handling unhandled UI exception");
        }
        catch (Exception ex)
        {
            BestEffort.LogFailure(null, ex, "logging unhandled UI exception");
            BestEffort.LogFailure(null, exception, "handling unhandled UI exception");
        }

        TryCancelDreamerCts(logger, "app unhandled exception");

        if (exception is OperationCanceledException or ObjectDisposedException)
            e.Handled = true;
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        var logger = TryGetAppLogger();
        TryCancelAndDisposeDreamerCts(logger, "process exit");
        TryStopStardewNpcAutonomyBackground(logger, "process exit");
        TryDisposeDreamerHttpClients(logger, "process exit");
    }

    private static ILogger<App>? TryGetAppLogger()
    {
        if (ReferenceEquals(Services, UninitializedAppServiceProvider.Instance))
            return null;

        try
        {
            return Services.GetService<ILogger<App>>();
        }
        catch (Exception ex)
        {
            BestEffort.LogFailure(null, ex, "resolving app logger");
            return null;
        }
    }

    private static void TryCancelDreamerCts(ILogger? logger, string reason)
    {
        System.Threading.CancellationTokenSource? dreamerCts;
        lock (_dreamerCtsLock)
        {
            dreamerCts = _dreamerCts;
        }

        try
        {
            dreamerCts?.Cancel();
        }
        catch (Exception ex)
        {
            BestEffort.LogFailure(logger, ex, "cancelling Dreamer cancellation token source", $"reason={reason}");
        }
    }

    private static void TryCancelAndDisposeDreamerCts(ILogger? logger, string reason)
    {
        System.Threading.CancellationTokenSource? dreamerCts;
        lock (_dreamerCtsLock)
        {
            dreamerCts = _dreamerCts;
            _dreamerCts = null;
        }

        TryCancelAndDisposeDreamerCts(dreamerCts, logger, reason);
    }

    private static void TryCancelAndDisposeDreamerCts(System.Threading.CancellationTokenSource? dreamerCts, ILogger? logger, string reason)
    {
        if (dreamerCts is null)
            return;

        try
        {
            dreamerCts.Cancel();
            dreamerCts.Dispose();
        }
        catch (Exception ex)
        {
            BestEffort.LogFailure(logger, ex, "cancelling and disposing Dreamer cancellation token source", $"reason={reason}");

            try
            {
                dreamerCts.Dispose();
            }
            catch (Exception disposeEx)
            {
                BestEffort.LogFailure(logger, disposeEx, "disposing Dreamer cancellation token source", $"reason={reason}");
            }
        }
    }

    private static void SetDreamerCts(System.Threading.CancellationTokenSource dreamerCts, ILogger? logger, string reason)
    {
        System.Threading.CancellationTokenSource? previousDreamerCts;
        lock (_dreamerCtsLock)
        {
            previousDreamerCts = _dreamerCts;
            _dreamerCts = dreamerCts;
        }

        TryCancelAndDisposeDreamerCts(previousDreamerCts, logger, reason);
    }

    private static void TryCancelAndDisposeDreamerCtsIfCurrent(System.Threading.CancellationTokenSource dreamerCts, ILogger? logger, string reason)
    {
        var shouldDispose = false;
        lock (_dreamerCtsLock)
        {
            if (ReferenceEquals(_dreamerCts, dreamerCts))
            {
                _dreamerCts = null;
                shouldDispose = true;
            }
        }

        if (shouldDispose)
            TryCancelAndDisposeDreamerCts(dreamerCts, logger, reason);
    }

    private static DreamerHttpClients GetOrCreateDreamerHttpClients()
    {
        lock (_dreamerHttpClientsLock)
        {
            _dreamerHttpClients ??= new DreamerHttpClients(
                walk: DreamerHttpClientFactory.Create(TimeSpan.FromMinutes(4)),
                echo: DreamerHttpClientFactory.Create(TimeSpan.FromMinutes(3)),
                rss: DreamerHttpClientFactory.Create(TimeSpan.FromMinutes(2)));

            return _dreamerHttpClients;
        }
    }

    private static void TryDisposeDreamerHttpClients(ILogger? logger, string reason)
    {
        DreamerHttpClients? dreamerHttpClients;
        lock (_dreamerHttpClientsLock)
        {
            dreamerHttpClients = _dreamerHttpClients;
            _dreamerHttpClients = null;
        }

        if (dreamerHttpClients is null)
            return;

        try
        {
            dreamerHttpClients.Dispose();
        }
        catch (Exception ex)
        {
            BestEffort.LogFailure(logger, ex, "disposing Dreamer HTTP clients", $"reason={reason}");
        }
    }

    /// <summary>
    /// Initializes dependency injection, creates the main window, and activates the application UI.
    /// </summary>
    /// <param name="args">Activation arguments provided by the system when the application is launched.</param>
    /// <exception cref="Exception">On failure during startup the exception is reported via startup diagnostics and rethrown.</exception>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            Services = ConfigureServices();
            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception ex)
        {
            StartupDiagnostics.ReportFatalStartupException(ex);
            throw;
        }
    }

    /// <summary>
    /// Configure dependency injection, register all application services, and perform post-build initialization.
    /// </summary>
    /// <remarks>
    /// Ensures the Hermes home and project directories exist and creates default SOUL.md and USER.md if missing (non-fatal on failure).
    /// Registers core services such as logging, chat clients, transcript and memory stores, skill and task managers, wiki, soul services, agent and orchestration services, tools and plugins, analytics, and Dreamer status.
    /// After building the provider it registers tools, initializes MCP (fire-and-forget), wires the UI permission callback, and starts Dreamer background components as appropriate.
    /// </remarks>
    /// <returns>The built <see cref="ServiceProvider"/> containing the registered application services.</returns>
    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Logging — file + debug sinks so logs are visible outside Visual Studio
        var logsDir = Path.Combine(HermesEnvironment.HermesHomePath, "hermes-cs", "logs");
        Directory.CreateDirectory(logsDir);
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
            // Suppress only per-request HttpClient Information chatter. If richer HTTP diagnostics
            // are enabled later, those categories can include sensitive request metadata; keeping the
            // filter scoped preserves Warning/Error transport failures and any separate app-level
            // connectivity telemetry from HermesChatService/RuntimeStatusService health checks.
            builder.AddFilter((category, level) =>
                category is null ||
                !category.StartsWith("System.Net.Http.HttpClient", StringComparison.Ordinal) ||
                level >= LogLevel.Warning);
            builder.AddProvider(new FileLoggerProvider(Path.Combine(logsDir, "hermes.log")));
        });

        // LLM config from environment/config.yaml
        var llmConfig = HermesEnvironment.CreateLlmConfig();
        services.AddSingleton(llmConfig);
        services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(5) });

        // Optional credential pool for multi-key rotation
        var credentialPool = HermesEnvironment.LoadCredentialPool();
        if (credentialPool is not null)
            services.AddSingleton(credentialPool);

        // Chat client factory — enables runtime model/provider swapping
        // Pattern from Claude Code: model read from state at call time, fresh client on swap
        services.AddSingleton(sp => new ChatClientFactory(
            sp.GetRequiredService<LlmConfig>(),
            new HttpClient { Timeout = TimeSpan.FromMinutes(5) },
            sp.GetRequiredService<ILogger<ChatClientFactory>>(),
            sp.GetService<CredentialPool>()));

        // Swappable proxy — all existing IChatClient consumers automatically route
        // through the factory's current client. No code changes needed anywhere else.
        services.AddSingleton<IChatClient>(sp =>
            new SwappableChatClient(sp.GetRequiredService<ChatClientFactory>()));

        // Hermes home directory — ensure all required dirs exist on startup
        var hermesHome = HermesEnvironment.HermesHomePath;
        var projectDir = Path.Combine(hermesHome, "hermes-cs");
        var memoryEnabled = IsConfigEnabled("memory", "memory_enabled");
        var userProfileEnabled = IsConfigEnabled("memory", "user_profile_enabled");
        var memoryAvailable = memoryEnabled || userProfileEnabled;
        foreach (var dir in new[]
        {
            hermesHome, projectDir,
            Path.Combine(hermesHome, "soul"),              // mistakes.jsonl, habits.jsonl
            Path.Combine(hermesHome, "memories"),          // Python-compatible curated MEMORY.md / USER.md
            Path.Combine(hermesHome, "dreamer"),            // Dreamer room (walks, projects, inbox)
            Path.Combine(projectDir, "transcripts"),
            Path.Combine(projectDir, "memory"),
            Path.Combine(projectDir, "skills"),
            Path.Combine(projectDir, "tasks"),
            Path.Combine(projectDir, "buddy"),
            Path.Combine(projectDir, "agents"),
            Path.Combine(projectDir, "analytics"),
        })
        {
            Directory.CreateDirectory(dir);
        }
        // Ensure SOUL.md and USER.md exist with defaults (non-fatal if write fails)
        try
        {
            var soulPath = Path.Combine(hermesHome, "SOUL.md");
            var userPath = Path.Combine(hermesHome, "USER.md");
            if (!File.Exists(soulPath))
                File.WriteAllText(soulPath, "# Agent Soul\n\nYou are a helpful AI assistant.\n");
            if (!File.Exists(userPath))
                File.WriteAllText(userPath, "# User Profile\n\nNo profile configured yet. Tell me about yourself.\n");
        }
        catch (Exception ex)
        {
            BestEffort.LogFailure(TryGetAppLogger(), ex, "creating default soul and user files");
        }

        // Transcript store
        var transcriptsDir = Path.Combine(projectDir, "transcripts");
        var sessionStateDbPath = Path.Combine(projectDir, "state.db");
        services.AddSingleton(sp => new SessionSearchIndex(
            sessionStateDbPath,
            sp.GetRequiredService<ILogger<SessionSearchIndex>>()));
        services.AddSingleton<SessionTodoStore>();
        services.AddSingleton<SessionTaskProjectionService>();
        services.AddSingleton(sp => new TranscriptStore(
            transcriptsDir,
            eagerFlush: true,
            messageObserver: sp.GetRequiredService<SessionTaskProjectionService>(),
            sessionStore: sp.GetRequiredService<SessionSearchIndex>()));

        var skillsDir = Path.Combine(projectDir, "skills");

        services.AddSingleton<INpcPackLoader, FileSystemNpcPackLoader>();
        services.AddSingleton<NpcRuntimeSupervisor>();
        services.AddSingleton<ResourceClaimRegistry>();
        services.AddSingleton<WorldCoordinationService>();
        services.AddSingleton<NpcRuntimeTraceIndex>();
        services.AddSingleton(_ => new NpcAutonomyBudget(new NpcAutonomyBudgetOptions(
            MaxToolIterations: ReadPositiveConfigInt("stardew", "npc_autonomy_max_tool_iterations", 6),
            MaxConcurrentLlmRequests: ReadPositiveConfigInt("stardew", "npc_autonomy_max_concurrent_llm_requests", 2),
            RestartCooldown: TimeSpan.FromSeconds(ReadPositiveConfigInt("stardew", "npc_autonomy_restart_cooldown_seconds", 5)),
            MaxRestartsPerScene: ReadPositiveConfigInt("stardew", "npc_autonomy_max_restarts_per_scene", 3),
            LlmTurnTimeout: TimeSpan.FromSeconds(ReadPositiveConfigInt("stardew", "npc_autonomy_llm_turn_timeout_seconds", 60)))));
        services.AddSingleton(sp => new StardewNpcPackSourceLocator(
            sp.GetRequiredService<INpcPackLoader>(),
            new StardewNpcPackSourceLocatorOptions(
                BaseDirectory: AppContext.BaseDirectory,
                CurrentDirectory: Environment.CurrentDirectory,
                WorkspaceDirectory: Environment.GetEnvironmentVariable("HERMES_DESKTOP_WORKSPACE"),
                MaxParentDepth: 8)));
        services.AddSingleton<IStardewNpcPackRootProvider>(sp => sp.GetRequiredService<StardewNpcPackSourceLocator>());
        services.AddSingleton(sp => new NpcRuntimeHost(
            sp.GetRequiredService<INpcPackLoader>(),
            sp.GetRequiredService<NpcRuntimeSupervisor>(),
            projectDir));
        services.AddSingleton<NpcRuntimeWorkspaceService>();
        services.AddSingleton(sp => new StardewNpcRuntimeBindingResolver(
            sp.GetRequiredService<INpcPackLoader>(),
            sp.GetRequiredService<IStardewNpcPackRootProvider>()));
        services.AddSingleton<IStardewGamingSkillRootProvider>(_ => new CompositeStardewGamingSkillRootProvider(
            Path.Combine(skillsDir, "gaming"),
            Path.Combine(AppContext.BaseDirectory, "skills", "gaming"),
            FindRepoSkillsDir() is { } repoSkillsDir ? Path.Combine(repoSkillsDir, "gaming") : null));
        services.AddSingleton<StardewNpcAutonomyPromptSupplementBuilder>();
        services.AddSingleton<INpcToolSurfaceSnapshotProvider>(sp => new NpcToolSurfaceSnapshotProvider(
            () => sp.GetRequiredService<McpManager>().Tools.Values));
        services.AddSingleton<IPrivateChatSessionLeaseCoordinator>(sp => new StardewNpcPrivateChatSessionLeaseCoordinator(
            projectDir,
            sp.GetRequiredService<NpcRuntimeSupervisor>(),
            sp.GetRequiredService<StardewNpcRuntimeBindingResolver>()));
        services.AddSingleton<IStardewBridgeDiscovery>(_ => new FileStardewBridgeDiscovery());
        services.AddSingleton(sp => new StardewNpcDebugActionService(
            sp.GetRequiredService<IStardewBridgeDiscovery>(),
            sp.GetRequiredService<HttpClient>()));
        services.AddSingleton(sp => new StardewAutonomyTickDebugService(
            sp.GetRequiredService<IStardewBridgeDiscovery>(),
            sp.GetRequiredService<HttpClient>(),
            sp.GetRequiredService<IChatClient>(),
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<SkillManager>(),
            sp.GetRequiredService<ICronScheduler>(),
            sp.GetRequiredService<NpcRuntimeSupervisor>(),
            sp.GetRequiredService<StardewNpcRuntimeBindingResolver>(),
            sp.GetRequiredService<StardewNpcAutonomyPromptSupplementBuilder>(),
            sp.GetRequiredService<INpcToolSurfaceSnapshotProvider>(),
            sp.GetRequiredService<WorldCoordinationService>(),
            memoryEnabled,
            userProfileEnabled,
            HermesEnvironment.MaxAgentIterations,
            projectDir));
        services.AddSingleton<INpcPrivateChatAgentRunner>(sp => new StardewNpcPrivateChatAgentRunner(
            sp.GetRequiredService<IChatClient>(),
            sp.GetRequiredService<ILoggerFactory>(),
            projectDir,
            sp.GetRequiredService<NpcRuntimeSupervisor>(),
            sp.GetRequiredService<SkillManager>(),
            sp.GetRequiredService<ICronScheduler>(),
            sp.GetRequiredService<StardewNpcRuntimeBindingResolver>(),
            sp.GetRequiredService<INpcToolSurfaceSnapshotProvider>(),
            memoryEnabled,
            userProfileEnabled,
            HermesEnvironment.MaxAgentIterations));
        services.AddSingleton(sp => new StardewPrivateChatRuntimeAdapter(
            sp.GetRequiredService<INpcPrivateChatAgentRunner>(),
            sp.GetRequiredService<ILogger<StardewPrivateChatRuntimeAdapter>>(),
            sessionLeaseCoordinator: sp.GetRequiredService<IPrivateChatSessionLeaseCoordinator>(),
            bindingResolver: sp.GetRequiredService<StardewNpcRuntimeBindingResolver>()));
        services.AddSingleton(sp => new StardewNpcAutonomyBackgroundService(
            sp.GetRequiredService<IStardewBridgeDiscovery>(),
            sp.GetRequiredService<HttpClient>(),
            sp.GetRequiredService<IChatClient>(),
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<SkillManager>(),
            sp.GetRequiredService<ICronScheduler>(),
            sp.GetRequiredService<NpcRuntimeSupervisor>(),
            sp.GetRequiredService<NpcRuntimeHost>(),
            sp.GetRequiredService<StardewNpcRuntimeBindingResolver>(),
            sp.GetRequiredService<StardewNpcAutonomyPromptSupplementBuilder>(),
            sp.GetRequiredService<INpcToolSurfaceSnapshotProvider>(),
            sp.GetRequiredService<StardewPrivateChatRuntimeAdapter>(),
            sp.GetRequiredService<NpcAutonomyBudget>(),
            sp.GetRequiredService<WorldCoordinationService>(),
            sp.GetRequiredService<ILogger<StardewNpcAutonomyBackgroundService>>(),
            new StardewNpcAutonomyBackgroundOptions(
                ReadConfigList("stardew", "npc_autonomy_enabled_ids")),
            memoryEnabled,
            userProfileEnabled,
            projectDir));

        // Memory manager
        var memoryDir = Path.Combine(hermesHome, "memories");
        services.AddSingleton(sp => new MemoryManager(
            memoryDir,
            sp.GetRequiredService<IChatClient>(),
            sp.GetRequiredService<ILogger<MemoryManager>>(),
            memoryCharLimit: ReadPositiveConfigInt("memory", "memory_char_limit", 2200),
            userCharLimit: ReadPositiveConfigInt("memory", "user_char_limit", 1375)));

        // Skill manager — reconcile bundled skills against the active user tree on startup (non-fatal)
        var skillsProvenancePath = BundledSkillCatalogService.GetDefaultProvenancePath(projectDir);
        var skillsQuarantineDir = BundledSkillCatalogService.GetDefaultQuarantineRoot(projectDir);
        try
        {
            var bundledSkillsDir = FindRepoSkillsDir();
            if (bundledSkillsDir is not null && Directory.Exists(bundledSkillsDir))
            {
                var skillCatalog = new BundledSkillCatalogService();
                skillCatalog.ReconcileActiveSkills(
                    bundledSkillsDir,
                    skillsDir,
                    skillsProvenancePath,
                    skillsQuarantineDir);
            }
        }
        catch (Exception ex)
        {
            BestEffort.LogFailure(TryGetAppLogger(), ex, "reconciling bundled skills");
        }
        services.AddSingleton(sp => new SkillManager(
            skillsDir,
            sp.GetRequiredService<ILogger<SkillManager>>()));

        // Permission manager + workspace-scoped permission memory
        var permissionMemoryDir = Path.Combine(projectDir, "permissions");
        var workspacePath = HermesEnvironment.AgentWorkingDirectory;
        services.AddSingleton(sp => new WorkspacePermissionRuleStore(
            permissionMemoryDir,
            workspacePath,
            sp.GetRequiredService<ILogger<WorkspacePermissionRuleStore>>()));
        services.AddSingleton(sp =>
        {
            var store = sp.GetRequiredService<WorkspacePermissionRuleStore>();
            var context = new PermissionContext();
            foreach (var rule in store.LoadAlwaysAllowRules())
            {
                context.AlwaysAllow.Add(rule);
            }

            return new PermissionManager(
                context,
                sp.GetRequiredService<ILogger<PermissionManager>>());
        });

        // Task manager
        var tasksDir = Path.Combine(projectDir, "tasks");
        services.AddSingleton(sp => new TaskManager(
            tasksDir,
            sp.GetRequiredService<ILogger<TaskManager>>()));
        services.AddSingleton<ICronScheduler, InMemoryCronScheduler>();

        // Buddy service (persisted to buddy/buddy.json under the project dir)
        var buddyDir = Path.Combine(projectDir, "buddy");
        var buddyConfigPath = Path.Combine(buddyDir, "buddy.json");
        services.AddSingleton(sp => new BuddyService(
            buddyConfigPath,
            sp.GetRequiredService<IChatClient>()));

        // Wiki system (persistent knowledge base)
        var wikiConfig = new Hermes.Agent.Wiki.WikiConfig();
        services.AddSingleton(wikiConfig);
        services.AddSingleton<Hermes.Agent.Wiki.IWikiStorage>(sp =>
            new Hermes.Agent.Wiki.LocalWikiStorage(sp.GetRequiredService<Hermes.Agent.Wiki.WikiConfig>()));
        services.AddSingleton(sp => new Hermes.Agent.Wiki.WikiSearchIndex(
            Path.Combine(sp.GetRequiredService<Hermes.Agent.Wiki.WikiConfig>().WikiPath, ".wiki-search.db"),
            sp.GetRequiredService<ILogger<Hermes.Agent.Wiki.WikiSearchIndex>>()));
        services.AddSingleton(sp => new Hermes.Agent.Wiki.WikiManager(
            sp.GetRequiredService<Hermes.Agent.Wiki.IWikiStorage>(),
            sp.GetRequiredService<Hermes.Agent.Wiki.WikiConfig>(),
            sp.GetRequiredService<Hermes.Agent.Wiki.WikiSearchIndex>(),
            sp.GetRequiredService<ILogger<Hermes.Agent.Wiki.WikiManager>>()));

        // Soul service (persistent identity, user profile, mistakes, habits)
        services.AddSingleton(sp => new SoulService(
            hermesHome,
            sp.GetRequiredService<ILogger<SoulService>>()));

        // Soul extractor (LLM-powered transcript analysis)
        services.AddSingleton(sp => new SoulExtractor(
            sp.GetRequiredService<IChatClient>(),
            sp.GetRequiredService<ILogger<SoulExtractor>>()));

        // Soul registry (browsable soul templates)
        var soulsSearchPaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "souls"), // Shipped with app
            Path.Combine(projectDir, "souls"),               // User-installed souls
        };
        services.AddSingleton(sp => new SoulRegistry(
            soulsSearchPaths,
            sp.GetRequiredService<ILogger<SoulRegistry>>()));

        // Agent profile manager (multi-agent configurations)
        var agentsDir = Path.Combine(projectDir, "agents");
        services.AddSingleton(sp => new AgentProfileManager(
            agentsDir,
            sp.GetRequiredService<SoulService>(),
            sp.GetRequiredService<ILogger<AgentProfileManager>>()));

        // Token budget & Prompt builder for Context Runtime
        services.AddSingleton(sp => new TokenBudget(maxTokens: 8000, recentTurnWindow: 6));
        services.AddSingleton(sp => AgentCapabilityAssembler.CreatePromptBuilder(new AgentPromptServices
        {
            SkillManager = sp.GetRequiredService<SkillManager>(),
            MemoryAvailable = memoryAvailable
        }));

        // Context manager (with soul integration)
        services.AddSingleton(sp => new ContextManager(
            sp.GetRequiredService<TranscriptStore>(),
            sp.GetRequiredService<IChatClient>(),
            sp.GetRequiredService<TokenBudget>(),
            sp.GetRequiredService<PromptBuilder>(),
            sp.GetRequiredService<ILogger<ContextManager>>(),
            soulService: sp.GetRequiredService<SoulService>(),
            pluginManager: sp.GetRequiredService<PluginManager>(),
            memoryOrchestrator: sp.GetRequiredService<HermesMemoryOrchestrator>(),
            taskProjectionService: sp.GetRequiredService<SessionTaskProjectionService>()));
        services.AddSingleton(sp => new TranscriptRecallService(
            sp.GetRequiredService<TranscriptStore>(),
            sp.GetRequiredService<ILogger<TranscriptRecallService>>(),
            sp.GetRequiredService<SessionSearchIndex>(),
            sp.GetRequiredService<IChatClient>()));
        services.AddSingleton(sp => new CuratedMemoryLifecycleProvider(
            sp.GetRequiredService<MemoryManager>(),
            includeMemory: memoryEnabled,
            includeUser: userProfileEnabled));
        services.AddSingleton<IMemoryProvider>(sp => sp.GetRequiredService<CuratedMemoryLifecycleProvider>());
        services.AddSingleton<IMemoryCompressionParticipant>(sp => sp.GetRequiredService<CuratedMemoryLifecycleProvider>());
        services.AddSingleton<IMemoryProvider>(sp => new TranscriptMemoryProvider(
            sp.GetRequiredService<TranscriptRecallService>()));
        services.AddSingleton(sp => new HermesMemoryOrchestrator(
            sp.GetServices<IMemoryProvider>(),
            sp.GetRequiredService<ILogger<HermesMemoryOrchestrator>>(),
            sp.GetServices<IMemoryCompressionParticipant>()));
        services.AddSingleton(sp => new TurnMemoryCoordinator(
            sp.GetRequiredService<ContextManager>(),
            sp.GetRequiredService<HermesMemoryOrchestrator>(),
            sp.GetRequiredService<ILogger<TurnMemoryCoordinator>>()));

        // MCP manager
        services.AddSingleton(sp => new McpManager(
            sp.GetRequiredService<ILogger<McpManager>>()));

        // Tool registry (shared across agent and subagents)
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        // Plugin manager
        services.AddSingleton(sp =>
        {
            var pm = new PluginManager(sp.GetRequiredService<ILogger<PluginManager>>());
            var builtinMemoryEnabled = !string.Equals(HermesEnvironment.ReadConfigSetting("plugins", "builtin_memory"), "false", StringComparison.OrdinalIgnoreCase);
            pm.Register(new BuiltinMemoryPlugin(
                sp.GetRequiredService<MemoryManager>(),
                includeMemory: builtinMemoryEnabled && memoryEnabled,
                includeUser: builtinMemoryEnabled && userProfileEnabled));
            return pm;
        });
        services.AddSingleton(sp =>
        {
            var nudgeInterval = memoryEnabled || userProfileEnabled
                ? ReadNonNegativeConfigInt("memory", "nudge_interval", MemoryReviewDefaults.NudgeInterval)
                : 0;
            return new MemoryReviewService(
                sp.GetRequiredService<IChatClient>(),
                sp.GetRequiredService<MemoryManager>(),
                sp.GetRequiredService<ILogger<MemoryReviewService>>(),
                sp.GetRequiredService<PluginManager>(),
                nudgeInterval,
                sp.GetRequiredService<SkillManager>(),
                ReadNonNegativeConfigInt("skills", "creation_nudge_interval", MemoryReviewDefaults.SkillCreationNudgeInterval));
        });

        // Analytics / Insights service
        var insightsDir = Path.Combine(projectDir, "analytics");
        services.AddSingleton(sp => new InsightsService(
            insightsDir,
            sp.GetRequiredService<ILogger<InsightsService>>()));

        // Dreamer (background free-association worker) — status for Dashboard; loop started post-build
        services.AddSingleton(_ => new DreamerStatus());

        // Core agent — wired with all optional dependencies. MaxToolIterations
        // is read from config.yaml (agent.max_turns) so the SettingsPage value
        // actually takes effect; otherwise the default would be 25.
        services.AddSingleton(sp =>
        {
            var agent = new Agent(
                sp.GetRequiredService<IChatClient>(),
                sp.GetRequiredService<ILogger<Agent>>(),
                permissions: sp.GetRequiredService<PermissionManager>(),
                transcripts: sp.GetRequiredService<TranscriptStore>(),
                memories: sp.GetRequiredService<MemoryManager>(),
                contextManager: sp.GetRequiredService<ContextManager>(),
                soulService: sp.GetRequiredService<SoulService>(),
                pluginManager: sp.GetRequiredService<PluginManager>(),
                turnMemoryCoordinator: sp.GetRequiredService<TurnMemoryCoordinator>(),
                memoryReviewService: sp.GetRequiredService<MemoryReviewService>());
            agent.MaxToolIterations = HermesEnvironment.MaxAgentIterations;
            return agent;
        });

        // Agent service (subagent spawning, worktree isolation). Pass the
        // configured iteration limit so subagents honor agent.max_turns too.
        var worktreesDir = Path.Combine(projectDir, "worktrees");
        services.AddSingleton(sp => new AgentService(
            sp,
            sp.GetRequiredService<ILogger<AgentService>>(),
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<IChatClient>(),
            worktreesDir,
            defaultMaxToolIterations: HermesEnvironment.MaxAgentIterations));

        // Coordinator service (multi-worker orchestration)
        var coordinatorStateDir = Path.Combine(projectDir, "coordinator");
        services.AddSingleton(sp => new CoordinatorService(
            sp.GetRequiredService<AgentService>(),
            sp.GetRequiredService<TaskManager>(),
            sp.GetRequiredService<ILogger<CoordinatorService>>(),
            sp.GetRequiredService<IChatClient>(),
            coordinatorStateDir));

        // Skill invoker (for slash command support)
        services.AddSingleton(sp => new Hermes.Agent.Skills.SkillInvoker(
            sp.GetRequiredService<Hermes.Agent.Skills.SkillManager>(),
            sp.GetRequiredService<IChatClient>(),
            sp.GetRequiredService<ILogger<Hermes.Agent.Skills.SkillInvoker>>()));

        // Chat service (pure C# — no sidecar)
        services.AddSingleton<HermesChatService>();
        services.AddSingleton<RuntimeStatusService>();

        var provider = services.BuildServiceProvider();
        Services = provider;

        // ── Post-build: Register all tools and connect MCP ──
        RegisterAllTools(provider);
        InitializeMcpAsync(provider, projectDir);

        // Wire permission prompt callback to show a ContentDialog in the UI
        WirePermissionCallback(provider);

        StartDreamerBackground(provider, hermesHome, projectDir);
        StartStardewNpcAutonomyBackground(provider);

        return provider;
    }

    private static void StartStardewNpcAutonomyBackground(IServiceProvider provider)
    {
        var logger = provider.GetService<ILogger<App>>();
        try
        {
            provider.GetRequiredService<StardewNpcAutonomyBackgroundService>().Start();
        }
        catch (Exception ex)
        {
            if (logger is not null)
                logger.LogWarning(ex, "Stardew autonomy background start failed");
            else
                BestEffort.LogFailure(null, ex, "starting Stardew autonomy background service");
        }
    }

    private static void TryStopStardewNpcAutonomyBackground(ILogger? logger, string reason)
    {
        if (ReferenceEquals(Services, UninitializedAppServiceProvider.Instance))
            return;

        try
        {
            Services.GetService<StardewNpcAutonomyBackgroundService>()?.Stop();
        }
        catch (Exception ex)
        {
            BestEffort.LogFailure(logger, ex, "stopping Stardew autonomy background service", $"reason={reason}");
        }
    }

    /// <summary>
    /// Initializes Dreamer components and starts its continuous background loop.
    /// </summary>
    /// <param name="hermesHome">Path to the Hermes home directory (used for Dreamer layout and config).</param>
    /// <param name="projectDir">Path to the project directory (used to locate the transcripts directory).</param>
    /// <remarks>
    /// This method creates/uses long-lived HTTP clients, loads Dreamer configuration and room layout, constructs the DreamerService and related helpers, sets a static cancellation token source, and launches the Dreamer loop via a background task. Startup failures are non-fatal: exceptions are caught and written to debug output only.
    /// </remarks>
    private static void StartDreamerBackground(IServiceProvider provider, string hermesHome, string projectDir)
    {
        var logger = provider.GetService<ILogger<App>>();
        var dreamerStatus = provider.GetService<DreamerStatus>();
        var insights = provider.GetService<InsightsService>();

        try
        {
            var cfgPath = Path.Combine(hermesHome, "config.yaml");
            var lf = provider.GetRequiredService<ILoggerFactory>();
            var room = new DreamerRoom(hermesHome, lf.CreateLogger<DreamerRoom>());
            room.EnsureLayout();

            // Long-lived HttpClients for Dreamer. Do not attach logging handlers that record
            // request/response headers — LLM calls carry API keys on every request.
            // These clients are created with automatic decompression disabled, proxy usage
            // disabled, and sanitized default headers before any request-specific auth headers
            // are applied. Do not attach logging handlers that record request/response headers
            // or full requests.
            var dreamerHttpClients = GetOrCreateDreamerHttpClients();
            var walkHttp = dreamerHttpClients.Walk;
            var echoHttp = dreamerHttpClients.Echo;
            var rssHttp = dreamerHttpClients.Rss;

            // Factory methods to create fresh clients from current config
            IChatClient CreateWalkClient(DreamerConfig cfg) => new OpenAiClient(cfg.ToWalkLlmConfig(), walkHttp);
            IChatClient CreateEchoClient(DreamerConfig cfg) => new OpenAiClient(cfg.ToEchoLlmConfig(), echoHttp);

            var rss = new RssFetcher(rssHttp, room, lf.CreateLogger<RssFetcher>());
            var dreamer = new DreamerService(
                hermesHome,
                cfgPath,
                room,
                CreateWalkClient,
                CreateEchoClient,
                provider.GetRequiredService<TranscriptStore>(),
                provider.GetRequiredService<InsightsService>(),
                provider.GetRequiredService<DreamerStatus>(),
                rss,
                lf.CreateLogger<DreamerService>(),
                lf);

            var dreamerCts = new System.Threading.CancellationTokenSource();
            dreamerStatus?.ClearStartupFailure();
            SetDreamerCts(dreamerCts, logger, "starting Dreamer background loop");
            _ = Task.Run(async () =>
            {
                try
                {
                    await dreamer.RunForeverAsync(dreamerCts.Token);
                }
                finally
                {
                    TryCancelAndDisposeDreamerCtsIfCurrent(dreamerCts, logger, "Dreamer background loop exit");
                }
            });
        }
        catch (Exception ex)
        {
            dreamerStatus?.SetStartupFailure(ex.Message);
            insights?.RecordDreamerStartupFailure(ex);

            try
            {
                insights?.Save();
            }
            catch (Exception saveEx)
            {
                BestEffort.LogFailure(logger, saveEx, "persisting Dreamer startup failure insights");
            }

            if (logger is not null)
                logger.LogError(ex, "Dreamer background start failed");
            else
                BestEffort.LogFailure(null, ex, "starting Dreamer background loop");
        }
    }

    /// <summary>
    /// Wire the Agent's permission callback to show a WinUI ContentDialog when Ask is returned.
    /// Delegates UI construction to PermissionDialogService for separation of concerns.
    /// </summary>
    private static void WirePermissionCallback(IServiceProvider services)
    {
        var agent = services.GetRequiredService<Hermes.Agent.Core.Agent>();
        var permissionManager = services.GetRequiredService<PermissionManager>();
        var permissionStore = services.GetRequiredService<WorkspacePermissionRuleStore>();
        // Resolve the dialog service once; it captures the active window's
        // DispatcherQueue and XamlRoot internally and is safe to reuse across
        // many permission prompts. PermissionDialogService is the dedicated
        // dialog view + formatter extracted from this code-behind so this
        // file stays focused on app lifecycle / DI orchestration only —
        // see PermissionDialogService.cs for the construction logic plus the
        // dispatcher-shutdown deadlock guard.
        agent.PermissionPromptCallback = async (toolName, message, toolArguments) =>
        {
            if (App.Current is App app && app._window is not null)
            {
                var dialogService = new HermesDesktop.Services.PermissionDialogService(
                    app._window,
                    TryGetAppLogger());
                var decision = await dialogService.ShowPermissionDecisionAsync(message, toolName, toolArguments);
                switch (decision)
                {
                    case PermissionPromptDecision.AlwaysAllowTool:
                        // Persist for this workspace so repeated tool calls no
                        // longer prompt on future runs.
                        if (permissionManager.AddAlwaysAllowRule(toolName))
                        {
                            permissionStore.SaveAlwaysAllowRules(permissionManager.GetAlwaysAllowRulesSnapshot());
                        }
                        return true;

                    case PermissionPromptDecision.AllowOnce:
                        return true;

                    default:
                        return false;
                }
            }
            return false;
        };
    }

    /// <summary>
    /// Register all built-in tools with the Agent after DI is built.
    /// </summary>
    private static void RegisterAllTools(IServiceProvider services)
    {
        var agent = services.GetRequiredService<Agent>();
        var toolRegistry = services.GetRequiredService<IToolRegistry>();
        var chatClient = services.GetRequiredService<IChatClient>();

        var todoStore = services.GetRequiredService<SessionTodoStore>();
        var memoryManager = services.GetRequiredService<MemoryManager>();
        var memoryAvailable = IsConfigEnabled("memory", "memory_enabled") ||
                              IsConfigEnabled("memory", "user_profile_enabled");
        MigrateLegacyMemoriesIfNeeded(memoryManager.MemoryDir);
        var checkpointDir = Path.Combine(HermesEnvironment.HermesHomePath, "checkpoints");

        AgentCapabilityAssembler.RegisterAllTools(
            agent,
            new AgentCapabilityServices
            {
                ChatClient = chatClient,
                ToolRegistry = toolRegistry,
                TodoStore = todoStore,
                CronScheduler = services.GetRequiredService<ICronScheduler>(),
                MemoryManager = memoryManager,
                PluginManager = services.GetRequiredService<PluginManager>(),
                TranscriptRecallService = services.GetRequiredService<TranscriptRecallService>(),
                SkillManager = services.GetRequiredService<SkillManager>(),
                CheckpointDirectory = checkpointDir,
                MemoryAvailable = memoryAvailable
            },
            Enumerable.Empty<ITool>());
    }

    /// <summary>
    /// Import old timestamped YAML-frontmatter memories into the Python-compatible
    /// fixed files without deleting or moving the legacy files.
    /// </summary>
    private static void MigrateLegacyMemoriesIfNeeded(string currentMemoryDir)
    {
        try
        {
            var legacyDir = Path.Combine(HermesEnvironment.HermesHomePath, "hermes-cs", "memory");
            if (!Directory.Exists(legacyDir)) return;

            var legacyFiles = Directory.GetFiles(legacyDir, "*.md");
            if (legacyFiles.Length == 0) return;

            Directory.CreateDirectory(currentMemoryDir);

            foreach (var src in legacyFiles)
            {
                var fileName = Path.GetFileName(src);
                if (string.Equals(fileName, "MEMORY.md", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, "USER.md", StringComparison.OrdinalIgnoreCase))
                {
                    ImportLegacyEntries(
                        File.ReadAllText(src),
                        Path.Combine(currentMemoryDir, fileName.Equals("USER.md", StringComparison.OrdinalIgnoreCase) ? "USER.md" : "MEMORY.md"));
                    continue;
                }

                var (target, body) = ParseLegacyMemoryFile(src);
                if (string.IsNullOrWhiteSpace(body))
                    continue;

                ImportLegacyEntries(
                    body,
                    Path.Combine(currentMemoryDir, target == "user" ? "USER.md" : "MEMORY.md"));
            }
        }
        catch (Exception ex)
        {
            BestEffort.LogFailure(TryGetAppLogger(), ex, "migrating legacy memory directory");
        }
    }

    private static int ReadPositiveConfigInt(string section, string key, int fallback)
    {
        var raw = HermesEnvironment.ReadConfigSetting(section, key);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : fallback;
    }

    private static bool IsConfigEnabled(string section, string key)
        => MemorySettings.IsEnabled(HermesEnvironment.ReadConfigSetting(section, key));

    private static int ReadNonNegativeConfigInt(string section, string key, int fallback)
    {
        var raw = HermesEnvironment.ReadConfigSetting(section, key);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0
            ? value
            : fallback;
    }

    private static IReadOnlyList<string> ReadConfigList(string section, string key)
    {
        var raw = HermesEnvironment.ReadConfigSetting(section, key);
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        return raw
            .Split([','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static (string Target, string Body) ParseLegacyMemoryFile(string path)
    {
        var content = File.ReadAllText(path).Trim();
        if (!content.StartsWith("---", StringComparison.Ordinal))
            return ("memory", content);

        var end = content.IndexOf("---", 3, StringComparison.Ordinal);
        if (end < 0)
            return ("memory", content);

        var frontmatter = content[3..end];
        var body = content[(end + 3)..].Trim();
        var typeLine = frontmatter
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.TrimStart().StartsWith("type:", StringComparison.OrdinalIgnoreCase));
        var type = typeLine?.Split(':', 2)[1].Trim().Trim('"', '\'').ToLowerInvariant();
        return (type == "user" ? "user" : "memory", body);
    }

    private static void ImportLegacyEntries(string rawEntries, string destinationPath)
    {
        var existing = ReadMemoryEntries(destinationPath);
        var imported = rawEntries
            .Split(MemoryManager.EntryDelimiter, StringSplitOptions.None)
            .Select(entry => entry.Trim())
            .Where(entry => !string.IsNullOrWhiteSpace(entry));

        var merged = existing.Concat(imported).Distinct(StringComparer.Ordinal).ToList();
        File.WriteAllText(destinationPath, string.Join(MemoryManager.EntryDelimiter, merged));
    }

    private static List<string> ReadMemoryEntries(string path)
    {
        if (!File.Exists(path))
            return new List<string>();

        var raw = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        return raw
            .Split(MemoryManager.EntryDelimiter, StringSplitOptions.None)
            .Select(entry => entry.Trim())
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Find the repo's skills/ directory by walking up from the build output to find .git or skills/.</summary>
    private static string? FindRepoSkillsDir()
    {
        // Walk up from build output to find the repo root (contains .git or skills/)
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10 && dir is not null; i++)
        {
            var skillsCandidate = Path.Combine(dir, "skills");
            if (Directory.Exists(skillsCandidate) && Directory.Exists(Path.Combine(dir, ".git")))
            {
                System.Diagnostics.Debug.WriteLine($"Found repo skills at: {skillsCandidate}");
                return skillsCandidate;
            }
            // Also check for skills/ without .git (user may have extracted without git)
            if (Directory.Exists(skillsCandidate) && Directory.EnumerateDirectories(skillsCandidate).Any())
            {
                System.Diagnostics.Debug.WriteLine($"Found skills dir at: {skillsCandidate}");
                return skillsCandidate;
            }
            dir = Path.GetDirectoryName(dir);
        }

        System.Diagnostics.Debug.WriteLine("Could not find bundled skills directory");
        return null;
    }

    /// <summary>
    /// Load MCP server configs and connect (fire-and-forget on startup).
    /// <summary>
    /// Initializes the MCP subsystem by loading MCP configuration files from standard locations, connecting to configured MCP servers, and registering any discovered MCP tools with the Agent and tool registry.
    /// </summary>
    /// <param name="projectDir">Path to the project directory; used as one of the locations to search for an mcp.json configuration file.</param>
    /// <remarks>
    /// Initialization errors are non-fatal: exceptions are logged and startup continues without MCP tools.
    /// </remarks>
    private static async void InitializeMcpAsync(IServiceProvider services, string projectDir)
    {
        try
        {
            var mcpManager = services.GetRequiredService<McpManager>();
            var agent = services.GetRequiredService<Agent>();
            var toolRegistry = services.GetRequiredService<IToolRegistry>();

            // Check for MCP config in standard locations
            var mcpConfigPaths = new[]
            {
                Path.Combine(projectDir, "mcp.json"),
                Path.Combine(HermesEnvironment.HermesHomePath, "mcp.json"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hermes", "mcp.json")
            };

            foreach (var configPath in mcpConfigPaths)
            {
                if (File.Exists(configPath))
                {
                    await mcpManager.LoadFromConfigAsync(configPath);
                }
            }

            // Connect to all configured servers
            await mcpManager.ConnectAllAsync();

            // Register discovered MCP tools with the Agent
            AgentCapabilityAssembler.RegisterDiscoveredTools(agent, toolRegistry, mcpManager.Tools.Values);
        }
        catch (Exception ex)
        {
            // MCP initialization is non-critical — log and continue
            var logger = services.GetRequiredService<ILogger<App>>();
            logger.LogWarning(ex, "MCP initialization failed, continuing without MCP tools");
        }
    }

    private sealed class DreamerHttpClients : IDisposable
    {
        public DreamerHttpClients(HttpClient walk, HttpClient echo, HttpClient rss)
        {
            Walk = walk;
            Echo = echo;
            Rss = rss;
        }

        public HttpClient Walk { get; }
        public HttpClient Echo { get; }
        public HttpClient Rss { get; }

        public void Dispose()
        {
            DisposeClient(Walk);
            DisposeClient(Echo);
            DisposeClient(Rss);
        }

        private static void DisposeClient(HttpClient client)
        {
            client.CancelPendingRequests();
            client.Dispose();
        }
    }

    /// <summary>Sentinel <see cref="IServiceProvider"/> used only until DI is built in <see cref="OnLaunched"/>.</summary>
    private sealed class UninitializedAppServiceProvider : IServiceProvider
    {
        public static readonly UninitializedAppServiceProvider Instance = new();

        private UninitializedAppServiceProvider()
        {
        }

        public object? GetService(Type serviceType) => null;
    }
}
