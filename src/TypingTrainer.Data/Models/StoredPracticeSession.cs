namespace TypingTrainer.Data.Models;

public sealed record StoredPracticeSession(
    Guid Id,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    string Mode,
    string TargetText,
    int TargetLength,
    double RawWpm,
    double NetWpm,
    double Accuracy,
    double? Consistency,
    int TotalKeypresses,
    int CorrectKeypresses,
    int IncorrectKeypresses,
    int CorrectedErrors,
    int UncorrectedErrors,
    long DurationMs);
