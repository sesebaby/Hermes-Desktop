using System.Text.Json;
using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Hermes.Agent.Memory;
using Hermes.Agent.Skills;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Services;

[TestClass]
public sealed class SkillSelfEvolutionParityTests
{
    private string _tempDir = null!;
    private SkillManager _skillManager = null!;

    [TestInitialize]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"hermes-skill-parity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _skillManager = new SkillManager(_tempDir, NullLogger<SkillManager>.Instance);
    }

    [TestCleanup]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public void SkillManageToolSchema_UsesPythonActionsAndRequiredFields()
    {
        var agent = new Agent(new StubChatClient(), NullLogger<Agent>.Instance);
        agent.RegisterTool(new SkillManageTool(_skillManager));

        var schema = agent.GetToolDefinitions().Single(t => t.Name == "skill_manage").Parameters;
        var properties = schema.GetProperty("properties");

        CollectionAssert.AreEqual(
            new[] { "create", "patch", "edit", "delete", "write_file", "remove_file" },
            properties.GetProperty("action").GetProperty("enum").EnumerateArray().Select(e => e.GetString()).ToArray());
        CollectionAssert.AreEqual(
            new[] { "action", "name" },
            schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToArray());
        Assert.IsTrue(properties.TryGetProperty("old_string", out _));
        Assert.IsTrue(properties.TryGetProperty("new_string", out _));
        Assert.IsTrue(properties.TryGetProperty("file_path", out _));
        Assert.IsTrue(properties.TryGetProperty("file_content", out _));
    }

    [TestMethod]
    public async Task SkillManageTool_CreatePatchEditDeleteSkill()
    {
        var tool = new SkillManageTool(_skillManager);
        var content = """
            ---
            name: build-fixer
            description: Fix build failures with exact commands
            tools: session_search, todo
            ---

            Run `dotnet build`.
            """;

        var created = await tool.ExecuteAsync(new SkillManageParameters
        {
            Action = "create",
            Name = "build-fixer",
            Content = content,
            Category = "dev"
        }, CancellationToken.None);
        Assert.IsTrue(created.Success, created.Content);

        var patched = await tool.ExecuteAsync(new SkillManageParameters
        {
            Action = "patch",
            Name = "build-fixer",
            OldString = "Run `dotnet build`.",
            NewString = "Run `dotnet test` first, then `dotnet build`."
        }, CancellationToken.None);
        Assert.IsTrue(patched.Success, patched.Content);
        StringAssert.Contains(await _skillManager.ReadSkillContentAsync("build-fixer", CancellationToken.None), "dotnet test");

        var edited = await tool.ExecuteAsync(new SkillManageParameters
        {
            Action = "edit",
            Name = "build-fixer",
            Content = content.Replace("Fix build failures", "Repair build failures")
        }, CancellationToken.None);
        Assert.IsTrue(edited.Success, edited.Content);
        StringAssert.Contains(_skillManager.GetSkill("build-fixer")!.Description, "Repair build failures");

        var deleted = await tool.ExecuteAsync(new SkillManageParameters
        {
            Action = "delete",
            Name = "build-fixer"
        }, CancellationToken.None);
        Assert.IsTrue(deleted.Success, deleted.Content);
        Assert.IsNull(_skillManager.GetSkill("build-fixer"));
    }

    [TestMethod]
    public async Task SkillManageTool_PatchUsesPythonStyleFuzzyMatchingAndPreview()
    {
        var tool = new SkillManageTool(_skillManager);
        var content = """
            ---
            name: fuzzy-skill
            description: Fuzzy patch test
            tools: session_search
            ---

            # Commands

                def hello():
                    print("hi")
            """;
        await tool.ExecuteAsync(new SkillManageParameters
        {
            Action = "create",
            Name = "fuzzy-skill",
            Content = content
        }, CancellationToken.None);

        var fuzzyPatch = await tool.ExecuteAsync(new SkillManageParameters
        {
            Action = "patch",
            Name = "fuzzy-skill",
            OldString = "def hello():\n    print(\"hi\")",
            NewString = "def hello():\n    print(\"hello world\")"
        }, CancellationToken.None);
        Assert.IsTrue(fuzzyPatch.Success, fuzzyPatch.Content);
        StringAssert.Contains(await _skillManager.ReadSkillContentAsync("fuzzy-skill", CancellationToken.None), "hello world");

        var noMatch = await tool.ExecuteAsync(new SkillManageParameters
        {
            Action = "patch",
            Name = "fuzzy-skill",
            OldString = "this does not exist anywhere",
            NewString = "replacement"
        }, CancellationToken.None);
        Assert.IsFalse(noMatch.Success);
        StringAssert.Contains(noMatch.Content, "file_preview");
    }

    [TestMethod]
    public async Task SkillManageTool_WriteAndRemoveSupportingFileWithinAllowedDirs()
    {
        await CreateSkillAsync("research-helper");
        var tool = new SkillManageTool(_skillManager);

        var write = await tool.ExecuteAsync(new SkillManageParameters
        {
            Action = "write_file",
            Name = "research-helper",
            FilePath = "references/example.md",
            FileContent = "reference body"
        }, CancellationToken.None);
        Assert.IsTrue(write.Success, write.Content);

        var view = await new SkillViewTool(_skillManager).ExecuteAsync(new SkillViewParameters
        {
            Name = "research-helper",
            FilePath = "references/example.md"
        }, CancellationToken.None);
        StringAssert.Contains(view.Content, "reference body");

        var remove = await tool.ExecuteAsync(new SkillManageParameters
        {
            Action = "remove_file",
            Name = "research-helper",
            FilePath = "references/example.md"
        }, CancellationToken.None);
        Assert.IsTrue(remove.Success, remove.Content);
    }

    [TestMethod]
    public async Task SkillManageTool_RejectsPathTraversalAndUnsupportedDirs()
    {
        await CreateSkillAsync("safe-skill");
        var tool = new SkillManageTool(_skillManager);

        var traversal = await tool.ExecuteAsync(new SkillManageParameters
        {
            Action = "write_file",
            Name = "safe-skill",
            FilePath = "../escape.md",
            FileContent = "bad"
        }, CancellationToken.None);
        Assert.IsFalse(traversal.Success);

        var unsupported = await tool.ExecuteAsync(new SkillManageParameters
        {
            Action = "write_file",
            Name = "safe-skill",
            FilePath = "notes/example.md",
            FileContent = "bad"
        }, CancellationToken.None);
        Assert.IsFalse(unsupported.Success);
        StringAssert.Contains(unsupported.Content, "references");
    }

    [TestMethod]
    public async Task SkillViewTool_RejectsSymlinkEscape()
    {
        await CreateSkillAsync("link-safe");
        var skill = _skillManager.GetSkill("link-safe")!;
        var secretPath = Path.Combine(_tempDir, "outside-secret.txt");
        await File.WriteAllTextAsync(secretPath, "TOP SECRET DATA");
        var linkPath = Path.Combine(_skillManager.GetSkillDirectory(skill), "evil-link");

        try
        {
            File.CreateSymbolicLink(linkPath, secretPath);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
        {
            Assert.Inconclusive($"Symlink creation is unavailable in this environment: {ex.Message}");
        }

        var view = await new SkillViewTool(_skillManager).ExecuteAsync(new SkillViewParameters
        {
            Name = "link-safe",
            FilePath = "evil-link"
        }, CancellationToken.None);

        Assert.IsFalse(view.Success);
        StringAssert.Contains(view.Content, "escapes");
        Assert.IsFalse(view.Content.Contains("TOP SECRET DATA", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SkillsListAndViewTools_ReturnSkillInventoryAndFullContent()
    {
        await CreateSkillAsync("parity-helper", "Parity helper description");

        var list = await new SkillsListTool(_skillManager).ExecuteAsync(new SkillsListParameters(), CancellationToken.None);
        StringAssert.Contains(list.Content, "parity-helper");
        StringAssert.Contains(list.Content, "Parity helper description");

        var view = await new SkillViewTool(_skillManager).ExecuteAsync(new SkillViewParameters
        {
            Name = "parity-helper"
        }, CancellationToken.None);
        StringAssert.Contains(view.Content, "Skill instructions for parity-helper");
        StringAssert.Contains(view.Content, "linked_files");
    }

    [TestMethod]
    public async Task SkillViewTool_ReturnsMetadataAndBinaryFileInfo()
    {
        var content = """
            ---
            name: metadata-helper
            description: Metadata helper
            tags: [alpha, beta]
            related_skills: [other-helper]
            ---

            Skill instructions for metadata-helper.
            """;
        await _skillManager.CreateSkillFromContentAsync("metadata-helper", content, category: null, CancellationToken.None);
        var skill = _skillManager.GetSkill("metadata-helper")!;
        var assetDir = Path.Combine(_skillManager.GetSkillDirectory(skill), "assets");
        Directory.CreateDirectory(assetDir);
        await File.WriteAllBytesAsync(Path.Combine(assetDir, "blob.bin"), new byte[] { 0, 1, 2, 3, 4 });

        var view = await new SkillViewTool(_skillManager).ExecuteAsync(new SkillViewParameters
        {
            Name = "metadata-helper"
        }, CancellationToken.None);
        StringAssert.Contains(view.Content, "\"tags\":[\"alpha\",\"beta\"]");
        StringAssert.Contains(view.Content, "\"related_skills\":[\"other-helper\"]");

        var binary = await new SkillViewTool(_skillManager).ExecuteAsync(new SkillViewParameters
        {
            Name = "metadata-helper",
            FilePath = "assets/blob.bin"
        }, CancellationToken.None);
        Assert.IsTrue(binary.Success, binary.Content);
        StringAssert.Contains(binary.Content, "[Binary file: blob.bin");
        StringAssert.Contains(binary.Content, "\"is_binary\":true");
    }

    [TestMethod]
    public void SystemPrompts_Build_IncludesSkillsGuidanceOnlyWhenRequested()
    {
        var withoutSkills = SystemPrompts.Build(
            includeMemoryGuidance: true,
            includeSessionSearchGuidance: true,
            includeSkillsGuidance: false);
        var withSkills = SystemPrompts.Build(
            includeMemoryGuidance: true,
            includeSessionSearchGuidance: true,
            includeSkillsGuidance: true);

        Assert.IsFalse(withoutSkills.Contains("skill_manage", StringComparison.Ordinal));
        StringAssert.Contains(withSkills, "skill_manage");
        StringAssert.Contains(withSkills, "Skills that aren't maintained become liabilities.");
    }

    [TestMethod]
    public async Task SkillManager_BuildSkillsMandatoryPrompt_ListsAvailableSkills()
    {
        await CreateSkillAsync("mandatory-helper", "Mandatory prompt helper");

        var prompt = _skillManager.BuildSkillsMandatoryPrompt();

        StringAssert.Contains(prompt, "## Skills (mandatory)");
        StringAssert.Contains(prompt, "mandatory-helper: Mandatory prompt helper");
        StringAssert.Contains(prompt, "MUST load it with skill_view(name)");
    }

    [TestMethod]
    public async Task MemoryReviewService_CombinedReview_ExecutesMemoryAndSkillToolCalls()
    {
        var reviewClient = new SequenceChatClient(
            new ChatResponse
            {
                ToolCalls = new List<ToolCall>
                {
                    new()
                    {
                        Id = "list",
                        Name = "skills_list",
                        Arguments = "{}"
                    }
                }
            },
            new ChatResponse
            {
                ToolCalls = new List<ToolCall>
                {
                    new()
                    {
                        Id = "create",
                        Name = "skill_manage",
                        Arguments = JsonSerializer.Serialize(new
                        {
                            action = "create",
                            name = "review-created",
                            content = "---\nname: review-created\ndescription: Created by review\ntools: session_search\n---\n\nUse this later."
                        })
                    },
                    new()
                    {
                        Id = "memory",
                        Name = "memory",
                        Arguments = "{\"action\":\"add\",\"target\":\"user\",\"content\":\"User prefers parity-first work.\"}"
                    }
                }
            },
            new ChatResponse { Content = "Done." });
        var memoryManager = new MemoryManager(_tempDir, reviewClient, NullLogger<MemoryManager>.Instance);
        var service = new MemoryReviewService(
            reviewClient,
            memoryManager,
            NullLogger<MemoryReviewService>.Instance,
            pluginManager: null,
            nudgeInterval: 1,
            skillManager: _skillManager,
            skillNudgeInterval: 1);

        var results = await service.ReviewConversationAsync(
            new[]
            {
                new Message { Role = "user", Content = "We fixed a tricky issue." },
                new Message { Role = "assistant", Content = "Resolved with a reusable workflow." }
            },
            reviewMemory: true,
            reviewSkills: true,
            CancellationToken.None);

        Assert.IsTrue(results.Any(r => r.Success && r.Content.Contains("review-created", StringComparison.Ordinal)));
        Assert.IsTrue(results.Any(r => r.Success && r.Content.Contains("User prefers parity-first work", StringComparison.Ordinal)));
        Assert.IsNotNull(_skillManager.GetSkill("review-created"));
    }

    [TestMethod]
    public async Task MemoryReviewService_QueueAfterTurn_RaisesNotificationWhenNothingSaved()
    {
        var reviewClient = new ObservingReviewChatClient();
        var memoryManager = new MemoryManager(_tempDir, reviewClient, NullLogger<MemoryManager>.Instance);
        var service = new MemoryReviewService(
            reviewClient,
            memoryManager,
            NullLogger<MemoryReviewService>.Instance,
            nudgeInterval: 0,
            skillManager: _skillManager,
            skillNudgeInterval: 1);
        var completed = new TaskCompletionSource<BackgroundReviewNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.BackgroundReviewCompleted += notification => completed.TrySetResult(notification);

        var queued = service.QueueAfterTurn(
            "queue-session",
            new[]
            {
                new Message { Role = "user", Content = "We used a tool." },
                new Message { Role = "assistant", Content = "Done." }
            },
            finalResponse: "Done.",
            interrupted: false,
            toolIterations: 1,
            skillManageUsed: false);

        Assert.IsTrue(queued);

        var winner = await Task.WhenAny(completed.Task, Task.Delay(1000));
        Assert.AreSame(completed.Task, winner, "Background review notification should be raised.");

        var notification = await completed.Task;
        Assert.AreEqual("queue-session", notification.SessionId);
        Assert.IsTrue(notification.ReviewSkills);
        Assert.IsFalse(notification.ReviewMemory);
        Assert.IsTrue(notification.Success);
        Assert.IsFalse(notification.HasActions);
        StringAssert.Contains(notification.Summary, "nothing to save");
    }

    [TestMethod]
    public async Task Agent_SkillReviewNudge_TriggersAfterConfiguredToolIterations()
    {
        var mainClient = new SequenceChatClient(
            new ChatResponse
            {
                ToolCalls = new List<ToolCall>
                {
                    new() { Id = "list", Name = "skills_list", Arguments = "{}" }
                }
            },
            new ChatResponse { Content = "done" });
        var reviewClient = new ObservingReviewChatClient();
        var memoryManager = new MemoryManager(_tempDir, reviewClient, NullLogger<MemoryManager>.Instance);
        var reviewService = new MemoryReviewService(
            reviewClient,
            memoryManager,
            NullLogger<MemoryReviewService>.Instance,
            nudgeInterval: 0,
            skillManager: _skillManager,
            skillNudgeInterval: 1);
        var agent = new Agent(mainClient, NullLogger<Agent>.Instance, memoryReviewService: reviewService);
        agent.RegisterTool(new SkillsListTool(_skillManager));
        agent.RegisterTool(new SkillViewTool(_skillManager));
        agent.RegisterTool(new SkillManageTool(_skillManager));

        var response = await agent.ChatAsync("use a tool", new Session { Id = "skill-review-trigger" }, CancellationToken.None);

        Assert.AreEqual("done", response);
        var completed = await Task.WhenAny(reviewClient.Called.Task, Task.Delay(1000));
        Assert.AreSame(reviewClient.Called.Task, completed, "Skill review should run after configured tool iterations.");
        CollectionAssert.Contains(reviewClient.LastToolNames.ToList(), "skills_list");
        CollectionAssert.Contains(reviewClient.LastToolNames.ToList(), "skill_manage");
        await WaitForAsync(
            () => agent.ActivityLog.Any(entry => entry.ToolName == "background_review"),
            "Background review activity should be recorded.");
        var reviewEntry = agent.ActivityLog.Last(entry => entry.ToolName == "background_review");
        StringAssert.Contains(reviewEntry.OutputSummary, "nothing to save");
    }

    [TestMethod]
    public async Task Agent_SkillReviewNudge_ResetsWhenSkillManageIsUsed()
    {
        var skillContent = """
            ---
            name: reset-created
            description: Created in foreground
            tools: session_search
            ---

            Use this later.
            """;
        var mainClient = new SequenceChatClient(
            new ChatResponse
            {
                ToolCalls = new List<ToolCall>
                {
                    new()
                    {
                        Id = "create",
                        Name = "skill_manage",
                        Arguments = JsonSerializer.Serialize(new
                        {
                            action = "create",
                            name = "reset-created",
                            content = skillContent
                        })
                    }
                }
            },
            new ChatResponse { Content = "done" });
        var reviewClient = new ObservingReviewChatClient();
        var memoryManager = new MemoryManager(_tempDir, reviewClient, NullLogger<MemoryManager>.Instance);
        var reviewService = new MemoryReviewService(
            reviewClient,
            memoryManager,
            NullLogger<MemoryReviewService>.Instance,
            nudgeInterval: 0,
            skillManager: _skillManager,
            skillNudgeInterval: 1);
        var agent = new Agent(mainClient, NullLogger<Agent>.Instance, memoryReviewService: reviewService);
        agent.RegisterTool(new SkillManageTool(_skillManager));

        var response = await agent.ChatAsync("create a skill", new Session { Id = "skill-review-reset" }, CancellationToken.None);

        Assert.AreEqual("done", response);
        Assert.IsNotNull(_skillManager.GetSkill("reset-created"));
        await Task.Delay(200);
        Assert.AreEqual(0, reviewClient.CompleteWithToolsCalls);
    }

    private async Task CreateSkillAsync(string name, string description = "Test skill")
    {
        var content = $"""
            ---
            name: {name}
            description: {description}
            tools: session_search
            ---

            Skill instructions for {name}.
            """;

        await _skillManager.CreateSkillFromContentAsync(name, content, category: null, CancellationToken.None);
    }

    private static async Task WaitForAsync(Func<bool> condition, string message, int timeoutMs = 1000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;

            await Task.Delay(25);
        }

        Assert.Fail(message);
    }

    private sealed class StubChatClient : IChatClient
    {
        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct) => Task.FromResult("");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
            => Task.FromResult(new ChatResponse { Content = "" });

        public async IAsyncEnumerable<string> StreamAsync(IEnumerable<Message> messages, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<StreamEvent> StreamAsync(
            string? systemPrompt,
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition>? tools = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class SequenceChatClient : IChatClient
    {
        private readonly Queue<ChatResponse> _responses;

        public SequenceChatClient(params ChatResponse[] responses)
        {
            _responses = new Queue<ChatResponse>(responses);
        }

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct) => Task.FromResult("");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
            => Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : new ChatResponse { Content = "Done." });

        public async IAsyncEnumerable<string> StreamAsync(IEnumerable<Message> messages, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<StreamEvent> StreamAsync(
            string? systemPrompt,
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition>? tools = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class ObservingReviewChatClient : IChatClient
    {
        public int CompleteWithToolsCalls { get; private set; }
        public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyList<string> LastToolNames { get; private set; } = Array.Empty<string>();

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct) => Task.FromResult("");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            CompleteWithToolsCalls++;
            LastToolNames = tools.Select(t => t.Name).ToList();
            Called.TrySetResult();
            return Task.FromResult(new ChatResponse { Content = "Nothing to save." });
        }

        public async IAsyncEnumerable<string> StreamAsync(IEnumerable<Message> messages, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<StreamEvent> StreamAsync(
            string? systemPrompt,
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition>? tools = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
