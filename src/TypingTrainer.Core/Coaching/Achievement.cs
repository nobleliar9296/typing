namespace TypingTrainer.Core.Coaching;

public sealed record Achievement(
    string Id,
    string Title,
    string Description,
    bool IsUnlocked);

