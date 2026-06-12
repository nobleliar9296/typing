using TypingTrainer.Data.Models;

namespace TypingTrainer.Data.Content;

public interface ITextFileImportService
{
    Task<TextImportResult> ImportTextFileAsync(
        string filePath,
        TextImportOptions options,
        IProgress<TextImportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
