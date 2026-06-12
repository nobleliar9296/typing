namespace TypingTrainer.Core.Typing;

public sealed record TypingSessionOptions(
    ErrorAdvanceMode ErrorAdvanceMode,
    bool AllowBackspace)
{
    public static TypingSessionOptions Default { get; } = new(
        ErrorAdvanceMode.AdvanceOnError,
        AllowBackspace: true);
}
