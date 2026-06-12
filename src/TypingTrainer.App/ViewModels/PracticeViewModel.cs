using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using TypingTrainer.App.Services;
using TypingTrainer.Core.Keyboard;
using TypingTrainer.Core.Lessons;
using TypingTrainer.Core.Models;
using TypingTrainer.Core.Skill;
using TypingTrainer.Core.Typing;
using TypingTrainer.Data.Models;

namespace TypingTrainer.App.ViewModels;

public sealed class PracticeViewModel : INotifyPropertyChanged
{
    private readonly ISessionPersistenceQueue _sessionPersistenceQueue;
    private readonly ILessonService _lessonService;
    private readonly ICharacterToKeyMapper _keyMapper = new QwertyCharacterToKeyMapper();
    private readonly VisualKeyboardLayout _visualKeyboardLayout = QwertyVisualKeyboardLayout.Create();
    private TypingSession _session;
    private TypingStateSnapshot _currentState;
    private LessonGenerationResult _currentLesson;
    private LessonMode _activeLessonMode = LessonMode.Fixed;
    private LessonMode _selectedLessonMode = LessonMode.Adaptive;
    private bool _completionQueued;
    private bool _isGeneratingLesson;
    private string _completionStatus = string.Empty;
    private string _typingFeedback = string.Empty;
    private SessionSummary? _lastSummary;
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

    public string AccuracyText => (CurrentState.Accuracy * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";

    public string ElapsedText => TimeSpan.FromMilliseconds(CurrentState.ElapsedMs).ToString(@"m\:ss", CultureInfo.InvariantCulture);

    public string ErrorsText => CurrentState.CurrentErrors.ToString(CultureInfo.InvariantCulture);

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

    public Visibility CompletionPanelVisibility => _completionQueued
        ? Visibility.Visible
        : Visibility.Collapsed;

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

    public Visibility VisualKeyboardVisibility => ShowVisualKeyboard
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string CompletionRawWpmText => FormatWpm(_lastSummary?.RawWpm ?? 0);

    public string CompletionNetWpmText => FormatWpm(CalculateNetWpm(_lastSummary));

    public string CompletionAccuracyText => FormatPercent(_lastSummary?.Accuracy ?? 0);

    public string CompletionErrorsText => (_lastSummary?.CurrentErrors ?? 0).ToString(CultureInfo.InvariantCulture);

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

    public async Task GenerateNextLessonAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsGeneratingLesson = true;
            CompletionStatus = "Generating lesson...";
            _settings = await _lessonService.GetSettingsAsync(cancellationToken);
            var lesson = await _lessonService.GenerateNextLessonAsync(SelectedLessonMode, cancellationToken);
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

    private void StartSession(LessonGenerationResult lesson, LessonMode mode)
    {
        _currentLesson = lesson;
        _activeLessonMode = mode;
        _session = new TypingSession(string.IsNullOrWhiteSpace(lesson.Text)
            ? FixedLessonGenerator.FixedLessonText
            : lesson.Text,
            CreateTypingSessionOptions());
        _completionQueued = false;
        _lastSummary = null;
        CompletionStatus = string.Empty;
        TypingFeedback = string.Empty;
        OnVisualKeyboardSettingsChanged();
        CurrentState = _session.GetSnapshot(Stopwatch.GetTimestamp());
        OnLessonChanged();
        OnCompletionChanged();
    }

    public void HandleCharacter(char character)
    {
        if (_completionQueued || IsGeneratingLesson)
        {
            return;
        }

        var result = _session.ProcessCharacter(character, Stopwatch.GetTimestamp());
        CurrentState = result.State;
        TypingFeedback = result.WasRejected
            ? result.FeedbackMessage ?? "Wrong key. Try again."
            : string.Empty;

        if (result.State.IsComplete && !_completionQueued)
        {
            _completionQueued = true;
            OnCompletionChanged();
            _ = CompleteAndQueueSessionAsync();
        }
    }

    public void HandleBackspace()
    {
        if (_completionQueued || IsGeneratingLesson)
        {
            return;
        }

        var result = _session.ProcessBackspace(Stopwatch.GetTimestamp());
        CurrentState = result.State;
        TypingFeedback = result.WasAccepted ? string.Empty : result.FeedbackMessage ?? string.Empty;
    }

    public SessionSummary CompleteSession()
    {
        var timestampTicks = Stopwatch.GetTimestamp();
        var summary = _session.Complete(timestampTicks);
        CurrentState = _session.GetSnapshot(timestampTicks);
        return summary;
    }

    private async Task CompleteAndQueueSessionAsync()
    {
        try
        {
            CompletionStatus = "Session complete. Saving locally...";
            var summary = CompleteSession();
            var events = _session.GetEvents();
            _lastSummary = summary;
            OnCompletionChanged();

            if (_settings.AutoSaveCompletedSessions)
            {
                await _sessionPersistenceQueue.EnqueueCompletedSessionAsync(
                    summary,
                    events,
                    _activeLessonMode.ToString());

                CompletionStatus = "Session complete. Queued for local save.";
            }
            else
            {
                CompletionStatus = "Session complete. Auto-save is off.";
            }
        }
        catch
        {
            CompletionStatus = "Session complete. Local save could not be queued.";
        }
    }

    private void OnStateChanged()
    {
        OnPropertyChanged(nameof(CurrentState));
        OnPropertyChanged(nameof(RawWpmText));
        OnPropertyChanged(nameof(AccuracyText));
        OnPropertyChanged(nameof(ElapsedText));
        OnPropertyChanged(nameof(ErrorsText));
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
        OnPropertyChanged(nameof(CompletionPanelVisibility));
        OnPropertyChanged(nameof(IsInputEnabled));
        OnPropertyChanged(nameof(CompletionRawWpmText));
        OnPropertyChanged(nameof(CompletionNetWpmText));
        OnPropertyChanged(nameof(CompletionAccuracyText));
        OnPropertyChanged(nameof(CompletionErrorsText));
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

    private TypingSessionOptions CreateTypingSessionOptions()
    {
        return new TypingSessionOptions(
            _settings.RequireCorrectKeyToAdvance
                ? ErrorAdvanceMode.RequireCorrectKey
                : ErrorAdvanceMode.AdvanceOnError,
            _settings.BackspaceAllowed);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
