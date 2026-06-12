using Microsoft.UI.Xaml;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using TypingTrainer.App.Services;
using TypingTrainer.Core.Keyboard;
using TypingTrainer.Core.Lessons;
using TypingTrainer.Core.Models;
using TypingTrainer.Core.Review;
using TypingTrainer.Core.Skill;
using TypingTrainer.Core.Typing;
using TypingTrainer.Data.Models;

namespace TypingTrainer.App.ViewModels;

public sealed class PracticeViewModel : INotifyPropertyChanged
{
    private readonly ISessionPersistenceQueue _sessionPersistenceQueue;
    private readonly ILessonService _lessonService;
    private readonly SessionReviewGenerator _reviewGenerator = new();
    private readonly ICharacterToKeyMapper _keyMapper = new QwertyCharacterToKeyMapper();
    private readonly VisualKeyboardLayout _visualKeyboardLayout = QwertyVisualKeyboardLayout.Create();
    private readonly List<SessionNetWpmSample> _sessionNetWpmSamples = [];
    private TypingSession _session;
    private TypingStateSnapshot _currentState;
    private LessonGenerationResult _currentLesson;
    private LessonMode _activeLessonMode = LessonMode.Fixed;
    private LessonMode _selectedLessonMode = LessonMode.Adaptive;
    private PracticeLessonSize _selectedLessonSize = PracticeLessonSize.Small;
    private bool _completionQueued;
    private bool _isGeneratingLesson;
    private bool _isPaused;
    private bool _isStopped;
    private long? _lastEscapeTimestampTicks;
    private int _sessionGeneration;
    private string _completionStatus = string.Empty;
    private string _pauseStatus = string.Empty;
    private string _typingFeedback = string.Empty;
    private SessionSummary? _lastSummary;
    private SessionReview? _lastReview;
    private IReadOnlyList<TypingInputEvent> _lastCompletedEvents = Array.Empty<TypingInputEvent>();
    private AppSettings _settings = AppSettings.Defaults;

    public PracticeViewModel(
        ISessionPersistenceQueue sessionPersistenceQueue,
        ILessonService lessonService)
    {
        _sessionPersistenceQueue = sessionPersistenceQueue;
        _lessonService = lessonService;
        _currentLesson = new FixedLessonGenerator().Generate(
            SkillProfileDefaults.Empty(),
            new LessonGenerationOptions(
                LessonMode.Fixed,
                LessonLengthKind.Characters,
                FixedLessonGenerator.FixedLessonText.Length,
                KeyboardLayoutRepository.Qwerty));
        _session = new TypingSession(_currentLesson.Text);
        _currentState = _session.GetSnapshot(Stopwatch.GetTimestamp());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TypingStateSnapshot CurrentState
    {
        get => _currentState;
        private set
        {
            if (!Equals(_currentState, value))
            {
                _currentState = value;
                OnStateChanged();
            }
        }
    }

    public string LessonTitle => $"{SelectedLessonMode} lesson";

    public LessonMode SelectedLessonMode
    {
        get => _selectedLessonMode;
        private set
        {
            if (_selectedLessonMode != value)
            {
                _selectedLessonMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LessonTitle));
            }
        }
    }

    public PracticeLessonSize SelectedLessonSize
    {
        get => _selectedLessonSize;
        private set
        {
            if (_selectedLessonSize != value)
            {
                _selectedLessonSize = value;
                OnPropertyChanged();
            }
        }
    }

    public string LessonReason => _currentLesson.Reason;

    public string FocusKeysText => _currentLesson.FocusCharacters.Count == 0
        ? "Focus keys: balanced"
        : $"Focus keys: {string.Join(' ', _currentLesson.FocusCharacters)}";

    public string FocusBigramsText => _currentLesson.FocusBigrams.Count == 0
        ? "Focus bigrams: balanced"
        : $"Focus bigrams: {string.Join(' ', _currentLesson.FocusBigrams)}";

    public string LessonContentText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_currentLesson.ContentTitle)
                && string.IsNullOrWhiteSpace(_currentLesson.ContentSource))
            {
                return string.Empty;
            }

            return $"Source: {_currentLesson.ContentSource ?? "Local"} - {_currentLesson.ContentTitle ?? "Practice text"}";
        }
    }

    public Visibility LessonContentVisibility => string.IsNullOrWhiteSpace(LessonContentText)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public string RawWpmText => CurrentState.RawWpm.ToString("0", CultureInfo.InvariantCulture);

    public string NetWpmText => FormatWpm(CalculateLiveNetWpm(CurrentState));

    public string AccuracyText => (CurrentState.Accuracy * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";

    public string ElapsedText => TimeSpan.FromMilliseconds(CurrentState.ElapsedMs).ToString(@"m\:ss", CultureInfo.InvariantCulture);

    public string ErrorsText => CurrentState.CurrentErrors.ToString(CultureInfo.InvariantCulture);

    public string ProgressText => CurrentState.TargetText.Length == 0
        ? "0%"
        : (Math.Min(CurrentState.CursorIndex, CurrentState.TargetText.Length) / (double)CurrentState.TargetText.Length)
            .ToString("P0", CultureInfo.InvariantCulture);

    public double ProgressPercent => CurrentState.TargetText.Length == 0
        ? 0
        : Math.Clamp(
            Math.Min(CurrentState.CursorIndex, CurrentState.TargetText.Length) * 100.0 / CurrentState.TargetText.Length,
            0,
            100);

    public string CharactersText => $"{Math.Min(CurrentState.CursorIndex, CurrentState.TargetText.Length)} / {CurrentState.TargetText.Length}";

    public string PaceGuidanceText
    {
        get
        {
            var target = _settings.GoalTargetNetWpm;
            var current = CalculateLiveNetWpm(CurrentState);
            if (CurrentState.TypedCharacterKeypresses == 0)
            {
                return $"Target pace: {target:0} net WPM";
            }

            if (CurrentState.Accuracy < _settings.GoalTargetAccuracyPercent / 100.0)
            {
                return "Accuracy first: slow down until errors settle.";
            }

            var gap = target - current;
            return gap <= 0
                ? $"On pace: {current:0.0} net WPM vs {target:0}"
                : $"Behind pace by {gap:0.0} WPM";
        }
    }

    public bool IsComplete => CurrentState.IsComplete;

    public bool IsGeneratingLesson
    {
        get => _isGeneratingLesson;
        private set
        {
            if (_isGeneratingLesson != value)
            {
                _isGeneratingLesson = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsInputEnabled));
            }
        }
    }

    public bool IsInputEnabled => !IsGeneratingLesson && !_completionQueued;

    public Visibility ReviewPopupVisibility => _completionQueued
        ? Visibility.Visible
        : Visibility.Collapsed;

    public double PracticeContentOpacity => _completionQueued ? 0.38 : 1.0;

    public string CompletionStatus
    {
        get => _completionStatus;
        private set
        {
            if (_completionStatus != value)
            {
                _completionStatus = value;
                OnPropertyChanged();
            }
        }
    }

    public string TypingFeedback
    {
        get => _typingFeedback;
        private set
        {
            if (_typingFeedback != value)
            {
                _typingFeedback = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TypingFeedbackVisibility));
            }
        }
    }

    public Visibility TypingFeedbackVisibility => string.IsNullOrWhiteSpace(TypingFeedback)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            if (_isPaused != value)
            {
                _isPaused = value;
                OnPropertyChanged();
            }
        }
    }

    public string PauseStatus
    {
        get => _pauseStatus;
        private set
        {
            if (_pauseStatus != value)
            {
                _pauseStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PauseTitle));
                OnPropertyChanged(nameof(PauseOverlayVisibility));
            }
        }
    }

    public string PauseTitle => _isStopped ? "Stopped" : "Paused";

    public Visibility PauseOverlayVisibility => string.IsNullOrWhiteSpace(PauseStatus)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public VisualKeyboardLayout VisualKeyboardLayout => _visualKeyboardLayout;

    public char? CurrentExpectedCharacter => CurrentState.CurrentExpectedCharacter;

    public CharacterKeyMapping? CurrentKeyMapping => CurrentExpectedCharacter is char character
        ? _keyMapper.Map(character)
        : null;

    public string? HighlightedKeyId => CurrentKeyMapping?.KeyId;

    public string? HighlightedShiftKeyId => CurrentKeyMapping?.ShiftKeyId;

    public bool ShowVisualKeyboard => _settings.ShowVisualKeyboard;

    public bool ShowFingerColors => _settings.ShowFingerColors;

    public bool ShowFingerLabels => _settings.ShowFingerLabels;

    public double PracticeTextScale => _settings.PracticeTextScalePercent / 100.0;

    public double VisualKeyboardScale => _settings.VisualKeyboardScalePercent / 100.0;

    public string PracticeFontFamily => _settings.PracticeFontFamily;

    public string PracticeTextContrast => _settings.PracticeTextContrast;

    public string PracticeCursorStyle => _settings.PracticeCursorStyle;

    public double PracticeLineWidthMax => _settings.PracticeLineWidth switch
    {
        "Narrow" => 760,
        "Wide" => 1320,
        _ => 1040
    };

    public Visibility VisualKeyboardVisibility => ShowVisualKeyboard
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string CompletionRawWpmText => FormatWpm(_lastSummary?.RawWpm ?? 0);

    public string CompletionNetWpmText => FormatWpm(CalculateNetWpm(_lastSummary));

    public string CompletionAccuracyText => FormatPercent(_lastSummary?.Accuracy ?? 0);

    public string CompletionErrorsText => (_lastSummary?.CurrentErrors ?? 0).ToString(CultureInfo.InvariantCulture);

    public IReadOnlyList<ChartPointViewModel> SessionNetWpmPoints => _sessionNetWpmSamples
        .Select(sample => new ChartPointViewModel(
            FormatElapsedLabel(sample.ElapsedMs),
            sample.NetWpm))
        .ToArray();

    public string SessionNetWpmHighText => FormatNetWpmExtreme("High", samples => samples.Max());

    public string SessionNetWpmLowText => FormatNetWpmExtreme("Low", samples => samples.Min());

    public IReadOnlyList<PracticeReviewRow> ReviewRows => BuildReviewRows(_lastReview);

    public IReadOnlyList<string> ReviewNotes => _lastReview?.Notes ?? Array.Empty<string>();

    public Visibility ReviewVisibility => _lastReview is not null
        ? Visibility.Visible
        : Visibility.Collapsed;

    public bool CanPracticeMistakes => _lastReview?.HasPracticeTargets == true;

    public Visibility PracticeMistakesVisibility => CanPracticeMistakes
        ? Visibility.Visible
        : Visibility.Collapsed;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _settings = await _lessonService.GetSettingsAsync(cancellationToken);
        SelectedLessonMode = await _lessonService.GetDefaultLessonModeAsync(cancellationToken);
        await GenerateNextLessonAsync(cancellationToken);
    }

    public async Task ChangeLessonModeAsync(LessonMode mode, CancellationToken cancellationToken = default)
    {
        SelectedLessonMode = mode;
        await GenerateNextLessonAsync(cancellationToken);
    }

    public async Task ChangeLessonSizeAsync(PracticeLessonSize size, CancellationToken cancellationToken = default)
    {
        SelectedLessonSize = size;
        await GenerateNextLessonAsync(cancellationToken);
    }

    public async Task GenerateNextLessonAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsGeneratingLesson = true;
            CompletionStatus = "Generating lesson...";
            _settings = await _lessonService.GetSettingsAsync(cancellationToken);
            var targetCharacters = PracticeLessonSizeTargets.GetTargetCharacters(
                SelectedLessonSize,
                _settings.LessonLengthCharacters);
            var lesson = await _lessonService.GenerateNextLessonAsync(
                SelectedLessonMode,
                targetCharacters,
                cancellationToken);
            StartSession(lesson, SelectedLessonMode);
        }
        finally
        {
            IsGeneratingLesson = false;
        }
    }

    public void StartNewLesson()
    {
        _settings = _lessonService.GetSettingsAsync().GetAwaiter().GetResult();
        StartSession(_currentLesson, _activeLessonMode);
    }

    public async Task PracticeMistakesAsync(CancellationToken cancellationToken = default)
    {
        if (_lastReview is null || !CanPracticeMistakes)
        {
            return;
        }

        try
        {
            IsGeneratingLesson = true;
            CompletionStatus = "Generating review lesson...";
            _settings = await _lessonService.GetSettingsAsync(cancellationToken);
            var targetCharacters = PracticeLessonSizeTargets.GetTargetCharacters(
                PracticeLessonSize.Small,
                _settings.LessonLengthCharacters);
            var lesson = await _lessonService.GenerateMistakeReplayLessonAsync(
                _lastReview,
                _lastCompletedEvents,
                targetCharacters,
                cancellationToken);
            SelectedLessonMode = LessonMode.Review;
            StartSession(lesson, LessonMode.Review);
        }
        finally
        {
            IsGeneratingLesson = false;
        }
    }

    private void StartSession(LessonGenerationResult lesson, LessonMode mode)
    {
        _sessionGeneration++;
        _currentLesson = lesson;
        _activeLessonMode = mode;
        _session = new TypingSession(string.IsNullOrWhiteSpace(lesson.Text)
            ? FixedLessonGenerator.FixedLessonText
            : lesson.Text,
            CreateTypingSessionOptions());
        _completionQueued = false;
        IsPaused = false;
        _isStopped = false;
        _lastEscapeTimestampTicks = null;
        _lastSummary = null;
        _lastReview = null;
        _lastCompletedEvents = Array.Empty<TypingInputEvent>();
        _sessionNetWpmSamples.Clear();
        CompletionStatus = string.Empty;
        PauseStatus = string.Empty;
        TypingFeedback = string.Empty;
        OnVisualKeyboardSettingsChanged();
        CurrentState = _session.GetSnapshot(Stopwatch.GetTimestamp());
        OnLessonChanged();
        OnCompletionChanged();
        OnReviewChanged();
        OnSessionNetWpmChanged();
    }

    public void HandleCharacter(char character)
    {
        if (IsGeneratingLesson)
        {
            return;
        }

        if (_isStopped || _completionQueued || CurrentState.IsComplete)
        {
            StartSession(_currentLesson, _activeLessonMode);
        }

        if (IsPaused)
        {
            return;
        }

        _lastEscapeTimestampTicks = null;
        var result = _session.ProcessCharacter(character, Stopwatch.GetTimestamp());
        CurrentState = result.State;
        RecordSessionNetWpmSample(result.State);
        TypingFeedback = result.WasRejected
            ? result.FeedbackMessage ?? "Wrong key. Try again."
            : string.Empty;

        if (result.State.IsComplete && !_completionQueued)
        {
            _completionQueued = true;
            var completedSession = _session;
            var completedGeneration = _sessionGeneration;
            var completedMode = _activeLessonMode;
            OnCompletionChanged();
            _ = CompleteAndQueueSessionAsync(completedSession, completedGeneration, completedMode);
        }
    }

    public void HandleBackspace()
    {
        if (_completionQueued || IsGeneratingLesson || IsPaused || _isStopped)
        {
            return;
        }

        _lastEscapeTimestampTicks = null;
        var result = _session.ProcessBackspace(Stopwatch.GetTimestamp());
        CurrentState = result.State;
        if (result.WasAccepted)
        {
            RecordSessionNetWpmSample(result.State);
        }

        TypingFeedback = result.WasAccepted ? string.Empty : result.FeedbackMessage ?? string.Empty;
    }

    public void HandleEscape(long timestampTicks)
    {
        if (IsGeneratingLesson)
        {
            return;
        }

        if (_lastEscapeTimestampTicks is long lastEscapeTicks
            && GetElapsedMilliseconds(lastEscapeTicks, timestampTicks) <= 1_000)
        {
            StopCurrentSession();
            return;
        }

        _lastEscapeTimestampTicks = timestampTicks;
        if (IsPaused)
        {
            IsPaused = false;
            PauseStatus = string.Empty;
            TypingFeedback = string.Empty;
        }
        else
        {
            IsPaused = true;
            PauseStatus = "Paused. Press Esc to resume, or Esc twice to end.";
            TypingFeedback = string.Empty;
        }
    }

    public SessionSummary CompleteSession()
    {
        var timestampTicks = Stopwatch.GetTimestamp();
        var summary = _session.Complete(timestampTicks);
        CurrentState = _session.GetSnapshot(timestampTicks);
        return summary;
    }

    private async Task CompleteAndQueueSessionAsync(
        TypingSession completedSession,
        int completedGeneration,
        LessonMode completedMode)
    {
        try
        {
            SetCompletionStatusIfCurrent(completedGeneration, "Session complete. Saving locally...");
            var timestampTicks = Stopwatch.GetTimestamp();
            var summary = completedSession.Complete(timestampTicks);
            var events = completedSession.GetEvents();
            if (IsCurrentCompletion(completedGeneration))
            {
                CurrentState = completedSession.GetSnapshot(timestampTicks);
                _lastSummary = summary;
                _lastCompletedEvents = events;
                _lastReview = _reviewGenerator.Generate(summary, events);
                OnCompletionChanged();
                OnReviewChanged();
            }

            if (_settings.AutoSaveCompletedSessions)
            {
                await _sessionPersistenceQueue.EnqueueCompletedSessionAsync(
                    summary,
                    events,
                    completedMode.ToString());

                SetCompletionStatusIfCurrent(completedGeneration, "Session complete. Queued for local save.");
            }
            else
            {
                SetCompletionStatusIfCurrent(completedGeneration, "Session complete. Auto-save is off.");
            }
        }
        catch
        {
            SetCompletionStatusIfCurrent(completedGeneration, "Session complete. Local save could not be queued.");
        }
    }

    private void OnStateChanged()
    {
        OnPropertyChanged(nameof(CurrentState));
        OnPropertyChanged(nameof(RawWpmText));
        OnPropertyChanged(nameof(NetWpmText));
        OnPropertyChanged(nameof(AccuracyText));
        OnPropertyChanged(nameof(ElapsedText));
        OnPropertyChanged(nameof(ErrorsText));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(CharactersText));
        OnPropertyChanged(nameof(PaceGuidanceText));
        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(IsInputEnabled));
        OnPropertyChanged(nameof(CompletionStatus));
        OnPropertyChanged(nameof(TypingFeedback));
        OnPropertyChanged(nameof(TypingFeedbackVisibility));
        OnKeyboardHighlightChanged();
    }

    private void OnLessonChanged()
    {
        OnPropertyChanged(nameof(LessonTitle));
        OnPropertyChanged(nameof(LessonReason));
        OnPropertyChanged(nameof(FocusKeysText));
        OnPropertyChanged(nameof(FocusBigramsText));
        OnPropertyChanged(nameof(LessonContentText));
        OnPropertyChanged(nameof(LessonContentVisibility));
    }

    private void OnCompletionChanged()
    {
        OnPropertyChanged(nameof(ReviewPopupVisibility));
        OnPropertyChanged(nameof(PracticeContentOpacity));
        OnPropertyChanged(nameof(IsInputEnabled));
        OnPropertyChanged(nameof(CompletionRawWpmText));
        OnPropertyChanged(nameof(CompletionNetWpmText));
        OnPropertyChanged(nameof(CompletionAccuracyText));
        OnPropertyChanged(nameof(CompletionErrorsText));
    }

    private void OnReviewChanged()
    {
        OnPropertyChanged(nameof(ReviewRows));
        OnPropertyChanged(nameof(ReviewNotes));
        OnPropertyChanged(nameof(ReviewVisibility));
        OnPropertyChanged(nameof(CanPracticeMistakes));
        OnPropertyChanged(nameof(PracticeMistakesVisibility));
    }

    private void OnSessionNetWpmChanged()
    {
        OnPropertyChanged(nameof(SessionNetWpmPoints));
        OnPropertyChanged(nameof(SessionNetWpmHighText));
        OnPropertyChanged(nameof(SessionNetWpmLowText));
    }

    private void OnKeyboardHighlightChanged()
    {
        OnPropertyChanged(nameof(CurrentExpectedCharacter));
        OnPropertyChanged(nameof(CurrentKeyMapping));
        OnPropertyChanged(nameof(HighlightedKeyId));
        OnPropertyChanged(nameof(HighlightedShiftKeyId));
    }

    private void OnVisualKeyboardSettingsChanged()
    {
        OnPropertyChanged(nameof(VisualKeyboardLayout));
        OnPropertyChanged(nameof(ShowVisualKeyboard));
        OnPropertyChanged(nameof(ShowFingerColors));
        OnPropertyChanged(nameof(ShowFingerLabels));
        OnPropertyChanged(nameof(PracticeTextScale));
        OnPropertyChanged(nameof(VisualKeyboardScale));
        OnPropertyChanged(nameof(PracticeFontFamily));
        OnPropertyChanged(nameof(PracticeTextContrast));
        OnPropertyChanged(nameof(PracticeCursorStyle));
        OnPropertyChanged(nameof(PracticeLineWidthMax));
        OnPropertyChanged(nameof(VisualKeyboardVisibility));
        OnKeyboardHighlightChanged();
    }

    private static string FormatWpm(double value)
    {
        return value.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static string FormatPercent(double value)
    {
        return value.ToString("P1", CultureInfo.InvariantCulture);
    }

    private static double CalculateNetWpm(SessionSummary? summary)
    {
        if (summary is null || summary.DurationMs <= 0)
        {
            return 0;
        }

        var elapsedMinutes = summary.DurationMs / 60_000.0;
        var netWpm = ((summary.CorrectCharacterKeypresses - summary.CurrentErrors) / 5.0) / elapsedMinutes;
        return Math.Max(0, netWpm);
    }

    private static double CalculateLiveNetWpm(TypingStateSnapshot snapshot)
    {
        if (snapshot.ElapsedMs <= 0 || snapshot.TypedCharacterKeypresses <= 0)
        {
            return 0;
        }

        var elapsedMinutes = snapshot.ElapsedMs / 60_000.0;
        var netWpm = ((snapshot.CorrectCharacterKeypresses - snapshot.CurrentErrors) / 5.0) / elapsedMinutes;
        return Math.Max(0, netWpm);
    }

    private void RecordSessionNetWpmSample(TypingStateSnapshot snapshot)
    {
        if (snapshot.TypedCharacterKeypresses <= 0)
        {
            return;
        }

        _sessionNetWpmSamples.Add(new SessionNetWpmSample(
            Math.Max(0, snapshot.ElapsedMs),
            CalculateLiveNetWpm(snapshot)));
        OnSessionNetWpmChanged();
    }

    private string FormatNetWpmExtreme(string label, Func<IReadOnlyList<double>, double> selector)
    {
        var samples = _sessionNetWpmSamples
            .Select(sample => sample.NetWpm)
            .Where(value => value > 0)
            .ToArray();
        return samples.Length == 0
            ? $"{label}: --"
            : $"{label}: {selector(samples):0.0} WPM";
    }

    private static string FormatElapsedLabel(double elapsedMs)
    {
        return TimeSpan.FromMilliseconds(elapsedMs).ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<PracticeReviewRow> BuildReviewRows(SessionReview? review)
    {
        if (review is null)
        {
            return Array.Empty<PracticeReviewRow>();
        }

        var rows = new List<PracticeReviewRow>();
        rows.AddRange(review.MostMissedKeys.Select(row => ToReviewRow("Missed key", row)));
        rows.AddRange(review.WeakestBigrams.Select(row => ToReviewRow("Weak bigram", row)));

        foreach (var row in review.SlowestKeys)
        {
            if (rows.Any(existing => existing.Label == row.DisplayCharacter))
            {
                continue;
            }

            rows.Add(ToReviewRow("Slow key", row));
        }

        return rows
            .OrderByDescending(row => row.WeaknessPercent)
            .ThenBy(row => row.Kind, StringComparer.Ordinal)
            .Take(8)
            .ToArray();
    }

    private static PracticeReviewRow ToReviewRow(string kind, SessionReviewKeyRow row)
    {
        return new PracticeReviewRow(
            kind,
            row.DisplayCharacter,
            FormatPercent(row.Accuracy),
            FormatLatency(row.MedianLatencyMs),
            row.Samples.ToString(CultureInfo.InvariantCulture),
            row.WeaknessScore * 100);
    }

    private static PracticeReviewRow ToReviewRow(string kind, SessionReviewBigramRow row)
    {
        return new PracticeReviewRow(
            kind,
            row.DisplayBigram,
            FormatPercent(row.Accuracy),
            FormatLatency(row.MedianLatencyMs),
            row.Samples.ToString(CultureInfo.InvariantCulture),
            row.WeaknessScore * 100);
    }

    private static string FormatLatency(double? value)
    {
        return value is null ? "-" : value.Value.ToString("0", CultureInfo.InvariantCulture);
    }

    private TypingSessionOptions CreateTypingSessionOptions()
    {
        return new TypingSessionOptions(
            _settings.RequireCorrectKeyToAdvance
                ? ErrorAdvanceMode.RequireCorrectKey
                : ErrorAdvanceMode.AdvanceOnError,
            _settings.BackspaceAllowed);
    }

    private void StopCurrentSession()
    {
        _completionQueued = false;
        _lastSummary = null;
        _lastReview = null;
        _lastCompletedEvents = Array.Empty<TypingInputEvent>();
        _lastEscapeTimestampTicks = null;
        IsPaused = false;
        _isStopped = true;
        PauseStatus = "Session ended. Press any key to restart.";
        CompletionStatus = string.Empty;
        TypingFeedback = string.Empty;
        OnCompletionChanged();
        OnReviewChanged();
        OnPropertyChanged(nameof(IsInputEnabled));
    }

    private bool IsCurrentCompletion(int completedGeneration)
    {
        return completedGeneration == _sessionGeneration && _completionQueued;
    }

    private void SetCompletionStatusIfCurrent(int completedGeneration, string status)
    {
        if (IsCurrentCompletion(completedGeneration))
        {
            CompletionStatus = status;
        }
    }

    private static double GetElapsedMilliseconds(long startTicks, long endTicks)
    {
        return (endTicks - startTicks) * 1_000.0 / Stopwatch.Frequency;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record PracticeReviewRow(
    string Kind,
    string Label,
    string Accuracy,
    string MedianLatencyMs,
    string Samples,
    double WeaknessPercent);

public sealed record SessionNetWpmSample(
    double ElapsedMs,
    double NetWpm);
