using Microsoft.Data.Sqlite;
using TypingTrainer.Core.Learning;
using TypingTrainer.Core.Skill;
using TypingTrainer.Data.Database;

namespace TypingTrainer.Data.Services;

public sealed class SkillProfileQueryService : ISkillProfileQueryService
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly IUtcClock _clock;

    public SkillProfileQueryService(SqliteConnectionFactory connectionFactory, IUtcClock? clock = null)
    {
        _connectionFactory = connectionFactory;
        _clock = clock ?? new SystemUtcClock();
    }

    public async Task<UserSkillProfile> GetUserSkillProfileAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var summary = await LoadSummaryAsync(connection, cancellationToken).ConfigureAwait(false);

        if (summary.CompletedSessionCount == 0)
        {
            return SkillProfileDefaults.Empty(_clock.UtcNow.UtcDateTime);
        }

        var characterEvents = await LoadCharacterEventsAsync(connection, cancellationToken).ConfigureAwait(false);
        var characterStats = AnalyticsComputation.BuildCharacterStats(characterEvents);
        var bigramStats = AnalyticsComputation.BuildBigramStats(characterEvents);
        var learningProfile = await LoadLearningProfileAsync(connection, _clock.UtcNow, cancellationToken).ConfigureAwait(false);

        return new UserSkillProfile(
            characterStats
                .Where(stat => stat.Character.Length == 1)
                .ToDictionary(stat => stat.Character[0], ToCharacterSkill),
            bigramStats.ToDictionary(stat => stat.Bigram, ToBigramSkill),
            summary.CompletedSessionCount,
            TimeSpan.FromMilliseconds(summary.TotalPracticeMs),
            summary.CreatedAtUtc,
            learningProfile.DueTargets,
            learningProfile.MasterySummary);
    }

    private static CharacterSkill ToCharacterSkill(CharacterStat stat)
    {
        return new CharacterSkill(
            stat.Character[0],
            stat.ExposureCount,
            stat.CorrectCount,
            stat.IncorrectCount,
            stat.Accuracy,
            stat.MedianLatencyMs,
            stat.AverageLatencyMs,
            stat.WeaknessScore,
            CharacterUnlockPlanner.CalculateConfidence(stat.ExposureCount, stat.Accuracy, stat.MedianLatencyMs));
    }

    private static BigramSkill ToBigramSkill(BigramStat stat)
    {
        return new BigramSkill(
            stat.Bigram,
            stat.ExposureCount,
            stat.CorrectCount,
            stat.IncorrectCount,
            stat.Accuracy,
            stat.MedianLatencyMs,
            stat.AverageLatencyMs,
            stat.WeaknessScore);
    }

    private static async Task<SkillProfileSummary> LoadSummaryAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*), COALESCE(SUM(duration_ms), 0), MIN(started_at_utc)
            FROM practice_sessions;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new SkillProfileSummary(0, 0, DateTime.UtcNow);
        }

        var completedSessionCount = reader.GetInt32(0);
        var totalPracticeMs = reader.GetInt64(1);
        var createdAtUtc = reader.IsDBNull(2)
            ? DateTime.UtcNow
            : DateTimeOffset.Parse(reader.GetString(2)).UtcDateTime;

        return new SkillProfileSummary(completedSessionCount, totalPracticeMs, createdAtUtc);
    }

    private static async Task<IReadOnlyList<AnalyticsKeyEventRow>> LoadCharacterEventsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT session_id, position, expected_char, actual_char, event_kind,
                   is_correct, was_correction, delta_previous_ms, elapsed_ms, timestamp_ticks
            FROM (
                SELECT event.session_id, event.position, event.expected_char, event.actual_char,
                       event.event_kind, event.is_correct, event.was_correction,
                       event.delta_previous_ms, event.elapsed_ms, event.timestamp_ticks,
                       session.started_at_utc
                FROM key_events event
                INNER JOIN practice_sessions session ON session.id = event.session_id
                WHERE event.event_kind = 'Character'
                  AND event.expected_char IS NOT NULL
                ORDER BY session.started_at_utc DESC, event.timestamp_ticks DESC
                LIMIT 5000
            )
            ORDER BY session_id, timestamp_ticks ASC;
            """;

        var events = new List<AnalyticsKeyEventRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            events.Add(new AnalyticsKeyEventRow(
                Guid.Parse(reader.GetString(0)),
                reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5) == 1,
                reader.GetInt32(6) == 1,
                reader.IsDBNull(7) ? null : reader.GetDouble(7),
                reader.GetDouble(8),
                reader.GetInt64(9)));
        }

        return events;
    }

    private static async Task<LearningProfileSnapshot> LoadLearningProfileAsync(
        SqliteConnection connection,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var summary = await LoadMasterySummaryAsync(connection, nowUtc, cancellationToken).ConfigureAwait(false);
        var dueTargets = await LoadDueTargetsAsync(connection, nowUtc, cancellationToken).ConfigureAwait(false);
        return new LearningProfileSnapshot(dueTargets, summary);
    }

    private static async Task<MasterySummary> LoadMasterySummaryAsync(
        SqliteConnection connection,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT mastery_state,
                   COUNT(*),
                   SUM(CASE WHEN next_due_utc <= $nowUtc THEN 1 ELSE 0 END)
            FROM learning_items
            GROUP BY mastery_state;
            """;
        command.Parameters.AddWithValue("$nowUtc", nowUtc.ToString("O"));

        var counts = new Dictionary<MasteryState, int>();
        var dueCount = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var state = Enum.Parse<MasteryState>(reader.GetString(0));
            counts[state] = reader.GetInt32(1);
            dueCount += reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
        }

        return new MasterySummary(
            GetCount(counts, MasteryState.New),
            GetCount(counts, MasteryState.Learning),
            GetCount(counts, MasteryState.Unstable),
            GetCount(counts, MasteryState.Mastered),
            GetCount(counts, MasteryState.Regressing),
            dueCount);
    }

    private static async Task<IReadOnlyList<LearningTarget>> LoadDueTargetsAsync(
        SqliteConnection connection,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT target_type, target, mastery_state, weakness_score, stability_score,
                   exposure_count, accuracy, median_latency_ms, next_due_utc, primary_mistake_cause
            FROM learning_items
            WHERE next_due_utc <= $nowUtc
            ORDER BY next_due_utc ASC, weakness_score DESC, stability_score ASC
            LIMIT 16;
            """;
        command.Parameters.AddWithValue("$nowUtc", nowUtc.ToString("O"));

        var targets = new List<LearningTarget>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            targets.Add(new LearningTarget(
                Enum.Parse<LearningItemType>(reader.GetString(0)),
                reader.GetString(1),
                Enum.Parse<MasteryState>(reader.GetString(2)),
                reader.GetDouble(3),
                reader.GetDouble(4),
                reader.GetInt32(5),
                reader.GetDouble(6),
                reader.IsDBNull(7) ? null : reader.GetDouble(7),
                DateTimeOffset.Parse(reader.GetString(8)),
                Enum.Parse<MistakeCause>(reader.GetString(9))));
        }

        return targets;
    }

    private static int GetCount(IReadOnlyDictionary<MasteryState, int> counts, MasteryState state)
    {
        return counts.TryGetValue(state, out var count) ? count : 0;
    }

    private sealed record SkillProfileSummary(
        int CompletedSessionCount,
        long TotalPracticeMs,
        DateTime CreatedAtUtc);

    private sealed record LearningProfileSnapshot(
        IReadOnlyList<LearningTarget> DueTargets,
        MasterySummary MasterySummary);
}
