using TypingTrainer.Core.Lessons;

namespace TypingTrainer.Core.Coaching;

public sealed record PracticePlanStep(
    int Order,
    string Title,
    string Description,
    LessonMode RecommendedMode,
    LessonLengthKind LengthKind,
    int TargetLength,
    int Minutes);

