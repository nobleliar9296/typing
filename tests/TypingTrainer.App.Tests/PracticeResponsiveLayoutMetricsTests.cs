using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.App.Services;

namespace TypingTrainer.App.Tests;

[TestClass]
public sealed class PracticeResponsiveLayoutMetricsTests
{
    [TestMethod]
    public void FromViewport_DesktopWidthKeepsSideStatsAndDesktopKeyboardCap()
    {
        var metrics = Create(width: 1366, height: 768);

        Assert.IsFalse(metrics.CompactStats);
        Assert.IsFalse(metrics.StackedSelectors);
        Assert.AreEqual(1280, metrics.KeyboardMaxWidth);
        Assert.IsNull(metrics.KpiTileWidth);
        Assert.IsTrue(metrics.PracticeTextMaxWidth <= 1040);
    }

    [TestMethod]
    public void FromViewport_NarrowWidthStacksControlsAndUsesCompactStats()
    {
        var metrics = Create(width: 900, height: 700);

        Assert.IsTrue(metrics.CompactStats);
        Assert.IsTrue(metrics.StackedSelectors);
        Assert.IsTrue(metrics.CompactHeader);
        Assert.IsTrue(metrics.VeryCompactHeader);
        Assert.IsNotNull(metrics.KpiTileWidth);
        Assert.IsTrue(metrics.PracticeTextMaxWidth <= 900);
        Assert.IsTrue(metrics.KeyboardMaxWidth <= 900);
    }

    [TestMethod]
    public void FromViewport_ShortHeightReducesScaleAndTextHeight()
    {
        var tall = Create(width: 1200, height: 980);
        var shortWindow = Create(width: 1200, height: 620);

        Assert.IsTrue(shortWindow.Scale < tall.Scale);
        Assert.IsTrue(shortWindow.KeyboardScale < tall.KeyboardScale);
        Assert.IsTrue(shortWindow.PracticeTextMaxHeight <= tall.PracticeTextMaxHeight);
    }

    [TestMethod]
    public void FromViewport_UserTextAndKeyboardScalesAreAppliedAfterViewportScale()
    {
        var normal = Create(width: 1200, height: 820, practiceTextScale: 1.0, visualKeyboardScale: 1.0);
        var enlarged = Create(width: 1200, height: 820, practiceTextScale: 1.2, visualKeyboardScale: 1.2);

        Assert.IsTrue(enlarged.PracticeTextDisplayScale > normal.PracticeTextDisplayScale);
        Assert.IsTrue(enlarged.KeyboardScale > normal.KeyboardScale);
        Assert.IsTrue(enlarged.FourLineTextHeight > normal.FourLineTextHeight);
    }

    [TestMethod]
    public void FromViewport_ReviewPopupFitsInsideViewport()
    {
        var metrics = Create(width: 760, height: 620);

        Assert.IsTrue(metrics.ReviewMaxWidth <= 760);
        Assert.IsTrue(metrics.ReviewMaxHeight <= 620);
        Assert.IsTrue(metrics.ReviewMargin <= 48);
        Assert.IsTrue(metrics.ReviewPadding <= 28);
    }

    [TestMethod]
    public void FromViewport_RejectsInvalidViewport()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Create(width: 0, height: 620));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Create(width: 760, height: -1));
    }

    private static PracticeResponsiveLayoutMetrics Create(
        double width,
        double height,
        double practiceTextScale = 1.0,
        double visualKeyboardScale = 1.0)
    {
        return PracticeResponsiveLayoutMetrics.FromViewport(
            width,
            height,
            headerActualHeight: 0,
            statsVisible: true,
            practiceTextScale,
            visualKeyboardScale,
            practiceLineWidthMax: 1040);
    }
}
