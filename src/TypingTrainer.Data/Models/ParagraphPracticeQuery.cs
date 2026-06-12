namespace TypingTrainer.Data.Models;

public sealed record ParagraphPracticeQuery(
    int TargetCharacters,
    bool AllowCapitalLetters,
    bool AllowNumbers,
    bool AllowPunctuation,
    bool UseImportedContent,
    bool UseBuiltInContent);
