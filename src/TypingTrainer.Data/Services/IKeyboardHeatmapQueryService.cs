using TypingTrainer.Data.Models;

namespace TypingTrainer.Data.Services;

public interface IKeyboardHeatmapQueryService
{
    Task<IReadOnlyList<KeyboardHeatmapKeyRow>> GetHeatmapAsync(
        AnalyticsRange range,
        string? modeFilter = null,
        CancellationToken cancellationToken = default);
}

