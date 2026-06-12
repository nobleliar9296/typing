namespace TypingTrainer.Data.Models;

public sealed record TextImportResult(
    Guid PackId,
    string PackName,
    int ParagraphsImported,
    long BytesRead);
