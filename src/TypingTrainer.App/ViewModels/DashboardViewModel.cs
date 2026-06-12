using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using TypingTrainer.Data.Models;
using TypingTrainer.Data.Repositories;
using TypingTrainer.Data.Services;

namespace TypingTrainer.App.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private readonly IAnalyticsQueryService _analyticsQueryService;
    private readonly IAppSettingsRepository _settingsRepository;
    private AnalyticsRange _selectedRange = AnalyticsRange.Last7Days;
    private DashboardSnapshot? _snapshot;
    private DashboardSnapshot? _weeklySnapshot;
    private AppSettings _settings = AppSettings.Defaults;
    private bool _isLoading;
    private string? _errorMessage;
    private string _goalStatus = string.Empty;
    private string _selectedModeFilter = AllModesFilter;

    public const string AllModesFilter = "All modes";

    public DashboardViewModel(
        IAnalyticsQueryService analyticsQueryService,
        IAppSettingsRepository settingsRepository)
    {
        _analyticsQueryService = analyticsQueryService;
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
        GoalStatus = "Typing goals saved locally.";
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
        OnGoalSettingsChanged();
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

public sealed record AnalyticsDisplayRow(
    string Label,
    string Accuracy,
    string MedianLatencyMs,
    string ExposureCount,
    string WeaknessScore,
    double WeaknessPercent);
