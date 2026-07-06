using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.App.Services;

namespace TypingTrainer.App.Tests;

[TestClass]
public sealed class AppResponsiveLayoutMetricsTests
{
    [TestMethod]
    public void FromViewport_Desktop_KeepsDesktopSpacing()
    {
        var metrics = AppResponsiveLayoutMetrics.FromViewport(1440, 950);

        Assert.IsFalse(metrics.CompactWidth);
        Assert.IsFalse(metrics.NarrowWidth);
        Assert.AreEqual(32, metrics.PageHorizontalPadding);
        Assert.AreEqual(24, metrics.ContentSpacing);
        Assert.AreEqual(180, metrics.FilterControlWidth);
        Assert.AreEqual(1120, metrics.MaxContentWidth);
    }

    [TestMethod]
    public void FromViewport_TabletWidth_UsesCompactLayout()
    {
        var metrics = AppResponsiveLayoutMetrics.FromViewport(850, 760);

        Assert.IsTrue(metrics.CompactWidth);
        Assert.IsFalse(metrics.NarrowWidth);
        Assert.IsTrue(double.IsPositiveInfinity(metrics.MaxContentWidth));
        Assert.IsTrue(metrics.PageHorizontalPadding < 32);
        Assert.IsTrue(metrics.ContentSpacing < 24);
    }

    [TestMethod]
    public void FromViewport_NarrowWidth_ShrinksControls()
    {
        var metrics = AppResponsiveLayoutMetrics.FromViewport(520, 720);

        Assert.IsTrue(metrics.NarrowWidth);
        Assert.IsTrue(metrics.VeryNarrowWidth);
        Assert.IsTrue(metrics.FilterControlWidth <= 150);
        Assert.IsTrue(metrics.FormLabelWidth <= 130);
        Assert.IsTrue(metrics.CompactFormControlWidth <= 130);
    }

    [TestMethod]
    public void FromViewport_ShortHeight_ReducesTableAndChartHeight()
    {
        var metrics = AppResponsiveLayoutMetrics.FromViewport(1200, 620);

        Assert.IsTrue(metrics.ShortHeight);
        Assert.AreEqual(190, metrics.TableMaxHeight);
        Assert.AreEqual(190, metrics.ChartMinHeight);
        Assert.IsTrue(metrics.PageTopPadding < 24);
    }

    [TestMethod]
    public void FromViewport_InvalidViewport_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => AppResponsiveLayoutMetrics.FromViewport(0, 720));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => AppResponsiveLayoutMetrics.FromViewport(720, -1));
    }
}
