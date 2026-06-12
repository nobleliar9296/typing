namespace TypingTrainer.Core.Keyboard;

public sealed record CharacterKeyMapping(
    char Character,
    string KeyId,
    bool RequiresShift,
    string? ShiftKeyId);
