namespace TypingTrainer.Data.Models;

public sealed record PracticeCalendarDay(
    DateOnly Date,
    int SessionCount,
    TimeSpan PracticeTime,
    double AverageQualityScore,
    double AverageNetWpm,
    double Accuracy);
