namespace TypingTrainer.App.Services;

internal static class SessionPersistenceStatusText
{
    public const string Saved = "Session complete. Saved locally.";
    public const string SavedWithLearningUpdateFailure = "Session complete. Saved locally, but learning progress could not be updated.";
    public const string Failed = "Session complete. The session could not be saved.";
    public const string Canceled = "Session complete. Local save was canceled.";

    public static string FromResult(SessionPersistenceResult result)
    {
        return result.Status switch
        {
            SessionPersistenceStatus.Saved => Saved,
            SessionPersistenceStatus.SavedWithLearningUpdateFailure => SavedWithLearningUpdateFailure,
            SessionPersistenceStatus.Canceled => Canceled,
            _ => Failed
        };
    }
}
