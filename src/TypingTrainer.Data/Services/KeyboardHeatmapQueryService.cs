using Microsoft.Data.Sqlite;
using TypingTrainer.Core.Keyboard;
using TypingTrainer.Data.Database;
using TypingTrainer.Data.Models;

namespace TypingTrainer.Data.Services;

public sealed class KeyboardHeatmapQueryService : IKeyboardHeatmapQueryService
{
    private const double MinimumLatencyMs = 20;
    private const double MaximumLatencyMs = 2000;

    private static readonly QwertyCharacterToKeyMapper KeyMapper = new();
    private static readonly IReadOnlyDictionary<string, VisualKeyboardKey> LayoutKeys =
        QwertyVisualKeyboardLayout
            .Create()
            .Rows
            .SelectMany(row => row.Keys)
            .ToDictionary(key => key.Id, StringComparer.Ordinal);

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
        var samples = events
            .Select(ToPhysicalKeySample)
            .OfType<PhysicalKeySample>()
            .ToArray();
        var globalMedianLatency = AnalyticsComputation.Median(samples
            .Select(row => row.DeltaPreviousMs)
            .Where(IsLatencySample)
            .Select(value => value!.Value));

        return samples
            .GroupBy(sample => sample.KeyId, StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.First();
                var exposureCount = group.Count();
                var correctCount = group.Count(row => row.IsCorrect);
                var accuracy = AnalyticsComputation.Divide(correctCount, exposureCount);
                var latencySamples = group
                    .Select(row => row.DeltaPreviousMs)
                    .Where(IsLatencySample)
                    .Select(value => value!.Value)
                    .ToArray();
                var medianLatency = AnalyticsComputation.Median(latencySamples);

                return new KeyboardHeatmapKeyRow(
                    first.KeyLabel,
                    first.Character,
                    exposureCount,
                    correctCount,
                    exposureCount - correctCount,
                    accuracy,
                    medianLatency,
                    WeaknessScoreCalculator.Calculate(exposureCount, accuracy, medianLatency, globalMedianLatency));
            })
            .OrderBy(row => row.KeyLabel, StringComparer.Ordinal)
            .ToArray();
    }

    private static PhysicalKeySample? ToPhysicalKeySample(AnalyticsKeyEventRow row)
    {
        if (string.IsNullOrEmpty(row.ExpectedChar))
        {
            return null;
        }

        var expectedCharacter = row.ExpectedChar[0];
        var mapping = KeyMapper.Map(expectedCharacter);
        if (mapping is null)
        {
            var fallbackLabel = ToFallbackLabel(expectedCharacter);
            return new PhysicalKeySample(
                $"Fallback:{fallbackLabel}",
                fallbackLabel,
                expectedCharacter,
                row.IsCorrect,
                row.DeltaPreviousMs);
        }

        var key = LayoutKeys.TryGetValue(mapping.KeyId, out var layoutKey)
            ? layoutKey
            : null;

        return new PhysicalKeySample(
            mapping.KeyId,
            key?.PrimaryLabel ?? mapping.KeyId,
            ToRepresentativeCharacter(key, expectedCharacter),
            row.IsCorrect,
            row.DeltaPreviousMs);
    }

    private static char ToRepresentativeCharacter(VisualKeyboardKey? key, char fallback)
    {
        if (key?.Output?.Length > 0)
        {
            return key.Output[0];
        }

        return key?.Role switch
        {
            KeyRole.Space => ' ',
            KeyRole.Enter => '\n',
            KeyRole.Tab => '\t',
            _ => char.ToLowerInvariant(fallback)
        };
    }

    private static string ToFallbackLabel(char character)
    {
        return character switch
        {
            ' ' => "Space",
            '\t' => "Tab",
            '\n' or '\r' => "Enter",
            _ => character.ToString()
        };
    }

    private static bool IsLatencySample(double? value)
    {
        return value is >= MinimumLatencyMs and <= MaximumLatencyMs;
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

    private sealed record PhysicalKeySample(
        string KeyId,
        string KeyLabel,
        char Character,
        bool IsCorrect,
        double? DeltaPreviousMs);
}
