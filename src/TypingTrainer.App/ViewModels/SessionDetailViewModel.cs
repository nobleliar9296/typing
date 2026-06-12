using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using TypingTrainer.Core.Training;
using TypingTrainer.Data.Models;
using TypingTrainer.Data.Repositories;
using TypingTrainer.Data.Services;

namespace TypingTrainer.App.ViewModels;

public sealed class SessionDetailViewModel : INotifyPropertyChanged
{
    private readonly ISessionDetailQueryService _sessionDetailQueryService;
    private readonly IAppSettingsRepository _settingsRepository;
    private readonly SessionQualityCalculator _qualityCalculator = new();
    private SessionDetailSnapshot? _snapshot;
    private AppSettings _settings = AppSettings.Defaults;
    private bool _isLoading;
    private string? _errorMessage;

    public SessionDetailViewModel(
        ISessionDetailQueryService sessionDetailQueryService,
        IAppSettingsRepository settingsRepository)
    {
        _sessionDetailQueryService = sessionDetailQueryService;
        _settingsRepository = settingsRepository;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public SessionDetailSnapshot? Snapshot
    {
        get => _snapshot;
        private set
        {
            _snapshot = value;
            OnSnapshotChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            _isLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LoadingVisibility));
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            _errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ErrorVisibility));
        }
    }

    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ErrorVisibility => string.IsNullOrWhiteSpace(ErrorMessage) ? Visibility.Collapsed : Visibility.Visible;

    public string Title => Snapshot is null
        ? "Session Detail"
        : $"{Snapshot.Session.StartedAtUtc.ToLocalTime():MMM d, h:mm tt} - {Snapshot.Session.Mode}";

    public string NetWpmText => FormatWpm(Snapshot?.Session.NetWpm ?? 0);

    public string AccuracyText => FormatPercent(Snapshot?.Session.Accuracy ?? 0);

    public string DurationText => FormatDuration(TimeSpan.FromMilliseconds(Snapshot?.Session.DurationMs ?? 0));

    public string MistakesText => ((Snapshot?.Session.IncorrectKeypresses ?? 0) + (Snapshot?.Session.UncorrectedErrors ?? 0)).ToString(CultureInfo.InvariantCulture);

    public string QualityScoreText => QualityResult.Score.ToString("0", CultureInfo.InvariantCulture);

    public string QualityGradeText => QualityResult.Grade;

    public string QualityExplanationText =>
        $"Quality blends accuracy, net WPM against your {_settings.GoalTargetNetWpm} WPM goal, consistency, and control.";

    public IReadOnlyList<SessionTimelinePoint> TimelineRows => Snapshot?.Timeline ?? Array.Empty<SessionTimelinePoint>();

    public IReadOnlyList<ChartPointViewModel> TimelineNetWpmPoints => TimelineRows
        .Select(point => new ChartPointViewModel(point.Label, point.NetWpm))
        .ToArray();

    public IReadOnlyList<ChartPointViewModel> TimelineAccuracyPoints => TimelineRows
        .Select(point => new ChartPointViewModel(point.Label, point.AccuracyPercent))
        .ToArray();

    public IReadOnlyList<SessionDetailMistakeRow> MistakeRows => Snapshot?.Mistakes ?? Array.Empty<SessionDetailMistakeRow>();

    public IReadOnlyList<SessionDetailAnalyticsRow> SlowestKeyRows => Snapshot?.SlowestKeys
        .Select(row => new SessionDetailAnalyticsRow(row.DisplayCharacter, FormatPercent(row.Accuracy), FormatLatency(row.MedianLatencyMs), row.ExposureCount.ToString(CultureInfo.InvariantCulture)))
        .ToArray() ?? Array.Empty<SessionDetailAnalyticsRow>();

    public IReadOnlyList<SessionDetailAnalyticsRow> SlowestBigramRows => Snapshot?.SlowestBigrams
        .Select(row => new SessionDetailAnalyticsRow(row.DisplayBigram, FormatPercent(row.Accuracy), FormatLatency(row.MedianLatencyMs), row.ExposureCount.ToString(CultureInfo.InvariantCulture)))
        .ToArray() ?? Array.Empty<SessionDetailAnalyticsRow>();

    public async Task LoadAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            _settings = await _settingsRepository.GetSettingsAsync(cancellationToken);
            Snapshot = await _sessionDetailQueryService.GetSessionDetailAsync(sessionId, cancellationToken);
            if (Snapshot is null)
            {
                ErrorMessage = "Session detail could not be found.";
            }
        }
        catch
        {
            ErrorMessage = "Session detail could not be loaded.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OnSnapshotChanged()
    {
        OnPropertyChanged(nameof(Snapshot));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(NetWpmText));
        OnPropertyChanged(nameof(AccuracyText));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(MistakesText));
        OnPropertyChanged(nameof(QualityScoreText));
        OnPropertyChanged(nameof(QualityGradeText));
        OnPropertyChanged(nameof(QualityExplanationText));
        OnPropertyChanged(nameof(TimelineRows));
        OnPropertyChanged(nameof(TimelineNetWpmPoints));
        OnPropertyChanged(nameof(TimelineAccuracyPoints));
        OnPropertyChanged(nameof(MistakeRows));
        OnPropertyChanged(nameof(SlowestKeyRows));
        OnPropertyChanged(nameof(SlowestBigramRows));
    }

    private SessionQualityResult QualityResult
    {
        get
        {
            if (Snapshot is null)
            {
                return _qualityCalculator.Calculate(new SessionQualityInputs(0, 0, _settings.GoalTargetNetWpm, null, 0, 0));
            }

            var session = Snapshot.Session;
            var completionRatio = session.TargetLength <= 0
                ? 1
                : Math.Min(1, session.TotalKeypresses / (double)session.TargetLength);
            var controlRatio = session.TargetLength <= 0
                ? 1
                : Math.Clamp(1 - (session.UncorrectedErrors / (double)session.TargetLength), 0, 1);

            return _qualityCalculator.Calculate(new SessionQualityInputs(
                session.Accuracy,
                session.NetWpm,
                _settings.GoalTargetNetWpm,
                session.Consistency ?? CalculateTimelineConsistency(TimelineRows),
                completionRatio,
                controlRatio));
        }
    }

    private static double? CalculateTimelineConsistency(IReadOnlyList<SessionTimelinePoint> points)
    {
        var samples = points
            .Select(point => point.NetWpm)
            .Where(value => value > 0)
            .ToArray();
        if (samples.Length < 2)
        {
            return null;
        }

        var average = samples.Average();
        if (average <= 0)
        {
            return 1;
        }

        var variance = samples.Average(value => Math.Pow(value - average, 2));
        return Math.Clamp(1 - (Math.Sqrt(variance) / average), 0, 1);
    }

    private static string FormatWpm(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);

    private static string FormatPercent(double value) => value.ToString("P1", CultureInfo.InvariantCulture);

    private static string FormatLatency(double? value) => value is null ? "-" : value.Value.ToString("0", CultureInfo.InvariantCulture);

    private static string FormatDuration(TimeSpan value) => value.TotalMinutes >= 1
        ? $"{(int)value.TotalMinutes}m {value.Seconds}s"
        : $"{value.Seconds}s";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record SessionDetailAnalyticsRow(
    string Label,
    string Accuracy,
    string MedianLatencyMs,
    string Samples);
