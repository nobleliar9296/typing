namespace TypingTrainer.Data.Models;

public sealed record TextImportPreview(
    string OriginalSample,
    string CleanedSample,
    IReadOnlyList<string> CleanupNotes);
