namespace TypingTrainer.Core.Learning;

public sealed record LearningProgressResult(
    int ExposureCount,
    int CorrectCount,
    int IncorrectCount,
    double Accuracy,
    double? MedianLatencyMs,
    double WeaknessScore,
    double StabilityScore,
    MasteryState MasteryState,
    int IntervalDays,
    double EaseFactor,
    DateTimeOffset NextDueUtc);
