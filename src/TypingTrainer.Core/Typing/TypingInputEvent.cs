namespace TypingTrainer.Core.Typing;

public sealed record TypingInputEvent(
    Guid SessionId,
    int Position,
    char? ExpectedChar,
    char? ActualChar,
    InputEventKind Kind,
    bool IsCorrect,
    bool WasCorrection,
    long TimestampTicks,
    double ElapsedMs,
    double? DeltaFromPreviousMs);
