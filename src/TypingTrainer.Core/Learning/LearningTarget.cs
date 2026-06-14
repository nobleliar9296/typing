namespace TypingTrainer.Core.Learning;

public sealed record LearningTarget(
    LearningItemType Type,
    string Target,
    MasteryState MasteryState,
    double WeaknessScore,
    double StabilityScore,
    int ExposureCount,
    double Accuracy,
    double? MedianLatencyMs,
    DateTimeOffset? NextDueUtc,
    MistakeCause PrimaryMistakeCause);
