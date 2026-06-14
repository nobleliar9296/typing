using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.Core.Coaching;
using TypingTrainer.Core.Keyboard;
using TypingTrainer.Core.Learning;
using TypingTrainer.Core.Lessons;
using TypingTrainer.Core.Skill;

namespace TypingTrainer.Core.Tests;

[TestClass]
public sealed class LearningQualityTests
{
    [TestMethod]
    public void MasteryScorer_ProgressesFromNewToLearningToMastered()
    {
        var scorer = new MasteryScorer();
        var now = new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero);

        var newResult = scorer.Score(Input(sessionExposure: 4, sessionCorrect: 4), now);
        var learningResult = scorer.Score(Input(sessionExposure: 12, sessionCorrect: 11), now);
        var masteredResult = scorer.Score(Input(sessionExposure: 34, sessionCorrect: 34, medianLatencyMs: 120), now);

        Assert.AreEqual(MasteryState.New, newResult.MasteryState);
        Assert.AreEqual(MasteryState.Learning, learningResult.MasteryState);
        Assert.AreEqual(MasteryState.Mastered, masteredResult.MasteryState);
    }

    [TestMethod]
    public void MasteryScorer_MasteredItemRegressesAfterRecentDrop()
    {
        var scorer = new MasteryScorer();
        var now = new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero);

        var result = scorer.Score(new LearningProgressInput(
            LearningItemType.Character,
            "f",
            PreviousExposureCount: 40,
            PreviousCorrectCount: 40,
            PreviousIncorrectCount: 0,
            PreviousMasteryState: MasteryState.Mastered,
            PreviousIntervalDays: 7,
            PreviousEaseFactor: 2.2,
            SessionExposureCount: 5,
            SessionCorrectCount: 3,
            SessionIncorrectCount: 2,
            SessionMedianLatencyMs: 220), now);

        Assert.AreEqual(MasteryState.Regressing, result.MasteryState);
        Assert.AreEqual(now, result.NextDueUtc);
    }

    [TestMethod]
    public void SpacedReviewScheduler_GrowsSuccessfulIntervalsAndResetsWeakItems()
    {
        var now = new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero);

        var success = SpacedReviewScheduler.Schedule(
            now,
            previousIntervalDays: 2,
            previousEaseFactor: 2.0,
            MasteryState.Mastered,
            sessionAccuracy: 1.0,
            hadIncorrectInput: false);
        var weak = SpacedReviewScheduler.Schedule(
            now,
            previousIntervalDays: 5,
            previousEaseFactor: 2.0,
            MasteryState.Unstable,
            sessionAccuracy: 0.70,
            hadIncorrectInput: true);

        Assert.IsTrue(success.IntervalDays >= 4);
        Assert.IsTrue(success.NextDueUtc > now);
        Assert.AreEqual(0, weak.IntervalDays);
        Assert.AreEqual(now, weak.NextDueUtc);
    }

    [TestMethod]
    public void MistakeCauseClassifier_ClassifiesCommonMistakeCauses()
    {
        var classifier = new MistakeCauseClassifier();

        Assert.AreEqual(MistakeCause.AdjacentKey, classifier.Classify('f', 'g', 160, 1_000, 10_000));
        Assert.AreEqual(MistakeCause.SameFinger, classifier.Classify('r', 'f', 160, 1_000, 10_000));
        Assert.AreEqual(MistakeCause.WrongHand, classifier.Classify('f', 'j', 160, 1_000, 10_000));
        Assert.AreEqual(MistakeCause.ShiftIssue, classifier.Classify('?', '/', 160, 1_000, 10_000));
        Assert.AreEqual(MistakeCause.Punctuation, classifier.Classify(',', 'm', 160, 1_000, 10_000));
        Assert.AreEqual(MistakeCause.NumberRow, classifier.Classify('4', 'r', 160, 1_000, 10_000));
        Assert.AreEqual(MistakeCause.Rushed, classifier.Classify('q', 'm', 60, 1_000, 10_000));
        Assert.AreEqual(MistakeCause.Fatigue, classifier.Classify('q', 'm', 160, 9_000, 10_000));
    }

    [TestMethod]
    public void AdaptiveLessonGenerator_PrioritizesDueLearningTargets()
    {
        var profile = CreateProfileWithDueTarget('f');
        var generator = new AdaptiveLessonGenerator(new BuiltInWordListProvider(), new CharacterUnlockPlanner());

        var lesson = generator.Generate(
            profile,
            new LessonGenerationOptions(
                LessonMode.Adaptive,
                LessonLengthKind.Characters,
                120,
                KeyboardLayoutRepository.Qwerty,
                RandomSeed: 5));

        CollectionAssert.Contains(lesson.FocusCharacters.ToArray(), 'f');
        StringAssert.Contains(lesson.Reason, "Spaced review: f");
        Assert.AreEqual("f", lesson.LearningTargets.Single().Target);
    }

    [DataTestMethod]
    [DataRow(MistakeCause.AdjacentKey)]
    [DataRow(MistakeCause.SameFinger)]
    [DataRow(MistakeCause.ShiftIssue)]
    [DataRow(MistakeCause.Punctuation)]
    [DataRow(MistakeCause.NumberRow)]
    [DataRow(MistakeCause.Rushed)]
    [DataRow(MistakeCause.Fatigue)]
    public void MistakeCauseDrillGenerator_CreatesCauseSpecificDrills(MistakeCause cause)
    {
        var generator = new MistakeCauseDrillGenerator();

        var lesson = generator.Generate(
            new MistakeCauseDrillRequest(
                cause,
                ['f', 'j', '?', '4'],
                ["th", "fr"],
                AllowCapitalLetters: true,
                AllowNumbers: true,
                AllowPunctuation: true,
                RandomSeed: 9),
            targetCharacters: 120);

        Assert.IsFalse(string.IsNullOrWhiteSpace(lesson.Text));
        Assert.IsTrue(lesson.Text.Length >= 80);
        StringAssert.Contains(lesson.Reason, "Micro-drill");
    }

    [TestMethod]
    public void MistakeCauseDrillGenerator_RespectsPracticeFilters()
    {
        var generator = new MistakeCauseDrillGenerator();

        var lesson = generator.Generate(
            new MistakeCauseDrillRequest(
                MistakeCause.Punctuation,
                ['F', '?', '4'],
                ["F4", "A?"],
                AllowCapitalLetters: false,
                AllowNumbers: false,
                AllowPunctuation: false,
                RandomSeed: 11),
            targetCharacters: 140);

        Assert.IsFalse(lesson.Text.Any(char.IsUpper));
        Assert.IsFalse(lesson.Text.Any(char.IsDigit));
        Assert.IsFalse(lesson.Text.Any(char.IsPunctuation));
    }

    private static LearningProgressInput Input(
        int sessionExposure,
        int sessionCorrect,
        double? medianLatencyMs = 140)
    {
        return new LearningProgressInput(
            LearningItemType.Character,
            "f",
            PreviousExposureCount: 0,
            PreviousCorrectCount: 0,
            PreviousIncorrectCount: 0,
            MasteryState.New,
            PreviousIntervalDays: 0,
            PreviousEaseFactor: 2.0,
            sessionExposure,
            sessionCorrect,
            sessionExposure - sessionCorrect,
            medianLatencyMs);
    }

    private static UserSkillProfile CreateProfileWithDueTarget(char target)
    {
        var characters = "abcdefghijklmnopqrstuvwxyz".ToDictionary(
            character => character,
            character => new CharacterSkill(
                character,
                ExposureCount: 80,
                CorrectCount: character == target ? 64 : 80,
                IncorrectCount: character == target ? 16 : 0,
                Accuracy: character == target ? 0.8 : 1.0,
                MedianLatencyMs: character == target ? 260 : 120,
                AverageLatencyMs: character == target ? 270 : 125,
                WeaknessScore: character == target ? 0.8 : 0,
                ConfidenceScore: CharacterUnlockPlanner.CalculateConfidence(80, character == target ? 0.8 : 1.0, character == target ? 260 : 120)));

        return new UserSkillProfile(
            characters,
            new Dictionary<string, BigramSkill>(),
            CompletedSessionCount: 8,
            TotalPracticeTime: TimeSpan.FromMinutes(30),
            CreatedAtUtc: new DateTime(2026, 6, 13),
            DueLearningTargets:
            [
                new LearningTarget(
                    LearningItemType.Character,
                    target.ToString(),
                    MasteryState.Unstable,
                    WeaknessScore: 0.8,
                    StabilityScore: 0.4,
                    ExposureCount: 80,
                    Accuracy: 0.8,
                    MedianLatencyMs: 260,
                    NextDueUtc: new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero),
                    PrimaryMistakeCause: MistakeCause.AdjacentKey)
            ],
            MasterySummary: new MasterySummary(0, 5, 1, 20, 0, 1));
    }
}
