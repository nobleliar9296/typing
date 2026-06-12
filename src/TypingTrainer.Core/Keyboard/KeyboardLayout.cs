namespace TypingTrainer.Core.Keyboard;

public sealed record KeyboardLayout(
    string Name,
    IReadOnlyList<KeyboardLayoutStage> Stages);
