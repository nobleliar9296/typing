using Microsoft.Data.Sqlite;
using TypingTrainer.Data.Database;
using TypingTrainer.Data.Models;

namespace TypingTrainer.Data.Services;

public sealed class AnalyticsQueryService : IAnalyticsQueryService
{
    private const int RecentSessionLimit = 10;
    private const int AnalyticsLimit = 8;
    private const double MinimumLatencyMs = 20;
    private const double MaximumLatencyMs = 2000;

    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly IUtcClock _clock;

    public AnalyticsQueryService(SqliteConnectionFactory connectionFactory, IUtcClock? clock = null)
    {
        _connectionFactory = connectionFactory;
        _clock = clock ?? new SystemUtcClock();
    }

    public async Task<DashboardSnapshot> GetDashboardSnapshotAsync(
        AnalyticsRange range,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sessions = await LoadSessionsAsync(connection, cancellationToken).ConfigureAwait(false);
        var filteredSessions = FilterSessions(sessions, range).ToArray();
        var sessionIds = filteredSessions.Select(session => session.Id).ToHashSet();
        var characterEvents = sessionIds.Count == 0
            ? Array.Empty<AnalyticsKeyEventRow>()
            : await LoadCharacterEventsAsync(connection, sessionIds, cancellationToken).ConfigureAwait(false);

        var characterRows = BuildCharacterRows(characterEvents);
        var bigramRows = BuildBigramRows(characterEvents);

        return new DashboardSnapshot(
            BuildSummary(filteredSessions),
            BuildDailyMetrics(filteredSessions),
            BuildRecentSessions(filteredSessions),
            characterRows
                .OrderByDescending(row => row.WeaknessScore)
                .ThenByDescending(row => row.ExposureCount)
                .Take(AnalyticsLimit)
                .ToArray(),
            characterRows
                .Where(row => row.ExposureCount >= 3 && row.MedianLatencyMs is not null)
                .OrderByDescending(row => row.MedianLatencyMs)
                .ThenByDescending(row => row.ExposureCount)
                .Take(AnalyticsLimit)
                .ToArray(),
            bigramRows
                .OrderByDescending(row => row.WeaknessScore)
                .ThenByDescending(row => row.ExposureCount)
                .Take(AnalyticsLimit)
                .ToArray(),
            bigramRows
                .Where(row => row.ExposureCount >= 3 && row.MedianLatencyMs is not null)
                .OrderByDescending(row => row.MedianLatencyMs)
                .ThenByDescending(row => row.ExposureCount)
                .Take(AnalyticsLimit)
                .ToArray());
    }

    private IEnumerable<SessionRow> FilterSessions(
        IReadOnlyList<SessionRow> sessions,
        AnalyticsRange range)
    {
        if (range == AnalyticsRange.AllTime)
        {
            return sessions;
        }

        var days = range == AnalyticsRange.Last7Days ? 7 : 30;
        var startUtc = _clock.UtcNow.AddDays(-days);
        return sessions.Where(session => session.StartedAtUtc >= startUtc);
    }

    private static DashboardSummary BuildSummary(IReadOnlyList<SessionRow> sessions)
    {
        if (sessions.Count == 0)
        {
            return new DashboardSummary(
                SessionCount: 0,
                TotalPracticeTime: TimeSpan.Zero,
                AverageRawWpm: 0,
                AverageNetWpm: 0,
                BestNetWpm: 0,
                Accuracy: 0,
                AverageConsistency: null,
                TotalKeypresses: 0,
                CorrectKeypresses: 0,
                IncorrectKeypresses: 0,
                CorrectedErrors: 0,
                UncorrectedErrors: 0);
        }

        var totalKeypresses = sessions.Sum(session => session.TotalKeypresses);
        var correctKeypresses = sessions.Sum(session => session.CorrectKeypresses);
        var consistencySamples = sessions
            .Where(session => session.Consistency is not null)
            .Select(session => session.Consistency!.Value)
            .ToArray();

        return new DashboardSummary(
            sessions.Count,
            TimeSpan.FromMilliseconds(sessions.Sum(session => session.DurationMs)),
            sessions.Average(session => session.RawWpm),
            sessions.Average(session => session.NetWpm),
            sessions.Max(session => session.NetWpm),
            Divide(correctKeypresses, totalKeypresses),
            consistencySamples.Length == 0 ? null : consistencySamples.Average(),
            totalKeypresses,
            correctKeypresses,
            sessions.Sum(session => session.IncorrectKeypresses),
            sessions.Sum(session => session.CorrectedErrors),
            sessions.Sum(session => session.UncorrectedErrors));
    }

    private static IReadOnlyList<DailyMetricPoint> BuildDailyMetrics(IReadOnlyList<SessionRow> sessions)
    {
        return sessions
            .GroupBy(session => DateOnly.FromDateTime(session.StartedAtUtc.UtcDateTime))
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var totalKeypresses = group.Sum(session => session.TotalKeypresses);
                var correctKeypresses = group.Sum(session => session.CorrectKeypresses);

                return new DailyMetricPoint(
                    group.Key,
                    group.Count(),
                    TimeSpan.FromMilliseconds(group.Sum(session => session.DurationMs)),
                    group.Average(session => session.RawWpm),
                    group.Average(session => session.NetWpm),
                    group.Max(session => session.NetWpm),
                    Divide(correctKeypresses, totalKeypresses));
            })
            .ToArray();
    }

    private static IReadOnlyList<RecentSessionRow> BuildRecentSessions(IReadOnlyList<SessionRow> sessions)
    {
        return sessions
            .OrderByDescending(session => session.StartedAtUtc)
            .Take(RecentSessionLimit)
            .Select(session => new RecentSessionRow(
                session.Id,
                session.StartedAtUtc.UtcDateTime,
                TimeSpan.FromMilliseconds(session.DurationMs),
                session.Mode,
                session.TargetLength,
                session.RawWpm,
                session.NetWpm,
                session.Accuracy,
                session.Consistency))
            .ToArray();
    }

    private static IReadOnlyList<CharacterAnalyticsRow> BuildCharacterRows(
        IReadOnlyList<AnalyticsKeyEventRow> characterEvents)
    {
        return AnalyticsComputation.BuildCharacterStats(characterEvents)
            .Select(stat => new CharacterAnalyticsRow(
                stat.Character,
                stat.DisplayCharacter,
                stat.ExposureCount,
                stat.CorrectCount,
                stat.IncorrectCount,
                stat.Accuracy,
                stat.AverageLatencyMs,
                stat.MedianLatencyMs,
                stat.WeaknessScore))
            .ToArray();
    }

    private static IReadOnlyList<BigramAnalyticsRow> BuildBigramRows(
        IReadOnlyList<AnalyticsKeyEventRow> characterEvents)
    {
        return AnalyticsComputation.BuildBigramStats(characterEvents)
            .Select(stat => new BigramAnalyticsRow(
                stat.Bigram,
                stat.DisplayBigram,
                stat.ExposureCount,
                stat.CorrectCount,
                stat.IncorrectCount,
                stat.Accuracy,
                stat.AverageLatencyMs,
                stat.MedianLatencyMs,
                stat.WeaknessScore))
            .ToArray();
    }

    private static async Task<IReadOnlyList<SessionRow>> LoadSessionsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, started_at_utc, mode, target_length, raw_wpm, net_wpm,
                   accuracy, consistency, total_keypresses, correct_keypresses,
                   incorrect_keypresses, corrected_errors, uncorrected_errors,
                   duration_ms
            FROM practice_sessions;
            """;

        var sessions = new List<SessionRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            sessions.Add(new SessionRow(
                Guid.Parse(reader.GetString(0)),
                DateTimeOffset.Parse(reader.GetString(1)),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetDouble(4),
                reader.GetDouble(5),
                reader.GetDouble(6),
                reader.IsDBNull(7) ? null : reader.GetDouble(7),
                reader.GetInt32(8),
                reader.GetInt32(9),
                reader.GetInt32(10),
                reader.GetInt32(11),
                reader.GetInt32(12),
                reader.GetInt64(13)));
        }

        return sessions;
    }

    private static async Task<IReadOnlyList<AnalyticsKeyEventRow>> LoadCharacterEventsAsync(
        SqliteConnection connection,
        IReadOnlySet<Guid> sessionIds,
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
            var sessionId = Guid.Parse(reader.GetString(0));
            if (!sessionIds.Contains(sessionId))
            {
                continue;
            }

            events.Add(new AnalyticsKeyEventRow(
                sessionId,
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

    private static double Divide(int numerator, int denominator)
    {
        return denominator == 0 ? 0 : numerator / (double)denominator;
    }

    private sealed record SessionRow(
        Guid Id,
        DateTimeOffset StartedAtUtc,
        string Mode,
        int TargetLength,
        double RawWpm,
        double NetWpm,
        double Accuracy,
        double? Consistency,
        int TotalKeypresses,
        int CorrectKeypresses,
        int IncorrectKeypresses,
        int CorrectedErrors,
        int UncorrectedErrors,
        long DurationMs);

}
