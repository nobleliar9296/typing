namespace TypingTrainer.Data.Models;

public sealed record CharacterAnalyticsRow(
    string Character,
    string DisplayCharacter,
    int ExposureCount,
    int CorrectCount,
    int IncorrectCount,
    double Accuracy,
    double? AverageLatencyMs,
    double? MedianLatencyMs,
    double WeaknessScore);
