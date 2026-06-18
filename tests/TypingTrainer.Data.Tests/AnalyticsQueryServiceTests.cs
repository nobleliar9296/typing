using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.Data.Database;
using TypingTrainer.Data.Models;
using TypingTrainer.Data.Repositories;
using TypingTrainer.Data.Services;

namespace TypingTrainer.Data.Tests;

[TestClass]
public sealed class AnalyticsQueryServiceTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo FixedCentralTime = TimeZoneInfo.CreateCustomTimeZone(
        "FixedCentral",
        TimeSpan.FromHours(-5),
        "FixedCentral",
        "FixedCentral");

    [TestMethod]
    public async Task AnalyticsQueryService_GetDashboardSnapshot_WithNoSessions_ReturnsEmptySnapshot()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);

        var snapshot = await database.Analytics.GetDashboardSnapshotAsync(AnalyticsRange.AllTime);

        Assert.AreEqual(0, snapshot.Summary.SessionCount);
        Assert.AreEqual(0, snapshot.DailyMetrics.Count);
        Assert.AreEqual(0, snapshot.RecentSessions.Count);
        Assert.AreEqual(0, snapshot.WeakestCharacters.Count);
        Assert.AreEqual(0, snapshot.WeakestBigrams.Count);
    }

    [TestMethod]
    public async Task AnalyticsQueryService_GetDashboardSnapshot_CalculatesSummaryFromSessions()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);
        await database.SaveAsync(CreateSession(startedAtUtc: NowUtc.AddHours(-2), rawWpm: 50, netWpm: 45, total: 10, correct: 8));
        await database.SaveAsync(CreateSession(startedAtUtc: NowUtc.AddHours(-1), rawWpm: 70, netWpm: 65, total: 20, correct: 18));

        var snapshot = await database.Analytics.GetDashboardSnapshotAsync(AnalyticsRange.Last7Days);

        Assert.AreEqual(2, snapshot.Summary.SessionCount);
        Assert.AreEqual(60, snapshot.Summary.AverageRawWpm, 0.0001);
        Assert.AreEqual(55, snapshot.Summary.AverageNetWpm, 0.0001);
        Assert.AreEqual(65, snapshot.Summary.BestNetWpm, 0.0001);
        Assert.AreEqual(26 / 30.0, snapshot.Summary.Accuracy, 0.0001);
    }

    [TestMethod]
    public async Task AnalyticsQueryService_GetDashboardSnapshot_ModeFilterLimitsSummarySessions()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);
        await database.SaveAsync(CreateSession(startedAtUtc: NowUtc.AddHours(-2), mode: "Fixed", netWpm: 35));
        await database.SaveAsync(CreateSession(startedAtUtc: NowUtc.AddHours(-1), mode: "Paragraph", netWpm: 65));

        var snapshot = await database.Analytics.GetDashboardSnapshotAsync(AnalyticsRange.Last7Days, "Paragraph");

        Assert.AreEqual(1, snapshot.Summary.SessionCount);
        Assert.AreEqual(65, snapshot.Summary.AverageNetWpm, 0.0001);
    }

    [TestMethod]
    public async Task AnalyticsQueryService_GetDashboardSnapshot_ModeFilterLimitsKeyEvents()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);
        var fixedSession = CreateSession(startedAtUtc: NowUtc.AddHours(-2), mode: "Fixed", targetText: "e");
        var paragraphSession = CreateSession(startedAtUtc: NowUtc.AddHours(-1), mode: "Paragraph", targetText: "a");

        await database.SaveAsync(fixedSession, [CharacterEvent(fixedSession.Id, 0, 'e', 'x', false, 100, 1)]);
        await database.SaveAsync(paragraphSession, [CharacterEvent(paragraphSession.Id, 0, 'a', 'x', false, 100, 1)]);

        var snapshot = await database.Analytics.GetDashboardSnapshotAsync(AnalyticsRange.AllTime, "Paragraph");

        Assert.AreEqual(1, snapshot.WeakestCharacters.Count);
        Assert.AreEqual("a", snapshot.WeakestCharacters.Single().Character);
    }

    [TestMethod]
    public async Task AnalyticsQueryService_GetDashboardSnapshot_RangeFilterLimitsKeyEvents()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);
        var oldSession = CreateSession(startedAtUtc: NowUtc.AddDays(-8), targetText: "z");
        var currentSession = CreateSession(startedAtUtc: NowUtc.AddDays(-1), targetText: "a");

        await database.SaveAsync(oldSession, [CharacterEvent(oldSession.Id, 0, 'z', 'x', false, 100, 1)]);
        await database.SaveAsync(currentSession, [CharacterEvent(currentSession.Id, 0, 'a', 'x', false, 100, 1)]);

        var snapshot = await database.Analytics.GetDashboardSnapshotAsync(AnalyticsRange.Last7Days);

        Assert.AreEqual(1, snapshot.WeakestCharacters.Count);
        Assert.AreEqual("a", snapshot.WeakestCharacters.Single().Character);
    }

    [TestMethod]
    public async Task AnalyticsQueryService_GetDashboardSnapshot_ModeFilterIsCaseInsensitive()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);
        await database.SaveAsync(CreateSession(startedAtUtc: NowUtc.AddHours(-2), mode: "Fixed", netWpm: 35));
        await database.SaveAsync(CreateSession(startedAtUtc: NowUtc.AddHours(-1), mode: "Paragraph", netWpm: 65));

        var snapshot = await database.Analytics.GetDashboardSnapshotAsync(AnalyticsRange.AllTime, "paragraph");

        Assert.AreEqual(1, snapshot.Summary.SessionCount);
        Assert.AreEqual(65, snapshot.Summary.AverageNetWpm, 0.0001);
    }

    [TestMethod]
    public async Task AnalyticsQueryService_GetDashboardSnapshot_GroupsDailyMetricsByDate()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);
        await database.SaveAsync(CreateSession(startedAtUtc: new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero), netWpm: 40));
        await database.SaveAsync(CreateSession(startedAtUtc: new DateTimeOffset(2026, 6, 10, 11, 0, 0, TimeSpan.Zero), netWpm: 60));
        await database.SaveAsync(CreateSession(startedAtUtc: new DateTimeOffset(2026, 6, 11, 10, 0, 0, TimeSpan.Zero), netWpm: 80));

        var snapshot = await database.Analytics.GetDashboardSnapshotAsync(AnalyticsRange.Last7Days);

        Assert.AreEqual(2, snapshot.DailyMetrics.Count);
        Assert.AreEqual(new DateOnly(2026, 6, 10), snapshot.DailyMetrics[0].Date);
        Assert.AreEqual(2, snapshot.DailyMetrics[0].SessionCount);
        Assert.AreEqual(50, snapshot.DailyMetrics[0].AverageNetWpm, 0.0001);
        Assert.AreEqual(new DateOnly(2026, 6, 11), snapshot.DailyMetrics[1].Date);
    }

    [TestMethod]
    public async Task AnalyticsQueryService_GetDashboardSnapshot_GroupsDailyMetricsByLocalDate()
    {
        var nowUtc = new DateTimeOffset(2026, 6, 12, 4, 0, 0, TimeSpan.Zero);
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(nowUtc, FixedCentralTime);
        await database.SaveAsync(CreateSession(
            startedAtUtc: new DateTimeOffset(2026, 6, 12, 2, 30, 0, TimeSpan.Zero),
            netWpm: 42));

        var snapshot = await database.Analytics.GetDashboardSnapshotAsync(AnalyticsRange.Last7Days);

        Assert.AreEqual(1, snapshot.DailyMetrics.Count);
        Assert.AreEqual(new DateOnly(2026, 6, 11), snapshot.DailyMetrics[0].Date);
    }

    [TestMethod]
    public async Task AnalyticsQueryService_GetDashboardSnapshot_ReturnsRecentSessionsNewestFirst()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);
        var older = CreateSession(startedAtUtc: NowUtc.AddHours(-3));
        var newer = CreateSession(startedAtUtc: NowUtc.AddHours(-1));

        await database.SaveAsync(older);
        await database.SaveAsync(newer);

        var snapshot = await database.Analytics.GetDashboardSnapshotAsync(AnalyticsRange.AllTime);

        Assert.AreEqual(2, snapshot.RecentSessions.Count);
        Assert.AreEqual(newer.Id, snapshot.RecentSessions[0].SessionId);
        Assert.AreEqual(older.Id, snapshot.RecentSessions[1].SessionId);
    }

    [TestMethod]
    public async Task AnalyticsQueryService_GetDashboardSnapshot_CalculatesCharacterAccuracy()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);
        var session = CreateSession(startedAtUtc: NowUtc.AddHours(-1), targetText: "the");

        await database.SaveAsync(session, CreateTheEvents(session.Id));

        var snapshot = await database.Analytics.GetDashboardSnapshotAsync(AnalyticsRange.AllTime);
        var e = snapshot.WeakestCharacters.Single(row => row.Character == "e");

        Assert.AreEqual(1, e.ExposureCount);
        Assert.AreEqual(0, e.CorrectCount);
        Assert.AreEqual(1, e.IncorrectCount);
        Assert.AreEqual(0, e.Accuracy);
    }

    [TestMethod]
    public async Task KeyboardHeatmapQueryService_CalculatesKeyWeakness()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);
        var session = CreateSession(startedAtUtc: NowUtc.AddHours(-1), targetText: "ab");
        await database.SaveAsync(session,
        [
            CharacterEvent(session.Id, 0, 'a', 'x', false, 100, 1),
            CharacterEvent(session.Id, 1, 'b', 'b', true, 120, 2)
        ]);

        var rows = await database.Heatmap.GetHeatmapAsync(AnalyticsRange.AllTime);

        var a = rows.Single(row => row.Character == 'a');
        Assert.AreEqual(1, a.ExposureCount);
        Assert.AreEqual(0, a.Accuracy);
    }

    [TestMethod]
    public async Task KeyboardHeatmapQueryService_MergesUppercaseAndLowercaseIntoPhysicalKey()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);
        var session = CreateSession(startedAtUtc: NowUtc.AddHours(-1), targetText: "kK");
        await database.SaveAsync(session,
        [
            CharacterEvent(session.Id, 0, 'k', 'k', true, 100, 1),
            CharacterEvent(session.Id, 1, 'K', 'K', true, 120, 2)
        ]);

        var rows = await database.Heatmap.GetHeatmapAsync(AnalyticsRange.AllTime);
        var kRows = rows
            .Where(row => string.Equals(row.KeyLabel, "K", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.AreEqual(1, kRows.Length);
        Assert.AreEqual(2, kRows[0].ExposureCount);
        Assert.AreEqual(1, kRows[0].Accuracy);
    }

    [TestMethod]
    public async Task SessionDetailQueryService_ReturnsTimelineMistakesAndSlowTransitions()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);
        var session = CreateSession(startedAtUtc: NowUtc.AddHours(-1), targetText: "the");
        await database.SaveAsync(session, CreateTheEvents(session.Id));

        var detail = await database.SessionDetail.GetSessionDetailAsync(session.Id);

        Assert.IsNotNull(detail);
        Assert.AreEqual(session.Id, detail.Session.Id);
        Assert.IsTrue(detail.Events.Count > 0);
        Assert.IsTrue(detail.Timeline.Count > 0);
        Assert.IsTrue(detail.Mistakes.Count > 0);
        Assert.IsTrue(detail.SlowestBigrams.Count > 0);
    }

    [TestMethod]
    public async Task AnalyticsQueryService_GetDashboardSnapshot_CalculatesCharacterLatency()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);
        var session = CreateSession(startedAtUtc: NowUtc.AddHours(-1), targetText: "the");

        await database.SaveAsync(session, CreateTheEvents(session.Id));

        var snapshot = await database.Analytics.GetDashboardSnapshotAsync(AnalyticsRange.AllTime);
        var h = snapshot.WeakestCharacters.Single(row => row.Character == "h");

        Assert.AreEqual(120, h.AverageLatencyMs);
        Assert.AreEqual(120, h.MedianLatencyMs);
    }

    [TestMethod]
    public async Task AnalyticsQueryService_GetDashboardSnapshot_CalculatesBigramStats()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);
        var session = CreateSession(startedAtUtc: NowUtc.AddHours(-1), targetText: "the");

        await database.SaveAsync(session, CreateTheEvents(session.Id));

        var snapshot = await database.Analytics.GetDashboardSnapshotAsync(AnalyticsRange.AllTime);
        var th = snapshot.WeakestBigrams.Single(row => row.Bigram == "th");
        var he = snapshot.WeakestBigrams.Single(row => row.Bigram == "he");

        Assert.AreEqual(1, th.ExposureCount);
        Assert.AreEqual(1, th.CorrectCount);
        Assert.AreEqual(120, th.MedianLatencyMs);
        Assert.AreEqual(0, he.Accuracy);
        Assert.AreEqual(240, he.MedianLatencyMs);
    }

    [TestMethod]
    public async Task AnalyticsQueryService_GetDashboardSnapshot_FiltersLongPausesFromLatency()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);
        var session = CreateSession(startedAtUtc: NowUtc.AddHours(-1), targetText: "ab");
        var events = new[]
        {
            CharacterEvent(session.Id, position: 0, expected: 'a', actual: 'a', isCorrect: true, deltaPreviousMs: null, timestampTicks: 1),
            CharacterEvent(session.Id, position: 1, expected: 'b', actual: 'b', isCorrect: true, deltaPreviousMs: 5000, timestampTicks: 2)
        };

        await database.SaveAsync(session, events);

        var snapshot = await database.Analytics.GetDashboardSnapshotAsync(AnalyticsRange.AllTime);
        var b = snapshot.WeakestCharacters.Single(row => row.Character == "b");

        Assert.AreEqual(1, b.ExposureCount);
        Assert.IsNull(b.AverageLatencyMs);
        Assert.IsNull(b.MedianLatencyMs);
    }

    [TestMethod]
    public async Task AnalyticsQueryService_GetDashboardSnapshot_RespectsLast7DaysRange()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);
        await database.SaveAsync(CreateSession(startedAtUtc: NowUtc.AddDays(-8)));
        await database.SaveAsync(CreateSession(startedAtUtc: NowUtc.AddDays(-2)));

        var snapshot = await database.Analytics.GetDashboardSnapshotAsync(AnalyticsRange.Last7Days);

        Assert.AreEqual(1, snapshot.Summary.SessionCount);
    }

    [TestMethod]
    public async Task AnalyticsQueryService_GetDashboardSnapshot_Last7DaysUsesLocalCalendarDates()
    {
        var nowUtc = new DateTimeOffset(2026, 6, 12, 4, 0, 0, TimeSpan.Zero);
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(nowUtc, FixedCentralTime);
        await database.SaveAsync(CreateSession(startedAtUtc: LocalToUtc(2026, 6, 4, 10, 0)));
        await database.SaveAsync(CreateSession(startedAtUtc: LocalToUtc(2026, 6, 5, 10, 0)));

        var snapshot = await database.Analytics.GetDashboardSnapshotAsync(AnalyticsRange.Last7Days);

        Assert.AreEqual(1, snapshot.Summary.SessionCount);
        Assert.AreEqual(new DateOnly(2026, 6, 5), snapshot.DailyMetrics.Single().Date);
    }

    [TestMethod]
    public async Task AnalyticsQueryService_GetDashboardSnapshot_RespectsLast30DaysRange()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);
        await database.SaveAsync(CreateSession(startedAtUtc: NowUtc.AddDays(-31)));
        await database.SaveAsync(CreateSession(startedAtUtc: NowUtc.AddDays(-20)));
        await database.SaveAsync(CreateSession(startedAtUtc: NowUtc.AddDays(-2)));

        var snapshot = await database.Analytics.GetDashboardSnapshotAsync(AnalyticsRange.Last30Days);

        Assert.AreEqual(2, snapshot.Summary.SessionCount);
    }

    [TestMethod]
    public async Task AnalyticsQueryService_GetDashboardSnapshot_Last30DaysUsesLocalCalendarDates()
    {
        var nowUtc = new DateTimeOffset(2026, 6, 12, 4, 0, 0, TimeSpan.Zero);
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(nowUtc, FixedCentralTime);
        await database.SaveAsync(CreateSession(startedAtUtc: LocalToUtc(2026, 5, 12, 10, 0)));
        await database.SaveAsync(CreateSession(startedAtUtc: LocalToUtc(2026, 5, 13, 10, 0)));

        var snapshot = await database.Analytics.GetDashboardSnapshotAsync(AnalyticsRange.Last30Days);

        Assert.AreEqual(1, snapshot.Summary.SessionCount);
        Assert.AreEqual(new DateOnly(2026, 5, 13), snapshot.DailyMetrics.Single().Date);
    }

    [TestMethod]
    public async Task TrainingHistoryQueryService_WithNoSessions_ReturnsSafeSnapshot()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);

        var snapshot = await database.TrainingHistory.GetTrainingHistoryAsync(AnalyticsRange.AllTime, modeFilter: null);

        Assert.AreEqual(0, snapshot.QualitySummary.SessionCount);
        Assert.AreEqual(0, snapshot.QualitySummary.AverageScore);
        Assert.AreEqual(0, snapshot.PersonalBests.Count);
        Assert.AreEqual(0, snapshot.CalendarDays.Count);
        Assert.AreEqual(0, snapshot.RecentQuality.Count);
    }

    [TestMethod]
    public async Task TrainingHistoryQueryService_PersonalBestsChooseCorrectSessionsAndRespectModeFilter()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);
        await database.SaveAsync(CreateSession(startedAtUtc: NowUtc.AddHours(-3), mode: "Fixed", targetText: new string('a', 120), netWpm: 90, total: 120, correct: 100));
        var paragraph = CreateSession(startedAtUtc: NowUtc.AddHours(-1), mode: "Paragraph", targetText: new string('a', 120), netWpm: 55, total: 120, correct: 118);
        await database.SaveAsync(paragraph);

        var snapshot = await database.TrainingHistory.GetTrainingHistoryAsync(AnalyticsRange.AllTime, "Paragraph");

        var bestNetWpm = snapshot.PersonalBests.Single(row => row.Kind == "best-net-wpm");
        Assert.AreEqual(paragraph.Id, bestNetWpm.SessionId);
        Assert.AreEqual(55, bestNetWpm.Value, 0.0001);
        Assert.IsTrue(snapshot.PersonalBests.Any(row => row.Kind == "best-paragraph"));
        Assert.IsFalse(snapshot.PersonalBests.Any(row => row.Mode == "Fixed"));
    }

    [TestMethod]
    public async Task TrainingHistoryQueryService_ModeFilterIsCaseInsensitive()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);
        var paragraph = CreateSession(startedAtUtc: NowUtc.AddHours(-1), mode: "Paragraph", targetText: new string('a', 120), netWpm: 55, total: 120, correct: 118);
        await database.SaveAsync(CreateSession(startedAtUtc: NowUtc.AddHours(-2), mode: "Fixed", targetText: new string('a', 120), netWpm: 90, total: 120, correct: 100));
        await database.SaveAsync(paragraph);

        var snapshot = await database.TrainingHistory.GetTrainingHistoryAsync(AnalyticsRange.AllTime, "paragraph");

        Assert.AreEqual(1, snapshot.QualitySummary.SessionCount);
        Assert.AreEqual(paragraph.Id, snapshot.PersonalBests.Single(row => row.Kind == "best-net-wpm").SessionId);
    }

    [TestMethod]
    public async Task TrainingHistoryQueryService_PracticeCalendarGroupsByLocalDate()
    {
        var nowUtc = new DateTimeOffset(2026, 6, 12, 4, 0, 0, TimeSpan.Zero);
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(nowUtc, FixedCentralTime);
        await database.SaveAsync(CreateSession(startedAtUtc: new DateTimeOffset(2026, 6, 12, 2, 30, 0, TimeSpan.Zero)));

        var snapshot = await database.TrainingHistory.GetTrainingHistoryAsync(AnalyticsRange.Last7Days, modeFilter: null);

        var activeDay = snapshot.CalendarDays.Single(day => day.SessionCount == 1);
        Assert.AreEqual(new DateOnly(2026, 6, 11), activeDay.Date);
    }

    [TestMethod]
    public async Task TrainingHistoryQueryService_PracticeCalendarRespectsLast7AndLast30Ranges()
    {
        var nowUtc = new DateTimeOffset(2026, 6, 12, 4, 0, 0, TimeSpan.Zero);
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(nowUtc, FixedCentralTime);
        await database.SaveAsync(CreateSession(startedAtUtc: LocalToUtc(2026, 6, 4, 10, 0)));
        await database.SaveAsync(CreateSession(startedAtUtc: LocalToUtc(2026, 6, 5, 10, 0)));
        await database.SaveAsync(CreateSession(startedAtUtc: LocalToUtc(2026, 5, 12, 10, 0)));

        var last7 = await database.TrainingHistory.GetTrainingHistoryAsync(AnalyticsRange.Last7Days, modeFilter: null);
        var last30 = await database.TrainingHistory.GetTrainingHistoryAsync(AnalyticsRange.Last30Days, modeFilter: null);

        Assert.AreEqual(7, last7.CalendarDays.Count);
        Assert.AreEqual(1, last7.QualitySummary.SessionCount);
        Assert.AreEqual(30, last30.CalendarDays.Count);
        Assert.AreEqual(2, last30.QualitySummary.SessionCount);
    }

    [TestMethod]
    public async Task TrainingHistoryQueryService_QualitySummaryCalculatesAverageBestAndTrend()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);
        await database.Settings.SaveSettingsAsync(AppSettings.Defaults with { GoalTargetNetWpm = 60 });
        await database.SaveAsync(CreateSession(startedAtUtc: NowUtc.AddHours(-5), netWpm: 20, total: 100, correct: 70));
        await database.SaveAsync(CreateSession(startedAtUtc: NowUtc.AddHours(-4), netWpm: 25, total: 100, correct: 75));
        await database.SaveAsync(CreateSession(startedAtUtc: NowUtc.AddHours(-3), netWpm: 30, total: 100, correct: 80));
        await database.SaveAsync(CreateSession(startedAtUtc: NowUtc.AddHours(-2), netWpm: 55, total: 100, correct: 95));
        await database.SaveAsync(CreateSession(startedAtUtc: NowUtc.AddHours(-1), netWpm: 60, total: 100, correct: 98));

        var snapshot = await database.TrainingHistory.GetTrainingHistoryAsync(AnalyticsRange.AllTime, modeFilter: null);

        Assert.AreEqual(5, snapshot.QualitySummary.SessionCount);
        Assert.IsTrue(snapshot.QualitySummary.AverageScore > 0);
        Assert.IsTrue(snapshot.QualitySummary.BestScore >= snapshot.QualitySummary.AverageScore);
        Assert.IsTrue(snapshot.QualitySummary.RecentTrend > 0);
    }

    [TestMethod]
    public async Task TrainingHistoryQueryService_LongAndParagraphRecordsUseThresholds()
    {
        await using var database = await AnalyticsTestDatabase.CreateInitializedAsync(NowUtc);
        await database.SaveAsync(CreateSession(startedAtUtc: NowUtc.AddHours(-3), mode: "Paragraph", targetText: new string('a', 999), netWpm: 45, total: 999, correct: 950));
        var longSession = CreateSession(startedAtUtc: NowUtc.AddHours(-2), mode: "Paragraph", targetText: new string('a', 1000), netWpm: 50, total: 1000, correct: 970);
        await database.SaveAsync(longSession);

        var snapshot = await database.TrainingHistory.GetTrainingHistoryAsync(AnalyticsRange.AllTime, modeFilter: null);

        Assert.AreEqual(longSession.Id, snapshot.PersonalBests.Single(row => row.Kind == "best-long").SessionId);
        Assert.AreEqual(longSession.Id, snapshot.PersonalBests.Single(row => row.Kind == "best-paragraph").SessionId);
    }

    private static StoredPracticeSession CreateSession(
        DateTimeOffset startedAtUtc,
        string targetText = "ab",
        string mode = "fixed",
        double rawWpm = 50,
        double netWpm = 45,
        int total = 2,
        int correct = 1)
    {
        return new StoredPracticeSession(
            Guid.NewGuid(),
            startedAtUtc,
            startedAtUtc.AddMinutes(1),
            mode,
            targetText,
            targetText.Length,
            rawWpm,
            netWpm,
            correct / (double)Math.Max(1, total),
            null,
            total,
            correct,
            total - correct,
            0,
            total - correct,
            60_000);
    }

    private static StoredKeyEvent[] CreateTheEvents(Guid sessionId)
    {
        return
        [
            CharacterEvent(sessionId, position: 0, expected: 't', actual: 't', isCorrect: true, deltaPreviousMs: null, timestampTicks: 1),
            CharacterEvent(sessionId, position: 1, expected: 'h', actual: 'h', isCorrect: true, deltaPreviousMs: 120, timestampTicks: 2),
            CharacterEvent(sessionId, position: 2, expected: 'e', actual: 'x', isCorrect: false, deltaPreviousMs: 240, timestampTicks: 3)
        ];
    }

    private static StoredKeyEvent CharacterEvent(
        Guid sessionId,
        int position,
        char expected,
        char actual,
        bool isCorrect,
        double? deltaPreviousMs,
        long timestampTicks)
    {
        return new StoredKeyEvent(
            Id: null,
            sessionId,
            position,
            expected,
            actual,
            "Character",
            isCorrect,
            WasCorrection: false,
            timestampTicks,
            ElapsedMs: deltaPreviousMs ?? 0,
            deltaPreviousMs);
    }

    private static DateTimeOffset LocalToUtc(int year, int month, int day, int hour, int minute)
    {
        return new DateTimeOffset(year, month, day, hour, minute, 0, FixedCentralTime.BaseUtcOffset)
            .ToUniversalTime();
    }

    private sealed class AnalyticsTestDatabase : IAsyncDisposable
    {
        private AnalyticsTestDatabase(
            string directoryPath,
            SqliteConnectionFactory connectionFactory,
            PracticeSessionRepository repository,
            AnalyticsQueryService analytics,
            DateTimeOffset nowUtc,
            TimeZoneInfo? localTimeZone)
        {
            DirectoryPath = directoryPath;
            Repository = repository;
            Analytics = analytics;
            Heatmap = new KeyboardHeatmapQueryService(connectionFactory);
            SessionDetail = new SessionDetailQueryService(repository);
            Settings = new AppSettingsRepository(connectionFactory);
            TrainingHistory = new TrainingHistoryQueryService(connectionFactory, Settings, new FixedUtcClock(nowUtc), localTimeZone ?? TimeZoneInfo.Utc);
        }

        public string DirectoryPath { get; }

        public PracticeSessionRepository Repository { get; }

        public AnalyticsQueryService Analytics { get; }

        public KeyboardHeatmapQueryService Heatmap { get; }

        public SessionDetailQueryService SessionDetail { get; }

        public AppSettingsRepository Settings { get; }

        public TrainingHistoryQueryService TrainingHistory { get; }

        public static async Task<AnalyticsTestDatabase> CreateInitializedAsync(
            DateTimeOffset nowUtc,
            TimeZoneInfo? localTimeZone = null)
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "TypingTrainer.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            var databasePath = Path.Combine(directoryPath, "typingtrainer.db");
            var connectionFactory = new SqliteConnectionFactory(new FixedDatabasePath(databasePath));
            var initializer = new DatabaseInitializer(connectionFactory, new MigrationRunner());
            await initializer.InitializeAsync();

            var repository = new PracticeSessionRepository(connectionFactory);
            var analytics = new AnalyticsQueryService(
                connectionFactory,
                new FixedUtcClock(nowUtc),
                localTimeZone ?? TimeZoneInfo.Utc);

            return new AnalyticsTestDatabase(directoryPath, connectionFactory, repository, analytics, nowUtc, localTimeZone);
        }

        public Task SaveAsync(StoredPracticeSession session)
        {
            return SaveAsync(session, Array.Empty<StoredKeyEvent>());
        }

        public Task SaveAsync(StoredPracticeSession session, IReadOnlyList<StoredKeyEvent> events)
        {
            return Repository.SaveCompletedSessionAsync(session, events);
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class FixedDatabasePath : IAppDatabasePath
    {
        private readonly string _databasePath;

        public FixedDatabasePath(string databasePath)
        {
            _databasePath = databasePath;
        }

        public string GetDatabasePath()
        {
            return _databasePath;
        }
    }

    private sealed class FixedUtcClock : IUtcClock
    {
        public FixedUtcClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }
    }
}
