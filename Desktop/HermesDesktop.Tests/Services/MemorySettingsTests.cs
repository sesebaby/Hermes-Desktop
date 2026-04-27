using Hermes.Agent.Memory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Services;

[TestClass]
public class MemorySettingsTests
{
    [TestMethod]
    public void IsEnabled_DefaultsMissingValuesToEnabledForPythonParity()
    {
        Assert.IsTrue(MemorySettings.IsEnabled(null));
        Assert.IsTrue(MemorySettings.IsEnabled(""));
        Assert.IsTrue(MemorySettings.IsEnabled("   "));
    }

    [TestMethod]
    public void IsEnabled_HonorsExplicitFalse()
    {
        Assert.IsFalse(MemorySettings.IsEnabled("false"));
        Assert.IsFalse(MemorySettings.IsEnabled("FALSE"));
    }
}
