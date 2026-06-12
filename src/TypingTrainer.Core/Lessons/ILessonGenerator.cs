using TypingTrainer.Core.Skill;

namespace TypingTrainer.Core.Lessons;

public interface ILessonGenerator
{
    LessonGenerationResult Generate(
        UserSkillProfile skillProfile,
        LessonGenerationOptions options);
}
