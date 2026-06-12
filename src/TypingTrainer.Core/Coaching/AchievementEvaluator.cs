namespace TypingTrainer.Core.Coaching;

public sealed class AchievementEvaluator
{
    public IReadOnlyList<Achievement> Evaluate(CoachingStats stats)
    {
        var milestones = new[]
        {
            Create("first-session", "First Session", "Complete your first saved practice session.", stats.SessionCount >= 1),
            Create("five-sessions", "Five Sessions", "Build a small baseline with 5 saved sessions.", stats.SessionCount >= 5),
            Create("twenty-five-sessions", "25 Sessions", "Complete 25 saved sessions.", stats.SessionCount >= 25),
            Create("hundred-sessions", "100 Sessions", "Complete 100 saved sessions.", stats.SessionCount >= 100),
            Create("accuracy-95", "Accuracy Control", "Complete a session at 95% accuracy or better.", stats.BestAccuracy >= 0.95),
            Create("net-50", "50 Net WPM", "Reach 50 net WPM in a saved session.", stats.BestNetWpm >= 50),
            Create("net-60", "60 Net WPM", "Reach 60 net WPM in a saved session.", stats.BestNetWpm >= 60),
            Create("net-70", "70 Net WPM", "Reach 70 net WPM in a saved session.", stats.BestNetWpm >= 70),
            Create("seven-day-streak", "Seven-Day Streak", "Practice on 7 local calendar days in a row.", stats.CurrentPracticeStreakDays >= 7),
            Create("weekly-target", "Weekly Target", "Reach your weekly practice time target.", stats.WeeklyPracticeMinutes >= stats.GoalWeeklyPracticeMinutes)
        };

        return milestones;
    }

    private static Achievement Create(string id, string title, string description, bool unlocked)
    {
        return new Achievement(id, title, description, unlocked);
    }
}

