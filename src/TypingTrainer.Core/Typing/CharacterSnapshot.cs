namespace TypingTrainer.Core.Typing;

public sealed record CharacterSnapshot(
    int Position,
    char ExpectedChar,
    char? ActualChar,
    CharacterState State,
    bool HadRejectedInput = false);
