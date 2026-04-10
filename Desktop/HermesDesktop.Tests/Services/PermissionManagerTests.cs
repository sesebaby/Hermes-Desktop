using Hermes.Agent.Permissions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Services;

[TestClass]
public class PermissionManagerTests
{
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
}
