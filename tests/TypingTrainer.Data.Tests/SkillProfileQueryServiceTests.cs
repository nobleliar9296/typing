using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.Core.Learning;
using TypingTrainer.Data.Database;
using TypingTrainer.Data.Models;
using TypingTrainer.Data.Repositories;
using TypingTrainer.Data.Services;

namespace TypingTrainer.Data.Tests;

[TestClass]
public sealed class SkillProfileQueryServiceTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task SkillProfileQueryService_NoSessions_ReturnsEmptyProfile()
    {
        await using var database = await SkillProfileTestDatabase.CreateInitializedAsync(NowUtc);

        var profile = await database.SkillProfile.GetUserSkillProfileAsync();

        Assert.AreEqual(0, profile.CompletedSessionCount);
        Assert.AreEqual(0, profile.Characters.Count);
        Assert.AreEqual(0, profile.Bigrams.Count);
        Assert.AreEqual(TimeSpan.Zero, profile.TotalPracticeTime);
    }

    [TestMethod]
    public async Task SkillProfileQueryService_CharacterEvents_CalculatesCharacterSkill()
    {
        await using var database = await SkillProfileTestDatabase.CreateInitializedAsync(NowUtc);
        var session = CreateSession("ab");

        await database.SaveAsync(session, [
            CharacterEvent(session.Id, 0, 'a', 'a', isCorrect: true, deltaPreviousMs: null, timestampTicks: 1),
            CharacterEvent(session.Id, 1, 'b', 'x', isCorrect: false, deltaPreviousMs: 100, timestampTicks: 2)
        ]);

        var profile = await database.SkillProfile.GetUserSkillProfileAsync();

        Assert.IsTrue(profile.Characters.ContainsKey('a'));
        Assert.AreEqual(1, profile.Characters['a'].ExposureCount);
        Assert.AreEqual(1, profile.Characters['a'].CorrectCount);
        Assert.AreEqual(0, profile.Characters['b'].Accuracy);
    }

    [TestMethod]
    public async Task SkillProfileQueryService_BigramEvents_CalculatesBigramSkill()
    {
        await using var database = await SkillProfileTestDatabase.CreateInitializedAsync(NowUtc);
        var session = CreateSession("ab");

        await database.SaveAsync(session, [
            CharacterEvent(session.Id, 0, 'a', 'a', isCorrect: true, deltaPreviousMs: null, timestampTicks: 1),
            CharacterEvent(session.Id, 1, 'b', 'x', isCorrect: false, deltaPreviousMs: 100, timestampTicks: 2)
        ]);

        var profile = await database.SkillProfile.GetUserSkillProfileAsync();

        Assert.IsTrue(profile.Bigrams.ContainsKey("ab"));
        Assert.AreEqual(1, profile.Bigrams["ab"].ExposureCount);
        Assert.AreEqual(0, profile.Bigrams["ab"].CorrectCount);
        Assert.AreEqual(100, profile.Bigrams["ab"].MedianLatencyMs);
    }

    [TestMethod]
    public async Task SkillProfileQueryService_FiltersLongPausesFromLatency()
    {
        await using var database = await SkillProfileTestDatabase.CreateInitializedAsync(NowUtc);
        var session = CreateSession("ab");

        await database.SaveAsync(session, [
            CharacterEvent(session.Id, 0, 'a', 'a', isCorrect: true, deltaPreviousMs: null, timestampTicks: 1),
            CharacterEvent(session.Id, 1, 'b', 'b', isCorrect: true, deltaPreviousMs: 5000, timestampTicks: 2)
        ]);

        var profile = await database.SkillProfile.GetUserSkillProfileAsync();

        Assert.AreEqual(1, profile.Characters['b'].ExposureCount);
        Assert.IsNull(profile.Characters['b'].AverageLatencyMs);
        Assert.IsNull(profile.Characters['b'].MedianLatencyMs);
    }

    [TestMethod]
    public async Task SkillProfileQueryService_ComputesCompletedSessionCountAndPracticeTime()
    {
        await using var database = await SkillProfileTestDatabase.CreateInitializedAsync(NowUtc);

        await database.SaveAsync(CreateSession("ab", durationMs: 60_000));
        await database.SaveAsync(CreateSession("cd", durationMs: 120_000));

        var profile = await database.SkillProfile.GetUserSkillProfileAsync();

        Assert.AreEqual(2, profile.CompletedSessionCount);
        Assert.AreEqual(TimeSpan.FromMinutes(3), profile.TotalPracticeTime);
    }

    [TestMethod]
    public async Task SkillProfileQueryService_ReturnsDueLearningTargets()
    {
        await using var database = await SkillProfileTestDatabase.CreateInitializedAsync(NowUtc);
        var session = CreateSession("fffff", durationMs: 30_000);
        var events = Enumerable.Range(0, 5)
            .Select(index => CharacterEvent(
                session.Id,
                index,
                'f',
                index < 3 ? 'f' : 'g',
                isCorrect: index < 3,
                deltaPreviousMs: 120,
                timestampTicks: index + 1))
            .ToArray();

        await database.SaveAndUpdateLearningAsync(session, events);

        var profile = await database.SkillProfile.GetUserSkillProfileAsync();

        Assert.IsTrue(profile.DueLearningTargets.Any(target =>
            target.Type == LearningItemType.Character && target.Target == "f"));
        Assert.IsTrue(profile.MasterySummary.DueCount > 0);
    }

    private static StoredPracticeSession CreateSession(string targetText, long durationMs = 60_000)
    {
        return new StoredPracticeSession(
            Guid.NewGuid(),
            NowUtc.AddMinutes(-10),
            NowUtc.AddMinutes(-9),
            "Adaptive",
            targetText,
            targetText.Length,
            RawWpm: 50,
            NetWpm: 45,
            Accuracy: 0.5,
            Consistency: null,
            TotalKeypresses: targetText.Length,
            CorrectKeypresses: 1,
            IncorrectKeypresses: Math.Max(0, targetText.Length - 1),
            CorrectedErrors: 0,
            UncorrectedErrors: Math.Max(0, targetText.Length - 1),
            DurationMs: durationMs);
    }

    private static StoredKeyEvent CharacterEvent(
        Guid sessionId,
        int position,
        char expected,
        char actual,
        bool isCorrect,
        double? deltaPreviousMs,
        long timestampTicks)
    {
        return new StoredKeyEvent(
            Id: null,
            sessionId,
            position,
            expected,
            actual,
            "Character",
            isCorrect,
            WasCorrection: false,
            timestampTicks,
            ElapsedMs: deltaPreviousMs ?? 0,
            deltaPreviousMs);
    }

    private sealed class SkillProfileTestDatabase : IAsyncDisposable
    {
        private SkillProfileTestDatabase(
            string directoryPath,
            PracticeSessionRepository repository,
            LearningProgressRepository learningProgress,
            SkillProfileQueryService skillProfile)
        {
            DirectoryPath = directoryPath;
            Repository = repository;
            LearningProgress = learningProgress;
            SkillProfile = skillProfile;
        }

        public string DirectoryPath { get; }

        public PracticeSessionRepository Repository { get; }

        public LearningProgressRepository LearningProgress { get; }

        public SkillProfileQueryService SkillProfile { get; }

        public static async Task<SkillProfileTestDatabase> CreateInitializedAsync(DateTimeOffset nowUtc)
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "TypingTrainer.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            var databasePath = Path.Combine(directoryPath, "typingtrainer.db");
            var connectionFactory = new SqliteConnectionFactory(new FixedDatabasePath(databasePath));
            var initializer = new DatabaseInitializer(connectionFactory, new MigrationRunner());
            await initializer.InitializeAsync();

            var repository = new PracticeSessionRepository(connectionFactory);
            var learningProgress = new LearningProgressRepository(connectionFactory, new FixedUtcClock(nowUtc));
            var skillProfile = new SkillProfileQueryService(connectionFactory, new FixedUtcClock(nowUtc));

            return new SkillProfileTestDatabase(directoryPath, repository, learningProgress, skillProfile);
        }

        public Task SaveAsync(StoredPracticeSession session)
        {
            return SaveAsync(session, Array.Empty<StoredKeyEvent>());
        }

        public Task SaveAsync(StoredPracticeSession session, IReadOnlyList<StoredKeyEvent> events)
        {
            return Repository.SaveCompletedSessionAsync(session, events);
        }

        public async Task SaveAndUpdateLearningAsync(StoredPracticeSession session, IReadOnlyList<StoredKeyEvent> events)
        {
            await Repository.SaveCompletedSessionAsync(session, events);
            await LearningProgress.UpdateFromCompletedSessionAsync(session, events);
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class FixedDatabasePath : IAppDatabasePath
    {
        private readonly string _databasePath;

        public FixedDatabasePath(string databasePath)
        {
            _databasePath = databasePath;
        }

        public string GetDatabasePath()
        {
            return _databasePath;
        }
    }

    private sealed class FixedUtcClock : IUtcClock
    {
        public FixedUtcClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }
    }
}
