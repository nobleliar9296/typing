using TypingTrainer.Core.Content;
using TypingTrainer.Data.Models;

namespace TypingTrainer.Data.Content;

public interface IContentQueryService
{
    Task<PracticeContentItem?> GetNextParagraphAsync(
        ParagraphPracticeQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PracticeContentItem>> GetParagraphsAsync(
        ParagraphPracticeQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentPackRow>> GetContentPacksAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TypingTrainer.Core.Content.PracticeContentItem>> GetContentPackPreviewAsync(
        Guid packId,
        int limit = 5,
        CancellationToken cancellationToken = default);

    Task RenameContentPackAsync(
        Guid packId,
        string name,
        CancellationToken cancellationToken = default);

    Task SetContentPackEnabledAsync(
        Guid packId,
        bool enabled,
        CancellationToken cancellationToken = default);

    Task<bool> HasEnabledImportedContentAsync(
        CancellationToken cancellationToken = default);

    Task DeleteContentPackAsync(
        Guid packId,
        CancellationToken cancellationToken = default);
}
