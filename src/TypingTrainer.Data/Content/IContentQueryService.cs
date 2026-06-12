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

    Task<bool> HasEnabledImportedContentAsync(
        CancellationToken cancellationToken = default);

    Task DeleteContentPackAsync(
        Guid packId,
        CancellationToken cancellationToken = default);
}
