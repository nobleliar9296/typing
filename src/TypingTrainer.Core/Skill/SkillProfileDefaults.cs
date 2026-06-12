namespace TypingTrainer.Core.Skill;

public static class SkillProfileDefaults
{
    public static UserSkillProfile Empty(DateTime? createdAtUtc = null)
    {
        return new UserSkillProfile(
            new Dictionary<char, CharacterSkill>(),
            new Dictionary<string, BigramSkill>(),
            CompletedSessionCount: 0,
            TotalPracticeTime: TimeSpan.Zero,
            CreatedAtUtc: createdAtUtc ?? DateTime.UtcNow);
    }
}
