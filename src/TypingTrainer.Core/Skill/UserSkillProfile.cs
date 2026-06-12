namespace TypingTrainer.Core.Skill;

public sealed record UserSkillProfile(
    IReadOnlyDictionary<char, CharacterSkill> Characters,
    IReadOnlyDictionary<string, BigramSkill> Bigrams,
    int CompletedSessionCount,
    TimeSpan TotalPracticeTime,
    DateTime CreatedAtUtc);
