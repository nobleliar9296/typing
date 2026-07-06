using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.App.Services;

namespace TypingTrainer.App.Tests;

[TestClass]
public sealed class DpiHelperTests
{
    [DataTestMethod]
    [DataRow(96u, 1.0)]
    [DataRow(120u, 1.25)]
    [DataRow(144u, 1.5)]
    [DataRow(168u, 1.75)]
    [DataRow(192u, 2.0)]
    public void GetScaleFactor_ConvertsDpiToWindowsScale(uint dpi, double expectedScale)
    {
        Assert.AreEqual(expectedScale, DpiHelper.GetScaleFactor(dpi), 0.0001);
    }

    [TestMethod]
    public void GetScaleFactor_ZeroDpiFallsBackToOne()
    {
        Assert.AreEqual(1.0, DpiHelper.GetScaleFactor(0), 0.0001);
    }

    [TestMethod]
    public void LogicalAndPhysicalConversions_UseScaleFactor()
    {
        var scale = DpiHelper.GetScaleFactor(144);

        Assert.AreEqual(300, DpiHelper.LogicalToPhysical(200, scale), 0.0001);
        Assert.AreEqual(200, DpiHelper.PhysicalToLogical(300, scale), 0.0001);
    }

    [TestMethod]
    public void PhysicalToLogical_InvalidScale_ReturnsInput()
    {
        Assert.AreEqual(250, DpiHelper.PhysicalToLogical(250, 0), 0.0001);
    }
}
