using TypingTrainer.Data.Models;

namespace TypingTrainer.Data.Services;

public interface ITrainingHistoryQueryService
{
    Task<TrainingHistorySnapshot> GetTrainingHistoryAsync(
        AnalyticsRange range,
        string? modeFilter,
        CancellationToken cancellationToken = default);
}
