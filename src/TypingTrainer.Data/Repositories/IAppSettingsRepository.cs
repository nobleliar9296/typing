using TypingTrainer.Data.Models;

namespace TypingTrainer.Data.Repositories;

public interface IAppSettingsRepository
{
    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
