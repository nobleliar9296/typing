using Microsoft.UI.Xaml;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using TypingTrainer.App.Navigation;
using TypingTrainer.App.Services;
using TypingTrainer.Core.Coaching;
using TypingTrainer.Core.Keyboard;
using TypingTrainer.Core.Learning;
using TypingTrainer.Core.Lessons;
using TypingTrainer.Core.Models;
using TypingTrainer.Core.Review;
using TypingTrainer.Core.Skill;
using TypingTrainer.Core.Typing;
using TypingTrainer.Data.Models;

namespace TypingTrainer.App.ViewModels;

public sealed class PracticeViewModel : INotifyPropertyChanged
{
    private const double SessionNetWpmWarmupMs = 3_000;
    private const int SessionNetWpmWarmupKeypresses = 12;

    private readonly ISessionPersistenceQueue _sessionPersistenceQueue;
    private readonly ILessonService _lessonService;
    private readonly SessionReviewGenerator _reviewGenerator = new();
    private readonly ICharacterToKeyMapper _keyMapper = new QwertyCharacterToKeyMapper();
    private readonly VisualKeyboardLayout _visualKeyboardLayout = QwertyVisualKeyboardLayout.Create();
    private readonly List<SessionNetWpmSample> _sessionNetWpmSamples = [];
    private readonly DispatcherTimer _countdownTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private TypingSession _session;
    private TypingStateSnapshot _currentState;
    private LessonGenerationResult _currentLesson;
    private LessonMode _activeLessonMode = LessonMode.Fixed;
    private LessonMode _selectedLessonMode = LessonMode.Adaptive;
    private PracticeLessonSize _selectedLessonSize = PracticeLessonSize.Small;
    private bool _completionQueued;
    private bool _isGeneratingLesson;
    private bool _isPaused;
    private bool _isCountdownActive;
    private bool _isReviewPopupDismissed;
    private bool _isStopped;
    private bool _hasStartedTyping;
    private long? _lastEscapeTimestampTicks;
    private int _countdownRemaining;
    private int _sessionGeneration;
    private string _completionStatus = string.Empty;
    private string _pauseStatus = string.Empty;
    private string _typingFeedback = string.Empty;
    private string _clipboardStatus = string.Empty;
    private string _reviewRetryStatus = string.Empty;
    private SessionSummary? _lastSummary;
    private SessionReview? _lastReview;
    private IReadOnlyList<TypingInputEvent> _lastCompletedEvents = Array.Empty<TypingInputEvent>();
    private IReadOnlyList<LearningTarget> _completedLearningTargets = Array.Empty<LearningTarget>();
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
        _countdownTimer.Tick += CountdownTimer_Tick;
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

    public bool IsInputEnabled => !IsGeneratingLesson && !_completionQueued && !_isCountdownActive;

    public Visibility PracticeStatsVisibility => _settings.ZenModeEnabled
        ? Visibility.Collapsed
        : Visibility.Visible;

    public bool ZenModeEnabled
    {
        get => _settings.ZenModeEnabled;
        set
        {
            if (_settings.ZenModeEnabled == value)
            {
                return;
            }

            _settings = _settings with { ZenModeEnabled = value };
            OnVisualKeyboardSettingsChanged();
            _ = SavePracticeSettingsAsync(_settings);
        }
    }

    public bool KeySoundEnabled => _settings.KeySoundEnabled;

    public bool MistakeSoundEnabled => _settings.MistakeSoundEnabled;

    public string ReadyStatusText
    {
        get
        {
            if (_isCountdownActive)
            {
                return $"Starting in {_countdownRemaining}...";
            }

            if (!_hasStartedTyping && !CurrentState.IsComplete && !_completionQueued)
            {
                return _settings.CountdownSeconds > 0
                    ? "Ready. Press any key to start the countdown."
                    : "Ready. Start typing.";
            }

            return string.Empty;
        }
    }

    public Visibility ReadyStatusVisibility => string.IsNullOrWhiteSpace(ReadyStatusText)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility ReviewPopupVisibility => _completionQueued && !_isReviewPopupDismissed
        ? Visibility.Visible
        : Visibility.Collapsed;

    public double PracticeContentOpacity => ReviewPopupVisibility == Visibility.Visible ? 0.52 : 1.0;

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

    public string ClipboardStatus
    {
        get => _clipboardStatus;
        private set
        {
            if (_clipboardStatus != value)
            {
                _clipboardStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ClipboardStatusVisibility));
            }
        }
    }

    public Visibility ClipboardStatusVisibility => string.IsNullOrWhiteSpace(ClipboardStatus)
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

    public string PracticeFontFamily => AppSettings.NormalizePracticeFontFamily(_settings.PracticeFontFamily);

    public string PracticeTextContrast => _settings.PracticeTextContrast;

    public string PracticeCursorStyle => AppSettings.NormalizeCursorStyle(_settings.PracticeCursorStyle);

    public bool ShowSpaceDots => _settings.ShowSpaceDots;

    public double PracticeLineWidthMax => _settings.PracticeLineWidth switch
    {
        "Narrow" => 760,
        "Wide" => 1320,
        _ => 1040
    };

    public Visibility VisualKeyboardVisibility => ShowVisualKeyboard && !_settings.ZenModeEnabled
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string CompletionRawWpmText => FormatWpm(_lastSummary?.RawWpm ?? 0);

    public string CompletionNetWpmText => FormatWpm(CalculateNetWpm(_lastSummary));

    public string CompletionAccuracyText => FormatPercent(_lastSummary?.Accuracy ?? 0);

    public string CompletionErrorsText => (_lastSummary?.CurrentErrors ?? 0).ToString(CultureInfo.InvariantCulture);

    public IReadOnlyList<ChartPointViewModel> SessionNetWpmPoints => GetSessionNetWpmChartSamples()
        .Select(sample => new ChartPointViewModel(
            FormatElapsedLabel(sample.ElapsedMs),
            sample.NetWpm))
        .ToArray();

    public string SessionNetWpmHighText => FormatNetWpmExtreme("High", samples => samples.Max());

    public string SessionNetWpmLowText => FormatNetWpmExtreme("Low", samples => samples.Min());

    public IReadOnlyList<PracticeReviewRow> ReviewRows => BuildReviewRows(_lastReview);

    public IReadOnlyList<CostlyMistakeRow> CostlyMistakeRows => BuildCostlyMistakeRows(_lastCompletedEvents);

    public IReadOnlyList<MistakeCauseRow> MistakeCauseRows => BuildMistakeCauseRows(_lastSummary, _lastCompletedEvents);

    public Visibility MistakeCauseVisibility => MistakeCauseRows.Count > 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public IReadOnlyList<LearningFocusRow> LearningFocusRows => BuildLearningFocusRows(_completedLearningTargets);

    public Visibility LearningFocusVisibility => LearningFocusRows.Count > 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string ReviewRecommendationTitle => BuildReviewRecommendation().Title;

    public string ReviewRecommendationDetail => BuildReviewRecommendation().Detail;

    public string ReviewRetryStatus
    {
        get => _reviewRetryStatus;
        private set
        {
            if (_reviewRetryStatus != value)
            {
                _reviewRetryStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReviewRetryStatusVisibility));
            }
        }
    }

    public Visibility ReviewRetryStatusVisibility => string.IsNullOrWhiteSpace(ReviewRetryStatus)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public IReadOnlyList<string> ReviewNotes => _lastReview?.Notes ?? Array.Empty<string>();

    public Visibility ReviewVisibility => _lastReview is not null
        ? Visibility.Visible
        : Visibility.Collapsed;

    public bool CanPracticeMistakes => _lastReview?.HasPracticeTargets == true;

    public Visibility PracticeMistakesVisibility => CanPracticeMistakes
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string RecommendedFollowUpText => BuildRecommendedFollowUp().ButtonText;

    public string RecommendedFollowUpDetail => BuildRecommendedFollowUp().Detail;

    public Visibility RecommendedFollowUpVisibility => _lastSummary is null
        ? Visibility.Collapsed
        : Visibility.Visible;

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

    public async Task ApplyLaunchRequestAsync(
        PracticeLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IsGeneratingLesson = true;
            CompletionStatus = "Starting practice...";
            _settings = await _lessonService.GetSettingsAsync(cancellationToken);
            SelectedLessonMode = request.Mode == LessonMode.Clipboard
                ? LessonMode.Adaptive
                : request.Mode;
            SelectedLessonSize = request.Size;
            var targetCharacters = request.TargetCharacters
                ?? PracticeLessonSizeTargets.GetTargetCharacters(
                    SelectedLessonSize,
                    _settings.LessonLengthCharacters);
            var lesson = await _lessonService.GenerateNextLessonAsync(
                SelectedLessonMode,
                targetCharacters,
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(request.Reason))
            {
                lesson = lesson with { Reason = request.Reason };
            }

            StartSession(lesson, SelectedLessonMode);
        }
        finally
        {
            IsGeneratingLesson = false;
        }
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

    public async Task StartNewLessonAsync(CancellationToken cancellationToken = default)
    {
        if (IsGeneratingLesson)
        {
            return;
        }

        try
        {
            IsGeneratingLesson = true;
            CompletionStatus = "Restarting lesson...";
            _settings = await _lessonService.GetSettingsAsync(cancellationToken);
            StartSession(_currentLesson, _activeLessonMode);
        }
        catch (OperationCanceledException)
        {
            CompletionStatus = "Restart canceled.";
        }
        catch (Exception ex)
        {
            StartupExceptionLogger.Log("PracticeViewModel.StartNewLesson", ex);
            CompletionStatus = "Lesson could not restart.";
        }
        finally
        {
            IsGeneratingLesson = false;
        }
    }

    public void DismissReviewPopup()
    {
        if (!_completionQueued || _isReviewPopupDismissed)
        {
            return;
        }

        _isReviewPopupDismissed = true;
        OnCompletionChanged();
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

    public async Task StartRecommendedFollowUpAsync(CancellationToken cancellationToken = default)
    {
        var followUp = BuildRecommendedFollowUp();
        switch (followUp.Kind)
        {
            case RecommendedFollowUpKind.SpacedReview:
                await ApplyLaunchRequestAsync(
                    new PracticeLaunchRequest(
                        LessonMode.Adaptive,
                        PracticeLessonSize.Small,
                        TargetCharacters: null,
                        Reason: followUp.Detail),
                    cancellationToken);
                break;
            case RecommendedFollowUpKind.MistakeCauseDrill:
                if (!await StartMistakeCauseDrillAsync(cancellationToken))
                {
                    await PracticeMistakesOrNextAsync(cancellationToken);
                }

                break;
            case RecommendedFollowUpKind.WpmDipRetry:
                if (followUp.ChartPointIndex is int pointIndex && StartRetryFromNetWpmPoint(pointIndex))
                {
                    return;
                }

                await PracticeMistakesOrNextAsync(cancellationToken);
                break;
            case RecommendedFollowUpKind.PracticeMistakes:
                await PracticeMistakesAsync(cancellationToken);
                break;
            case RecommendedFollowUpKind.NextParagraph:
            default:
                SelectedLessonMode = LessonMode.Paragraph;
                SelectedLessonSize = PracticeLessonSize.Small;
                await GenerateNextLessonAsync(cancellationToken);
                break;
        }
    }

    public async Task StartClipboardLessonAsync(string clipboardText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clipboardText))
        {
            ClipboardStatus = "Copy some text first, then choose Practice copied text.";
            return;
        }

        try
        {
            IsGeneratingLesson = true;
            CompletionStatus = "Preparing clipboard lesson...";
            _settings = await _lessonService.GetSettingsAsync(cancellationToken);
            var targetCharacters = PracticeLessonSizeTargets.GetTargetCharacters(
                SelectedLessonSize,
                _settings.LessonLengthCharacters);
            var lesson = await _lessonService.GenerateClipboardLessonAsync(
                clipboardText,
                targetCharacters,
                cancellationToken);
            SelectedLessonMode = LessonMode.Clipboard;
            StartSession(lesson, LessonMode.Clipboard);
            ClipboardStatus = "Copied text loaded as a one-off lesson.";
        }
        finally
        {
            IsGeneratingLesson = false;
        }
    }

    public void SetClipboardUnavailable()
    {
        ClipboardStatus = "Clipboard text unavailable. Copy plain text and try again.";
    }

    public bool StartRetryFromNetWpmPoint(int pointIndex)
    {
        var chartSamples = GetSessionNetWpmChartSamples();
        if (_lastSummary is null || chartSamples.Count == 0)
        {
            ReviewRetryStatus = "No completed session text is available for retry.";
            return false;
        }

        var boundedIndex = Math.Clamp(pointIndex, 0, chartSamples.Count - 1);
        var sample = chartSamples[boundedIndex];
        var retryText = ExtractRetrySlice(_lastSummary.TargetText, sample.CursorIndex, 220);
        if (string.IsNullOrWhiteSpace(retryText))
        {
            ReviewRetryStatus = "Could not find enough text around that point.";
            return false;
        }

        var lesson = new LessonGenerationResult(
            retryText,
            retryText.Where(character => !char.IsWhiteSpace(character)).ToHashSet(),
            Array.Empty<char>(),
            Array.Empty<string>(),
            $"Retry pace dip near {FormatElapsedLabel(sample.ElapsedMs)}",
            "WPM dip retry",
            "Last session");
        SelectedLessonMode = LessonMode.Review;
        StartSession(lesson, LessonMode.Review);
        return true;
    }

    private async Task PracticeMistakesOrNextAsync(CancellationToken cancellationToken)
    {
        if (CanPracticeMistakes)
        {
            await PracticeMistakesAsync(cancellationToken);
            return;
        }

        SelectedLessonMode = LessonMode.Paragraph;
        SelectedLessonSize = PracticeLessonSize.Small;
        await GenerateNextLessonAsync(cancellationToken);
    }

    private async Task<bool> StartMistakeCauseDrillAsync(CancellationToken cancellationToken)
    {
        var request = BuildTopMistakeCauseDrillRequest();
        if (request is null)
        {
            return false;
        }

        try
        {
            IsGeneratingLesson = true;
            CompletionStatus = "Generating micro-drill...";
            _settings = await _lessonService.GetSettingsAsync(cancellationToken);
            var targetCharacters = PracticeLessonSizeTargets.GetTargetCharacters(
                PracticeLessonSize.Small,
                _settings.LessonLengthCharacters);
            var lesson = await _lessonService.GenerateMistakeCauseDrillLessonAsync(
                request,
                targetCharacters,
                cancellationToken);
            SelectedLessonMode = LessonMode.Review;
            SelectedLessonSize = PracticeLessonSize.Small;
            StartSession(lesson, LessonMode.Review);
            return true;
        }
        finally
        {
            IsGeneratingLesson = false;
        }
    }

    public string CreateReviewSummaryText()
    {
        var lines = new List<string>
        {
            "Typing Trainer session review",
            $"Raw WPM: {CompletionRawWpmText}",
            $"Net WPM: {CompletionNetWpmText}",
            $"Accuracy: {CompletionAccuracyText}",
            $"Errors: {CompletionErrorsText}",
            $"{ReviewRecommendationTitle}: {ReviewRecommendationDetail}",
            string.Empty,
            "Most costly mistakes"
        };

        foreach (var row in CostlyMistakeRows.DefaultIfEmpty(new CostlyMistakeRow("-", "-", "-", "-", 0)))
        {
            lines.Add($"{row.Label}: {row.Impact}, {row.Accuracy}, {row.MedianLatencyMs} ms, {row.Samples} samples");
        }

        lines.Add(string.Empty);
        lines.Add("Notes");
        lines.AddRange(ReviewNotes.DefaultIfEmpty("No review notes were generated."));
        return string.Join(Environment.NewLine, lines);
    }

    private void StartSession(LessonGenerationResult lesson, LessonMode mode)
    {
        _countdownTimer.Stop();
        _sessionGeneration++;
        _currentLesson = lesson;
        _activeLessonMode = mode;
        _session = new TypingSession(string.IsNullOrWhiteSpace(lesson.Text)
            ? FixedLessonGenerator.FixedLessonText
            : lesson.Text,
            CreateTypingSessionOptions());
        _completionQueued = false;
        _isCountdownActive = false;
        _isReviewPopupDismissed = false;
        IsPaused = false;
        _isStopped = false;
        _hasStartedTyping = false;
        _countdownRemaining = 0;
        _lastEscapeTimestampTicks = null;
        _lastSummary = null;
        _lastReview = null;
        _lastCompletedEvents = Array.Empty<TypingInputEvent>();
        _completedLearningTargets = Array.Empty<LearningTarget>();
        _sessionNetWpmSamples.Clear();
        CompletionStatus = string.Empty;
        PauseStatus = string.Empty;
        TypingFeedback = string.Empty;
        ClipboardStatus = string.Empty;
        ReviewRetryStatus = string.Empty;
        OnVisualKeyboardSettingsChanged();
        CurrentState = _session.GetSnapshot(Stopwatch.GetTimestamp());
        OnLessonChanged();
        OnCompletionChanged();
        OnReviewChanged();
        OnSessionNetWpmChanged();
    }

    public PracticeInputFeedback HandleCharacter(char character)
    {
        if (IsGeneratingLesson)
        {
            return PracticeInputFeedback.None;
        }

        if (_isStopped || _completionQueued || CurrentState.IsComplete)
        {
            StartSession(_currentLesson, _activeLessonMode);
        }

        if (IsPaused)
        {
            return PracticeInputFeedback.None;
        }

        if (_isCountdownActive)
        {
            return PracticeInputFeedback.None;
        }

        if (!_hasStartedTyping && _settings.CountdownSeconds > 0)
        {
            StartCountdown();
            return PracticeInputFeedback.CountdownStarted;
        }

        _hasStartedTyping = true;
        _lastEscapeTimestampTicks = null;
        var result = _session.ProcessCharacter(character, Stopwatch.GetTimestamp());
        CurrentState = result.State;
        if (result.Event is not null)
        {
            RecordSessionNetWpmSample(result.State);
        }

        var shouldShowMistakeFeedback = result.WasRejected || (result.WasAccepted && !result.WasCorrect);
        TypingFeedback = shouldShowMistakeFeedback
            ? result.FeedbackMessage ?? "Wrong key."
            : string.Empty;

        if (result.State.IsComplete && !_completionQueued)
        {
            _completionQueued = true;
            _isReviewPopupDismissed = false;
            var completedSession = _session;
            var completedGeneration = _sessionGeneration;
            var completedMode = _activeLessonMode;
            OnCompletionChanged();
            _ = CompleteAndQueueSessionAsync(completedSession, completedGeneration, completedMode);
        }

        return shouldShowMistakeFeedback
            ? PracticeInputFeedback.Mistake
            : result.WasAccepted ? PracticeInputFeedback.Key : PracticeInputFeedback.None;
    }

    public PracticeInputFeedback HandleBackspace()
    {
        if (_completionQueued || IsGeneratingLesson || IsPaused || _isStopped)
        {
            return PracticeInputFeedback.None;
        }

        _lastEscapeTimestampTicks = null;
        var result = _session.ProcessBackspace(Stopwatch.GetTimestamp());
        CurrentState = result.State;
        if (result.WasAccepted)
        {
            RecordSessionNetWpmSample(result.State);
        }

        TypingFeedback = result.WasAccepted ? string.Empty : result.FeedbackMessage ?? string.Empty;
        if (!result.WasAccepted)
        {
            return PracticeInputFeedback.None;
        }

        return result.Event?.WasCorrection == true
            ? PracticeInputFeedback.Correction
            : PracticeInputFeedback.Key;
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
                _completedLearningTargets = Array.Empty<LearningTarget>();
                _lastReview = _reviewGenerator.Generate(summary, events);
                OnCompletionChanged();
                OnReviewChanged();
            }

            if (_settings.AutoSaveCompletedSessions)
            {
                var saveResult = await _sessionPersistenceQueue.EnqueueCompletedSessionAsync(
                    summary,
                    events,
                    completedMode.ToString());

                switch (saveResult.Status)
                {
                    case SessionPersistenceStatus.Saved:
                        SetCompletionStatusIfCurrent(completedGeneration, "Session complete. Updating learning plan...");
                        try
                        {
                            var profile = await _lessonService.GetSkillProfileAsync();
                            if (IsCurrentCompletion(completedGeneration))
                            {
                                _completedLearningTargets = profile.DueLearningTargets.Take(6).ToArray();
                                OnReviewChanged();
                            }

                            SetCompletionStatusIfCurrent(completedGeneration, SessionPersistenceStatusText.Saved);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            StartupExceptionLogger.Log("PracticeViewModel.RefreshLearningTargets", ex);
                            SetCompletionStatusIfCurrent(completedGeneration, "Session complete. Saved locally, but learning targets could not refresh.");
                        }

                        break;
                    case SessionPersistenceStatus.SavedWithLearningUpdateFailure:
                        _completedLearningTargets = Array.Empty<LearningTarget>();
                        SetCompletionStatusIfCurrent(completedGeneration, SessionPersistenceStatusText.FromResult(saveResult));
                        break;
                    case SessionPersistenceStatus.Canceled:
                        _completedLearningTargets = Array.Empty<LearningTarget>();
                        SetCompletionStatusIfCurrent(completedGeneration, SessionPersistenceStatusText.FromResult(saveResult));
                        break;
                    case SessionPersistenceStatus.Failed:
                    default:
                        _completedLearningTargets = Array.Empty<LearningTarget>();
                        SetCompletionStatusIfCurrent(completedGeneration, SessionPersistenceStatusText.FromResult(saveResult));
                        break;
                }
            }
            else
            {
                _completedLearningTargets = Array.Empty<LearningTarget>();
                SetCompletionStatusIfCurrent(completedGeneration, "Session complete. Auto-save is off.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StartupExceptionLogger.Log("PracticeViewModel.CompleteAndQueueSession", ex);
            SetCompletionStatusIfCurrent(completedGeneration, "Session complete. Local save could not be queued.");
        }
        catch (OperationCanceledException)
        {
            SetCompletionStatusIfCurrent(completedGeneration, "Session complete. Local save was canceled.");
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
        OnPropertyChanged(nameof(ReadyStatusText));
        OnPropertyChanged(nameof(ReadyStatusVisibility));
        OnPropertyChanged(nameof(CompletionStatus));
        OnPropertyChanged(nameof(TypingFeedback));
        OnPropertyChanged(nameof(TypingFeedbackVisibility));
        OnPropertyChanged(nameof(ClipboardStatus));
        OnPropertyChanged(nameof(ClipboardStatusVisibility));
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
        OnPropertyChanged(nameof(ReadyStatusText));
        OnPropertyChanged(nameof(ReadyStatusVisibility));
        OnPropertyChanged(nameof(CompletionRawWpmText));
        OnPropertyChanged(nameof(CompletionNetWpmText));
        OnPropertyChanged(nameof(CompletionAccuracyText));
        OnPropertyChanged(nameof(CompletionErrorsText));
    }

    private void OnReviewChanged()
    {
        OnPropertyChanged(nameof(ReviewRows));
        OnPropertyChanged(nameof(CostlyMistakeRows));
        OnPropertyChanged(nameof(MistakeCauseRows));
        OnPropertyChanged(nameof(MistakeCauseVisibility));
        OnPropertyChanged(nameof(LearningFocusRows));
        OnPropertyChanged(nameof(LearningFocusVisibility));
        OnPropertyChanged(nameof(ReviewRecommendationTitle));
        OnPropertyChanged(nameof(ReviewRecommendationDetail));
        OnPropertyChanged(nameof(ReviewNotes));
        OnPropertyChanged(nameof(ReviewVisibility));
        OnPropertyChanged(nameof(CanPracticeMistakes));
        OnPropertyChanged(nameof(PracticeMistakesVisibility));
        OnPropertyChanged(nameof(RecommendedFollowUpText));
        OnPropertyChanged(nameof(RecommendedFollowUpDetail));
        OnPropertyChanged(nameof(RecommendedFollowUpVisibility));
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
        OnPropertyChanged(nameof(ZenModeEnabled));
        OnPropertyChanged(nameof(PracticeStatsVisibility));
        OnPropertyChanged(nameof(KeySoundEnabled));
        OnPropertyChanged(nameof(MistakeSoundEnabled));
        OnPropertyChanged(nameof(PracticeTextScale));
        OnPropertyChanged(nameof(VisualKeyboardScale));
        OnPropertyChanged(nameof(PracticeFontFamily));
        OnPropertyChanged(nameof(PracticeTextContrast));
        OnPropertyChanged(nameof(PracticeCursorStyle));
        OnPropertyChanged(nameof(ShowSpaceDots));
        OnPropertyChanged(nameof(PracticeLineWidthMax));
        OnPropertyChanged(nameof(VisualKeyboardVisibility));
        OnKeyboardHighlightChanged();
    }

    private void StartCountdown()
    {
        _countdownRemaining = Math.Clamp(_settings.CountdownSeconds, 1, 3);
        _isCountdownActive = true;
        TypingFeedback = string.Empty;
        OnCountdownChanged();
        _countdownTimer.Start();
    }

    private void CountdownTimer_Tick(object? sender, object e)
    {
        _countdownRemaining--;
        if (_countdownRemaining <= 0)
        {
            _countdownTimer.Stop();
            _isCountdownActive = false;
            _hasStartedTyping = true;
        }

        OnCountdownChanged();
    }

    private void OnCountdownChanged()
    {
        OnPropertyChanged(nameof(IsInputEnabled));
        OnPropertyChanged(nameof(ReadyStatusText));
        OnPropertyChanged(nameof(ReadyStatusVisibility));
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
            CalculateLiveNetWpm(snapshot),
            Math.Clamp(snapshot.CursorIndex, 0, snapshot.TargetText.Length),
            snapshot.TypedCharacterKeypresses));
        OnSessionNetWpmChanged();
    }

    private string FormatNetWpmExtreme(string label, Func<IReadOnlyList<double>, double> selector)
    {
        var samples = GetSessionNetWpmChartSamples()
            .Select(sample => sample.NetWpm)
            .ToArray();
        return samples.Length == 0
            ? $"{label}: --"
            : $"{label}: {selector(samples):0.0} WPM";
    }

    private IReadOnlyList<SessionNetWpmSample> GetSessionNetWpmChartSamples()
    {
        return _sessionNetWpmSamples
            .Where(IsStableSessionNetWpmSample)
            .ToArray();
    }

    private static bool IsStableSessionNetWpmSample(SessionNetWpmSample sample)
    {
        return sample.ElapsedMs >= SessionNetWpmWarmupMs
            && sample.TypedKeypresses >= SessionNetWpmWarmupKeypresses
            && sample.NetWpm > 0;
    }

    private static string FormatElapsedLabel(double elapsedMs)
    {
        return TimeSpan.FromMilliseconds(elapsedMs).ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    private RecommendedFollowUp BuildRecommendedFollowUp()
    {
        if (_lastSummary is null)
        {
            return new RecommendedFollowUp(
                RecommendedFollowUpKind.None,
                "Do This Next",
                "Complete a session to unlock a targeted next step.",
                ChartPointIndex: null);
        }

        if (_completedLearningTargets.Count > 0)
        {
            var first = _completedLearningTargets[0];
            return new RecommendedFollowUp(
                RecommendedFollowUpKind.SpacedReview,
                "Do This Next",
                $"Spaced review: practice {FormatLearningTarget(first)} while it is due.",
                ChartPointIndex: null);
        }

        var drillRequest = BuildTopMistakeCauseDrillRequest();
        if (drillRequest is not null)
        {
            return new RecommendedFollowUp(
                RecommendedFollowUpKind.MistakeCauseDrill,
                "Do This Next",
                $"Micro-drill: {FormatMistakeCause(drillRequest.Cause).ToLowerInvariant()} for {FormatDrillTargets(drillRequest)}.",
                ChartPointIndex: null);
        }

        var dipIndex = GetLowestStableNetWpmPointIndex();
        if (dipIndex is int pointIndex)
        {
            var sample = GetSessionNetWpmChartSamples()[pointIndex];
            return new RecommendedFollowUp(
                RecommendedFollowUpKind.WpmDipRetry,
                "Do This Next",
                $"Retry the lowest WPM dip near {FormatElapsedLabel(sample.ElapsedMs)}.",
                pointIndex);
        }

        if (CanPracticeMistakes)
        {
            return new RecommendedFollowUp(
                RecommendedFollowUpKind.PracticeMistakes,
                "Do This Next",
                "Replay the mistake-heavy parts of the last session.",
                ChartPointIndex: null);
        }

        return new RecommendedFollowUp(
            RecommendedFollowUpKind.NextParagraph,
            "Do This Next",
            "Start one short paragraph to build clean flow.",
            ChartPointIndex: null);
    }

    private (string Title, string Detail) BuildReviewRecommendation()
    {
        if (_lastSummary is null)
        {
            return ("What to fix next", "Complete a session to unlock targeted review guidance.");
        }

        if (_completedLearningTargets.Count > 0)
        {
            var first = _completedLearningTargets[0];
            return ("Spaced review ready", $"Start with {FormatLearningTarget(first)}; it is due based on mastery and recent mistakes.");
        }

        if (HasFatiguePattern(GetSessionNetWpmChartSamples(), _lastCompletedEvents))
        {
            return ("Shorten the next run", "Your pace faded late while errors rose. Use a shorter paragraph or Zen Mode and stop before fatigue sets in.");
        }

        var costly = CostlyMistakeRows.FirstOrDefault();
        if (costly is not null)
        {
            return ("Practice the costliest miss", $"Start with {costly.Label}; it cost the most combined accuracy and latency in this session.");
        }

        if (_lastSummary.Accuracy < _settings.GoalTargetAccuracyPercent / 100.0)
        {
            return ("Accuracy first", "Use Require Correct Key and Weak Keys until accuracy is back above target.");
        }

        return ("Build flow", "Run one short paragraph or retry the lowest WPM dip from the graph.");
    }

    private static bool HasFatiguePattern(
        IReadOnlyList<SessionNetWpmSample> samples,
        IReadOnlyList<TypingInputEvent> events)
    {
        var positiveSamples = samples.Where(sample => sample.NetWpm > 0).ToArray();
        if (positiveSamples.Length < 6)
        {
            return false;
        }

        var split = positiveSamples.Length / 2;
        var earlyWpm = positiveSamples.Take(split).Average(sample => sample.NetWpm);
        var lateWpm = positiveSamples.Skip(split).Average(sample => sample.NetWpm);
        var characterEvents = events
            .Where(item => item.Kind == InputEventKind.Character)
            .OrderBy(item => item.TimestampTicks)
            .ToArray();
        if (characterEvents.Length < 20)
        {
            return false;
        }

        var eventSplit = characterEvents.Length / 2;
        var earlyErrorRate = characterEvents.Take(eventSplit).Count(item => !item.IsCorrect) / (double)eventSplit;
        var lateEvents = characterEvents.Skip(eventSplit).ToArray();
        var lateErrorRate = lateEvents.Count(item => !item.IsCorrect) / (double)Math.Max(1, lateEvents.Length);

        return lateWpm < earlyWpm * 0.85 && lateErrorRate > earlyErrorRate + 0.04;
    }

    private static IReadOnlyList<CostlyMistakeRow> BuildCostlyMistakeRows(IReadOnlyList<TypingInputEvent> events)
    {
        var characterEvents = events
            .Where(item => item.Kind == InputEventKind.Character && item.ExpectedChar is not null)
            .ToArray();
        if (characterEvents.Length == 0)
        {
            return Array.Empty<CostlyMistakeRow>();
        }

        var globalMedian = Median(characterEvents
            .Select(item => item.DeltaFromPreviousMs)
            .Where(value => value is >= 20 and <= 2000)
            .Select(value => value!.Value));

        return characterEvents
            .GroupBy(item => item.ExpectedChar!.Value)
            .Select(group =>
            {
                var samples = group.Count();
                var correct = group.Count(item => item.IsCorrect);
                var incorrect = samples - correct;
                var latencies = group
                    .Select(item => item.DeltaFromPreviousMs)
                    .Where(value => value is >= 20 and <= 2000)
                    .Select(value => value!.Value)
                    .ToArray();
                var median = Median(latencies);
                var latencyCost = median is double medianValue && globalMedian is double globalValue
                    ? Math.Max(0, medianValue - globalValue) / 100.0
                    : 0;
                var impact = incorrect * 10.0 + latencyCost + Math.Max(0, 1.0 - correct / (double)samples) * 10.0;

                return new CostlyMistakeRow(
                    DisplayChar(group.Key),
                    FormatPercent(correct / (double)samples),
                    FormatLatency(median),
                    samples.ToString(CultureInfo.InvariantCulture),
                    impact);
            })
            .Where(row => row.ImpactScore > 0)
            .OrderByDescending(row => row.ImpactScore)
            .ThenBy(row => row.Label, StringComparer.Ordinal)
            .Take(6)
            .ToArray();
    }

    private static IReadOnlyList<MistakeCauseRow> BuildMistakeCauseRows(
        SessionSummary? summary,
        IReadOnlyList<TypingInputEvent> events)
    {
        if (summary is null)
        {
            return Array.Empty<MistakeCauseRow>();
        }

        var classifier = new MistakeCauseClassifier();
        var mistakes = events
            .Where(item => item.Kind == InputEventKind.Character
                && !item.IsCorrect
                && item.ExpectedChar is not null
                && item.ActualChar is not null)
            .Select(item => new
            {
                Event = item,
                Cause = classifier.Classify(
                    item.ExpectedChar!.Value,
                    item.ActualChar!.Value,
                    item.DeltaFromPreviousMs,
                    item.ElapsedMs,
                    summary.DurationMs)
            })
            .ToArray();

        if (mistakes.Length == 0)
        {
            return Array.Empty<MistakeCauseRow>();
        }

        return mistakes
            .GroupBy(item => item.Cause)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => FormatMistakeCause(group.Key), StringComparer.Ordinal)
            .Take(5)
            .Select(group =>
            {
                var targets = group
                    .GroupBy(item => item.Event.ExpectedChar!.Value)
                    .OrderByDescending(targetGroup => targetGroup.Count())
                    .ThenBy(targetGroup => DisplayChar(targetGroup.Key), StringComparer.Ordinal)
                    .Take(3)
                    .Select(targetGroup => DisplayChar(targetGroup.Key));
                return new MistakeCauseRow(
                    FormatMistakeCause(group.Key),
                    group.Count().ToString(CultureInfo.InvariantCulture),
                    string.Join(", ", targets),
                    group.Count() * 100.0 / mistakes.Length);
            })
            .ToArray();
    }

    private MistakeCauseDrillRequest? BuildTopMistakeCauseDrillRequest()
    {
        if (_lastSummary is null)
        {
            return null;
        }

        var classifier = new MistakeCauseClassifier();
        var mistakes = _lastCompletedEvents
            .Where(item => item.Kind == InputEventKind.Character
                && !item.IsCorrect
                && item.ExpectedChar is not null
                && item.ActualChar is not null)
            .Select(item => new
            {
                Event = item,
                Cause = classifier.Classify(
                    item.ExpectedChar!.Value,
                    item.ActualChar!.Value,
                    item.DeltaFromPreviousMs,
                    item.ElapsedMs,
                    _lastSummary.DurationMs)
            })
            .ToArray();

        if (mistakes.Length == 0)
        {
            return null;
        }

        var topCause = mistakes
            .GroupBy(item => item.Cause)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => FormatMistakeCause(group.Key), StringComparer.Ordinal)
            .First();
        var targetCharacters = topCause
            .GroupBy(item => item.Event.ExpectedChar!.Value)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => DisplayChar(group.Key), StringComparer.Ordinal)
            .Select(group => group.Key)
            .Where(character => !char.IsWhiteSpace(character))
            .Take(6)
            .ToArray();
        var targetBigrams = _lastReview?.FocusBigrams.Take(6).ToArray()
            ?? Array.Empty<string>();

        return new MistakeCauseDrillRequest(
            topCause.Key,
            targetCharacters,
            targetBigrams,
            _settings.AllowCapitalLetters,
            _settings.AllowNumbers,
            _settings.AllowPunctuation);
    }

    private int? GetLowestStableNetWpmPointIndex()
    {
        var samples = GetSessionNetWpmChartSamples();
        if (samples.Count < 2)
        {
            return null;
        }

        return samples
            .Select((sample, index) => new { sample.NetWpm, Index = index })
            .OrderBy(item => item.NetWpm)
            .First()
            .Index;
    }

    private static string FormatDrillTargets(MistakeCauseDrillRequest request)
    {
        var targets = request.TargetCharacters
            .Select(DisplayChar)
            .Concat(request.TargetBigrams)
            .Where(target => !string.IsNullOrWhiteSpace(target))
            .Distinct(StringComparer.Ordinal)
            .Take(4)
            .ToArray();

        return targets.Length == 0
            ? "controlled flow"
            : string.Join(", ", targets);
    }

    private static IReadOnlyList<LearningFocusRow> BuildLearningFocusRows(IReadOnlyList<LearningTarget> targets)
    {
        return targets
            .Take(6)
            .Select(target => new LearningFocusRow(
                FormatLearningTarget(target),
                target.Type == LearningItemType.Character ? "Key" : "Bigram",
                FormatMasteryState(target.MasteryState),
                FormatMistakeCause(target.PrimaryMistakeCause),
                FormatPercent(target.Accuracy),
                target.NextDueUtc is null ? "Due" : "Due now"))
            .ToArray();
    }

    private static string ExtractRetrySlice(string targetText, int cursorIndex, int targetLength)
    {
        if (string.IsNullOrWhiteSpace(targetText))
        {
            return string.Empty;
        }

        var center = Math.Clamp(cursorIndex, 0, targetText.Length - 1);
        var start = Math.Max(0, center - targetLength / 2);
        var end = Math.Min(targetText.Length, start + targetLength);
        start = Math.Max(0, Math.Min(start, end - 1));

        var leftBoundary = targetText.LastIndexOf(' ', start);
        if (leftBoundary >= 0 && center - leftBoundary < targetLength)
        {
            start = leftBoundary + 1;
        }

        var rightBoundary = targetText.IndexOf(' ', Math.Min(end, targetText.Length - 1));
        if (rightBoundary > start)
        {
            end = rightBoundary;
        }

        return targetText[start..end].Trim();
    }

    private static double? Median(IEnumerable<double> values)
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

    private static string DisplayChar(char character)
    {
        return character switch
        {
            ' ' => "Space",
            '\n' or '\r' => "Enter",
            '\t' => "Tab",
            _ => character.ToString()
        };
    }

    private static string FormatLearningTarget(LearningTarget target)
    {
        return target.Type == LearningItemType.Character && target.Target == " "
            ? "Space"
            : target.Target;
    }

    private static string FormatMistakeCause(MistakeCause cause)
    {
        return cause switch
        {
            MistakeCause.AdjacentKey => "Adjacent key",
            MistakeCause.SameFinger => "Same finger",
            MistakeCause.WrongHand => "Wrong hand",
            MistakeCause.ShiftIssue => "Shift issue",
            MistakeCause.NumberRow => "Number row",
            _ => cause.ToString()
        };
    }

    private static string FormatMasteryState(MasteryState state)
    {
        return state switch
        {
            MasteryState.New => "New",
            MasteryState.Learning => "Learning",
            MasteryState.Unstable => "Unstable",
            MasteryState.Mastered => "Mastered",
            MasteryState.Regressing => "Regressing",
            _ => state.ToString()
        };
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
        _countdownTimer.Stop();
        _completionQueued = false;
        _isCountdownActive = false;
        _lastSummary = null;
        _lastReview = null;
        _lastCompletedEvents = Array.Empty<TypingInputEvent>();
        _completedLearningTargets = Array.Empty<LearningTarget>();
        _lastEscapeTimestampTicks = null;
        _hasStartedTyping = false;
        _countdownRemaining = 0;
        IsPaused = false;
        _isStopped = true;
        PauseStatus = "Session ended. Press any key to restart.";
        CompletionStatus = string.Empty;
        TypingFeedback = string.Empty;
        OnCompletionChanged();
        OnReviewChanged();
        OnPropertyChanged(nameof(IsInputEnabled));
    }

    private async Task SavePracticeSettingsAsync(AppSettings settings)
    {
        try
        {
            await _lessonService.SaveSettingsAsync(settings);
        }
        catch
        {
            // Practice display toggles should never interrupt typing if persistence is unavailable.
        }
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

public sealed record CostlyMistakeRow(
    string Label,
    string Accuracy,
    string MedianLatencyMs,
    string Samples,
    double ImpactScore)
{
    public string Impact => ImpactScore.ToString("0.0", CultureInfo.InvariantCulture);

    public double ImpactPercent => Math.Clamp(ImpactScore, 0, 100);
}

public sealed record MistakeCauseRow(
    string Cause,
    string Count,
    string Targets,
    double Percent);

public sealed record LearningFocusRow(
    string Target,
    string Kind,
    string State,
    string Cause,
    string Accuracy,
    string Due);

public sealed record SessionNetWpmSample(
    double ElapsedMs,
    double NetWpm,
    int CursorIndex,
    int TypedKeypresses);

internal sealed record RecommendedFollowUp(
    RecommendedFollowUpKind Kind,
    string ButtonText,
    string Detail,
    int? ChartPointIndex);

internal enum RecommendedFollowUpKind
{
    None,
    SpacedReview,
    MistakeCauseDrill,
    WpmDipRetry,
    PracticeMistakes,
    NextParagraph
}

public enum PracticeInputFeedback
{
    None,
    Key,
    Mistake,
    Correction,
    CountdownStarted
}
