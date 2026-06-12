namespace TypingTrainer.Data.Models;

public sealed record RecentQualityRow(
    Guid SessionId,
    DateTimeOffset StartedAtUtc,
    string Mode,
    double Score,
    string Grade);
