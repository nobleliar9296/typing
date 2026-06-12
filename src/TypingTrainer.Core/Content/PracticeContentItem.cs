namespace TypingTrainer.Core.Content;

public sealed record PracticeContentItem(
    string Id,
    PracticeContentKind Kind,
    string Title,
    string Text,
    string Language,
    string Source,
    string License,
    IReadOnlyList<string> Tags,
    IReadOnlySet<char> CharacterSet,
    int WordCount,
    int CharacterCount,
    bool ContainsCapitalLetters,
    bool ContainsNumbers,
    bool ContainsPunctuation,
    double AverageWordLength,
    double DifficultyScore,
    string? PackId = null,
    DateTimeOffset? CreatedAtUtc = null,
    DateTimeOffset? LastUsedAtUtc = null,
    int UseCount = 0);
