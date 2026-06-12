namespace TypingTrainer.Data.Models;

public sealed record SessionTimelinePoint(
    string Label,
    double ElapsedMs,
    double NetWpm,
    double AccuracyPercent);

