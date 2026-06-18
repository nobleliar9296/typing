namespace TypingTrainer.App.Services;

public enum SessionPersistenceStatus
{
    Saved,
    SavedWithLearningUpdateFailure,
    Failed,
    Canceled
}

public sealed record SessionPersistenceResult(
    SessionPersistenceStatus Status,
    Guid SessionId,
    Exception? Error = null)
{
    public bool SessionWasSaved =>
        Status is SessionPersistenceStatus.Saved or SessionPersistenceStatus.SavedWithLearningUpdateFailure;
}
