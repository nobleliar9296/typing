using TypingTrainer.Core.Analytics;
using TypingTrainer.Core.Models;

namespace TypingTrainer.Core.Typing;

public sealed class TypingSession
{
    private readonly TypedPosition[] _positions;
    private readonly bool[] _positionWasCleared;
    private readonly TypingSessionOptions _options;
    private readonly List<TypingInputEvent> _events = [];

    private long? _startedTimestampTicks;
    private long? _lastEventTimestampTicks;
    private int _typedCharacterKeypresses;
    private int _correctCharacterKeypresses;
    private int _incorrectCharacterKeypresses;
    private int _backspaceCount;

    public TypingSession(string targetText, TypingSessionOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(targetText))
        {
            throw new ArgumentException("Target text must contain at least one printable character.", nameof(targetText));
        }

        SessionId = Guid.NewGuid();
        TargetText = targetText;
        _options = options ?? TypingSessionOptions.Default;
        _positions = new TypedPosition[targetText.Length];
        _positionWasCleared = new bool[targetText.Length];
    }

    public Guid SessionId { get; }

    public string TargetText { get; }

    public int CursorIndex { get; private set; }

    public bool IsComplete => CursorIndex >= TargetText.Length;

    public IReadOnlyList<TypingInputEvent> Events => GetEvents();

    public TypingInputEvent[] GetEvents()
    {
        return _events.ToArray();
    }

    public TypingStateSnapshot GetSnapshot(long timestampTicks)
    {
        var elapsedMs = GetElapsedMs(timestampTicks);
        var characters = new CharacterSnapshot[TargetText.Length];
        var currentErrors = 0;

        for (var index = 0; index < TargetText.Length; index++)
        {
            var position = _positions[index];
            var state = GetCharacterState(index, position);

            if (state == CharacterState.Incorrect)
            {
                currentErrors++;
            }

            characters[index] = new CharacterSnapshot(
                index,
                TargetText[index],
                position.ActualChar,
                state);
        }

        return new TypingStateSnapshot(
            SessionId,
            TargetText,
            CursorIndex,
            IsComplete,
            characters,
            _typedCharacterKeypresses,
            _correctCharacterKeypresses,
            _incorrectCharacterKeypresses,
            _backspaceCount,
            currentErrors,
            elapsedMs,
            MetricsCalculator.CalculateRawWpm(_typedCharacterKeypresses, elapsedMs),
            MetricsCalculator.CalculateAccuracy(_correctCharacterKeypresses, _typedCharacterKeypresses),
            IsComplete ? null : TargetText[CursorIndex]);
    }

    public KeyProcessResult ProcessCharacter(char typedChar, long timestampTicks)
    {
        if (char.IsControl(typedChar))
        {
            return Ignored(timestampTicks, "Control characters are ignored by the typing engine.");
        }

        if (IsComplete)
        {
            return Ignored(timestampTicks, "The lesson is already complete.");
        }

        _startedTimestampTicks ??= timestampTicks;

        var position = CursorIndex;
        var expectedChar = TargetText[position];
        var isCorrect = typedChar == expectedChar;
        var wasCorrection = _positionWasCleared[position];
        var shouldAdvance = isCorrect || _options.ErrorAdvanceMode == ErrorAdvanceMode.AdvanceOnError;

        if (shouldAdvance)
        {
            _positions[position] = new TypedPosition(typedChar, isCorrect);
            CursorIndex++;
        }

        _typedCharacterKeypresses++;

        if (isCorrect)
        {
            _correctCharacterKeypresses++;
        }
        else
        {
            _incorrectCharacterKeypresses++;
        }

        var inputEvent = CreateEvent(
            position,
            expectedChar,
            typedChar,
            InputEventKind.Character,
            isCorrect,
            wasCorrection,
            timestampTicks);

        _events.Add(inputEvent);
        _lastEventTimestampTicks = timestampTicks;

        return new KeyProcessResult(
            GetSnapshot(timestampTicks),
            inputEvent,
            WasAccepted: shouldAdvance,
            Message: shouldAdvance ? null : "Wrong key. Try again.",
            DidAdvance: shouldAdvance,
            WasCorrect: isCorrect,
            WasRejected: !shouldAdvance,
            FeedbackMessage: shouldAdvance ? null : "Wrong key. Try again.");
    }

    public KeyProcessResult ProcessBackspace(long timestampTicks)
    {
        if (!_options.AllowBackspace)
        {
            return Ignored(timestampTicks, "Backspace is disabled for this session.");
        }

        if (CursorIndex <= 0)
        {
            return Ignored(timestampTicks, "There is no typed character to remove.");
        }

        var removedPosition = CursorIndex - 1;
        var removed = _positions[removedPosition];

        CursorIndex = removedPosition;
        _positions[removedPosition] = default;
        _positionWasCleared[removedPosition] = true;
        _backspaceCount++;

        var inputEvent = CreateEvent(
            removedPosition,
            TargetText[removedPosition],
            removed.ActualChar,
            InputEventKind.Backspace,
            IsCorrect: true,
            WasCorrection: true,
            timestampTicks);

        _events.Add(inputEvent);
        _lastEventTimestampTicks = timestampTicks;

        return new KeyProcessResult(
            GetSnapshot(timestampTicks),
            inputEvent,
            WasAccepted: true,
            DidAdvance: false,
            WasCorrect: true);
    }

    public SessionSummary Complete(long timestampTicks)
    {
        var snapshot = GetSnapshot(timestampTicks);

        return new SessionSummary(
            snapshot.SessionId,
            snapshot.TargetText,
            snapshot.IsComplete,
            snapshot.TypedCharacterKeypresses,
            snapshot.CorrectCharacterKeypresses,
            snapshot.IncorrectCharacterKeypresses,
            snapshot.BackspaceCount,
            snapshot.CurrentErrors,
            snapshot.ElapsedMs,
            snapshot.RawWpm,
            snapshot.Accuracy);
    }

    private CharacterState GetCharacterState(int position, TypedPosition typedPosition)
    {
        if (typedPosition.ActualChar is not null)
        {
            return typedPosition.IsCorrect ? CharacterState.Correct : CharacterState.Incorrect;
        }

        return position == CursorIndex && !IsComplete ? CharacterState.Current : CharacterState.Pending;
    }

    private KeyProcessResult Ignored(long timestampTicks, string message)
    {
        return new KeyProcessResult(
            GetSnapshot(timestampTicks),
            Event: null,
            WasAccepted: false,
            Message: message,
            FeedbackMessage: message);
    }

    private TypingInputEvent CreateEvent(
        int position,
        char? expectedChar,
        char? actualChar,
        InputEventKind kind,
        bool IsCorrect,
        bool WasCorrection,
        long timestampTicks)
    {
        var elapsedMs = GetElapsedMs(timestampTicks);
        double? deltaMs = _lastEventTimestampTicks is long previousTimestampTicks
            ? MetricsCalculator.TicksToMilliseconds(previousTimestampTicks, timestampTicks)
            : null;

        return new TypingInputEvent(
            SessionId,
            position,
            expectedChar,
            actualChar,
            kind,
            IsCorrect,
            WasCorrection,
            timestampTicks,
            elapsedMs,
            deltaMs);
    }

    private double GetElapsedMs(long timestampTicks)
    {
        return _startedTimestampTicks is long startedTimestampTicks
            ? MetricsCalculator.TicksToMilliseconds(startedTimestampTicks, timestampTicks)
            : 0;
    }

    private readonly record struct TypedPosition(char? ActualChar, bool IsCorrect);
}
