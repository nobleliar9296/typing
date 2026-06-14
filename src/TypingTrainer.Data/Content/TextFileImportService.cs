using System.Text;
using TypingTrainer.Core.Content;
using TypingTrainer.Data.Models;

namespace TypingTrainer.Data.Content;

public sealed class TextFileImportService : ITextFileImportService
{
    private const int BatchSize = 200;
    private const int DefaultPreviewCharacters = 1_200;

    private readonly ContentImportRepository _repository;

    public TextFileImportService(ContentImportRepository repository)
    {
        _repository = repository;
    }

    public async Task<TextImportPreview> PreviewTextFileAsync(
        string filePath,
        TextImportOptions options,
        int maxCharacters = DefaultPreviewCharacters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return new TextImportPreview(
                string.Empty,
                string.Empty,
                ["Choose a .txt file to preview before importing."]);
        }

        if (!File.Exists(filePath))
        {
            return new TextImportPreview(
                string.Empty,
                string.Empty,
                ["File not found. Choose a readable .txt file."]);
        }

        var sampleLimit = Math.Clamp(maxCharacters, 120, 4_000);
        var sample = await ReadPreviewSampleAsync(filePath, sampleLimit, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(sample))
        {
            return new TextImportPreview(
                string.Empty,
                string.Empty,
                ["File is empty or contains only whitespace."]);
        }

        var cleanedSample = BuildCleanedPreview(sample, options, sampleLimit);
        var notes = BuildCleanupNotes(options).ToArray();
        return new TextImportPreview(
            TrimPreviewText(sample, sampleLimit),
            TrimPreviewText(cleanedSample, sampleLimit),
            notes);
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
                options.LowercaseWhenImported,
                options.NormalizeToAscii,
                options.StripPunctuation))
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

    private static async Task<string> ReadPreviewSampleAsync(
        string filePath,
        int maxCharacters,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 16 * 1024,
            useAsync: true);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 16 * 1024);
        var buffer = new char[maxCharacters];
        var count = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
        return new string(buffer, 0, count);
    }

    private static string BuildCleanedPreview(string sample, TextImportOptions options, int maxCharacters)
    {
        var paragraphs = ParagraphChunker.SplitParagraphs(
            EnumeratePreviewLines(sample),
            minParagraphCharacters: 1,
            maxParagraphCharacters: Math.Max(1, maxCharacters),
            options.NormalizeWhitespace,
            options.LowercaseWhenImported,
            options.NormalizeToAscii,
            options.StripPunctuation);

        return string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
    }

    private static IEnumerable<string> EnumeratePreviewLines(string sample)
    {
        using var reader = new StringReader(sample);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    private static IEnumerable<string> BuildCleanupNotes(TextImportOptions options)
    {
        yield return options.NormalizeToAscii ? "ASCII normalization on" : "ASCII normalization off";
        yield return options.NormalizeWhitespace ? "Whitespace normalized" : "Whitespace preserved";
        yield return options.LowercaseWhenImported ? "Lowercase on" : "Original casing kept";
        yield return options.StripPunctuation ? "Punctuation removed" : "Punctuation kept";
    }

    private static string TrimPreviewText(string text, int maxCharacters)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= maxCharacters
            ? trimmed
            : trimmed[..maxCharacters].Trim();
    }
}
