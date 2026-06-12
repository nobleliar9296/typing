namespace TypingTrainer.Core.Review;

public sealed record SessionReviewBigramRow(
    string Bigram,
    string DisplayBigram,
    int Samples,
    int CorrectCount,
    int IncorrectCount,
    double Accuracy,
    double? MedianLatencyMs,
    double WeaknessScore);

