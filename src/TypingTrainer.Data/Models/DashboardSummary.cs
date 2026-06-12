namespace TypingTrainer.Data.Models;

public sealed record DashboardSummary(
    int SessionCount,
    TimeSpan TotalPracticeTime,
    double AverageRawWpm,
    double AverageNetWpm,
    double BestNetWpm,
    double Accuracy,
    double? AverageConsistency,
    int TotalKeypresses,
    int CorrectKeypresses,
    int IncorrectKeypresses,
    int CorrectedErrors,
    int UncorrectedErrors);
