using TypingTrainer.Core.Keyboard;

namespace TypingTrainer.Core.Lessons;

public sealed record LessonGenerationOptions(
    LessonMode Mode,
    LessonLengthKind LengthKind,
    int TargetLength,
    KeyboardLayout KeyboardLayout,
    int? RandomSeed = null,
    bool AllowCapitalLetters = false,
    bool AllowNumbers = false,
    bool AllowPunctuation = false,
    string TrainingFocus = "Balanced",
    string DifficultyPreset = "Custom");
