using System.Collections.Concurrent;
using System.Threading.Channels;
using TypingTrainer.Core.Models;
using TypingTrainer.Core.Typing;
using TypingTrainer.Data.Models;
using TypingTrainer.Data.Repositories;

namespace TypingTrainer.App.Services;

public sealed class SessionPersistenceQueue : ISessionPersistenceQueue
{
    private readonly IPracticeSessionRepository _practiceSessionRepository;
    private readonly ILearningProgressRepository _learningProgressRepository;
    private readonly Channel<PendingSession> _channel;
    private readonly ConcurrentDictionary<Guid, Task<SessionPersistenceResult>> _pendingTasks = new();
    private readonly Action<string, Exception> _logException;

    public SessionPersistenceQueue(
        IPracticeSessionRepository practiceSessionRepository,
        ILearningProgressRepository learningProgressRepository,
        Action<string, Exception>? logException = null)
    {
        _practiceSessionRepository = practiceSessionRepository;
        _learningProgressRepository = learningProgressRepository;
        _logException = logException ?? StartupExceptionLogger.Log;
        _channel = Channel.CreateUnbounded<PendingSession>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _ = Task.Run(ProcessQueueAsync);
    }

    public Exception? LastError { get; private set; }

    public async ValueTask<SessionPersistenceResult> EnqueueCompletedSessionAsync(
        SessionSummary summary,
        IReadOnlyList<TypingInputEvent> events,
        string mode = "fixed",
        CancellationToken cancellationToken = default)
    {
        var pendingSession = new PendingSession(summary, events.ToArray(), mode);
        _pendingTasks[pendingSession.Id] = pendingSession.Completion.Task;

        try
        {
            await _channel.Writer.WriteAsync(pendingSession, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            _pendingTasks.TryRemove(pendingSession.Id, out _);
            return pendingSession.Cancel(ex);
        }
        catch
        {
            _pendingTasks.TryRemove(pendingSession.Id, out _);
            throw;
        }

        return await pendingSession.Completion.Task.ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SessionPersistenceResult>> FlushAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<SessionPersistenceResult>();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pending = _pendingTasks.Values.Where(task => !task.IsCompleted).ToArray();

            if (pending.Length == 0)
            {
                return results;
            }

            results.AddRange(await Task.WhenAll(pending).WaitAsync(cancellationToken).ConfigureAwait(false));
        }
    }

    private async Task ProcessQueueAsync()
    {
        await foreach (var pendingSession in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                var storedSession = MapSession(pendingSession.Summary, pendingSession.Events, pendingSession.Mode);
                var storedEvents = pendingSession.Events.Select(MapEvent).ToArray();

                await _practiceSessionRepository
                    .SaveCompletedSessionAsync(storedSession, storedEvents)
                    .ConfigureAwait(false);

                try
                {
                    await _learningProgressRepository
                        .UpdateFromCompletedSessionAsync(storedSession, storedEvents)
                        .ConfigureAwait(false);

                    pendingSession.Complete(SessionPersistenceStatus.Saved);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LastError = ex;
                    _logException("SessionPersistenceQueue.LearningProgress", ex);
                    pendingSession.Complete(SessionPersistenceStatus.SavedWithLearningUpdateFailure, ex);
                }
                catch (OperationCanceledException ex)
                {
                    LastError = ex;
                    _logException("SessionPersistenceQueue.LearningProgressCanceled", ex);
                    pendingSession.Complete(SessionPersistenceStatus.SavedWithLearningUpdateFailure, ex);
                }
            }
            catch (OperationCanceledException ex)
            {
                LastError = ex;
                pendingSession.Cancel(ex);
            }
            catch (Exception ex)
            {
                LastError = ex;
                _logException("SessionPersistenceQueue.SaveCompletedSession", ex);
                pendingSession.Complete(SessionPersistenceStatus.Failed, ex);
            }
            finally
            {
                _pendingTasks.TryRemove(pendingSession.Id, out _);
            }
        }
    }

    private static StoredPracticeSession MapSession(
        SessionSummary summary,
        IReadOnlyList<TypingInputEvent> events,
        string mode)
    {
        var endedAtUtc = DateTimeOffset.UtcNow;
        var durationMs = Math.Max(0, (long)Math.Round(summary.DurationMs));
        var startedAtUtc = endedAtUtc.AddMilliseconds(-durationMs);
        var uncorrectedErrors = summary.CurrentErrors;
        var correctedErrors = events.Count(IsCorrectedErrorBackspace);

        return new StoredPracticeSession(
            summary.SessionId,
            startedAtUtc,
            endedAtUtc,
            mode,
            summary.TargetText,
            summary.TargetText.Length,
            summary.RawWpm,
            CalculateNetWpm(summary.CorrectCharacterKeypresses, uncorrectedErrors, summary.DurationMs),
            summary.Accuracy,
            Consistency: null,
            summary.TypedCharacterKeypresses,
            summary.CorrectCharacterKeypresses,
            summary.IncorrectCharacterKeypresses,
            correctedErrors,
            uncorrectedErrors,
            durationMs);
    }

    private static StoredKeyEvent MapEvent(TypingInputEvent keyEvent)
    {
        return new StoredKeyEvent(
            Id: null,
            keyEvent.SessionId,
            keyEvent.Position,
            keyEvent.ExpectedChar,
            keyEvent.ActualChar,
            keyEvent.Kind.ToString(),
            keyEvent.IsCorrect,
            keyEvent.WasCorrection,
            keyEvent.TimestampTicks,
            keyEvent.ElapsedMs,
            keyEvent.DeltaFromPreviousMs);
    }

    private static bool IsCorrectedErrorBackspace(TypingInputEvent keyEvent)
    {
        return keyEvent.Kind == InputEventKind.Backspace
            && keyEvent.ExpectedChar is not null
            && keyEvent.ActualChar is not null
            && keyEvent.ExpectedChar != keyEvent.ActualChar;
    }

    private static double CalculateNetWpm(
        int correctKeypresses,
        int uncorrectedErrors,
        double elapsedMs)
    {
        if (elapsedMs <= 0)
        {
            return 0;
        }

        var elapsedMinutes = elapsedMs / 60_000.0;
        var netWpm = ((correctKeypresses - uncorrectedErrors) / 5.0) / elapsedMinutes;
        return Math.Max(0, netWpm);
    }

    private sealed class PendingSession
    {
        public PendingSession(SessionSummary summary, IReadOnlyList<TypingInputEvent> events, string mode)
        {
            Id = Guid.NewGuid();
            Summary = summary;
            Events = events;
            Mode = mode;
            Completion = new TaskCompletionSource<SessionPersistenceResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Guid Id { get; }

        public SessionSummary Summary { get; }

        public IReadOnlyList<TypingInputEvent> Events { get; }

        public string Mode { get; }

        public TaskCompletionSource<SessionPersistenceResult> Completion { get; }

        public void Complete(SessionPersistenceStatus status, Exception? error = null)
        {
            Completion.TrySetResult(new SessionPersistenceResult(status, Summary.SessionId, error));
        }

        public SessionPersistenceResult Cancel(OperationCanceledException error)
        {
            var result = new SessionPersistenceResult(SessionPersistenceStatus.Canceled, Summary.SessionId, error);
            Completion.TrySetResult(result);
            return result;
        }
    }
}
