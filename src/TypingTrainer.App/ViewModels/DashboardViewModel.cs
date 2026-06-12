using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using TypingTrainer.Data.Models;
using TypingTrainer.Data.Services;

namespace TypingTrainer.App.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private readonly IAnalyticsQueryService _analyticsQueryService;
    private AnalyticsRange _selectedRange = AnalyticsRange.Last7Days;
    private DashboardSnapshot? _snapshot;
    private bool _isLoading;
    private string? _errorMessage;

    public DashboardViewModel(IAnalyticsQueryService analyticsQueryService)
    {
        _analyticsQueryService = analyticsQueryService;
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

    public string SessionCountText => Snapshot?.Summary.SessionCount.ToString(CultureInfo.InvariantCulture) ?? "0";

    public string PracticeTimeText => FormatDuration(Snapshot?.Summary.TotalPracticeTime ?? TimeSpan.Zero);

    public string AverageNetWpmText => FormatWpm(Snapshot?.Summary.AverageNetWpm ?? 0);

    public string BestNetWpmText => FormatWpm(Snapshot?.Summary.BestNetWpm ?? 0);

    public string AccuracyText => FormatPercent(Snapshot?.Summary.Accuracy ?? 0);

    public string ErrorsText => ((Snapshot?.Summary.IncorrectKeypresses ?? 0) + (Snapshot?.Summary.UncorrectedErrors ?? 0))
        .ToString(CultureInfo.InvariantCulture);

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
            Snapshot = await _analyticsQueryService
                .GetDashboardSnapshotAsync(SelectedRange, cancellationToken)
                .ConfigureAwait(true);
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

    private void OnSnapshotChanged()
    {
        OnPropertyChanged(nameof(Snapshot));
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(DashboardContentVisibility));
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

public sealed record AnalyticsDisplayRow(
    string Label,
    string Accuracy,
    string MedianLatencyMs,
    string ExposureCount,
    string WeaknessScore,
    double WeaknessPercent);
