using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.Core.Keyboard;
using TypingTrainer.Core.Skill;

namespace TypingTrainer.Core.Tests;

[TestClass]
public sealed class CharacterUnlockPlannerTests
{
    [TestMethod]
    public void CharacterUnlockPlanner_EmptyProfile_UnlocksBeginnerStages()
    {
        var planner = new CharacterUnlockPlanner();

        var unlocked = planner.GetUnlockedCharacters(
            SkillProfileDefaults.Empty(new DateTime(2026, 6, 11)),
            KeyboardLayoutRepository.Qwerty);

        CollectionAssert.IsSubsetOf(new[] { 'f', 'j', 'd', 'k', 's', 'l' }, unlocked.ToArray());
        Assert.IsFalse(unlocked.Contains('a'));
    }

    [TestMethod]
    public void CharacterUnlockPlanner_MasterCurrentStages_UnlocksOneAdditionalStage()
    {
        var profile = CreateProfile("fjdksl");
        var planner = new CharacterUnlockPlanner();

        var unlocked = planner.GetUnlockedCharacters(profile, KeyboardLayoutRepository.Qwerty);

        Assert.IsTrue(unlocked.Contains('a'));
        Assert.IsFalse(unlocked.Contains('r'));
    }

    [TestMethod]
    public void CharacterUnlockPlanner_DoesNotUnlockNextStage_WhenAccuracyTooLow()
    {
        var profile = CreateProfile("fjdksl", accuracy: 0.70);
        var planner = new CharacterUnlockPlanner();

        var unlocked = planner.GetUnlockedCharacters(profile, KeyboardLayoutRepository.Qwerty);

        Assert.IsFalse(unlocked.Contains('a'));
    }

    [TestMethod]
    public void CharacterUnlockPlanner_DoesNotUnlockMoreThanOneStageAtATime()
    {
        var profile = CreateProfile("fjdksla");
        var planner = new CharacterUnlockPlanner();

        var unlocked = planner.GetUnlockedCharacters(profile, KeyboardLayoutRepository.Qwerty);

        Assert.IsTrue(unlocked.Contains('r'));
        Assert.IsTrue(unlocked.Contains('u'));
        Assert.IsFalse(unlocked.Contains('w'));
    }

    private static UserSkillProfile CreateProfile(string characters, double accuracy = 0.96)
    {
        var skills = characters
            .Distinct()
            .ToDictionary(
                character => character,
                character => new CharacterSkill(
                    character,
                    ExposureCount: 80,
                    CorrectCount: (int)(80 * accuracy),
                    IncorrectCount: 80 - (int)(80 * accuracy),
                    Accuracy: accuracy,
                    MedianLatencyMs: 120,
                    AverageLatencyMs: 125,
                    WeaknessScore: 0,
                    ConfidenceScore: CharacterUnlockPlanner.CalculateConfidence(80, accuracy, 120)));

        return new UserSkillProfile(
            skills,
            new Dictionary<string, BigramSkill>(),
            CompletedSessionCount: 4,
            TotalPracticeTime: TimeSpan.FromMinutes(10),
            CreatedAtUtc: new DateTime(2026, 6, 11));
    }
}
