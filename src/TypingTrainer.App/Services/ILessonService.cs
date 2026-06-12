using TypingTrainer.Core.Lessons;
using TypingTrainer.Core.Review;
using TypingTrainer.Core.Typing;
using TypingTrainer.Data.Models;

namespace TypingTrainer.App.Services;

public interface ILessonService
{
    Task<LessonMode> GetDefaultLessonModeAsync(CancellationToken cancellationToken = default);

    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);

    Task<LessonGenerationResult> GenerateNextLessonAsync(
        LessonMode mode,
        int? targetCharactersOverride = null,
        CancellationToken cancellationToken = default);

    Task<LessonGenerationResult> GenerateClipboardLessonAsync(
        string clipboardText,
        int? targetCharactersOverride = null,
        CancellationToken cancellationToken = default);

    Task<LessonGenerationResult> GenerateReviewLessonAsync(
        SessionReview review,
        int targetCharacters,
        CancellationToken cancellationToken = default);

    Task<LessonGenerationResult> GenerateMistakeReplayLessonAsync(
        SessionReview review,
        IReadOnlyList<TypingInputEvent> events,
        int targetCharacters,
        CancellationToken cancellationToken = default);
}
