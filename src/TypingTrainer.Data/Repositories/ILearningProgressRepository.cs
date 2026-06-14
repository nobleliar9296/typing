using TypingTrainer.Data.Models;

namespace TypingTrainer.Data.Repositories;

public interface ILearningProgressRepository
{
    Task UpdateFromCompletedSessionAsync(
        StoredPracticeSession session,
        IReadOnlyList<StoredKeyEvent> events,
        CancellationToken cancellationToken = default);
}
