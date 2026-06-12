using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.Core.Keyboard;
using TypingTrainer.Core.Lessons;
using TypingTrainer.Core.Skill;

namespace TypingTrainer.Core.Tests;

[TestClass]
public sealed class AdaptiveLessonGeneratorTests
{
    [TestMethod]
    public void AdaptiveLessonGenerator_NewUser_GeneratesNonEmptyLesson()
    {
        var result = CreateGenerator().Generate(
            SkillProfileDefaults.Empty(new DateTime(2026, 6, 11)),
            Options(LessonMode.Adaptive, randomSeed: 1));

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Text));
    }

    [TestMethod]
    public void AdaptiveLessonGenerator_GeneratedText_UsesOnlyUnlockedCharactersAndSpaces()
    {
        var result = CreateGenerator().Generate(
            SkillProfileDefaults.Empty(new DateTime(2026, 6, 11)),
            Options(LessonMode.Adaptive, randomSeed: 42));

        foreach (var character in result.Text)
        {
            Assert.IsTrue(
                character == ' ' || result.UnlockedCharacters.Contains(character),
                $"Generated locked character: '{character}' in lesson '{result.Text}'");
        }
    }

    [TestMethod]
    public void AdaptiveLessonGenerator_WithSameSeed_IsDeterministic()
    {
        var generator = CreateGenerator();
        var profile = SkillProfileDefaults.Empty(new DateTime(2026, 6, 11));

        var first = generator.Generate(profile, Options(LessonMode.Adaptive, randomSeed: 123));
        var second = generator.Generate(profile, Options(LessonMode.Adaptive, randomSeed: 123));

        Assert.AreEqual(first.Text, second.Text);
    }

    [TestMethod]
    public void AdaptiveLessonGenerator_WeakKeysMode_FavorsWeakCharacters()
    {
        var profile = CreateWeakCharacterProfile('f');

        var result = CreateGenerator().Generate(profile, Options(LessonMode.WeakKeys, randomSeed: 5));

        Assert.IsTrue(result.FocusCharacters.Contains('f'));
        Assert.IsTrue(result.Text.Count(character => character == 'f') > 10);
    }

    [TestMethod]
    public void AdaptiveLessonGenerator_WeakBigramsMode_FavorsWeakBigrams()
    {
        var profile = CreateWeakBigramProfile("fj");

        var result = CreateGenerator().Generate(profile, Options(LessonMode.WeakBigrams, randomSeed: 5));

        Assert.IsTrue(result.FocusBigrams.Contains("fj"));
        Assert.IsTrue(result.Text.Contains("fj", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AdaptiveLessonGenerator_WeakLeftHandFocus_SelectsLeftHandWeakCharacters()
    {
        var profile = CreateHandFocusProfile();

        var result = CreateGenerator().Generate(
            profile,
            Options(LessonMode.Adaptive, randomSeed: 11, trainingFocus: "WeakLeftHand"));

        Assert.IsTrue(result.FocusCharacters.Contains('q'));
        Assert.IsFalse(result.FocusCharacters.Contains('p'));
    }

    [TestMethod]
    public void AdaptiveLessonGenerator_FocusWithSameSeed_IsDeterministic()
    {
        var generator = CreateGenerator();
        var profile = CreateHandFocusProfile();
        var options = Options(LessonMode.Adaptive, randomSeed: 22, trainingFocus: "SpeedFirst");

        var first = generator.Generate(profile, options);
        var second = generator.Generate(profile, options);

        Assert.AreEqual(first.Text, second.Text);
        CollectionAssert.AreEqual(first.FocusCharacters.ToArray(), second.FocusCharacters.ToArray());
    }

    [TestMethod]
    public void AdaptiveLessonGenerator_FixedMode_ReturnsFixedLesson()
    {
        var result = CreateGenerator().Generate(
            SkillProfileDefaults.Empty(new DateTime(2026, 6, 11)),
            Options(LessonMode.Fixed));

        Assert.AreEqual(FixedLessonGenerator.FixedLessonText, result.Text);
    }

    [TestMethod]
    public void AdaptiveLessonGenerator_FallbackDrill_WorksWhenFewWordsAvailable()
    {
        var generator = new AdaptiveLessonGenerator(new SparseWordListProvider(), new CharacterUnlockPlanner());

        var result = generator.Generate(
            SkillProfileDefaults.Empty(new DateTime(2026, 6, 11)),
            Options(LessonMode.Adaptive, randomSeed: 7));

        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Text));
        Assert.IsTrue(result.Text.All(character => character == ' ' || result.UnlockedCharacters.Contains(character)));
    }

    [TestMethod]
    public void AdaptiveLessonGenerator_UsesBuiltInWordList()
    {
        var result = CreateGenerator().Generate(
            CreateMasteredProfile(),
            Options(LessonMode.Adaptive, randomSeed: 9));
        var builtInWords = BuiltInWordList.CommonWords.ToHashSet();
        var generatedWords = result.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Assert.IsTrue(generatedWords.Length > 0);
        Assert.IsTrue(generatedWords.All(builtInWords.Contains));
    }

    private static AdaptiveLessonGenerator CreateGenerator()
    {
        return new AdaptiveLessonGenerator(new BuiltInWordListProvider(), new CharacterUnlockPlanner());
    }

    private static LessonGenerationOptions Options(
        LessonMode mode,
        int? randomSeed = null,
        string trainingFocus = "Balanced")
    {
        return new LessonGenerationOptions(
            mode,
            LessonLengthKind.Characters,
            120,
            KeyboardLayoutRepository.Qwerty,
            randomSeed,
            TrainingFocus: trainingFocus);
    }

    private static UserSkillProfile CreateWeakCharacterProfile(char weakCharacter)
    {
        var characters = "fjdksl".ToDictionary(
            character => character,
            character =>
            {
                var isWeak = character == weakCharacter;
                var accuracy = isWeak ? 0.45 : 0.98;

                return new CharacterSkill(
                    character,
                    ExposureCount: 40,
                    CorrectCount: isWeak ? 18 : 39,
                    IncorrectCount: isWeak ? 22 : 1,
                    Accuracy: accuracy,
                    MedianLatencyMs: isWeak ? 360 : 110,
                    AverageLatencyMs: isWeak ? 370 : 115,
                    WeaknessScore: isWeak ? 1.0 : 0.01,
                    ConfidenceScore: CharacterUnlockPlanner.CalculateConfidence(40, accuracy, isWeak ? 360 : 110));
            });

        return new UserSkillProfile(
            characters,
            new Dictionary<string, BigramSkill>(),
            CompletedSessionCount: 3,
            TotalPracticeTime: TimeSpan.FromMinutes(8),
            CreatedAtUtc: new DateTime(2026, 6, 11));
    }

    private static UserSkillProfile CreateWeakBigramProfile(string bigram)
    {
        var profile = CreateWeakCharacterProfile('f');
        var bigrams = new Dictionary<string, BigramSkill>
        {
            [bigram] = new(
                bigram,
                ExposureCount: 8,
                CorrectCount: 2,
                IncorrectCount: 6,
                Accuracy: 0.25,
                MedianLatencyMs: 420,
                AverageLatencyMs: 430,
                WeaknessScore: 1.0)
        };

        return profile with { Bigrams = bigrams };
    }

    private static UserSkillProfile CreateMasteredProfile()
    {
        var characters = "abcdefghijklmnopqrstuvwxyz".ToDictionary(
            character => character,
            character => new CharacterSkill(
                character,
                ExposureCount: 80,
                CorrectCount: 80,
                IncorrectCount: 0,
                Accuracy: 1.0,
                MedianLatencyMs: 100,
                AverageLatencyMs: 100,
                WeaknessScore: 0,
                ConfidenceScore: CharacterUnlockPlanner.CalculateConfidence(80, 1.0, 100)));

        return new UserSkillProfile(
            characters,
            new Dictionary<string, BigramSkill>(),
            CompletedSessionCount: 5,
            TotalPracticeTime: TimeSpan.FromMinutes(20),
            CreatedAtUtc: new DateTime(2026, 6, 11));
    }

    private static UserSkillProfile CreateHandFocusProfile()
    {
        var characters = "abcdefghijklmnopqrstuvwxyz".ToDictionary(
            character => character,
            character =>
            {
                var isLeftTarget = character == 'q';
                var isRightTarget = character == 'p';
                var isTarget = isLeftTarget || isRightTarget;
                var accuracy = isTarget ? 0.35 : 0.98;

                return new CharacterSkill(
                    character,
                    ExposureCount: 80,
                    CorrectCount: isTarget ? 28 : 78,
                    IncorrectCount: isTarget ? 52 : 2,
                    Accuracy: accuracy,
                    MedianLatencyMs: isTarget ? 480 : 120,
                    AverageLatencyMs: isTarget ? 500 : 125,
                    WeaknessScore: isTarget ? 1.0 : 0.01,
                    ConfidenceScore: CharacterUnlockPlanner.CalculateConfidence(80, accuracy, isTarget ? 480 : 120));
            });

        return new UserSkillProfile(
            characters,
            new Dictionary<string, BigramSkill>(),
            CompletedSessionCount: 8,
            TotalPracticeTime: TimeSpan.FromMinutes(35),
            CreatedAtUtc: new DateTime(2026, 6, 11));
    }

    private sealed class SparseWordListProvider : IWordListProvider
    {
        public IReadOnlyList<string> GetCommonWords()
        {
            return ["zzzz"];
        }
    }
}
