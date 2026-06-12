using TypingTrainer.Data.Models;

namespace TypingTrainer.Data.Services;

public interface IAnalyticsQueryService
{
    Task<DashboardSnapshot> GetDashboardSnapshotAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken = default);

    Task<DashboardSnapshot> GetDashboardSnapshotAsync(
        AnalyticsRange range,
        string? modeFilter,
        CancellationToken cancellationToken = default);
}
