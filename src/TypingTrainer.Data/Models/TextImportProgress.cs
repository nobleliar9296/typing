namespace TypingTrainer.Data.Models;

public sealed record TextImportProgress(
    long BytesRead,
    long? TotalBytes,
    int ParagraphsImported,
    string CurrentStatus);
