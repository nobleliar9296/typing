namespace TypingTrainer.Core.Lessons;

public sealed record LessonGenerationResult(
    string Text,
    IReadOnlySet<char> UnlockedCharacters,
    IReadOnlyList<char> FocusCharacters,
    IReadOnlyList<string> FocusBigrams,
    string Reason,
    string? ContentTitle = null,
    string? ContentSource = null);
