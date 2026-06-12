namespace TypingTrainer.Data.Models;

public sealed record QualitySummary(
    int SessionCount,
    double AverageScore,
    double BestScore,
    string CurrentGrade,
    double RecentTrend);
