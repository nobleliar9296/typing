namespace TypingTrainer.Data.Models;

public sealed record BigramAnalyticsRow(
    string Bigram,
    string DisplayBigram,
    int ExposureCount,
    int CorrectCount,
    int IncorrectCount,
    double Accuracy,
    double? AverageLatencyMs,
    double? MedianLatencyMs,
    double WeaknessScore);
