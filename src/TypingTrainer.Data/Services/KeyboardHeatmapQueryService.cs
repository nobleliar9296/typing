using Microsoft.Data.Sqlite;
using TypingTrainer.Data.Database;
using TypingTrainer.Data.Models;

namespace TypingTrainer.Data.Services;

public sealed class KeyboardHeatmapQueryService : IKeyboardHeatmapQueryService
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public KeyboardHeatmapQueryService(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<KeyboardHeatmapKeyRow>> GetHeatmapAsync(
        AnalyticsRange range,
        string? modeFilter = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var events = await LoadEventsAsync(connection, modeFilter, cancellationToken).ConfigureAwait(false);
        var stats = AnalyticsComputation.BuildCharacterStats(events);

        return stats
            .Select(stat => new KeyboardHeatmapKeyRow(
                stat.DisplayCharacter,
                stat.Character.Length == 0 ? '\0' : stat.Character[0],
                stat.ExposureCount,
                stat.CorrectCount,
                stat.IncorrectCount,
                stat.Accuracy,
                stat.MedianLatencyMs,
                stat.WeaknessScore))
            .OrderBy(row => row.KeyLabel, StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task<IReadOnlyList<AnalyticsKeyEventRow>> LoadEventsAsync(
        SqliteConnection connection,
        string? modeFilter,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT event.session_id, event.position, event.expected_char, event.actual_char,
                   event.event_kind, event.is_correct, event.was_correction,
                   event.delta_previous_ms, event.elapsed_ms, event.timestamp_ticks
            FROM key_events event
            INNER JOIN practice_sessions session ON session.id = event.session_id
            WHERE event.event_kind = 'Character'
              AND event.expected_char IS NOT NULL
              AND ($modeFilter IS NULL OR LOWER(session.mode) = LOWER($modeFilter))
            ORDER BY event.session_id, event.timestamp_ticks ASC;
            """;
        command.Parameters.AddWithValue("$modeFilter", string.IsNullOrWhiteSpace(modeFilter) ? DBNull.Value : modeFilter);

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
}

