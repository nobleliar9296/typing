using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.Core.Training;

namespace TypingTrainer.Core.Tests;

[TestClass]
public sealed class SessionQualityCalculatorTests
{
    [TestMethod]
    public void SessionQualityCalculator_RewardsHighAccuracyAndStrongNetWpm()
    {
        var result = new SessionQualityCalculator().Calculate(new SessionQualityInputs(
            Accuracy: 0.98,
            NetWpm: 72,
            TargetNetWpm: 60,
            Consistency: 0.95,
            CompletionRatio: 1,
            ControlRatio: 1));

        Assert.IsTrue(result.Score >= 95);
        Assert.AreEqual("A", result.Grade);
    }

    [TestMethod]
    public void SessionQualityCalculator_LowAccuracyReducesScore()
    {
        var result = new SessionQualityCalculator().Calculate(new SessionQualityInputs(
            Accuracy: 0.60,
            NetWpm: 80,
            TargetNetWpm: 60,
            Consistency: 1,
            CompletionRatio: 1,
            ControlRatio: 1));

        Assert.IsTrue(result.Score < 85);
    }

    [TestMethod]
    public void SessionQualityCalculator_PoorConsistencyReducesScore()
    {
        var stable = new SessionQualityCalculator().Calculate(new SessionQualityInputs(
            Accuracy: 0.95,
            NetWpm: 60,
            TargetNetWpm: 60,
            Consistency: 1,
            CompletionRatio: 1,
            ControlRatio: 1));
        var uneven = new SessionQualityCalculator().Calculate(new SessionQualityInputs(
            Accuracy: 0.95,
            NetWpm: 60,
            TargetNetWpm: 60,
            Consistency: 0.25,
            CompletionRatio: 1,
            ControlRatio: 1));

        Assert.IsTrue(stable.Score > uneven.Score);
        Assert.AreEqual(15, stable.Score - uneven.Score, 0.001);
    }

    [TestMethod]
    public void SessionQualityCalculator_ClampsScoreAndUsesGradeThresholds()
    {
        var calculator = new SessionQualityCalculator();
        var high = calculator.Calculate(new SessionQualityInputs(
            Accuracy: 5,
            NetWpm: 500,
            TargetNetWpm: 60,
            Consistency: 5,
            CompletionRatio: 5,
            ControlRatio: 5));
        var low = calculator.Calculate(new SessionQualityInputs(
            Accuracy: -1,
            NetWpm: -1,
            TargetNetWpm: 60,
            Consistency: -1,
            CompletionRatio: -1,
            ControlRatio: -1));

        Assert.AreEqual(100, high.Score);
        Assert.AreEqual("A", high.Grade);
        Assert.AreEqual(0, low.Score);
        Assert.AreEqual("Needs work", low.Grade);
        Assert.AreEqual("B", SessionQualityCalculator.GetGrade(80));
        Assert.AreEqual("C", SessionQualityCalculator.GetGrade(70));
        Assert.AreEqual("D", SessionQualityCalculator.GetGrade(60));
    }
}
