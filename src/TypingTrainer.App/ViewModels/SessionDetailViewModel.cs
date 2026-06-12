using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using TypingTrainer.Data.Models;
using TypingTrainer.Data.Services;

namespace TypingTrainer.App.ViewModels;

public sealed class SessionDetailViewModel : INotifyPropertyChanged
{
    private readonly ISessionDetailQueryService _sessionDetailQueryService;
    private SessionDetailSnapshot? _snapshot;
    private bool _isLoading;
    private string? _errorMessage;

    public SessionDetailViewModel(ISessionDetailQueryService sessionDetailQueryService)
    {
        _sessionDetailQueryService = sessionDetailQueryService;
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

    public IReadOnlyList<SessionTimelinePoint> TimelineRows => Snapshot?.Timeline ?? Array.Empty<SessionTimelinePoint>();

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
        OnPropertyChanged(nameof(TimelineRows));
        OnPropertyChanged(nameof(MistakeRows));
        OnPropertyChanged(nameof(SlowestKeyRows));
        OnPropertyChanged(nameof(SlowestBigramRows));
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

