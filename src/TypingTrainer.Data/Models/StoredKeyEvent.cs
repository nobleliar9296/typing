namespace TypingTrainer.Data.Models;

public sealed record StoredKeyEvent(
    long? Id,
    Guid SessionId,
    int Position,
    char? ExpectedChar,
    char? ActualChar,
    string EventKind,
    bool IsCorrect,
    bool WasCorrection,
    long TimestampTicks,
    double ElapsedMs,
    double? DeltaPreviousMs);
