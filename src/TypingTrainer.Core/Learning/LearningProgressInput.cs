namespace TypingTrainer.Core.Learning;

public sealed record LearningProgressInput(
    LearningItemType Type,
    string Target,
    int PreviousExposureCount,
    int PreviousCorrectCount,
    int PreviousIncorrectCount,
    MasteryState PreviousMasteryState,
    int PreviousIntervalDays,
    double PreviousEaseFactor,
    int SessionExposureCount,
    int SessionCorrectCount,
    int SessionIncorrectCount,
    double? SessionMedianLatencyMs);
