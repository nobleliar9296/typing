namespace TypingTrainer.Core.Lessons;

public enum PracticeLessonSize
{
    Small,
    Medium,
    Long
}

public static class PracticeLessonSizeTargets
{
    public const int MediumTargetCharacters = 1_250;
    public const int LongTargetCharacters = 5_000;

    public static int GetTargetCharacters(PracticeLessonSize size, int smallTargetCharacters)
    {
        return size switch
        {
            PracticeLessonSize.Medium => MediumTargetCharacters,
            PracticeLessonSize.Long => LongTargetCharacters,
            _ => Math.Max(20, smallTargetCharacters)
        };
    }
}
