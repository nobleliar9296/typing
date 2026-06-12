namespace TypingTrainer.Core.Keyboard;

public sealed record VisualKeyboardKey(
    string Id,
    string PrimaryLabel,
    string? SecondaryLabel,
    string? Output,
    KeyRole Role,
    FingerAssignment Finger,
    double WidthUnits);
