namespace TypingTrainer.Data.Models;

public sealed record ContentPackRow(
    Guid Id,
    string Name,
    string? SourcePath,
    string? SourceFileName,
    long? FileSizeBytes,
    DateTimeOffset CreatedAtUtc,
    int ParagraphCount,
    bool Enabled);
