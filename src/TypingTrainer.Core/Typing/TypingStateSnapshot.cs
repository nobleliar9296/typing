namespace TypingTrainer.Core.Typing;

public sealed record TypingStateSnapshot(
    Guid SessionId,
    string TargetText,
    int CursorIndex,
    bool IsComplete,
    IReadOnlyList<CharacterSnapshot> Characters,
    int TypedCharacterKeypresses,
    int CorrectCharacterKeypresses,
    int IncorrectCharacterKeypresses,
    int BackspaceCount,
    int CurrentErrors,
    double ElapsedMs,
    double RawWpm,
    double Accuracy,
    char? CurrentExpectedCharacter);
