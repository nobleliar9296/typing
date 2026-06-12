using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.Core.Keyboard;
using TypingTrainer.Core.Lessons;
using TypingTrainer.Core.Models;
using TypingTrainer.Core.Review;
using TypingTrainer.Core.Skill;
using TypingTrainer.Core.Typing;

namespace TypingTrainer.Core.Tests;

[TestClass]
public sealed class SessionReviewGeneratorTests
{
    [TestMethod]
    public void SessionReviewGenerator_RanksMissedKeys()
    {
        var review = new SessionReviewGenerator().Generate(
            Summary(currentErrors: 3),
            [
                Character('f', 'x', false, 1, null),
                Character('j', 'x', false, 2, 100),
                Character('f', 'd', false, 3, 100)
            ]);

        Assert.AreEqual('f', review.MostMissedKeys[0].Character);
        Assert.AreEqual(2, review.MostMissedKeys[0].IncorrectCount);
    }

    [TestMethod]
    public void SessionReviewGenerator_CountsCorrectedAndUncorrectedErrors()
    {
        var review = new SessionReviewGenerator().Generate(
            Summary(currentErrors: 1),
            [
                Character('f', 'x', false, 1, null),
                Backspace('f', 'x', 2)
            ]);

        Assert.AreEqual(1, review.CorrectedErrors);
        Assert.AreEqual(1, review.UncorrectedErrors);
    }

    [TestMethod]
    public void SessionReviewGenerator_ExcludesLongPausesFromLatency()
    {
        var review = new SessionReviewGenerator().Generate(
            Summary(currentErrors: 0),
            [
                Character('a', 'a', true, 1, null),
                Character('b', 'b', true, 2, 5000),
                Character('c', 'c', true, 3, 120)
            ]);

        Assert.IsFalse(review.SlowestKeys.Any(row => row.Character == 'b'));
        Assert.AreEqual(120, review.SlowestKeys.Single(row => row.Character == 'c').MedianLatencyMs);
    }

    [TestMethod]
    public void SessionReviewGenerator_EmptySessionProducesSafeReview()
    {
        var review = new SessionReviewGenerator().Generate(
            Summary(currentErrors: 0, typed: 0),
            Array.Empty<TypingInputEvent>());

        Assert.AreEqual(0, review.MostMissedKeys.Count);
        Assert.AreEqual(0, review.SlowestKeys.Count);
        Assert.AreEqual(0, review.WeakestBigrams.Count);
        Assert.IsTrue(review.Notes.Count > 0);
    }

    [TestMethod]
    public void SessionReviewGenerator_CreatePracticeProfile_UsesReviewWeaknessData()
    {
        var generator = new SessionReviewGenerator();
        var review = generator.Generate(
            Summary(currentErrors: 2),
            [
                Character('f', 'x', false, 1, null),
                Character('j', 'j', true, 2, 140),
                Character('f', 'd', false, 3, 150)
            ]);

        var profile = generator.CreatePracticeProfile(review, new DateTime(2026, 6, 11));

        Assert.IsTrue(profile.Characters.ContainsKey('f'));
        Assert.IsTrue(profile.Bigrams.Count > 0);
        Assert.IsTrue(profile.Characters['f'].WeaknessScore >= 0.5);
    }

    [TestMethod]
    public void ReviewPracticeProfile_GeneratesDeterministicFocusedLesson()
    {
        var generator = new SessionReviewGenerator();
        var review = generator.Generate(
            Summary(currentErrors: 2),
            [
                Character('f', 'x', false, 1, null),
                Character('j', 'j', true, 2, 140),
                Character('f', 'd', false, 3, 150)
            ]);
        var profile = generator.CreatePracticeProfile(review, new DateTime(2026, 6, 11));
        var lessonGenerator = new AdaptiveLessonGenerator(
            new BuiltInWordListProvider(),
            new CharacterUnlockPlanner());
        var options = new LessonGenerationOptions(
            LessonMode.WeakKeys,
            LessonLengthKind.Characters,
            160,
            KeyboardLayoutRepository.Qwerty,
            RandomSeed: 42);

        var first = lessonGenerator.Generate(profile, options);
        var second = lessonGenerator.Generate(profile, options);

        Assert.AreEqual(first.Text, second.Text);
        CollectionAssert.Contains(first.FocusCharacters.ToList(), 'f');
    }

    private static SessionSummary Summary(int currentErrors, int typed = 3)
    {
        return new SessionSummary(
            Guid.NewGuid(),
            "fff",
            IsComplete: true,
            TypedCharacterKeypresses: typed,
            CorrectCharacterKeypresses: Math.Max(0, typed - currentErrors),
            IncorrectCharacterKeypresses: currentErrors,
            BackspaceCount: 0,
            currentErrors,
            DurationMs: 1000,
            RawWpm: 10,
            Accuracy: typed == 0 ? 0 : Math.Max(0, typed - currentErrors) / (double)typed);
    }

    private static TypingInputEvent Character(
        char expected,
        char actual,
        bool isCorrect,
        long ticks,
        double? deltaMs)
    {
        return new TypingInputEvent(
            Guid.NewGuid(),
            Position: (int)ticks - 1,
            expected,
            actual,
            InputEventKind.Character,
            isCorrect,
            WasCorrection: false,
            ticks,
            ElapsedMs: deltaMs ?? 0,
            deltaMs);
    }

    private static TypingInputEvent Backspace(char expected, char actual, long ticks)
    {
        return new TypingInputEvent(
            Guid.NewGuid(),
            Position: 0,
            expected,
            actual,
            InputEventKind.Backspace,
            IsCorrect: true,
            WasCorrection: true,
            ticks,
            ElapsedMs: 0,
            DeltaFromPreviousMs: 100);
    }
}

