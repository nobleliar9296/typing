using System.Diagnostics;

namespace TypingTrainer.Core.Analytics;

public static class MetricsCalculator
{
    public static double CalculateRawWpm(int typedCharacterKeypresses, double elapsedMs)
    {
        if (typedCharacterKeypresses <= 0 || elapsedMs <= 0)
        {
            return 0;
        }

        var elapsedMinutes = elapsedMs / 60_000.0;
        return (typedCharacterKeypresses / 5.0) / elapsedMinutes;
    }

    public static double CalculateAccuracy(int correctCharacterKeypresses, int totalCharacterKeypresses)
    {
        if (totalCharacterKeypresses <= 0)
        {
            return 1.0;
        }

        return correctCharacterKeypresses / (double)totalCharacterKeypresses;
    }

    public static double TicksToMilliseconds(long startTimestampTicks, long currentTimestampTicks)
    {
        if (currentTimestampTicks <= startTimestampTicks)
        {
            return 0;
        }

        return (currentTimestampTicks - startTimestampTicks) * 1000.0 / Stopwatch.Frequency;
    }
}
