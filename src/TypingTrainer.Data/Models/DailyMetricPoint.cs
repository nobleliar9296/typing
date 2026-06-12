namespace TypingTrainer.Data.Models;

public sealed record DailyMetricPoint(
    DateOnly Date,
    int SessionCount,
    TimeSpan PracticeTime,
    double AverageRawWpm,
    double AverageNetWpm,
    double BestNetWpm,
    double Accuracy);
