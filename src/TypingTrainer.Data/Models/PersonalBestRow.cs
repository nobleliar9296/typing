namespace TypingTrainer.Data.Models;

public sealed record PersonalBestRow(
    string Kind,
    string Label,
    Guid? SessionId,
    DateOnly? Date,
    string? Mode,
    double Value,
    string Unit);
