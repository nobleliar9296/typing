namespace TypingTrainer.Core.Keyboard;

public sealed record KeyboardLayoutStage(
    string Name,
    IReadOnlySet<char> Characters);
