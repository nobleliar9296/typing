namespace TypingTrainer.Core.Keyboard;

public sealed record VisualKeyboardLayout(
    string Name,
    IReadOnlyList<VisualKeyboardRow> Rows);
