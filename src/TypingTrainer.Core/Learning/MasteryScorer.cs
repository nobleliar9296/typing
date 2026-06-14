using TypingTrainer.Core.Skill;

namespace TypingTrainer.Core.Learning;

public sealed class MasteryScorer
{
    public LearningProgressResult Score(
        LearningProgressInput input,
        DateTimeOffset nowUtc)
    {
        var exposureCount = input.PreviousExposureCount + input.SessionExposureCount;
        var correctCount = input.PreviousCorrectCount + input.SessionCorrectCount;
        var incorrectCount = input.PreviousIncorrectCount + input.SessionIncorrectCount;
        var accuracy = exposureCount == 0 ? 0 : correctCount / (double)exposureCount;
        var sessionAccuracy = input.SessionExposureCount == 0
            ? accuracy
            : input.SessionCorrectCount / (double)input.SessionExposureCount;
        var medianLatency = input.SessionMedianLatencyMs;
        var stability = CalculateStability(exposureCount, accuracy, medianLatency);
        var state = DetermineState(
            exposureCount,
            accuracy,
            sessionAccuracy,
            input.SessionExposureCount,
            medianLatency,
            input.PreviousMasteryState);
        var weakness = WeaknessScore.Calculate(exposureCount, accuracy, medianLatency, 180);
        var schedule = SpacedReviewScheduler.Schedule(
            nowUtc,
            input.PreviousIntervalDays,
            input.PreviousEaseFactor,
            state,
            sessionAccuracy,
            input.SessionIncorrectCount > 0);

        return new LearningProgressResult(
            exposureCount,
            correctCount,
            incorrectCount,
            accuracy,
            medianLatency,
            weakness,
            stability,
            state,
            schedule.IntervalDays,
            schedule.EaseFactor,
            schedule.NextDueUtc);
    }

    private static MasteryState DetermineState(
        int exposureCount,
        double accuracy,
        double sessionAccuracy,
        int sessionExposureCount,
        double? medianLatencyMs,
        MasteryState previousState)
    {
        if (exposureCount < 5)
        {
            return MasteryState.New;
        }

        if (previousState == MasteryState.Mastered
            && sessionExposureCount >= 3
            && sessionAccuracy < 0.85)
        {
            return MasteryState.Regressing;
        }

        if (exposureCount >= 30
            && accuracy >= 0.96
            && (medianLatencyMs is null or <= 360))
        {
            return MasteryState.Mastered;
        }

        if (exposureCount >= 8 && accuracy < 0.85)
        {
            return MasteryState.Unstable;
        }

        return MasteryState.Learning;
    }

    private static double CalculateStability(
        int exposureCount,
        double accuracy,
        double? medianLatencyMs)
    {
        var exposureFactor = Math.Min(1.0, exposureCount / 40.0);
        var latencyFactor = medianLatencyMs is double latency
            ? Math.Clamp(1.0 - Math.Max(0, latency - 180) / 600.0, 0.0, 1.0)
            : 0.65;
        return Math.Clamp((0.70 * accuracy + 0.30 * latencyFactor) * exposureFactor, 0.0, 1.0);
    }
}
