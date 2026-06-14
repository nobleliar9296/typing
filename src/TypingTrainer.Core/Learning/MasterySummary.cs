namespace TypingTrainer.Core.Learning;

public sealed record MasterySummary(
    int NewCount,
    int LearningCount,
    int UnstableCount,
    int MasteredCount,
    int RegressingCount,
    int DueCount)
{
    public static MasterySummary Empty { get; } = new(0, 0, 0, 0, 0, 0);
}
