using TypingTrainer.Core.Learning;

namespace TypingTrainer.Core.Skill;

public sealed record UserSkillProfile(
    IReadOnlyDictionary<char, CharacterSkill> Characters,
    IReadOnlyDictionary<string, BigramSkill> Bigrams,
    int CompletedSessionCount,
    TimeSpan TotalPracticeTime,
    DateTime CreatedAtUtc,
    IReadOnlyList<LearningTarget>? DueLearningTargets = null,
    MasterySummary? MasterySummary = null)
{
    public IReadOnlyList<LearningTarget> DueLearningTargets { get; init; } =
        DueLearningTargets ?? Array.Empty<LearningTarget>();

    public MasterySummary MasterySummary { get; init; } =
        MasterySummary ?? TypingTrainer.Core.Learning.MasterySummary.Empty;
}
