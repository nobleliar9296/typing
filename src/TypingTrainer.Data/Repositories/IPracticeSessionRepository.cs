using TypingTrainer.Data.Models;

namespace TypingTrainer.Data.Repositories;

public interface IPracticeSessionRepository
{
    Task SaveCompletedSessionAsync(
        StoredPracticeSession session,
        IReadOnlyList<StoredKeyEvent> events,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredPracticeSession>> GetRecentSessionsAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<StoredPracticeSession?> GetSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredKeyEvent>> GetSessionEventsAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task DeleteAllAsync(CancellationToken cancellationToken = default);
}
