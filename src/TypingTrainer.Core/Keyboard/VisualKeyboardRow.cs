namespace TypingTrainer.Core.Keyboard;

public sealed record VisualKeyboardRow(
    IReadOnlyList<VisualKeyboardKey> Keys);
