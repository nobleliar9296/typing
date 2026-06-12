namespace TypingTrainer.Data.Models;

public sealed record DashboardSnapshot(
    DashboardSummary Summary,
    IReadOnlyList<DailyMetricPoint> DailyMetrics,
    IReadOnlyList<RecentSessionRow> RecentSessions,
    IReadOnlyList<CharacterAnalyticsRow> WeakestCharacters,
    IReadOnlyList<CharacterAnalyticsRow> SlowestCharacters,
    IReadOnlyList<BigramAnalyticsRow> WeakestBigrams,
    IReadOnlyList<BigramAnalyticsRow> SlowestBigrams);
