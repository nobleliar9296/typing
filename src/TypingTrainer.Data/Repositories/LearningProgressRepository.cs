using Microsoft.Data.Sqlite;
using System.Text.Json;
using TypingTrainer.Core.Learning;
using TypingTrainer.Data.Database;
using TypingTrainer.Data.Models;
using TypingTrainer.Data.Services;

namespace TypingTrainer.Data.Repositories;

public sealed class LearningProgressRepository : ILearningProgressRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly IUtcClock _clock;
    private readonly MasteryScorer _masteryScorer = new();

    public LearningProgressRepository(
        SqliteConnectionFactory connectionFactory,
        IUtcClock? clock = null)
    {
        _connectionFactory = connectionFactory;
        _clock = clock ?? new SystemUtcClock();
    }

    public async Task UpdateFromCompletedSessionAsync(
        StoredPracticeSession session,
        IReadOnlyList<StoredKeyEvent> events,
        CancellationToken cancellationToken = default)
    {
        var aggregates = BuildAggregates(session, events);
        if (aggregates.Count == 0)
        {
            return;
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var nowUtc = _clock.UtcNow;

        foreach (var aggregate in aggregates)
        {
            var previous = await GetExistingItemAsync(
                connection,
                (SqliteTransaction)transaction,
                aggregate.Type,
                aggregate.Target,
                cancellationToken).ConfigureAwait(false);
            var input = new LearningProgressInput(
                aggregate.Type,
                aggregate.Target,
                previous?.ExposureCount ?? 0,
                previous?.CorrectCount ?? 0,
                previous?.IncorrectCount ?? 0,
                previous?.MasteryState ?? MasteryState.New,
                previous?.IntervalDays ?? 0,
                previous?.EaseFactor ?? 2.0,
                aggregate.ExposureCount,
                aggregate.CorrectCount,
                aggregate.IncorrectCount,
                aggregate.MedianLatencyMs);
            var result = _masteryScorer.Score(input, nowUtc);
            var causeCounts = MergeCauseCounts(previous?.CauseCounts, aggregate.CauseCounts);
            var primaryCause = GetPrimaryCause(causeCounts);

            await UpsertItemAsync(
                connection,
                (SqliteTransaction)transaction,
                aggregate,
                result,
                causeCounts,
                primaryCause,
                nowUtc,
                cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<SessionLearningAggregate> BuildAggregates(
        StoredPracticeSession session,
        IReadOnlyList<StoredKeyEvent> events)
    {
        var characterEvents = events
            .Where(item => item.EventKind == "Character" && item.ExpectedChar is not null)
            .OrderBy(item => item.TimestampTicks)
            .ToArray();
        var aggregates = new List<SessionLearningAggregate>();

        aggregates.AddRange(characterEvents
            .GroupBy(item => item.ExpectedChar!.Value)
            .Select(group => BuildCharacterAggregate(session, group.Key, group.ToArray())));
        aggregates.AddRange(BuildBigramAggregates(session, characterEvents));

        return aggregates;
    }

    private static SessionLearningAggregate BuildCharacterAggregate(
        StoredPracticeSession session,
        char target,
        IReadOnlyList<StoredKeyEvent> events)
    {
        var correct = events.Count(item => item.IsCorrect);
        var latencies = events
            .Select(item => item.DeltaPreviousMs)
            .Where(value => value is >= 20 and <= 2000)
            .Select(value => value!.Value)
            .ToArray();
        var causes = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var item in events.Where(item => !item.IsCorrect && item.ActualChar is not null))
        {
            AddCause(causes, Classify(session, item));
        }

        return new SessionLearningAggregate(
            LearningItemType.Character,
            target.ToString(),
            events.Count,
            correct,
            events.Count - correct,
            Median(latencies),
            causes);
    }

    private static IReadOnlyList<SessionLearningAggregate> BuildBigramAggregates(
        StoredPracticeSession session,
        IReadOnlyList<StoredKeyEvent> characterEvents)
    {
        var samples = characterEvents
            .Zip(characterEvents.Skip(1), (Previous, Current) => (Previous, Current))
            .Where(pair => pair.Previous.ExpectedChar is not null
                && pair.Current.ExpectedChar is not null
                && pair.Current.Position == pair.Previous.Position + 1)
            .GroupBy(pair => $"{pair.Previous.ExpectedChar!.Value}{pair.Current.ExpectedChar!.Value}", StringComparer.Ordinal);
        var aggregates = new List<SessionLearningAggregate>();

        foreach (var group in samples)
        {
            var pairs = group.ToArray();
            var correct = pairs.Count(pair => pair.Previous.IsCorrect && pair.Current.IsCorrect);
            var latencies = pairs
                .Select(pair => pair.Current.DeltaPreviousMs)
                .Where(value => value is >= 20 and <= 2000)
                .Select(value => value!.Value)
                .ToArray();
            var causes = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var pair in pairs.Where(pair => !pair.Previous.IsCorrect || !pair.Current.IsCorrect))
            {
                var cause = !pair.Current.IsCorrect && pair.Current.ActualChar is not null
                    ? Classify(session, pair.Current)
                    : pair.Previous.ActualChar is not null
                        ? Classify(session, pair.Previous)
                        : MistakeCause.Other;
                AddCause(causes, cause);
            }

            aggregates.Add(new SessionLearningAggregate(
                LearningItemType.Bigram,
                group.Key,
                pairs.Length,
                correct,
                pairs.Length - correct,
                Median(latencies),
                causes));
        }

        return aggregates;
    }

    private static MistakeCause Classify(StoredPracticeSession session, StoredKeyEvent item)
    {
        if (item.ExpectedChar is not char expected || item.ActualChar is not char actual)
        {
            return MistakeCause.Other;
        }

        return new MistakeCauseClassifier().Classify(
            expected,
            actual,
            item.DeltaPreviousMs,
            item.ElapsedMs,
            session.DurationMs);
    }

    private async Task<StoredLearningItem?> GetExistingItemAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        LearningItemType type,
        string target,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT exposure_count, correct_count, incorrect_count, mastery_state,
                   interval_days, ease_factor, cause_counts_json
            FROM learning_items
            WHERE target_type = $targetType AND target = $target;
            """;
        command.Parameters.AddWithValue("$targetType", type.ToString());
        command.Parameters.AddWithValue("$target", target);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new StoredLearningItem(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            Enum.Parse<MasteryState>(reader.GetString(3)),
            reader.GetInt32(4),
            reader.GetDouble(5),
            ReadCauseCounts(reader.GetString(6)));
    }

    private static async Task UpsertItemAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SessionLearningAggregate aggregate,
        LearningProgressResult result,
        IReadOnlyDictionary<string, int> causeCounts,
        MistakeCause primaryCause,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO learning_items (
                target_type, target, exposure_count, correct_count, incorrect_count,
                accuracy, median_latency_ms, weakness_score, stability_score, mastery_state,
                interval_days, ease_factor, last_seen_utc, next_due_utc,
                primary_mistake_cause, cause_counts_json
            )
            VALUES (
                $targetType, $target, $exposureCount, $correctCount, $incorrectCount,
                $accuracy, $medianLatencyMs, $weaknessScore, $stabilityScore, $masteryState,
                $intervalDays, $easeFactor, $lastSeenUtc, $nextDueUtc,
                $primaryMistakeCause, $causeCountsJson
            )
            ON CONFLICT(target_type, target) DO UPDATE SET
                exposure_count = excluded.exposure_count,
                correct_count = excluded.correct_count,
                incorrect_count = excluded.incorrect_count,
                accuracy = excluded.accuracy,
                median_latency_ms = excluded.median_latency_ms,
                weakness_score = excluded.weakness_score,
                stability_score = excluded.stability_score,
                mastery_state = excluded.mastery_state,
                interval_days = excluded.interval_days,
                ease_factor = excluded.ease_factor,
                last_seen_utc = excluded.last_seen_utc,
                next_due_utc = excluded.next_due_utc,
                primary_mistake_cause = excluded.primary_mistake_cause,
                cause_counts_json = excluded.cause_counts_json;
            """;
        command.Parameters.AddWithValue("$targetType", aggregate.Type.ToString());
        command.Parameters.AddWithValue("$target", aggregate.Target);
        command.Parameters.AddWithValue("$exposureCount", result.ExposureCount);
        command.Parameters.AddWithValue("$correctCount", result.CorrectCount);
        command.Parameters.AddWithValue("$incorrectCount", result.IncorrectCount);
        command.Parameters.AddWithValue("$accuracy", result.Accuracy);
        command.Parameters.AddWithValue("$medianLatencyMs", result.MedianLatencyMs is null ? DBNull.Value : result.MedianLatencyMs.Value);
        command.Parameters.AddWithValue("$weaknessScore", result.WeaknessScore);
        command.Parameters.AddWithValue("$stabilityScore", result.StabilityScore);
        command.Parameters.AddWithValue("$masteryState", result.MasteryState.ToString());
        command.Parameters.AddWithValue("$intervalDays", result.IntervalDays);
        command.Parameters.AddWithValue("$easeFactor", result.EaseFactor);
        command.Parameters.AddWithValue("$lastSeenUtc", nowUtc.ToString("O"));
        command.Parameters.AddWithValue("$nextDueUtc", result.NextDueUtc.ToString("O"));
        command.Parameters.AddWithValue("$primaryMistakeCause", primaryCause.ToString());
        command.Parameters.AddWithValue("$causeCountsJson", JsonSerializer.Serialize(causeCounts, JsonOptions));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyDictionary<string, int> MergeCauseCounts(
        IReadOnlyDictionary<string, int>? existing,
        IReadOnlyDictionary<string, int> current)
    {
        var result = new Dictionary<string, int>(existing ?? new Dictionary<string, int>(), StringComparer.Ordinal);
        foreach (var pair in current)
        {
            result[pair.Key] = result.TryGetValue(pair.Key, out var count)
                ? count + pair.Value
                : pair.Value;
        }

        return result;
    }

    private static MistakeCause GetPrimaryCause(IReadOnlyDictionary<string, int> causeCounts)
    {
        var cause = causeCounts
            .Where(pair => pair.Value > 0)
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .FirstOrDefault();

        return cause.Key is null
            ? MistakeCause.Other
            : Enum.Parse<MistakeCause>(cause.Key);
    }

    private static void AddCause(IDictionary<string, int> causes, MistakeCause cause)
    {
        var key = cause.ToString();
        causes[key] = causes.TryGetValue(key, out var count) ? count + 1 : 1;
    }

    private static IReadOnlyDictionary<string, int> ReadCauseCounts(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(json, JsonOptions)
                ?? new Dictionary<string, int>(StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }
    }

    private static double? Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values.OrderBy(value => value).ToArray();
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2.0;
    }

    private sealed record SessionLearningAggregate(
        LearningItemType Type,
        string Target,
        int ExposureCount,
        int CorrectCount,
        int IncorrectCount,
        double? MedianLatencyMs,
        IReadOnlyDictionary<string, int> CauseCounts);

    private sealed record StoredLearningItem(
        int ExposureCount,
        int CorrectCount,
        int IncorrectCount,
        MasteryState MasteryState,
        int IntervalDays,
        double EaseFactor,
        IReadOnlyDictionary<string, int> CauseCounts);
}
