using TypingTrainer.Data.Models;

namespace TypingTrainer.Data.Services;

public interface ISessionDetailQueryService
{
    Task<SessionDetailSnapshot?> GetSessionDetailAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
}

