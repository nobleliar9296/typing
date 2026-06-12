namespace TypingTrainer.Data.Services;

public interface IJsonExportService
{
    Task ExportAllSessionsAsync(
        string outputPath,
        CancellationToken cancellationToken = default);
}
