namespace TypingTrainer.Core.Typing;

public sealed record KeyProcessResult(
    TypingStateSnapshot State,
    TypingInputEvent? Event,
    bool WasAccepted,
    string? Message = null,
    bool DidAdvance = false,
    bool WasCorrect = false,
    bool WasRejected = false,
    string? FeedbackMessage = null);
