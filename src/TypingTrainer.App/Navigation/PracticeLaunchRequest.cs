using TypingTrainer.Core.Lessons;

namespace TypingTrainer.App.Navigation;

public sealed record PracticeLaunchRequest(
    LessonMode Mode,
    PracticeLessonSize Size,
    int? TargetCharacters,
    string Reason);
