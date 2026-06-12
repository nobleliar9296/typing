using Microsoft.Data.Sqlite;
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

        return new UserSkillProfile(
            characterStats
                .Where(stat => stat.Character.Length == 1)
                .ToDictionary(stat => stat.Character[0], ToCharacterSkill),
            bigramStats.ToDictionary(stat => stat.Bigram, ToBigramSkill),
            summary.CompletedSessionCount,
            TimeSpan.FromMilliseconds(summary.TotalPracticeMs),
            summary.CreatedAtUtc);
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
            FROM key_events
            WHERE event_kind = 'Character'
              AND expected_char IS NOT NULL
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

    private sealed record SkillProfileSummary(
        int CompletedSessionCount,
        long TotalPracticeMs,
        DateTime CreatedAtUtc);
}
