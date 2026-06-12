using TypingTrainer.Core.Lessons;
using TypingTrainer.Data.Models;

namespace TypingTrainer.App.Services;

public interface ILessonService
{
    Task<LessonMode> GetDefaultLessonModeAsync(CancellationToken cancellationToken = default);

    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<LessonGenerationResult> GenerateNextLessonAsync(
        LessonMode mode,
        CancellationToken cancellationToken = default);
}
