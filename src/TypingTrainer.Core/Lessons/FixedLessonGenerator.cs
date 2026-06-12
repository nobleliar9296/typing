using TypingTrainer.Core.Skill;

namespace TypingTrainer.Core.Lessons;

public sealed class FixedLessonGenerator : ILessonGenerator
{
    public const string FixedLessonText = "the quick brown fox jumps over the lazy dog";

    public LessonGenerationResult Generate(
        UserSkillProfile skillProfile,
        LessonGenerationOptions options)
    {
        return new LessonGenerationResult(
            FixedLessonText,
            FixedLessonText.Where(character => character != ' ').ToHashSet(),
            Array.Empty<char>(),
            Array.Empty<string>(),
            "Fixed sample sentence");
    }
}
