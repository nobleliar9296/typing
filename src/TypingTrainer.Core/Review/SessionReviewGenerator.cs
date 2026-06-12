using TypingTrainer.Core.Models;
using TypingTrainer.Core.Skill;
using TypingTrainer.Core.Typing;

namespace TypingTrainer.Core.Review;

public sealed class SessionReviewGenerator
{
    private const double MinimumLatencyMs = 20;
    private const double MaximumLatencyMs = 2000;
    private const int Limit = 5;

    public SessionReview Generate(SessionSummary summary, IReadOnlyList<TypingInputEvent> events)
    {
        var characterEvents = events
            .Where(item => item.Kind == InputEventKind.Character && item.ExpectedChar is not null)
            .OrderBy(item => item.TimestampTicks)
            .ToArray();
        var correctedErrors = events.Count(IsCorrectiveBackspace);
        var characterRows = BuildCharacterRows(characterEvents).ToArray();
        var bigramRows = BuildBigramRows(characterEvents).ToArray();

        var missedKeys = characterRows
            .Where(row => row.IncorrectCount > 0)
            .OrderByDescending(row => row.IncorrectCount)
            .ThenBy(row => row.Accuracy)
            .ThenBy(row => row.DisplayCharacter, StringComparer.Ordinal)
            .Take(Limit)
            .ToArray();
        var slowestKeys = characterRows
            .Where(row => row.MedianLatencyMs is not null)
            .OrderByDescending(row => row.MedianLatencyMs)
            .ThenBy(row => row.DisplayCharacter, StringComparer.Ordinal)
            .Take(Limit)
            .ToArray();
        var weakestBigrams = bigramRows
            .Where(row => row.Samples > 0)
            .OrderByDescending(row => row.WeaknessScore)
            .ThenByDescending(row => row.MedianLatencyMs ?? 0)
            .Take(Limit)
            .ToArray();

        return new SessionReview(
            correctedErrors,
            summary.CurrentErrors,
            missedKeys,
            slowestKeys,
            weakestBigrams,
            BuildNotes(summary, missedKeys, slowestKeys, weakestBigrams));
    }

    public UserSkillProfile CreatePracticeProfile(SessionReview review, DateTime? createdAtUtc = null)
    {
        var characterSkills = review.FocusCharacters
            .Select(character =>
            {
                var row = review.MostMissedKeys
                    .Concat(review.SlowestKeys)
                    .Where(item => item.Character == character)
                    .OrderByDescending(item => item.WeaknessScore)
                    .First();
                var exposure = Math.Max(5, row.Samples);
                var incorrect = Math.Max(1, row.IncorrectCount);
                var correct = Math.Max(0, exposure - incorrect);
                var accuracy = Divide(correct, exposure);

                return new CharacterSkill(
                    character,
                    exposure,
                    correct,
                    incorrect,
                    accuracy,
                    row.MedianLatencyMs,
                    row.MedianLatencyMs,
                    Math.Max(row.WeaknessScore, 0.5),
                    CharacterUnlockPlanner.CalculateConfidence(exposure, accuracy, row.MedianLatencyMs));
            })
            .ToDictionary(skill => skill.Character);

        var bigramSkills = review.FocusBigrams
            .Select(bigram =>
            {
                var row = review.WeakestBigrams.Single(item => item.Bigram == bigram);
                var exposure = Math.Max(3, row.Samples);
                var incorrect = Math.Max(1, row.IncorrectCount);
                var correct = Math.Max(0, exposure - incorrect);
                var accuracy = Divide(correct, exposure);

                return new BigramSkill(
                    bigram,
                    exposure,
                    correct,
                    incorrect,
                    accuracy,
                    row.MedianLatencyMs,
                    row.MedianLatencyMs,
                    Math.Max(row.WeaknessScore, 0.5));
            })
            .ToDictionary(skill => skill.Bigram, StringComparer.Ordinal);

        return new UserSkillProfile(
            characterSkills,
            bigramSkills,
            CompletedSessionCount: 1,
            TotalPracticeTime: TimeSpan.Zero,
            CreatedAtUtc: createdAtUtc ?? DateTime.UtcNow);
    }

    private static IEnumerable<SessionReviewKeyRow> BuildCharacterRows(IReadOnlyList<TypingInputEvent> events)
    {
        var latencySamplesByCharacter = events
            .Where(item => item.DeltaFromPreviousMs is >= MinimumLatencyMs and <= MaximumLatencyMs)
            .GroupBy(item => item.ExpectedChar!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.DeltaFromPreviousMs!.Value).ToArray());
        var globalMedianLatency = Median(latencySamplesByCharacter.Values.SelectMany(item => item).ToArray());

        foreach (var group in events.GroupBy(item => item.ExpectedChar!.Value))
        {
            var samples = group.Count();
            var correct = group.Count(item => item.IsCorrect);
            var incorrect = samples - correct;
            var accuracy = Divide(correct, samples);
            latencySamplesByCharacter.TryGetValue(group.Key, out var latencies);
            var medianLatency = Median(latencies ?? Array.Empty<double>());

            yield return new SessionReviewKeyRow(
                group.Key,
                DisplayCharacter(group.Key),
                samples,
                correct,
                incorrect,
                accuracy,
                medianLatency,
                WeaknessScore.Calculate(samples, accuracy, medianLatency, globalMedianLatency));
        }
    }

    private static IEnumerable<SessionReviewBigramRow> BuildBigramRows(IReadOnlyList<TypingInputEvent> events)
    {
        var pairs = events
            .Zip(events.Skip(1), (Previous, Current) => (Previous, Current))
            .Where(pair => pair.Previous.ExpectedChar is not null
                && pair.Current.ExpectedChar is not null
                && pair.Current.Position == pair.Previous.Position + 1)
            .Select(pair => new BigramSample(
                $"{pair.Previous.ExpectedChar!.Value}{pair.Current.ExpectedChar!.Value}",
                pair.Previous.IsCorrect && pair.Current.IsCorrect,
                pair.Current.DeltaFromPreviousMs))
            .ToArray();
        var latencySamplesByBigram = pairs
            .Where(item => item.LatencyMs is >= MinimumLatencyMs and <= MaximumLatencyMs)
            .GroupBy(item => item.Bigram)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.LatencyMs!.Value).ToArray(),
                StringComparer.Ordinal);
        var globalMedianLatency = Median(latencySamplesByBigram.Values.SelectMany(item => item).ToArray());

        foreach (var group in pairs.GroupBy(item => item.Bigram, StringComparer.Ordinal))
        {
            var samples = group.Count();
            var correct = group.Count(item => item.IsCorrect);
            var incorrect = samples - correct;
            var accuracy = Divide(correct, samples);
            latencySamplesByBigram.TryGetValue(group.Key, out var latencies);
            var medianLatency = Median(latencies ?? Array.Empty<double>());

            yield return new SessionReviewBigramRow(
                group.Key,
                DisplayBigram(group.Key),
                samples,
                correct,
                incorrect,
                accuracy,
                medianLatency,
                WeaknessScore.Calculate(samples, accuracy, medianLatency, globalMedianLatency));
        }
    }

    private static IReadOnlyList<string> BuildNotes(
        SessionSummary summary,
        IReadOnlyList<SessionReviewKeyRow> missedKeys,
        IReadOnlyList<SessionReviewKeyRow> slowestKeys,
        IReadOnlyList<SessionReviewBigramRow> weakestBigrams)
    {
        var notes = new List<string>();

        if (summary.TypedCharacterKeypresses == 0)
        {
            notes.Add("No typed characters were recorded in this session.");
        }
        else if (missedKeys.Count == 0 && summary.CurrentErrors == 0)
        {
            notes.Add("Clean accuracy. Keep building speed with paragraph flow.");
        }
        else if (missedKeys.Count > 0)
        {
            notes.Add($"Most missed key: {missedKeys[0].DisplayCharacter}.");
        }

        if (slowestKeys.Count > 0)
        {
            notes.Add($"Slowest key: {slowestKeys[0].DisplayCharacter} at {slowestKeys[0].MedianLatencyMs:0} ms.");
        }

        if (weakestBigrams.Count > 0)
        {
            notes.Add($"Weakest transition: {weakestBigrams[0].DisplayBigram}.");
        }

        if (notes.Count == 0)
        {
            notes.Add("Complete a few more sessions to unlock richer review guidance.");
        }

        notes.Add(summary.CurrentErrors > 0
            ? "Use Practice These Mistakes before moving to a new lesson."
            : "Repeat this mode once more if you want to stabilize the pace.");

        return notes.Take(3).ToArray();
    }

    private static bool IsCorrectiveBackspace(TypingInputEvent inputEvent)
    {
        return inputEvent.Kind == InputEventKind.Backspace
            && inputEvent.ExpectedChar is char expected
            && inputEvent.ActualChar is char actual
            && expected != actual;
    }

    private static double Divide(int numerator, int denominator)
    {
        return denominator == 0 ? 0 : numerator / (double)denominator;
    }

    private static double? Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values.OrderBy(value => value).ToArray();
        var midpoint = sorted.Length / 2;
        return sorted.Length % 2 == 1
            ? sorted[midpoint]
            : (sorted[midpoint - 1] + sorted[midpoint]) / 2.0;
    }

    private static string DisplayCharacter(char character)
    {
        return character == ' ' ? "Space" : character.ToString();
    }

    private static string DisplayBigram(string bigram)
    {
        return string.Join(' ', bigram.Select(DisplayCharacter));
    }

    private sealed record BigramSample(string Bigram, bool IsCorrect, double? LatencyMs);
}

