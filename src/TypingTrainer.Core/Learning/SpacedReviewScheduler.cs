namespace TypingTrainer.Core.Learning;

public static class SpacedReviewScheduler
{
    public static SpacedReviewSchedule Schedule(
        DateTimeOffset nowUtc,
        int previousIntervalDays,
        double previousEaseFactor,
        MasteryState state,
        double sessionAccuracy,
        bool hadIncorrectInput)
    {
        var ease = previousEaseFactor <= 0 ? 2.0 : previousEaseFactor;

        if (hadIncorrectInput
            || sessionAccuracy < 0.90
            || state is MasteryState.Unstable or MasteryState.Regressing)
        {
            ease = Math.Max(1.3, ease - 0.25);
            return new SpacedReviewSchedule(0, ease, nowUtc);
        }

        ease = Math.Min(2.8, ease + 0.10);
        var interval = previousIntervalDays <= 0
            ? 1
            : Math.Max(1, (int)Math.Ceiling(previousIntervalDays * ease));

        if (state == MasteryState.Mastered)
        {
            interval = Math.Max(3, interval);
        }

        return new SpacedReviewSchedule(interval, ease, nowUtc.AddDays(interval));
    }
}

public sealed record SpacedReviewSchedule(
    int IntervalDays,
    double EaseFactor,
    DateTimeOffset NextDueUtc);
