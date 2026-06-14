using Microsoft.UI.Xaml;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using TypingTrainer.App.Navigation;
using TypingTrainer.Core.Coaching;
using TypingTrainer.Core.Keyboard;
using TypingTrainer.Core.Lessons;
using TypingTrainer.Data.Models;
using TypingTrainer.Data.Repositories;
using TypingTrainer.Data.Services;

namespace TypingTrainer.App.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private readonly IAnalyticsQueryService _analyticsQueryService;
    private readonly IKeyboardHeatmapQueryService _keyboardHeatmapQueryService;
    private readonly ITrainingHistoryQueryService _trainingHistoryQueryService;
    private readonly IAppSettingsRepository _settingsRepository;
    private readonly TypingCoach _typingCoach = new();
    private readonly AchievementEvaluator _achievementEvaluator = new();
    private AnalyticsRange _selectedRange = AnalyticsRange.Last7Days;
    private DashboardSnapshot? _snapshot;
    private DashboardSnapshot? _weeklySnapshot;
    private TrainingHistorySnapshot? _trainingHistory;
    private IReadOnlyList<KeyboardHeatmapKeyRow> _heatmapRows = Array.Empty<KeyboardHeatmapKeyRow>();
    private AppSettings _settings = AppSettings.Defaults;
    private bool _isLoading;
    private string? _errorMessage;
    private string _goalStatus = string.Empty;
    private string _selectedModeFilter = AllModesFilter;
    private string _selectedKeyFilter = AllKeysFilter;
    private static readonly QwertyCharacterToKeyMapper KeyMapper = new();
    private static readonly IReadOnlyDictionary<string, VisualKeyboardKey> LayoutKeys =
        QwertyVisualKeyboardLayout
            .Create()
            .Rows
            .SelectMany(row => row.Keys)
            .ToDictionary(key => key.Id, StringComparer.Ordinal);

    public const string AllModesFilter = "All modes";
    public const string AllKeysFilter = "All keys";

    public DashboardViewModel(
        IAnalyticsQueryService analyticsQueryService,
        IKeyboardHeatmapQueryService keyboardHeatmapQueryService,
        ITrainingHistoryQueryService trainingHistoryQueryService,
        IAppSettingsRepository settingsRepository)
    {
        _analyticsQueryService = analyticsQueryService;
        _keyboardHeatmapQueryService = keyboardHeatmapQueryService;
        _trainingHistoryQueryService = trainingHistoryQueryService;
        _settingsRepository = settingsRepository;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AnalyticsRange SelectedRange
    {
        get => _selectedRange;
        set
        {
            if (_selectedRange != value)
            {
                _selectedRange = value;
                OnPropertyChanged();
            }
        }
    }

    public string SelectedModeFilter
    {
        get => _selectedModeFilter;
        set
        {
            if (_selectedModeFilter != value)
            {
                _selectedModeFilter = value;
                OnPropertyChanged();
            }
        }
    }

    public string SelectedKeyFilter
    {
        get => _selectedKeyFilter;
        set
        {
            if (_selectedKeyFilter != value)
            {
                _selectedKeyFilter = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HeatmapRows));
                OnPropertyChanged(nameof(HandFingerRows));
            }
        }
    }

    public DashboardSnapshot? Snapshot
    {
        get => _snapshot;
        private set
        {
            if (!Equals(_snapshot, value))
            {
                _snapshot = value;
                OnSnapshotChanged();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LoadingVisibility));
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ErrorVisibility));
            }
        }
    }

    public bool HasData => Snapshot?.Summary.SessionCount > 0;

    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ErrorVisibility => string.IsNullOrWhiteSpace(ErrorMessage)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility EmptyStateVisibility => !IsLoading && ErrorMessage is null && !HasData
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility DashboardContentVisibility => !IsLoading && ErrorMessage is null && HasData
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility GoalPanelVisibility => !IsLoading && ErrorMessage is null
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string SessionCountText => Snapshot?.Summary.SessionCount.ToString(CultureInfo.InvariantCulture) ?? "0";

    public string PracticeTimeText => FormatDuration(Snapshot?.Summary.TotalPracticeTime ?? TimeSpan.Zero);

    public string AverageNetWpmText => FormatWpm(Snapshot?.Summary.AverageNetWpm ?? 0);

    public string BestNetWpmText => FormatWpm(Snapshot?.Summary.BestNetWpm ?? 0);

    public string AccuracyText => FormatPercent(Snapshot?.Summary.Accuracy ?? 0);

    public string ErrorsText => ((Snapshot?.Summary.IncorrectKeypresses ?? 0) + (Snapshot?.Summary.UncorrectedErrors ?? 0))
        .ToString(CultureInfo.InvariantCulture);

    public string QualityAverageText => FormatScore(_trainingHistory?.QualitySummary.AverageScore ?? 0);

    public string QualityBestText => FormatScore(_trainingHistory?.QualitySummary.BestScore ?? 0);

    public string QualityGradeText => _trainingHistory?.QualitySummary.CurrentGrade ?? "Needs work";

    public string QualityTrendText
    {
        get
        {
            var trend = _trainingHistory?.QualitySummary.RecentTrend ?? 0;
            if (Math.Abs(trend) < 0.05)
            {
                return "No recent trend yet";
            }

            return trend > 0
                ? $"+{FormatScore(trend)} points vs prior sessions"
                : $"{FormatScore(trend)} points vs prior sessions";
        }
    }

    public double GoalTargetNetWpm
    {
        get => _settings.GoalTargetNetWpm;
        set => UpdateGoalSettings(_settings with { GoalTargetNetWpm = ClampToInt(value, 10, 250) });
    }

    public double GoalTargetAccuracyPercent
    {
        get => _settings.GoalTargetAccuracyPercent;
        set => UpdateGoalSettings(_settings with { GoalTargetAccuracyPercent = ClampToInt(value, 50, 100) });
    }

    public double GoalWeeklyPracticeMinutes
    {
        get => _settings.GoalWeeklyPracticeMinutes;
        set => UpdateGoalSettings(_settings with { GoalWeeklyPracticeMinutes = ClampToInt(value, 0, 10_080) });
    }

    public string GoalCurrentNetWpmText => FormatWpm(Snapshot?.Summary.AverageNetWpm ?? 0);

    public string GoalTargetNetWpmText => FormatWpm(_settings.GoalTargetNetWpm);

    public string GoalWpmGapText
    {
        get
        {
            var gap = Math.Max(0, _settings.GoalTargetNetWpm - (Snapshot?.Summary.AverageNetWpm ?? 0));
            return gap <= 0 ? "Goal reached" : $"{FormatWpm(gap)} WPM to go";
        }
    }

    public string GoalTargetAccuracyText => $"{_settings.GoalTargetAccuracyPercent}%";

    public string GoalWeeklyPracticeText => $"{_settings.GoalWeeklyPracticeMinutes} min/week";

    public string GoalWeeklyProgressText =>
        $"{FormatDuration(WeeklyPracticeTime)} practiced in the last 7 days";

    public string TodayRecommendationText => BuildTodayRecommendation();

    public string GoalStatus
    {
        get => _goalStatus;
        private set
        {
            if (_goalStatus != value)
            {
                _goalStatus = value;
                OnPropertyChanged();
            }
        }
    }

    public IReadOnlyList<string> GoalActionSteps => BuildGoalActionSteps();

    public IReadOnlyList<DashboardInsightRow> InsightRows => BuildInsightRows();

    public DailyPracticePlan DailyPlan => _typingCoach.BuildDailyPlan(
        BuildCoachingStats(),
        ParseTrainingFocus(_settings.GoalTrainingFocus),
        _settings.GoalTargetSessionMinutes,
        _settings.GoalTargetEssayWords);

    public IReadOnlyList<PracticePlanStepDisplayRow> DailyPlanRows => DailyPlan.Steps
        .Select(step => new PracticePlanStepDisplayRow(
            step.Order.ToString(CultureInfo.InvariantCulture),
            step.Title,
            step.Description,
            step.RecommendedMode.ToString(),
            $"{step.Minutes} min"))
        .ToArray();

    public PracticeLaunchRequest CreateDailyPlanLaunchRequest()
    {
        var plan = DailyPlan;
        var step = plan.Steps.FirstOrDefault();
        if (step is null)
        {
            return new PracticeLaunchRequest(
                LessonMode.Adaptive,
                PracticeLessonSize.Small,
                TargetCharacters: null,
                Reason: "Today's plan: adaptive practice");
        }

        return new PracticeLaunchRequest(
            step.RecommendedMode,
            GetPracticeSize(step.TargetLength),
            Math.Max(20, step.TargetLength),
            $"Today's plan: {step.Title} - {step.Description}");
    }

    public IReadOnlyList<AchievementDisplayRow> AchievementRows => _achievementEvaluator
        .Evaluate(BuildCoachingStats())
        .Select(item => new AchievementDisplayRow(
            item.Title,
            item.Description,
            item.IsUnlocked ? "Unlocked" : "Locked",
            item.IsUnlocked ? 100 : 0))
        .ToArray();

    public IReadOnlyList<KeyboardHeatmapDisplayRow> HeatmapRows => _heatmapRows
        .Where(MatchesKeyFilter)
        .OrderByDescending(row => row.WeaknessScore)
        .ThenByDescending(row => row.ExposureCount)
        .Select(row => new KeyboardHeatmapDisplayRow(
            row.KeyLabel,
            FormatPercent(row.Accuracy),
            FormatLatency(row.MedianLatencyMs),
            row.ExposureCount.ToString(CultureInfo.InvariantCulture),
            row.WeaknessScore * 100,
            FormatHeatmapTrend(row.WeaknessScore)))
        .ToArray();

    public IReadOnlyList<HandFingerDisplayRow> HandFingerRows => _heatmapRows
        .Select(row => (Row: row, Key: GetVisualKey(row.Character)))
        .Where(item => item.Key is not null)
        .GroupBy(item => ToFingerGroup(item.Key!.Finger), StringComparer.Ordinal)
        .Select(group =>
        {
            var rows = group.Select(item => item.Row).ToArray();
            var samples = rows.Sum(row => row.ExposureCount);
            var correct = rows.Sum(row => row.CorrectCount);
            var median = AnalyticsMedian(rows.Select(row => row.MedianLatencyMs).OfType<double>());
            var weakness = rows.Length == 0 ? 0 : rows.Average(row => row.WeaknessScore) * 100;
            return new HandFingerDisplayRow(
                group.Key,
                FormatPercent(samples == 0 ? 0 : correct / (double)samples),
                FormatLatency(median),
                samples.ToString(CultureInfo.InvariantCulture),
                weakness,
                FormatHeatmapTrend(weakness / 100.0));
        })
        .OrderByDescending(row => row.WeaknessPercent)
        .ToArray();

    public IReadOnlyList<PersonalBestDisplayRow> PersonalBestRows => _trainingHistory?.PersonalBests
        .Select(row => new PersonalBestDisplayRow(
            row.Label,
            FormatPersonalBestValue(row),
            FormatPersonalBestDetail(row),
            row.SessionId))
        .ToArray() ?? Array.Empty<PersonalBestDisplayRow>();

    public IReadOnlyList<PracticeCalendarDisplayRow> PracticeCalendarRows => _trainingHistory?.CalendarDays
        .Select(ToCalendarDisplayRow)
        .ToArray() ?? Array.Empty<PracticeCalendarDisplayRow>();

    public IReadOnlyList<ChartPointViewModel> QualityTrendPoints => _trainingHistory?.RecentQuality
        .OrderBy(row => row.StartedAtUtc)
        .Select(row => new ChartPointViewModel(
            row.StartedAtUtc.ToLocalTime().ToString("MMM d", CultureInfo.InvariantCulture),
            row.Score))
        .ToArray() ?? Array.Empty<ChartPointViewModel>();

    public IReadOnlyList<DailyMetricDisplayRow> DailyMetricRows => Snapshot?.DailyMetrics
        .Select(point => new DailyMetricDisplayRow(
            point.Date.ToString("MMM d", CultureInfo.InvariantCulture),
            FormatWpm(point.AverageNetWpm),
            FormatPercent(point.Accuracy),
            FormatDuration(point.PracticeTime),
            point.SessionCount.ToString(CultureInfo.InvariantCulture)))
        .ToArray() ?? Array.Empty<DailyMetricDisplayRow>();

    public IReadOnlyList<ChartPointViewModel> NetWpmPoints => Snapshot?.DailyMetrics
        .Select(point => new ChartPointViewModel(
            point.Date.ToString("MMM d", CultureInfo.InvariantCulture),
            point.AverageNetWpm))
        .ToArray() ?? Array.Empty<ChartPointViewModel>();

    public IReadOnlyList<ChartPointViewModel> AccuracyPoints => Snapshot?.DailyMetrics
        .Select(point => new ChartPointViewModel(
            point.Date.ToString("MMM d", CultureInfo.InvariantCulture),
            point.Accuracy * 100))
        .ToArray() ?? Array.Empty<ChartPointViewModel>();

    public IReadOnlyList<ChartPointViewModel> PracticeTimePoints => Snapshot?.DailyMetrics
        .Select(point => new ChartPointViewModel(
            point.Date.ToString("MMM d", CultureInfo.InvariantCulture),
            point.PracticeTime.TotalMinutes))
        .ToArray() ?? Array.Empty<ChartPointViewModel>();

    public IReadOnlyList<RecentSessionDisplayRow> RecentSessionRows => Snapshot?.RecentSessions
        .Select(session => new RecentSessionDisplayRow(
            session.SessionId,
            session.StartedAtUtc.ToLocalTime().ToString("MMM d, h:mm tt", CultureInfo.InvariantCulture),
            FormatWpm(session.NetWpm),
            FormatPercent(session.Accuracy),
            FormatDuration(session.Duration),
            session.Mode))
        .ToArray() ?? Array.Empty<RecentSessionDisplayRow>();

    public IReadOnlyList<AnalyticsDisplayRow> WeakestCharacterRows => Snapshot?.WeakestCharacters
        .Select(row => ToDisplayRow(row.DisplayCharacter, row))
        .ToArray() ?? Array.Empty<AnalyticsDisplayRow>();

    public IReadOnlyList<AnalyticsDisplayRow> SlowestCharacterRows => Snapshot?.SlowestCharacters
        .Select(row => ToDisplayRow(row.DisplayCharacter, row))
        .ToArray() ?? Array.Empty<AnalyticsDisplayRow>();

    public IReadOnlyList<AnalyticsDisplayRow> WeakestBigramRows => Snapshot?.WeakestBigrams
        .Select(row => ToDisplayRow(row.DisplayBigram, row))
        .ToArray() ?? Array.Empty<AnalyticsDisplayRow>();

    public IReadOnlyList<AnalyticsDisplayRow> SlowestBigramRows => Snapshot?.SlowestBigrams
        .Select(row => ToDisplayRow(row.DisplayBigram, row))
        .ToArray() ?? Array.Empty<AnalyticsDisplayRow>();

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            _settings = await _settingsRepository.GetSettingsAsync(cancellationToken).ConfigureAwait(true);
            Snapshot = await _analyticsQueryService
                .GetDashboardSnapshotAsync(SelectedRange, GetModeFilterValue(), cancellationToken)
                .ConfigureAwait(true);
            _weeklySnapshot = SelectedRange == AnalyticsRange.Last7Days
                ? Snapshot
                : await _analyticsQueryService
                    .GetDashboardSnapshotAsync(AnalyticsRange.Last7Days, cancellationToken)
                    .ConfigureAwait(true);
            _heatmapRows = await _keyboardHeatmapQueryService
                .GetHeatmapAsync(SelectedRange, GetModeFilterValue(), cancellationToken)
                .ConfigureAwait(true);
            _trainingHistory = await _trainingHistoryQueryService
                .GetTrainingHistoryAsync(SelectedRange, GetModeFilterValue(), cancellationToken)
                .ConfigureAwait(true);
            GoalStatus = string.Empty;
        }
        catch
        {
            ErrorMessage = "Analytics could not be loaded from local history.";
        }
        finally
        {
            IsLoading = false;
            OnSnapshotChanged();
        }
    }

    public async Task SaveGoalsAsync(CancellationToken cancellationToken = default)
    {
        await _settingsRepository.SaveSettingsAsync(_settings, cancellationToken).ConfigureAwait(true);
        _trainingHistory = await _trainingHistoryQueryService
            .GetTrainingHistoryAsync(SelectedRange, GetModeFilterValue(), cancellationToken)
            .ConfigureAwait(true);
        GoalStatus = "Typing goals saved locally.";
        OnTrainingHistoryChanged();
        OnGoalSettingsChanged();
    }

    private static AnalyticsDisplayRow ToDisplayRow(string label, CharacterAnalyticsRow row)
    {
        return new AnalyticsDisplayRow(
            label,
            FormatPercent(row.Accuracy),
            FormatLatency(row.MedianLatencyMs),
            row.ExposureCount.ToString(CultureInfo.InvariantCulture),
            row.WeaknessScore.ToString("0.00", CultureInfo.InvariantCulture),
            row.WeaknessScore * 100);
    }

    private static AnalyticsDisplayRow ToDisplayRow(string label, BigramAnalyticsRow row)
    {
        return new AnalyticsDisplayRow(
            label,
            FormatPercent(row.Accuracy),
            FormatLatency(row.MedianLatencyMs),
            row.ExposureCount.ToString(CultureInfo.InvariantCulture),
            row.WeaknessScore.ToString("0.00", CultureInfo.InvariantCulture),
            row.WeaknessScore * 100);
    }

    private static string FormatPercent(double value)
    {
        return value.ToString("P1", CultureInfo.InvariantCulture);
    }

    private static string FormatWpm(double value)
    {
        return value.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static string FormatScore(double value)
    {
        return value.ToString("0", CultureInfo.InvariantCulture);
    }

    private static string FormatLatency(double? value)
    {
        return value is null ? "-" : value.Value.ToString("0", CultureInfo.InvariantCulture);
    }

    private static string FormatDuration(TimeSpan value)
    {
        if (value.TotalHours >= 1)
        {
            return $"{(int)value.TotalHours}h {value.Minutes}m";
        }

        if (value.TotalMinutes >= 1)
        {
            return $"{(int)value.TotalMinutes}m {value.Seconds}s";
        }

        return $"{Math.Max(0, value.Seconds)}s";
    }

    private TimeSpan WeeklyPracticeTime => _weeklySnapshot?.Summary.TotalPracticeTime ?? TimeSpan.Zero;

    private PracticeCalendarDisplayRow ToCalendarDisplayRow(PracticeCalendarDay day)
    {
        var dailyTargetMinutes = Math.Max(5, _settings.GoalWeeklyPracticeMinutes / 7.0);
        var practicePercent = Math.Clamp(day.PracticeTime.TotalMinutes / dailyTargetMinutes * 100, 0, 100);
        var qualityPercent = Math.Clamp(day.AverageQualityScore, 0, 100);
        var practiceText = day.PracticeTime.TotalMinutes >= 1
            ? FormatDuration(day.PracticeTime)
            : "0s";
        var toolTip = day.SessionCount == 0
            ? $"{day.Date:MMM d}: no saved practice"
            : $"{day.Date:MMM d}: {practiceText}, {day.SessionCount} session(s), {FormatScore(day.AverageQualityScore)} quality";

        return new PracticeCalendarDisplayRow(
            day.Date.ToString("MMM d", CultureInfo.InvariantCulture),
            day.Date.Day.ToString(CultureInfo.InvariantCulture),
            day.SessionCount.ToString(CultureInfo.InvariantCulture),
            practiceText,
            day.SessionCount == 0 ? "-" : FormatScore(day.AverageQualityScore),
            toolTip,
            practicePercent,
            qualityPercent);
    }

    private static string FormatPersonalBestValue(PersonalBestRow row)
    {
        return row.Unit switch
        {
            "WPM" => $"{FormatWpm(row.Value)} WPM",
            "%" => $"{row.Value.ToString("0.0", CultureInfo.InvariantCulture)}%",
            "min" => FormatDuration(TimeSpan.FromMinutes(row.Value)),
            "day" or "days" => $"{row.Value.ToString("0", CultureInfo.InvariantCulture)} {row.Unit}",
            _ => $"{row.Value.ToString("0.0", CultureInfo.InvariantCulture)} {row.Unit}"
        };
    }

    private static string FormatPersonalBestDetail(PersonalBestRow row)
    {
        var parts = new List<string>();
        if (row.Date is not null)
        {
            parts.Add(row.Date.Value.ToString("MMM d", CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(row.Mode))
        {
            parts.Add(row.Mode);
        }

        return parts.Count == 0 ? "Local history" : string.Join(" - ", parts);
    }

    private IReadOnlyList<string> BuildGoalActionSteps()
    {
        var steps = new List<string>();
        var summary = Snapshot?.Summary;
        var sessionCount = summary?.SessionCount ?? 0;
        var accuracyTarget = _settings.GoalTargetAccuracyPercent / 100.0;
        var averageNetWpm = summary?.AverageNetWpm ?? 0;
        var accuracy = summary?.Accuracy ?? 0;
        var weeklyPracticeMinutes = WeeklyPracticeTime.TotalMinutes;
        var remainingPracticeMinutes = Math.Max(0, _settings.GoalWeeklyPracticeMinutes - weeklyPracticeMinutes);

        if (sessionCount < 5)
        {
            steps.Add($"Complete {5 - sessionCount} more saved sessions so the plan has a steadier baseline.");
        }

        if (accuracy < accuracyTarget)
        {
            var key = Snapshot?.WeakestCharacters.FirstOrDefault()?.DisplayCharacter;
            steps.Add(string.IsNullOrWhiteSpace(key)
                ? "Use Strict Accuracy Mode and Weak Keys until accuracy is consistently on target."
                : $"Use Strict Accuracy Mode and Weak Keys, starting with {key}.");
        }
        else if (averageNetWpm < _settings.GoalTargetNetWpm)
        {
            var bigram = Snapshot?.SlowestBigrams.FirstOrDefault()?.DisplayBigram;
            steps.Add(string.IsNullOrWhiteSpace(bigram)
                ? "Use Paragraph mode for flow, then Weak Bigrams to smooth slow transitions."
                : $"Use Paragraph mode for flow, then Weak Bigrams for {bigram}.");
        }

        if (remainingPracticeMinutes > 0)
        {
            steps.Add($"Practice {FormatDuration(TimeSpan.FromMinutes(remainingPracticeMinutes))} more this week.");
        }

        if (steps.Count == 0)
        {
            steps.Add("You are on pace. Raise the WPM target when this feels comfortable.");
        }

        steps.Add("Keep sessions short: 10-15 focused minutes beats one long grind.");
        steps.Add("After each paragraph block, review the weak keys list and run one targeted lesson.");

        return steps.Take(3).ToArray();
    }

    private CoachingStats BuildCoachingStats()
    {
        var snapshot = Snapshot;
        var weeklyPracticeMinutes = WeeklyPracticeTime.TotalMinutes;
        var dailyMetrics = snapshot?.DailyMetrics ?? Array.Empty<DailyMetricPoint>();

        return new CoachingStats(
            snapshot?.Summary.SessionCount ?? 0,
            snapshot?.Summary.AverageNetWpm ?? 0,
            snapshot?.Summary.BestNetWpm ?? 0,
            snapshot?.Summary.Accuracy ?? 0,
            snapshot?.RecentSessions.Select(session => session.Accuracy).DefaultIfEmpty(0).Max() ?? 0,
            weeklyPracticeMinutes,
            _settings.GoalTargetNetWpm,
            _settings.GoalTargetAccuracyPercent,
            _settings.GoalWeeklyPracticeMinutes,
            CalculateCurrentStreak(dailyMetrics),
            snapshot?.WeakestCharacters.FirstOrDefault()?.DisplayCharacter,
            snapshot?.SlowestBigrams.FirstOrDefault()?.DisplayBigram);
    }

    private static int CalculateCurrentStreak(IReadOnlyList<DailyMetricPoint> dailyMetrics)
    {
        if (dailyMetrics.Count == 0)
        {
            return 0;
        }

        var dates = dailyMetrics.Select(point => point.Date).ToHashSet();
        var current = dailyMetrics.Max(point => point.Date);
        var streak = 0;

        while (dates.Contains(current))
        {
            streak++;
            current = current.AddDays(-1);
        }

        return streak;
    }

    private static TrainingFocus ParseTrainingFocus(string value)
    {
        return Enum.TryParse<TrainingFocus>(value, ignoreCase: true, out var focus)
            ? focus
            : TrainingFocus.Balanced;
    }

    private IReadOnlyList<DashboardInsightRow> BuildInsightRows()
    {
        var snapshot = Snapshot;
        if (snapshot is null || snapshot.Summary.SessionCount == 0)
        {
            return
            [
                new DashboardInsightRow("Baseline", "No data yet", "Complete 5 saved sessions to make the coaching more useful."),
                new DashboardInsightRow("Next step", "Practice", "Start with Paragraph or Adaptive mode for a clean baseline."),
                new DashboardInsightRow("Local status", "Private", "Insights are calculated from your local SQLite history.")
            ];
        }

        var accuracyTarget = _settings.GoalTargetAccuracyPercent / 100.0;
        var weakestKey = snapshot.WeakestCharacters.FirstOrDefault();
        var slowestBigram = snapshot.SlowestBigrams.FirstOrDefault();
        var remainingPracticeMinutes = Math.Max(0, _settings.GoalWeeklyPracticeMinutes - WeeklyPracticeTime.TotalMinutes);
        var strength = snapshot.Summary.Accuracy >= accuracyTarget
            ? new DashboardInsightRow("Strength", "Accuracy", $"You are at {FormatPercent(snapshot.Summary.Accuracy)} against a {GoalTargetAccuracyText} target.")
            : new DashboardInsightRow("Strength", "Practice volume", $"{SessionCountText} saved sessions in this view.");
        var limiter = weakestKey is not null && weakestKey.Accuracy < accuracyTarget
            ? new DashboardInsightRow("Biggest limiter", weakestKey.DisplayCharacter, $"{FormatPercent(weakestKey.Accuracy)} accuracy across {weakestKey.ExposureCount} samples.")
            : slowestBigram is not null
                ? new DashboardInsightRow("Biggest limiter", slowestBigram.DisplayBigram, $"{FormatLatency(slowestBigram.MedianLatencyMs)} ms median transition.")
                : new DashboardInsightRow("Biggest limiter", "Pace", $"{GoalWpmGapText}.");
        var weekly = remainingPracticeMinutes > 0
            ? new DashboardInsightRow("This week", FormatDuration(TimeSpan.FromMinutes(remainingPracticeMinutes)), "Remaining to hit your weekly practice target.")
            : new DashboardInsightRow("This week", "On pace", "Weekly practice target reached.");

        return [strength, limiter, new DashboardInsightRow("Recommended", GetRecommendedModeName(), TodayRecommendationText), weekly];
    }

    private string BuildTodayRecommendation()
    {
        var snapshot = Snapshot;
        if (snapshot is null || snapshot.Summary.SessionCount == 0)
        {
            return "Complete 5 short sessions so the app can identify stable weak keys and speed patterns.";
        }

        var accuracyTarget = _settings.GoalTargetAccuracyPercent / 100.0;
        var remainingPracticeMinutes = Math.Max(0, _settings.GoalWeeklyPracticeMinutes - WeeklyPracticeTime.TotalMinutes);
        var durationText = remainingPracticeMinutes > 0
            ? $" Aim for {FormatDuration(TimeSpan.FromMinutes(Math.Min(15, remainingPracticeMinutes)))} today."
            : " Keep the session short and focused.";

        if (snapshot.Summary.Accuracy < accuracyTarget)
        {
            var key = snapshot.WeakestCharacters.FirstOrDefault()?.DisplayCharacter;
            return string.IsNullOrWhiteSpace(key)
                ? $"Run Weak Keys with Strict Accuracy Mode enabled.{durationText}"
                : $"Run Weak Keys with Strict Accuracy Mode, focusing first on {key}.{durationText}";
        }

        if (snapshot.Summary.AverageNetWpm < _settings.GoalTargetNetWpm)
        {
            var bigram = snapshot.SlowestBigrams.FirstOrDefault()?.DisplayBigram;
            return string.IsNullOrWhiteSpace(bigram)
                ? $"Run Paragraph mode, then finish with Weak Bigrams.{durationText}"
                : $"Run Paragraph mode, then smooth the {bigram} transition in Weak Bigrams.{durationText}";
        }

        return $"You are on target. Use Review or Paragraph mode to maintain control.{durationText}";
    }

    private string GetRecommendedModeName()
    {
        var snapshot = Snapshot;
        if (snapshot is null || snapshot.Summary.SessionCount == 0)
        {
            return "Paragraph";
        }

        return snapshot.Summary.Accuracy < (_settings.GoalTargetAccuracyPercent / 100.0)
            ? "Weak Keys"
            : snapshot.Summary.AverageNetWpm < _settings.GoalTargetNetWpm
                ? "Paragraph"
                : "Review";
    }

    private string? GetModeFilterValue()
    {
        return string.Equals(SelectedModeFilter, AllModesFilter, StringComparison.OrdinalIgnoreCase)
            ? null
            : SelectedModeFilter.Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    private bool MatchesKeyFilter(KeyboardHeatmapKeyRow row)
    {
        if (string.Equals(SelectedKeyFilter, AllKeysFilter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var key = GetVisualKey(row.Character);
        return SelectedKeyFilter switch
        {
            "Left hand" => key is not null && IsLeftFinger(key.Finger),
            "Right hand" => key is not null && IsRightFinger(key.Finger),
            "Home row" => key is not null && "ASDFGHJKL;".Contains(key.PrimaryLabel, StringComparison.OrdinalIgnoreCase),
            "Top row" => key is not null && "QWERTYUIOP".Contains(key.PrimaryLabel, StringComparison.OrdinalIgnoreCase),
            "Bottom row" => key is not null && "ZXCVBNM,./".Contains(key.PrimaryLabel, StringComparison.OrdinalIgnoreCase),
            "Letters" => char.IsLetter(row.Character),
            "Punctuation" => char.IsPunctuation(row.Character),
            "Numbers" => char.IsDigit(row.Character),
            _ => true
        };
    }

    private static VisualKeyboardKey? GetVisualKey(char character)
    {
        var mapping = KeyMapper.Map(character);
        return mapping is not null && LayoutKeys.TryGetValue(mapping.KeyId, out var key)
            ? key
            : null;
    }

    private static string ToFingerGroup(FingerAssignment finger)
    {
        return finger switch
        {
            FingerAssignment.LeftPinky => "Left pinky",
            FingerAssignment.LeftRing => "Left ring",
            FingerAssignment.LeftMiddle => "Left middle",
            FingerAssignment.LeftIndex => "Left index",
            FingerAssignment.LeftThumb => "Left thumb",
            FingerAssignment.RightThumb => "Right thumb",
            FingerAssignment.RightIndex => "Right index",
            FingerAssignment.RightMiddle => "Right middle",
            FingerAssignment.RightRing => "Right ring",
            FingerAssignment.RightPinky => "Right pinky",
            _ => "Other"
        };
    }

    private static bool IsLeftFinger(FingerAssignment finger)
    {
        return finger is FingerAssignment.LeftPinky
            or FingerAssignment.LeftRing
            or FingerAssignment.LeftMiddle
            or FingerAssignment.LeftIndex
            or FingerAssignment.LeftThumb;
    }

    private static bool IsRightFinger(FingerAssignment finger)
    {
        return finger is FingerAssignment.RightPinky
            or FingerAssignment.RightRing
            or FingerAssignment.RightMiddle
            or FingerAssignment.RightIndex
            or FingerAssignment.RightThumb;
    }

    private static string FormatHeatmapTrend(double weaknessScore)
    {
        return weaknessScore switch
        {
            >= 0.68 => "Worsening",
            >= 0.34 => "Steady",
            _ => "Improving"
        };
    }

    private static PracticeLessonSize GetPracticeSize(int targetCharacters)
    {
        if (targetCharacters >= PracticeLessonSizeTargets.LongTargetCharacters)
        {
            return PracticeLessonSize.Long;
        }

        if (targetCharacters >= PracticeLessonSizeTargets.MediumTargetCharacters)
        {
            return PracticeLessonSize.Medium;
        }

        return PracticeLessonSize.Small;
    }

    private static double? AnalyticsMedian(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(value => value).ToArray();
        if (sorted.Length == 0)
        {
            return null;
        }

        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2.0;
    }

    private void UpdateGoalSettings(AppSettings settings)
    {
        _settings = settings;
        GoalStatus = string.Empty;
        OnGoalSettingsChanged();
    }

    private void OnGoalSettingsChanged()
    {
        OnPropertyChanged(nameof(GoalTargetNetWpm));
        OnPropertyChanged(nameof(GoalTargetAccuracyPercent));
        OnPropertyChanged(nameof(GoalWeeklyPracticeMinutes));
        OnPropertyChanged(nameof(GoalCurrentNetWpmText));
        OnPropertyChanged(nameof(GoalTargetNetWpmText));
        OnPropertyChanged(nameof(GoalWpmGapText));
        OnPropertyChanged(nameof(GoalTargetAccuracyText));
        OnPropertyChanged(nameof(GoalWeeklyPracticeText));
        OnPropertyChanged(nameof(GoalWeeklyProgressText));
        OnPropertyChanged(nameof(TodayRecommendationText));
        OnPropertyChanged(nameof(GoalActionSteps));
        OnPropertyChanged(nameof(InsightRows));
        OnPropertyChanged(nameof(DailyPlan));
        OnPropertyChanged(nameof(DailyPlanRows));
        OnPropertyChanged(nameof(AchievementRows));
    }

    private static int ClampToInt(double value, int minimum, int maximum)
    {
        if (double.IsNaN(value))
        {
            return minimum;
        }

        return Math.Clamp((int)Math.Round(value), minimum, maximum);
    }

    private void OnSnapshotChanged()
    {
        OnPropertyChanged(nameof(Snapshot));
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(DashboardContentVisibility));
        OnPropertyChanged(nameof(GoalPanelVisibility));
        OnPropertyChanged(nameof(SessionCountText));
        OnPropertyChanged(nameof(PracticeTimeText));
        OnPropertyChanged(nameof(AverageNetWpmText));
        OnPropertyChanged(nameof(BestNetWpmText));
        OnPropertyChanged(nameof(AccuracyText));
        OnPropertyChanged(nameof(ErrorsText));
        OnPropertyChanged(nameof(DailyMetricRows));
        OnPropertyChanged(nameof(NetWpmPoints));
        OnPropertyChanged(nameof(AccuracyPoints));
        OnPropertyChanged(nameof(PracticeTimePoints));
        OnPropertyChanged(nameof(RecentSessionRows));
        OnPropertyChanged(nameof(WeakestCharacterRows));
        OnPropertyChanged(nameof(SlowestCharacterRows));
        OnPropertyChanged(nameof(WeakestBigramRows));
        OnPropertyChanged(nameof(SlowestBigramRows));
        OnPropertyChanged(nameof(HeatmapRows));
        OnPropertyChanged(nameof(HandFingerRows));
        OnTrainingHistoryChanged();
        OnGoalSettingsChanged();
    }

    private void OnTrainingHistoryChanged()
    {
        OnPropertyChanged(nameof(QualityAverageText));
        OnPropertyChanged(nameof(QualityBestText));
        OnPropertyChanged(nameof(QualityGradeText));
        OnPropertyChanged(nameof(QualityTrendText));
        OnPropertyChanged(nameof(PersonalBestRows));
        OnPropertyChanged(nameof(PracticeCalendarRows));
        OnPropertyChanged(nameof(QualityTrendPoints));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record DailyMetricDisplayRow(
    string Date,
    string AverageNetWpm,
    string Accuracy,
    string PracticeTime,
    string SessionCount);

public sealed record RecentSessionDisplayRow(
    Guid SessionId,
    string StartedAt,
    string NetWpm,
    string Accuracy,
    string Duration,
    string Mode);

public sealed record ChartPointViewModel(
    string Label,
    double Value);

public sealed record DashboardInsightRow(
    string Title,
    string Value,
    string Detail);

public sealed record PracticePlanStepDisplayRow(
    string Order,
    string Title,
    string Description,
    string Mode,
    string Minutes);

public sealed record AchievementDisplayRow(
    string Title,
    string Description,
    string Status,
    double Progress);

public sealed record PersonalBestDisplayRow(
    string Label,
    string Value,
    string Detail,
    Guid? SessionId);

public sealed record PracticeCalendarDisplayRow(
    string Date,
    string Day,
    string Sessions,
    string PracticeTime,
    string Quality,
    string ToolTip,
    double PracticePercent,
    double QualityPercent);

public sealed record KeyboardHeatmapDisplayRow(
    string Key,
    string Accuracy,
    string MedianLatencyMs,
    string Samples,
    double WeaknessPercent,
    string Trend);

public sealed record HandFingerDisplayRow(
    string Group,
    string Accuracy,
    string MedianLatencyMs,
    string Samples,
    double WeaknessPercent,
    string Trend);

public sealed record AnalyticsDisplayRow(
    string Label,
    string Accuracy,
    string MedianLatencyMs,
    string ExposureCount,
    string WeaknessScore,
    double WeaknessPercent);
