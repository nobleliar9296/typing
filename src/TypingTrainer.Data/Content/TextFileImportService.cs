using System.Text;
using TypingTrainer.Core.Content;
using TypingTrainer.Data.Models;

namespace TypingTrainer.Data.Content;

public sealed class TextFileImportService : ITextFileImportService
{
    private const int BatchSize = 200;

    private readonly ContentImportRepository _repository;

    public TextFileImportService(ContentImportRepository repository)
    {
        _repository = repository;
    }

    public async Task<TextImportResult> ImportTextFileAsync(
        string filePath,
        TextImportOptions options,
        IProgress<TextImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A file path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The import file was not found.", filePath);
        }

        var fileInfo = new FileInfo(filePath);
        var packName = string.IsNullOrWhiteSpace(options.PackName)
            ? Path.GetFileNameWithoutExtension(filePath)
            : options.PackName.Trim();
        var packId = await _repository.CreatePackAsync(
            packName,
            filePath,
            fileInfo.Length,
            cancellationToken).ConfigureAwait(false);

        var importedCount = 0;
        var batch = new List<PracticeContentItem>(BatchSize);
        var paragraphLines = new List<string>();

        try
        {
            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                useAsync: true);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 64 * 1024);

            progress?.Report(new TextImportProgress(0, fileInfo.Length, 0, "Starting import..."));

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    await FlushParagraphAsync().ConfigureAwait(false);
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    await FlushParagraphAsync().ConfigureAwait(false);
                }
                else
                {
                    paragraphLines.Add(line);
                }

                progress?.Report(new TextImportProgress(stream.Position, fileInfo.Length, importedCount, "Importing paragraphs..."));
            }

            await FlushBatchAsync().ConfigureAwait(false);
            progress?.Report(new TextImportProgress(fileInfo.Length, fileInfo.Length, importedCount, "Import complete."));

            return new TextImportResult(packId, packName, importedCount, fileInfo.Length);
        }
        catch
        {
            await _repository.DeletePackAsync(packId, CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        async Task FlushParagraphAsync()
        {
            if (paragraphLines.Count == 0)
            {
                return;
            }

            foreach (var paragraph in ParagraphChunker.SplitParagraphs(
                paragraphLines,
                options.MinParagraphCharacters,
                options.MaxParagraphCharacters,
                options.NormalizeWhitespace,
                options.LowercaseWhenImported))
            {
                importedCount++;
                batch.Add(ContentAnalyzer.AnalyzeParagraph(
                    Guid.NewGuid().ToString(),
                    packId.ToString(),
                    $"Paragraph {importedCount}",
                    paragraph,
                    "Imported text"));

                if (batch.Count >= BatchSize)
                {
                    await FlushBatchAsync().ConfigureAwait(false);
                }
            }

            paragraphLines.Clear();
        }

        async Task FlushBatchAsync()
        {
            if (batch.Count == 0)
            {
                return;
            }

            await _repository.InsertParagraphBatchAsync(packId, batch, cancellationToken).ConfigureAwait(false);
            batch.Clear();
        }
    }
}
