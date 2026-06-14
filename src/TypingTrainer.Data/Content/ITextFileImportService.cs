using TypingTrainer.Data.Models;

namespace TypingTrainer.Data.Content;

public interface ITextFileImportService
{
    Task<TextImportPreview> PreviewTextFileAsync(
        string filePath,
        TextImportOptions options,
        int maxCharacters = 1_200,
        CancellationToken cancellationToken = default);

    Task<TextImportResult> ImportTextFileAsync(
        string filePath,
        TextImportOptions options,
        IProgress<TextImportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
