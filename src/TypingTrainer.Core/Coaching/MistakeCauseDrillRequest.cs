using TypingTrainer.Core.Learning;

namespace TypingTrainer.Core.Coaching;

public sealed record MistakeCauseDrillRequest(
    MistakeCause Cause,
    IReadOnlyList<char> TargetCharacters,
    IReadOnlyList<string> TargetBigrams,
    bool AllowCapitalLetters,
    bool AllowNumbers,
    bool AllowPunctuation,
    int? RandomSeed = null);
