using TypingTrainer.Core.Keyboard;

namespace TypingTrainer.Core.Skill;

public sealed class CharacterUnlockPlanner
{
    private const int BeginnerStageCount = 3;
    private const int MinimumExposure = 40;
    private const double MinimumAccuracy = 0.88;
    private const double MinimumConfidence = 0.82;

    public IReadOnlySet<char> GetUnlockedCharacters(
        UserSkillProfile profile,
        KeyboardLayout layout)
    {
        var stageCount = GetUnlockedStageCount(profile, layout);

        return layout.Stages
            .Take(stageCount)
            .SelectMany(stage => stage.Characters)
            .Where(IsMilestone4Character)
            .ToHashSet();
    }

    public IReadOnlyList<char> GetNewestUnlockedStageCharacters(
        UserSkillProfile profile,
        KeyboardLayout layout)
    {
        var unlocked = GetUnlockedCharacters(profile, layout);

        for (var index = Math.Min(GetUnlockedStageCount(profile, layout), layout.Stages.Count) - 1; index >= 0; index--)
        {
            var stageCharacters = GetStageAdditions(layout, index)
                .Where(unlocked.Contains)
                .Where(IsMilestone4Character)
                .OrderBy(character => character)
                .ToArray();

            if (stageCharacters.Length > 0)
            {
                return stageCharacters;
            }
        }

        return unlocked.OrderBy(character => character).ToArray();
    }

    public static double CalculateConfidence(
        int exposureCount,
        double accuracy,
        double? medianLatencyMs)
    {
        var speedScore = medianLatencyMs is null
            ? 0.5
            : 1.0 - Clamp((medianLatencyMs.Value - 180) / 420, 0, 1);
        var exposureScore = Clamp(exposureCount / 80.0, 0, 1);
        var confidence = (0.60 * Clamp(accuracy, 0, 1)) + (0.25 * speedScore) + (0.15 * exposureScore);
        return Clamp(confidence, 0, 1);
    }

    private static int GetUnlockedStageCount(
        UserSkillProfile profile,
        KeyboardLayout layout)
    {
        var baseStageCount = Math.Min(BeginnerStageCount, layout.Stages.Count);

        if (profile.CompletedSessionCount == 0 || profile.Characters.Count == 0)
        {
            return baseStageCount;
        }

        var highestPracticedStage = GetHighestPracticedStage(profile, layout);
        var inferredCurrentStageCount = Math.Max(baseStageCount, highestPracticedStage + 1);

        if (inferredCurrentStageCount < layout.Stages.Count
            && AreStagesMastered(profile, layout, inferredCurrentStageCount))
        {
            return inferredCurrentStageCount + 1;
        }

        return inferredCurrentStageCount;
    }

    private static int GetHighestPracticedStage(UserSkillProfile profile, KeyboardLayout layout)
    {
        var highestPracticedStage = BeginnerStageCount - 1;

        for (var index = 0; index < layout.Stages.Count; index++)
        {
            if (GetStageAdditions(layout, index)
                .Where(IsMilestone4Character)
                .Any(character => profile.Characters.TryGetValue(character, out var skill) && skill.ExposureCount > 0))
            {
                highestPracticedStage = index;
            }
        }

        return Math.Min(highestPracticedStage, layout.Stages.Count - 1);
    }

    private static bool AreStagesMastered(
        UserSkillProfile profile,
        KeyboardLayout layout,
        int stageCount)
    {
        return layout.Stages
            .Take(stageCount)
            .SelectMany(stage => stage.Characters)
            .Where(IsMilestone4Character)
            .Distinct()
            .All(character => IsCharacterMastered(profile, character));
    }

    private static bool IsCharacterMastered(UserSkillProfile profile, char character)
    {
        if (!profile.Characters.TryGetValue(character, out var skill))
        {
            return false;
        }

        var confidence = CalculateConfidence(skill.ExposureCount, skill.Accuracy, skill.MedianLatencyMs);

        return skill.ExposureCount >= MinimumExposure
            && skill.Accuracy >= MinimumAccuracy
            && confidence >= MinimumConfidence;
    }

    private static bool IsMilestone4Character(char character)
    {
        return char.IsAsciiLetterLower(character);
    }

    private static IReadOnlySet<char> GetStageAdditions(KeyboardLayout layout, int stageIndex)
    {
        var previousCharacters = layout.Stages
            .Take(stageIndex)
            .SelectMany(stage => stage.Characters)
            .ToHashSet();

        return layout.Stages[stageIndex].Characters
            .Where(character => !previousCharacters.Contains(character))
            .ToHashSet();
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        return Math.Min(maximum, Math.Max(minimum, value));
    }
}
