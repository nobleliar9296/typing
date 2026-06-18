using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.App.Services;
using TypingTrainer.Core.Models;
using TypingTrainer.Core.Typing;
using TypingTrainer.Data.Models;
using TypingTrainer.Data.Repositories;

namespace TypingTrainer.App.Tests;

[TestClass]
public sealed class SessionPersistenceQueueTests
{
    [TestMethod]
    public async Task EnqueueCompletedSessionAsync_WhenSaveSucceeds_ReturnsSaved()
    {
        var sessions = new FakePracticeSessionRepository();
        var learning = new FakeLearningProgressRepository();
        var queue = new SessionPersistenceQueue(sessions, learning, (_, _) => { });

        var result = await queue.EnqueueCompletedSessionAsync(CreateSummary(), Array.Empty<TypingInputEvent>());

        Assert.AreEqual(SessionPersistenceStatus.Saved, result.Status);
        Assert.IsTrue(result.SessionWasSaved);
        Assert.AreEqual(1, sessions.SaveCount);
        Assert.AreEqual(1, learning.UpdateCount);
    }

    [TestMethod]
    public async Task EnqueueCompletedSessionAsync_WhenSessionSaveFails_ReturnsFailed()
    {
        var sessions = new FakePracticeSessionRepository { SaveError = new InvalidOperationException("database unavailable") };
        var queue = new SessionPersistenceQueue(sessions, new FakeLearningProgressRepository(), (_, _) => { });

        var result = await queue.EnqueueCompletedSessionAsync(CreateSummary(), Array.Empty<TypingInputEvent>());

        Assert.AreEqual(SessionPersistenceStatus.Failed, result.Status);
        Assert.IsFalse(result.SessionWasSaved);
        Assert.IsFalse(SessionPersistenceStatusText.FromResult(result).Contains("Saved locally", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task EnqueueCompletedSessionAsync_WhenLearningUpdateFails_ReturnsPartialSuccess()
    {
        var learning = new FakeLearningProgressRepository { UpdateError = new InvalidOperationException("learning unavailable") };
        var queue = new SessionPersistenceQueue(new FakePracticeSessionRepository(), learning, (_, _) => { });

        var result = await queue.EnqueueCompletedSessionAsync(CreateSummary(), Array.Empty<TypingInputEvent>());

        Assert.AreEqual(SessionPersistenceStatus.SavedWithLearningUpdateFailure, result.Status);
        Assert.IsTrue(result.SessionWasSaved);
        Assert.IsNotNull(result.Error);
    }

    [TestMethod]
    public async Task EnqueueCompletedSessionAsync_WhenCanceledBeforeQueueing_ReturnsCanceled()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var queue = new SessionPersistenceQueue(new FakePracticeSessionRepository(), new FakeLearningProgressRepository(), (_, _) => { });

        var result = await queue.EnqueueCompletedSessionAsync(
            CreateSummary(),
            Array.Empty<TypingInputEvent>(),
            cancellationToken: cancellation.Token);

        Assert.AreEqual(SessionPersistenceStatus.Canceled, result.Status);
        Assert.IsFalse(result.SessionWasSaved);
    }

    [TestMethod]
    public async Task FailedQueueItem_DoesNotStopLaterItems()
    {
        var sessions = new FakePracticeSessionRepository();
        sessions.SaveErrors.Enqueue(new InvalidOperationException("first failed"));
        sessions.SaveErrors.Enqueue(null);
        var queue = new SessionPersistenceQueue(sessions, new FakeLearningProgressRepository(), (_, _) => { });

        var failed = await queue.EnqueueCompletedSessionAsync(CreateSummary(), Array.Empty<TypingInputEvent>());
        var saved = await queue.EnqueueCompletedSessionAsync(CreateSummary(), Array.Empty<TypingInputEvent>());

        Assert.AreEqual(SessionPersistenceStatus.Failed, failed.Status);
        Assert.AreEqual(SessionPersistenceStatus.Saved, saved.Status);
        Assert.AreEqual(2, sessions.SaveCount);
    }

    [TestMethod]
    public async Task FlushAsync_ReturnsOutstandingFailure()
    {
        var sessions = new FakePracticeSessionRepository();
        sessions.BlockSave = true;
        var queue = new SessionPersistenceQueue(sessions, new FakeLearningProgressRepository(), (_, _) => { });

        var enqueueTask = queue.EnqueueCompletedSessionAsync(CreateSummary(), Array.Empty<TypingInputEvent>()).AsTask();
        await sessions.SaveStarted.Task;
        sessions.SaveError = new InvalidOperationException("save failed");

        var flushTask = queue.FlushAsync();
        sessions.AllowSave.SetResult();

        var results = await flushTask;
        var enqueueResult = await enqueueTask;

        Assert.AreEqual(SessionPersistenceStatus.Failed, enqueueResult.Status);
        Assert.IsTrue(results.Any(result => result.Status == SessionPersistenceStatus.Failed));
    }

    private static SessionSummary CreateSummary()
    {
        return new SessionSummary(
            Guid.NewGuid(),
            "ab",
            IsComplete: true,
            TypedCharacterKeypresses: 2,
            CorrectCharacterKeypresses: 2,
            IncorrectCharacterKeypresses: 0,
            BackspaceCount: 0,
            CurrentErrors: 0,
            DurationMs: 60_000,
            RawWpm: 24,
            Accuracy: 1);
    }

    private sealed class FakePracticeSessionRepository : IPracticeSessionRepository
    {
        public Queue<Exception?> SaveErrors { get; } = new();

        public Exception? SaveError { get; set; }

        public bool BlockSave { get; set; }

        public TaskCompletionSource SaveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AllowSave { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int SaveCount { get; private set; }

        public async Task SaveCompletedSessionAsync(
            StoredPracticeSession session,
            IReadOnlyList<StoredKeyEvent> events,
            CancellationToken cancellationToken = default)
        {
            SaveCount++;
            SaveStarted.TrySetResult();
            if (BlockSave)
            {
                await AllowSave.Task.WaitAsync(cancellationToken);
            }

            var error = SaveErrors.Count > 0 ? SaveErrors.Dequeue() : SaveError;
            if (error is not null)
            {
                throw error;
            }
        }

        public Task<IReadOnlyList<StoredPracticeSession>> GetRecentSessionsAsync(int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StoredPracticeSession>>(Array.Empty<StoredPracticeSession>());

        public Task<StoredPracticeSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<StoredPracticeSession?>(null);

        public Task<IReadOnlyList<StoredKeyEvent>> GetSessionEventsAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StoredKeyEvent>>(Array.Empty<StoredKeyEvent>());

        public Task DeleteAllAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeLearningProgressRepository : ILearningProgressRepository
    {
        public Exception? UpdateError { get; set; }

        public int UpdateCount { get; private set; }

        public Task UpdateFromCompletedSessionAsync(
            StoredPracticeSession session,
            IReadOnlyList<StoredKeyEvent> events,
            CancellationToken cancellationToken = default)
        {
            UpdateCount++;
            if (UpdateError is not null)
            {
                throw UpdateError;
            }

            return Task.CompletedTask;
        }
    }
}
