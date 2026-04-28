using Hermes.Agent.Permissions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Services;

[TestClass]
public class PermissionManagerTests
{
    private static PermissionManager CreateManager(
        PermissionMode mode,
        Action<PermissionContext>? configure = null)
    {
        var context = new PermissionContext { Mode = mode };
        configure?.Invoke(context);
        return new PermissionManager(context, NullLogger<PermissionManager>.Instance);
    }

    [TestMethod]
    public void Mode_ReflectsPermissionContextDefault()
    {
        var context = new PermissionContext { Mode = PermissionMode.Auto };
        var manager = new PermissionManager(context, NullLogger<PermissionManager>.Instance);

        Assert.AreEqual(PermissionMode.Auto, manager.Mode);
    }

    [TestMethod]
    public void Mode_SetterUpdatesUnderlyingContext()
    {
        var context = new PermissionContext { Mode = PermissionMode.Default };
        var manager = new PermissionManager(context, NullLogger<PermissionManager>.Instance);

        manager.Mode = PermissionMode.AcceptEdits;

        Assert.AreEqual(PermissionMode.AcceptEdits, context.Mode);
        Assert.AreEqual(PermissionMode.AcceptEdits, manager.Mode);
    }

    [TestMethod]
    public async Task CheckPermissionsAsync_BypassPermissions_AllowsAnyTool()
    {
        var manager = CreateManager(PermissionMode.BypassPermissions);
        const string input = "{\"path\":\"/tmp/file.txt\"}";

        var decision = await manager.CheckPermissionsAsync("todo_write", input, CancellationToken.None);

        Assert.AreEqual(PermissionBehavior.Allow, decision.Behavior);
        Assert.AreEqual(input, decision.UpdatedInput);
        Assert.IsTrue(decision.IsAllowed);
        Assert.IsFalse(decision.IsDenied);
    }

    [TestMethod]
    public async Task CheckPermissionsAsync_PlanMode_AllowsReadOnlyTools()
    {
        var manager = CreateManager(PermissionMode.Plan);

        var decision = await manager.CheckPermissionsAsync("session_search", "recent", CancellationToken.None);

        Assert.AreEqual(PermissionBehavior.Allow, decision.Behavior);
        Assert.IsTrue(decision.IsAllowed);
    }

    [TestMethod]
    public async Task CheckPermissionsAsync_PlanMode_DeniesMutatingTools()
    {
        var manager = CreateManager(PermissionMode.Plan);

        var decision = await manager.CheckPermissionsAsync("todo_write", "{\"items\":[]}", CancellationToken.None);

        Assert.AreEqual(PermissionBehavior.Deny, decision.Behavior);
        Assert.AreEqual("Cannot modify files in plan mode", decision.Message);
        Assert.IsTrue(decision.IsDenied);
    }

    [TestMethod]
    public async Task CheckPermissionsAsync_AlwaysAllowRule_OverridesDefaultAskBehavior()
    {
        var manager = CreateManager(PermissionMode.Default, context =>
            context.AlwaysAllow.Add(PermissionRule.AllowAll("todo_write")));

        var decision = await manager.CheckPermissionsAsync("todo_write", "{\"items\":[]}", CancellationToken.None);

        Assert.AreEqual(PermissionBehavior.Allow, decision.Behavior);
    }

    [TestMethod]
    public async Task CheckPermissionsAsync_AlwaysDenyRule_BlocksMatchingTool()
    {
        var manager = CreateManager(PermissionMode.Default, context =>
            context.AlwaysDeny.Add(PermissionRule.DenyAll("todo_write")));

        var decision = await manager.CheckPermissionsAsync("todo_write", "{\"items\":[]}", CancellationToken.None);

        Assert.AreEqual(PermissionBehavior.Deny, decision.Behavior);
        Assert.AreEqual("Blocked by permission rule", decision.Message);
    }

    [TestMethod]
    public async Task CheckPermissionsAsync_AlwaysAllowRule_TakesPrecedenceOverAlwaysDeny()
    {
        var manager = CreateManager(PermissionMode.Default, context =>
        {
            context.AlwaysAllow.Add(PermissionRule.AllowAll("todo_write"));
            context.AlwaysDeny.Add(PermissionRule.DenyAll("todo_write"));
        });

        var decision = await manager.CheckPermissionsAsync("todo_write", "{\"items\":[]}", CancellationToken.None);

        Assert.AreEqual(PermissionBehavior.Allow, decision.Behavior);
    }

    [TestMethod]
    public async Task CheckPermissionsAsync_AlwaysAskRule_ForcesPrompt()
    {
        var manager = CreateManager(PermissionMode.Auto, context =>
            context.AlwaysAsk.Add(PermissionRule.AllowAll("todo_write")));

        var decision = await manager.CheckPermissionsAsync("todo_write", "{\"items\":[]}", CancellationToken.None);

        Assert.AreEqual(PermissionBehavior.Ask, decision.Behavior);
        Assert.AreEqual("Requires permission: todo_write", decision.Message);
        Assert.IsTrue(decision.NeedsUserInput);
    }

    [TestMethod]
    public async Task CheckPermissionsAsync_AutoMode_AllowsReadOnlyToolWithReason()
    {
        var manager = CreateManager(PermissionMode.Auto);

        var decision = await manager.CheckPermissionsAsync("session_search", "recent", CancellationToken.None);

        Assert.AreEqual(PermissionBehavior.Allow, decision.Behavior);
        Assert.AreEqual("Auto-approved read-only operation", decision.DecisionReason);
    }

    [TestMethod]
    public async Task CheckPermissionsAsync_AutoMode_AsksForMutatingTool()
    {
        var manager = CreateManager(PermissionMode.Auto);

        var decision = await manager.CheckPermissionsAsync("todo_write", "{\"items\":[]}", CancellationToken.None);

        Assert.AreEqual(PermissionBehavior.Ask, decision.Behavior);
        Assert.AreEqual("Modify operation requires permission", decision.Message);
    }

    [TestMethod]
    public async Task CheckPermissionsAsync_AcceptEditsMode_AllowsInWorkspaceOperations()
    {
        var manager = CreateManager(PermissionMode.AcceptEdits);

        var decision = await manager.CheckPermissionsAsync("memory", "src/file.cs", CancellationToken.None);

        Assert.AreEqual(PermissionBehavior.Allow, decision.Behavior);
        Assert.AreEqual("Auto-approved: within workspace", decision.DecisionReason);
    }

    [TestMethod]
    public async Task CheckPermissionsAsync_PatternRule_MatchesGlobPattern()
    {
        var manager = CreateManager(PermissionMode.Default, context =>
            context.AlwaysDeny.Add(PermissionRule.AllowPattern("skill_invoke", "skill:*")));

        var blocked = await manager.CheckPermissionsAsync("skill_invoke", "skill:review", CancellationToken.None);
        var notBlocked = await manager.CheckPermissionsAsync("skill_invoke", "plain", CancellationToken.None);

        Assert.AreEqual(PermissionBehavior.Deny, blocked.Behavior);
        Assert.AreEqual(PermissionBehavior.Ask, notBlocked.Behavior);
    }

    [TestMethod]
    public async Task CheckPermissionsAsync_WildcardToolRule_MatchesAnyTool()
    {
        var manager = CreateManager(PermissionMode.Default, context =>
            context.AlwaysDeny.Add(PermissionRule.AllowPattern("*", "*.secrets*")));

        var decision = await manager.CheckPermissionsAsync("memory", "/workspace/.secrets/config.yaml", CancellationToken.None);

        Assert.AreEqual(PermissionBehavior.Deny, decision.Behavior);
    }

    [TestMethod]
    public async Task CheckPermissionsAsync_UnknownMode_FallsBackToAsk()
    {
        var manager = CreateManager((PermissionMode)999);

        var decision = await manager.CheckPermissionsAsync("session_search", "/workspace/readme.md", CancellationToken.None);

        Assert.AreEqual(PermissionBehavior.Ask, decision.Behavior);
        StringAssert.Contains(decision.Message ?? string.Empty, "Unknown mode");
    }

    [TestMethod]
    public async Task AddAlwaysAllowRule_DefaultMode_PreventsFuturePermissionPrompts()
    {
        var manager = CreateManager(PermissionMode.Default);
        var added = manager.AddAlwaysAllowRule("todo_write");

        var decision = await manager.CheckPermissionsAsync("todo_write", "{\"items\":[]}", CancellationToken.None);

        Assert.IsTrue(added);
        Assert.AreEqual(PermissionBehavior.Allow, decision.Behavior);
    }

    [TestMethod]
    public void AddAlwaysAllowRule_DuplicateRule_ReturnsFalse()
    {
        var manager = CreateManager(PermissionMode.Default);

        var first = manager.AddAlwaysAllowRule("todo_write");
        var second = manager.AddAlwaysAllowRule("todo_write");

        Assert.IsTrue(first);
        Assert.IsFalse(second);
    }

    [TestMethod]
    public void AddAlwaysAllowRule_InvalidToolName_ThrowsArgumentException()
    {
        var manager = CreateManager(PermissionMode.Default);

        Assert.ThrowsException<ArgumentException>(() => manager.AddAlwaysAllowRule("   "));
    }

    [TestMethod]
    public void HasAlwaysAllowRule_RespectsCaseInsensitiveRuleMatching()
    {
        var manager = CreateManager(PermissionMode.Default);
        manager.AddAlwaysAllowRule("todo_write");

        Assert.IsTrue(manager.HasAlwaysAllowRule("TODO_WRITE"));
    }

    [TestMethod]
    public void ClearAlwaysAllowRules_RemovesAllRememberedRules()
    {
        var manager = CreateManager(PermissionMode.Default);
        manager.AddAlwaysAllowRule("todo_write");
        manager.AddAlwaysAllowRule("memory");

        manager.ClearAlwaysAllowRules();

        Assert.IsFalse(manager.HasAlwaysAllowRule("todo_write"));
        Assert.IsFalse(manager.HasAlwaysAllowRule("memory"));
        Assert.AreEqual(0, manager.GetAlwaysAllowRulesSnapshot().Count);
    }
}
