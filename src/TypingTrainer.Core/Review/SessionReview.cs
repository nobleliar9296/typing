namespace TypingTrainer.Core.Review;

public sealed record SessionReview(
    int CorrectedErrors,
    int UncorrectedErrors,
    IReadOnlyList<SessionReviewKeyRow> MostMissedKeys,
    IReadOnlyList<SessionReviewKeyRow> SlowestKeys,
    IReadOnlyList<SessionReviewBigramRow> WeakestBigrams,
    IReadOnlyList<string> Notes)
{
    public bool HasPracticeTargets =>
        MostMissedKeys.Count > 0
        || SlowestKeys.Count > 0
        || WeakestBigrams.Count > 0;

    public IReadOnlyList<char> FocusCharacters => MostMissedKeys
        .Concat(SlowestKeys)
        .OrderByDescending(row => row.WeaknessScore)
        .Select(row => row.Character)
        .Distinct()
        .Take(6)
        .ToArray();

    public IReadOnlyList<string> FocusBigrams => WeakestBigrams
        .OrderByDescending(row => row.WeaknessScore)
        .Select(row => row.Bigram)
        .Distinct(StringComparer.Ordinal)
        .Take(8)
        .ToArray();
}

