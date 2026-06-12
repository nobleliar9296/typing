namespace TypingTrainer.Core.Models;

public sealed record SessionSummary(
    Guid SessionId,
    string TargetText,
    bool IsComplete,
    int TypedCharacterKeypresses,
    int CorrectCharacterKeypresses,
    int IncorrectCharacterKeypresses,
    int BackspaceCount,
    int CurrentErrors,
    double DurationMs,
    double RawWpm,
    double Accuracy);
