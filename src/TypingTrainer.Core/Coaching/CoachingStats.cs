namespace TypingTrainer.Core.Coaching;

public sealed record CoachingStats(
    int SessionCount,
    double AverageNetWpm,
    double BestNetWpm,
    double Accuracy,
    double BestAccuracy,
    double WeeklyPracticeMinutes,
    double GoalTargetNetWpm,
    double GoalTargetAccuracyPercent,
    double GoalWeeklyPracticeMinutes,
    int CurrentPracticeStreakDays,
    string? WeakestKey,
    string? SlowestBigram);

