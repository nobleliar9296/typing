using Microsoft.Data.Sqlite;
using TypingTrainer.Core.Training;
using TypingTrainer.Data.Database;
using TypingTrainer.Data.Models;
using TypingTrainer.Data.Repositories;

namespace TypingTrainer.Data.Services;

public sealed class TrainingHistoryQueryService : ITrainingHistoryQueryService
{
    private const int RecentQualityLimit = 12;

    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly IAppSettingsRepository _settingsRepository;
    private readonly IUtcClock _clock;
    private readonly TimeZoneInfo _localTimeZone;
    private readonly SessionQualityCalculator _qualityCalculator = new();

    public TrainingHistoryQueryService(
        SqliteConnectionFactory connectionFactory,
        IAppSettingsRepository settingsRepository,
        IUtcClock? clock = null,
        TimeZoneInfo? localTimeZone = null)
    {
        _connectionFactory = connectionFactory;
        _settingsRepository = settingsRepository;
        _clock = clock ?? new SystemUtcClock();
        _localTimeZone = localTimeZone ?? TimeZoneInfo.Local;
    }

    public async Task<TrainingHistorySnapshot> GetTrainingHistoryAsync(
        AnalyticsRange range,
        string? modeFilter,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsRepository.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var bounds = GetUtcRange(range);
        var filteredSessions = await LoadSessionsAsync(connection, bounds, modeFilter, cancellationToken).ConfigureAwait(false);
        var events = filteredSessions.Count == 0
            ? Array.Empty<AnalyticsKeyEventRow>()
            : await LoadCharacterEventsAsync(connection, bounds, modeFilter, cancellationToken).ConfigureAwait(false);
        var eventsBySession = events
            .GroupBy(item => item.SessionId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var scoredSessions = filteredSessions
            .Select(session => ScoreSession(
                session,
                eventsBySession.TryGetValue(session.Id, out var sessionEvents) ? sessionEvents : Array.Empty<AnalyticsKeyEventRow>(),
                settings.GoalTargetNetWpm))
            .OrderByDescending(item => item.Session.StartedAtUtc)
            .ToArray();

        return new TrainingHistorySnapshot(
            BuildQualitySummary(scoredSessions),
            BuildPersonalBests(scoredSessions),
            BuildCalendar(scoredSessions, range),
            BuildRecentQuality(scoredSessions));
    }

    private SessionWithQuality ScoreSession(
        SessionRecord session,
        IReadOnlyList<AnalyticsKeyEventRow> events,
        double targetNetWpm)
    {
        var consistency = session.Consistency ?? CalculateConsistency(events);
        var completionRatio = session.TargetLength <= 0
            ? 1
            : Math.Min(1, session.TotalKeypresses / (double)session.TargetLength);
        var controlRatio = session.TargetLength <= 0
            ? 1
            : Clamp01(1 - (session.UncorrectedErrors / (double)session.TargetLength));
        var result = _qualityCalculator.Calculate(new SessionQualityInputs(
            session.Accuracy,
            session.NetWpm,
            targetNetWpm,
            consistency,
            completionRatio,
            controlRatio));

        return new SessionWithQuality(session, result);
    }

    private static QualitySummary BuildQualitySummary(IReadOnlyList<SessionWithQuality> sessions)
    {
        if (sessions.Count == 0)
        {
            return new QualitySummary(0, 0, 0, SessionQualityCalculator.GetGrade(0), 0);
        }

        var scoresNewestFirst = sessions
            .OrderByDescending(item => item.Session.StartedAtUtc)
            .Select(item => item.Quality.Score)
            .ToArray();
        var recent = scoresNewestFirst.Take(3).ToArray();
        var previous = scoresNewestFirst.Skip(3).Take(3).ToArray();
        var trend = previous.Length == 0 ? 0 : recent.Average() - previous.Average();
        var average = sessions.Average(item => item.Quality.Score);

        return new QualitySummary(
            sessions.Count,
            average,
            sessions.Max(item => item.Quality.Score),
            SessionQualityCalculator.GetGrade(average),
            trend);
    }

    private IReadOnlyList<PersonalBestRow> BuildPersonalBests(IReadOnlyList<SessionWithQuality> sessions)
    {
        if (sessions.Count == 0)
        {
            return Array.Empty<PersonalBestRow>();
        }

        var rows = new List<PersonalBestRow>();
        AddSessionBest(rows, "best-net-wpm", "Best net WPM", sessions, item => item.Session.NetWpm, "WPM");
        AddSessionBest(rows, "best-accuracy", "Best accuracy", sessions.Where(item => item.Session.TargetLength >= 100), item => item.Session.Accuracy * 100, "%");
        AddLongestStreak(rows, sessions);
        AddMostPracticeInDay(rows, sessions);
        AddSessionBest(rows, "best-paragraph", "Best paragraph session", sessions.Where(item => IsMode(item.Session, "Paragraph")), item => item.Session.NetWpm, "WPM");
        AddSessionBest(rows, "best-long", "Best long session", sessions.Where(item => item.Session.TargetLength >= 1000), item => item.Session.NetWpm, "WPM");

        return rows;
    }

    private void AddSessionBest(
        List<PersonalBestRow> rows,
        string kind,
        string label,
        IEnumerable<SessionWithQuality> candidates,
        Func<SessionWithQuality, double> valueSelector,
        string unit)
    {
        var best = candidates
            .OrderByDescending(valueSelector)
            .ThenByDescending(item => item.Session.StartedAtUtc)
            .FirstOrDefault();
        if (best is null)
        {
            return;
        }

        rows.Add(new PersonalBestRow(
            kind,
            label,
            best.Session.Id,
            ToLocalDate(best.Session.StartedAtUtc),
            best.Session.Mode,
            valueSelector(best),
            unit));
    }

    private void AddLongestStreak(List<PersonalBestRow> rows, IReadOnlyList<SessionWithQuality> sessions)
    {
        var dates = sessions
            .Select(item => ToLocalDate(item.Session.StartedAtUtc))
            .Distinct()
            .OrderBy(date => date)
            .ToArray();
        if (dates.Length == 0)
        {
            return;
        }

        var bestLength = 1;
        var currentLength = 1;
        var bestEnd = dates[0];
        var currentEnd = dates[0];

        for (var index = 1; index < dates.Length; index++)
        {
            if (dates[index] == dates[index - 1].AddDays(1))
            {
                currentLength++;
            }
            else
            {
                currentLength = 1;
            }

            currentEnd = dates[index];
            if (currentLength >= bestLength)
            {
                bestLength = currentLength;
                bestEnd = currentEnd;
            }
        }

        rows.Add(new PersonalBestRow(
            "longest-streak",
            "Longest streak",
            null,
            bestEnd,
            null,
            bestLength,
            bestLength == 1 ? "day" : "days"));
    }

    private void AddMostPracticeInDay(List<PersonalBestRow> rows, IReadOnlyList<SessionWithQuality> sessions)
    {
        var bestDay = sessions
            .GroupBy(item => ToLocalDate(item.Session.StartedAtUtc))
            .Select(group => new
            {
                Date = group.Key,
                Minutes = group.Sum(item => item.Session.DurationMs) / 60_000.0
            })
            .OrderByDescending(item => item.Minutes)
            .ThenByDescending(item => item.Date)
            .FirstOrDefault();
        if (bestDay is null)
        {
            return;
        }

        rows.Add(new PersonalBestRow(
            "most-practice-day",
            "Most practice in one day",
            null,
            bestDay.Date,
            null,
            bestDay.Minutes,
            "min"));
    }

    private IReadOnlyList<PracticeCalendarDay> BuildCalendar(
        IReadOnlyList<SessionWithQuality> sessions,
        AnalyticsRange range)
    {
        var window = GetCalendarWindow(sessions, range);
        if (window is null)
        {
            return Array.Empty<PracticeCalendarDay>();
        }

        var byDate = sessions
            .GroupBy(item => ToLocalDate(item.Session.StartedAtUtc))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var days = new List<PracticeCalendarDay>();

        for (var date = window.Value.Start; date <= window.Value.End; date = date.AddDays(1))
        {
            if (!byDate.TryGetValue(date, out var daySessions))
            {
                days.Add(new PracticeCalendarDay(date, 0, TimeSpan.Zero, 0, 0, 0));
                continue;
            }

            var totalKeypresses = daySessions.Sum(item => item.Session.TotalKeypresses);
            var correctKeypresses = daySessions.Sum(item => item.Session.CorrectKeypresses);
            days.Add(new PracticeCalendarDay(
                date,
                daySessions.Length,
                TimeSpan.FromMilliseconds(daySessions.Sum(item => item.Session.DurationMs)),
                daySessions.Average(item => item.Quality.Score),
                daySessions.Average(item => item.Session.NetWpm),
                AnalyticsComputation.Divide(correctKeypresses, totalKeypresses)));
        }

        return days;
    }

    private (DateOnly Start, DateOnly End)? GetCalendarWindow(
        IReadOnlyList<SessionWithQuality> sessions,
        AnalyticsRange range)
    {
        if (range == AnalyticsRange.AllTime)
        {
            if (sessions.Count == 0)
            {
                return null;
            }

            var dates = sessions.Select(item => ToLocalDate(item.Session.StartedAtUtc)).ToArray();
            return (dates.Min(), dates.Max());
        }

        var days = range == AnalyticsRange.Last7Days ? 7 : 30;
        var end = ToLocalDate(_clock.UtcNow);
        return (end.AddDays(-(days - 1)), end);
    }

    private static IReadOnlyList<RecentQualityRow> BuildRecentQuality(IReadOnlyList<SessionWithQuality> sessions)
    {
        return sessions
            .OrderByDescending(item => item.Session.StartedAtUtc)
            .Take(RecentQualityLimit)
            .Select(item => new RecentQualityRow(
                item.Session.Id,
                item.Session.StartedAtUtc,
                item.Session.Mode,
                item.Quality.Score,
                item.Quality.Grade))
            .ToArray();
    }

    private static double? CalculateConsistency(IReadOnlyList<AnalyticsKeyEventRow> events)
    {
        var characterEvents = events
            .Where(item => string.Equals(item.EventKind, "Character", StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.TimestampTicks)
            .ToArray();
        if (characterEvents.Length < 20)
        {
            return null;
        }

        var points = new List<double>();
        var total = 0;
        var correct = 0;

        foreach (var keyEvent in characterEvents)
        {
            total++;
            if (keyEvent.IsCorrect)
            {
                correct++;
            }

            if (total % 10 != 0 && total != characterEvents.Length)
            {
                continue;
            }

            var minutes = keyEvent.ElapsedMs / 60_000.0;
            if (minutes <= 0)
            {
                continue;
            }

            points.Add((correct / 5.0) / minutes);
        }

        if (points.Count < 2)
        {
            return null;
        }

        var average = points.Average();
        if (average <= 0)
        {
            return 1;
        }

        var variance = points.Average(point => Math.Pow(point - average, 2));
        var standardDeviation = Math.Sqrt(variance);
        return Clamp01(1 - (standardDeviation / average));
    }

    private static bool IsMode(SessionRecord session, string mode)
    {
        return string.Equals(session.Mode, mode, StringComparison.OrdinalIgnoreCase);
    }

    private static double Clamp01(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        return Math.Min(1, Math.Max(0, value));
    }

    private DateOnly ToLocalDate(DateTimeOffset timestampUtc)
    {
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timestampUtc, _localTimeZone).DateTime);
    }

    private static async Task<IReadOnlyList<SessionRecord>> LoadSessionsAsync(
        SqliteConnection connection,
        UtcRange bounds,
        string? modeFilter,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, started_at_utc, mode, target_length, raw_wpm, net_wpm,
                   accuracy, consistency, total_keypresses, correct_keypresses,
                   incorrect_keypresses, corrected_errors, uncorrected_errors,
                   duration_ms
            FROM practice_sessions
            WHERE ($mode IS NULL OR mode COLLATE NOCASE = $mode)
              AND ($startUtc IS NULL OR started_at_utc >= $startUtc)
              AND ($endUtc IS NULL OR started_at_utc < $endUtc);
            """;
        AddFilterParameters(command, bounds, modeFilter);

        var sessions = new List<SessionRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            sessions.Add(new SessionRecord(
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
        UtcRange bounds,
        string? modeFilter,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT event.session_id, event.position, event.expected_char, event.actual_char, event.event_kind,
                   event.is_correct, event.was_correction, event.delta_previous_ms, event.elapsed_ms, event.timestamp_ticks
            FROM key_events event
            INNER JOIN practice_sessions session ON session.id = event.session_id
            WHERE event.event_kind = 'Character'
              AND event.expected_char IS NOT NULL
              AND ($mode IS NULL OR session.mode COLLATE NOCASE = $mode)
              AND ($startUtc IS NULL OR session.started_at_utc >= $startUtc)
              AND ($endUtc IS NULL OR session.started_at_utc < $endUtc)
            ORDER BY event.session_id, event.timestamp_ticks ASC;
            """;
        AddFilterParameters(command, bounds, modeFilter);

        var events = new List<AnalyticsKeyEventRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var sessionId = Guid.Parse(reader.GetString(0));
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

    private sealed record SessionRecord(
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

    private UtcRange GetUtcRange(AnalyticsRange range)
    {
        if (range == AnalyticsRange.AllTime)
        {
            return new UtcRange(null, null);
        }

        var days = range == AnalyticsRange.Last7Days ? 7 : 30;
        var todayLocal = ToLocalDate(_clock.UtcNow);
        var startLocal = todayLocal.AddDays(-(days - 1));
        var endLocal = todayLocal.AddDays(1);
        return new UtcRange(ToUtcBoundary(startLocal), ToUtcBoundary(endLocal));
    }

    private DateTimeOffset ToUtcBoundary(DateOnly localDate)
    {
        var localDateTime = DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localDateTime, _localTimeZone));
    }

    private static void AddFilterParameters(SqliteCommand command, UtcRange bounds, string? modeFilter)
    {
        command.Parameters.AddWithValue("$mode", string.IsNullOrWhiteSpace(modeFilter) ? DBNull.Value : modeFilter);
        command.Parameters.AddWithValue("$startUtc", bounds.StartUtc is null ? DBNull.Value : bounds.StartUtc.Value.ToString("O"));
        command.Parameters.AddWithValue("$endUtc", bounds.EndUtc is null ? DBNull.Value : bounds.EndUtc.Value.ToString("O"));
    }

    private sealed record UtcRange(DateTimeOffset? StartUtc, DateTimeOffset? EndUtc);

    private sealed record SessionWithQuality(
        SessionRecord Session,
        SessionQualityResult Quality);
}
