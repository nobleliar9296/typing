using TypingTrainer.Data.Models;
using TypingTrainer.Data.Repositories;

namespace TypingTrainer.Data.Services;

public sealed class SessionDetailQueryService : ISessionDetailQueryService
{
    private const int AnalyticsLimit = 8;
    private readonly IPracticeSessionRepository _repository;

    public SessionDetailQueryService(IPracticeSessionRepository repository)
    {
        _repository = repository;
    }

    public async Task<SessionDetailSnapshot?> GetSessionDetailAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _repository.GetSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return null;
        }

        var events = await _repository.GetSessionEventsAsync(sessionId, cancellationToken).ConfigureAwait(false);
        var characterEvents = events
            .Where(item => string.Equals(item.EventKind, "Character", StringComparison.OrdinalIgnoreCase)
                && item.ExpectedChar is not null)
            .Select(item => new AnalyticsKeyEventRow(
                item.SessionId,
                item.Position,
                item.ExpectedChar?.ToString(),
                item.ActualChar?.ToString(),
                item.EventKind,
                item.IsCorrect,
                item.WasCorrection,
                item.DeltaPreviousMs,
                item.ElapsedMs,
                item.TimestampTicks))
            .ToArray();
        var slowestKeys = AnalyticsComputation.BuildCharacterStats(characterEvents)
            .Where(stat => stat.MedianLatencyMs is not null)
            .OrderByDescending(stat => stat.MedianLatencyMs)
            .Take(AnalyticsLimit)
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
        var slowestBigrams = AnalyticsComputation.BuildBigramStats(characterEvents)
            .Where(stat => stat.MedianLatencyMs is not null)
            .OrderByDescending(stat => stat.MedianLatencyMs)
            .Take(AnalyticsLimit)
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

        return new SessionDetailSnapshot(
            session,
            events,
            BuildTimeline(events),
            BuildMistakes(events),
            slowestKeys,
            slowestBigrams);
    }

    private static IReadOnlyList<SessionTimelinePoint> BuildTimeline(IReadOnlyList<StoredKeyEvent> events)
    {
        var points = new List<SessionTimelinePoint>();
        var total = 0;
        var correct = 0;

        foreach (var keyEvent in events.Where(item => string.Equals(item.EventKind, "Character", StringComparison.OrdinalIgnoreCase)))
        {
            total++;
            if (keyEvent.IsCorrect)
            {
                correct++;
            }

            if (total % 10 == 0 || total == 1)
            {
                var minutes = Math.Max(keyEvent.ElapsedMs / 60_000.0, 0.0001);
                var netWpm = Math.Max(0, (correct / 5.0) / minutes);
                points.Add(new SessionTimelinePoint(
                    $"{TimeSpan.FromMilliseconds(keyEvent.ElapsedMs):m\\:ss}",
                    keyEvent.ElapsedMs,
                    netWpm,
                    AnalyticsComputation.Divide(correct, total) * 100));
            }
        }

        return points;
    }

    private static IReadOnlyList<SessionDetailMistakeRow> BuildMistakes(IReadOnlyList<StoredKeyEvent> events)
    {
        return events
            .Where(item => !item.IsCorrect
                || string.Equals(item.EventKind, "Backspace", StringComparison.OrdinalIgnoreCase)
                && item.ExpectedChar is not null
                && item.ActualChar is not null
                && item.ExpectedChar != item.ActualChar)
            .Select(item => new SessionDetailMistakeRow(
                item.Position,
                DisplayChar(item.ExpectedChar),
                DisplayChar(item.ActualChar),
                item.EventKind,
                item.ElapsedMs))
            .Take(80)
            .ToArray();
    }

    private static string DisplayChar(char? character)
    {
        return character switch
        {
            null => "-",
            ' ' => "Space",
            '\n' => "Enter",
            '\r' => "Enter",
            _ => character.Value.ToString()
        };
    }
}

