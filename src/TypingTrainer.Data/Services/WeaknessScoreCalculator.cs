namespace TypingTrainer.Data.Services;

public static class WeaknessScoreCalculator
{
    public static double Calculate(
        int exposureCount,
        double accuracy,
        double? medianLatencyMs,
        double? globalMedianLatencyMs)
    {
        if (exposureCount <= 0)
        {
            return 0;
        }

        var accuracyPenalty = 1.0 - Clamp(accuracy, 0, 1);
        var speedPenalty = 0.0;

        if (medianLatencyMs is double median && globalMedianLatencyMs is double globalMedian && globalMedian > 0)
        {
            speedPenalty = Clamp((median - globalMedian) / globalMedian, 0, 2);
        }

        var exposureFactor = Math.Min(1.0, exposureCount / 30.0);
        var weaknessScore = exposureFactor * ((0.65 * accuracyPenalty) + (0.35 * speedPenalty));
        return Clamp(weaknessScore, 0, 1);
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        return Math.Min(maximum, Math.Max(minimum, value));
    }
}
