namespace TypingTrainer.Data.Models;

public sealed record RecentSessionRow(
    Guid SessionId,
    DateTime StartedAtUtc,
    TimeSpan Duration,
    string Mode,
    int TargetLength,
    double RawWpm,
    double NetWpm,
    double Accuracy,
    double? Consistency);
