namespace TypingTrainer.Core.Review;

public sealed record SessionReviewKeyRow(
    char Character,
    string DisplayCharacter,
    int Samples,
    int CorrectCount,
    int IncorrectCount,
    double Accuracy,
    double? MedianLatencyMs,
    double WeaknessScore);

