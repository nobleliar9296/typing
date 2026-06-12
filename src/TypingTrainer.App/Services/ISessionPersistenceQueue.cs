using TypingTrainer.Core.Models;
using TypingTrainer.Core.Typing;

namespace TypingTrainer.App.Services;

public interface ISessionPersistenceQueue
{
    Exception? LastError { get; }

    ValueTask EnqueueCompletedSessionAsync(
        SessionSummary summary,
        IReadOnlyList<TypingInputEvent> events,
        string mode = "fixed",
        CancellationToken cancellationToken = default);

    Task FlushAsync(CancellationToken cancellationToken = default);
}
