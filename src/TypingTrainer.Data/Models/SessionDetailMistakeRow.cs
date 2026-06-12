namespace TypingTrainer.Data.Models;

public sealed record SessionDetailMistakeRow(
    int Position,
    string Expected,
    string Actual,
    string Kind,
    double ElapsedMs);

