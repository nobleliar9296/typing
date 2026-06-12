namespace TypingTrainer.Data.Services;

internal static class AnalyticsComputation
{
    private const double MinimumLatencyMs = 20;
    private const double MaximumLatencyMs = 2000;

    public static IReadOnlyList<CharacterStat> BuildCharacterStats(
        IReadOnlyList<AnalyticsKeyEventRow> characterEvents)
    {
        var globalMedianLatency = Median(characterEvents
            .Select(row => row.DeltaPreviousMs)
            .Where(IsLatencySample)
            .Select(value => value!.Value));

        return characterEvents
            .Where(row => row.ExpectedChar is not null)
            .GroupBy(row => row.ExpectedChar!)
            .Select(group =>
            {
                var exposureCount = group.Count();
                var correctCount = group.Count(row => row.IsCorrect);
                var accuracy = Divide(correctCount, exposureCount);
                var latencySamples = group
                    .Select(row => row.DeltaPreviousMs)
                    .Where(IsLatencySample)
                    .Select(value => value!.Value)
                    .ToArray();
                var medianLatency = Median(latencySamples);

                return new CharacterStat(
                    group.Key,
                    ToDisplayCharacter(group.Key),
                    exposureCount,
                    correctCount,
                    exposureCount - correctCount,
                    accuracy,
                    latencySamples.Length == 0 ? null : latencySamples.Average(),
                    medianLatency,
                    WeaknessScoreCalculator.Calculate(exposureCount, accuracy, medianLatency, globalMedianLatency));
            })
            .ToArray();
    }

    public static IReadOnlyList<BigramStat> BuildBigramStats(
        IReadOnlyList<AnalyticsKeyEventRow> characterEvents)
    {
        var bigramSamples = new List<BigramSample>();

        foreach (var sessionGroup in characterEvents.GroupBy(row => row.SessionId))
        {
            AnalyticsKeyEventRow? previous = null;

            foreach (var current in sessionGroup.OrderBy(row => row.TimestampTicks))
            {
                if (previous is not null
                    && current.Position == previous.Position + 1
                    && previous.ExpectedChar is not null
                    && current.ExpectedChar is not null)
                {
                    bigramSamples.Add(new BigramSample(
                        previous.ExpectedChar + current.ExpectedChar,
                        previous.IsCorrect && current.IsCorrect,
                        current.DeltaPreviousMs));
                }

                previous = current;
            }
        }

        var globalMedianLatency = Median(bigramSamples
            .Select(row => row.LatencyMs)
            .Where(IsLatencySample)
            .Select(value => value!.Value));

        return bigramSamples
            .GroupBy(sample => sample.Bigram)
            .Select(group =>
            {
                var exposureCount = group.Count();
                var correctCount = group.Count(sample => sample.IsCorrect);
                var accuracy = Divide(correctCount, exposureCount);
                var latencySamples = group
                    .Select(sample => sample.LatencyMs)
                    .Where(IsLatencySample)
                    .Select(value => value!.Value)
                    .ToArray();
                var medianLatency = Median(latencySamples);

                return new BigramStat(
                    group.Key,
                    ToDisplayBigram(group.Key),
                    exposureCount,
                    correctCount,
                    exposureCount - correctCount,
                    accuracy,
                    latencySamples.Length == 0 ? null : latencySamples.Average(),
                    medianLatency,
                    WeaknessScoreCalculator.Calculate(exposureCount, accuracy, medianLatency, globalMedianLatency));
            })
            .ToArray();
    }

    public static double? Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(value => value).ToArray();
        if (sorted.Length == 0)
        {
            return null;
        }

        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2.0;
    }

    public static double Divide(int numerator, int denominator)
    {
        return denominator == 0 ? 0 : numerator / (double)denominator;
    }

    private static bool IsLatencySample(double? value)
    {
        return value is >= MinimumLatencyMs and <= MaximumLatencyMs;
    }

    private static string ToDisplayCharacter(string value)
    {
        return value switch
        {
            " " => "Space",
            "\t" => "Tab",
            "\n" => "Enter",
            "\r" => "Enter",
            _ => value
        };
    }

    private static string ToDisplayBigram(string bigram)
    {
        return string.Join(" ", bigram.Select(character => ToDisplayCharacter(character.ToString())));
    }

    private sealed record BigramSample(
        string Bigram,
        bool IsCorrect,
        double? LatencyMs);
}

internal sealed record AnalyticsKeyEventRow(
    Guid SessionId,
    int Position,
    string? ExpectedChar,
    string? ActualChar,
    string EventKind,
    bool IsCorrect,
    bool WasCorrection,
    double? DeltaPreviousMs,
    double ElapsedMs,
    long TimestampTicks);

internal sealed record CharacterStat(
    string Character,
    string DisplayCharacter,
    int ExposureCount,
    int CorrectCount,
    int IncorrectCount,
    double Accuracy,
    double? AverageLatencyMs,
    double? MedianLatencyMs,
    double WeaknessScore);

internal sealed record BigramStat(
    string Bigram,
    string DisplayBigram,
    int ExposureCount,
    int CorrectCount,
    int IncorrectCount,
    double Accuracy,
    double? AverageLatencyMs,
    double? MedianLatencyMs,
    double WeaknessScore);
