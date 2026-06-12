using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.Core.Coaching;
using TypingTrainer.Core.Lessons;
using TypingTrainer.Core.Models;
using TypingTrainer.Core.Review;
using TypingTrainer.Core.Typing;

namespace TypingTrainer.Core.Tests;

[TestClass]
public sealed class CoachingTests
{
    [TestMethod]
    public void TypingCoach_NoData_BuildsBaselinePlan()
    {
        var plan = new TypingCoach().BuildDailyPlan(
            Stats(sessionCount: 0),
            TrainingFocus.Balanced,
            targetSessionMinutes: 15,
            targetEssayWords: 1000);

        Assert.AreEqual("Build baseline", plan.Steps[0].Title);
        Assert.AreEqual(LessonMode.Paragraph, plan.Steps[0].RecommendedMode);
    }

    [TestMethod]
    public void TypingCoach_LowAccuracy_UsesWeakKeys()
    {
        var plan = new TypingCoach().BuildDailyPlan(
            Stats(accuracy: 0.88, weakestKey: "f"),
            TrainingFocus.Balanced,
            targetSessionMinutes: 15,
            targetEssayWords: 1000);

        Assert.AreEqual(LessonMode.WeakKeys, plan.Steps[0].RecommendedMode);
        StringAssert.Contains(plan.Steps[0].Description, "f");
    }

    [TestMethod]
    public void TypingCoach_SpeedGap_UsesParagraphAndWeakBigrams()
    {
        var plan = new TypingCoach().BuildDailyPlan(
            Stats(accuracy: 0.98, averageNetWpm: 42, slowestBigram: "t h"),
            TrainingFocus.Balanced,
            targetSessionMinutes: 15,
            targetEssayWords: 1000);

        Assert.AreEqual(LessonMode.Paragraph, plan.Steps[0].RecommendedMode);
        Assert.AreEqual(LessonMode.WeakBigrams, plan.Steps[1].RecommendedMode);
    }

    [TestMethod]
    public void TypingCoach_EssayFocus_UsesLongParagraphTarget()
    {
        var plan = new TypingCoach().BuildDailyPlan(
            Stats(accuracy: 0.98, averageNetWpm: 70),
            TrainingFocus.EssayEndurance,
            targetSessionMinutes: 20,
            targetEssayWords: 1000);

        Assert.AreEqual(LessonMode.Paragraph, plan.Steps[0].RecommendedMode);
        Assert.AreEqual(5000, plan.Steps[0].TargetLength);
    }

    [TestMethod]
    public void MistakeReplayGenerator_PrioritizesMistakesDeterministically()
    {
        var review = new SessionReviewGenerator().Generate(
            Summary(),
            [
                Character('f', 'x', false, 0, null),
                Character('j', 'j', true, 1, 120),
                Character('f', 'd', false, 2, 130)
            ]);
        var generator = new MistakeReplayGenerator();

        var first = generator.Generate(review, Array.Empty<TypingInputEvent>(), 120, randomSeed: 4);
        var second = generator.Generate(review, Array.Empty<TypingInputEvent>(), 120, randomSeed: 4);

        Assert.AreEqual(first.Text, second.Text);
        StringAssert.Contains(first.Text, "f");
    }

    [TestMethod]
    public void AchievementEvaluator_UnlocksMilestones()
    {
        var achievements = new AchievementEvaluator().Evaluate(Stats(
            sessionCount: 25,
            averageNetWpm: 55,
            bestNetWpm: 62,
            accuracy: 0.96,
            bestAccuracy: 0.97,
            weeklyPracticeMinutes: 90,
            currentPracticeStreakDays: 7));

        Assert.IsTrue(achievements.Single(item => item.Id == "twenty-five-sessions").IsUnlocked);
        Assert.IsTrue(achievements.Single(item => item.Id == "net-60").IsUnlocked);
        Assert.IsTrue(achievements.Single(item => item.Id == "seven-day-streak").IsUnlocked);
        Assert.IsTrue(achievements.Single(item => item.Id == "weekly-target").IsUnlocked);
    }

    private static CoachingStats Stats(
        int sessionCount = 10,
        double averageNetWpm = 50,
        double bestNetWpm = 55,
        double accuracy = 0.96,
        double bestAccuracy = 0.96,
        double weeklyPracticeMinutes = 40,
        int currentPracticeStreakDays = 2,
        string? weakestKey = null,
        string? slowestBigram = null)
    {
        return new CoachingStats(
            sessionCount,
            averageNetWpm,
            bestNetWpm,
            accuracy,
            bestAccuracy,
            weeklyPracticeMinutes,
            GoalTargetNetWpm: 60,
            GoalTargetAccuracyPercent: 95,
            GoalWeeklyPracticeMinutes: 75,
            currentPracticeStreakDays,
            weakestKey,
            slowestBigram);
    }

    private static SessionSummary Summary()
    {
        return new SessionSummary(Guid.NewGuid(), "fjf", true, 3, 1, 2, 0, 2, 1000, 10, 1 / 3.0);
    }

    private static TypingInputEvent Character(char expected, char actual, bool correct, int position, double? deltaMs)
    {
        return new TypingInputEvent(
            Guid.NewGuid(),
            position,
            expected,
            actual,
            InputEventKind.Character,
            correct,
            WasCorrection: false,
            TimestampTicks: position + 1,
            ElapsedMs: deltaMs ?? 0,
            deltaMs);
    }
}

