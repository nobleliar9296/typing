using TypingTrainer.Core.Skill;

namespace TypingTrainer.Data.Services;

public interface ISkillProfileQueryService
{
    Task<UserSkillProfile> GetUserSkillProfileAsync(
        CancellationToken cancellationToken = default);
}
