using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using TypingTrainer.Core.Learning;
using TypingTrainer.Data.Database;
using TypingTrainer.Data.Models;
using TypingTrainer.Data.Repositories;
using TypingTrainer.Data.Services;

namespace TypingTrainer.Data.Tests;

[TestClass]
public sealed class PersistenceTests
{
    [TestMethod]
    public async Task DatabaseInitializer_CreatesExpectedTables()
    {
        await using var database = await TestDatabase.CreateInitializedAsync();

        var tableNames = await database.GetTableNamesAsync();

        CollectionAssert.IsSubsetOf(
            new[] { "schema_version", "practice_sessions", "key_events", "content_packs", "practice_content_items", "app_settings", "learning_items" },
            tableNames.ToArray());
    }

    [TestMethod]
    public async Task DatabaseInitializer_RecordsSchemaVersionFour()
    {
        await using var database = await TestDatabase.CreateInitializedAsync();

        await using var connection = await database.ConnectionFactory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MAX(version) FROM schema_version;";

        var version = Convert.ToInt32(await command.ExecuteScalarAsync());

        Assert.AreEqual(4, version);
    }

    [TestMethod]
    public async Task DatabaseInitializer_CreatesModeStartedIndex()
    {
        await using var database = await TestDatabase.CreateInitializedAsync();

        await using var connection = await database.ConnectionFactory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'index'
              AND name = 'idx_practice_sessions_mode_started';
            """;

        var indexCount = Convert.ToInt32(await command.ExecuteScalarAsync());

        Assert.AreEqual(1, indexCount);
    }

    [TestMethod]
    public async Task PracticeSessionRepository_SaveCompletedSession_PersistsSession()
    {
        await using var database = await TestDatabase.CreateInitializedAsync();
        var session = CreateSession();

        await database.Repository.SaveCompletedSessionAsync(session, Array.Empty<StoredKeyEvent>());

        var stored = await database.Repository.GetSessionAsync(session.Id);

        Assert.IsNotNull(stored);
        Assert.AreEqual(session.Id, stored.Id);
        Assert.AreEqual(session.TargetText, stored.TargetText);
        Assert.AreEqual(session.RawWpm, stored.RawWpm);
    }

    [TestMethod]
    public async Task PracticeSessionRepository_SaveCompletedSession_PersistsKeyEvents()
    {
        await using var database = await TestDatabase.CreateInitializedAsync();
        var session = CreateSession();
        var events = CreateEvents(session.Id);

        await database.Repository.SaveCompletedSessionAsync(session, events);

        var storedEvents = await database.Repository.GetSessionEventsAsync(session.Id);

        Assert.AreEqual(2, storedEvents.Count);
        Assert.AreEqual('a', storedEvents[0].ExpectedChar);
        Assert.AreEqual('x', storedEvents[1].ActualChar);
        Assert.AreEqual("Character", storedEvents[0].EventKind);
    }

    [TestMethod]
    public async Task PracticeSessionRepository_GetRecentSessions_ReturnsNewestFirst()
    {
        await using var database = await TestDatabase.CreateInitializedAsync();
        var older = CreateSession(startedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-10));
        var newer = CreateSession(startedAtUtc: DateTimeOffset.UtcNow);

        await database.Repository.SaveCompletedSessionAsync(older, Array.Empty<StoredKeyEvent>());
        await database.Repository.SaveCompletedSessionAsync(newer, Array.Empty<StoredKeyEvent>());

        var recent = await database.Repository.GetRecentSessionsAsync(2);

        Assert.AreEqual(2, recent.Count);
        Assert.AreEqual(newer.Id, recent[0].Id);
        Assert.AreEqual(older.Id, recent[1].Id);
    }

    [TestMethod]
    public async Task PracticeSessionRepository_DeleteAll_RemovesSessionsAndEvents()
    {
        await using var database = await TestDatabase.CreateInitializedAsync();
        var session = CreateSession();

        await database.Repository.SaveCompletedSessionAsync(session, CreateEvents(session.Id));
        await database.Repository.DeleteAllAsync();

        var storedSession = await database.Repository.GetSessionAsync(session.Id);
        var storedEvents = await database.Repository.GetSessionEventsAsync(session.Id);

        Assert.IsNull(storedSession);
        Assert.AreEqual(0, storedEvents.Count);
    }

    [TestMethod]
    public async Task LearningProgressRepository_UpdateFromCompletedSession_UpsertsLearningItems()
    {
        await using var database = await TestDatabase.CreateInitializedAsync();
        var session = CreateSession();
        var events = CreateEvents(session.Id);

        await database.Repository.SaveCompletedSessionAsync(session, events);
        await database.LearningProgress.UpdateFromCompletedSessionAsync(session, events);

        await using var connection = await database.ConnectionFactory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT target_type, target, exposure_count, incorrect_count, mastery_state, primary_mistake_cause
            FROM learning_items
            WHERE target_type = 'Character' AND target = 'b';
            """;

        await using var reader = await command.ExecuteReaderAsync();
        Assert.IsTrue(await reader.ReadAsync());
        Assert.AreEqual("Character", reader.GetString(0));
        Assert.AreEqual("b", reader.GetString(1));
        Assert.AreEqual(1, reader.GetInt32(2));
        Assert.AreEqual(1, reader.GetInt32(3));
        Assert.AreEqual(MasteryState.New.ToString(), reader.GetString(4));
        Assert.AreEqual(MistakeCause.Other.ToString(), reader.GetString(5));
    }

    [TestMethod]
    public async Task JsonExportService_ExportAllSessions_WritesValidJson()
    {
        await using var database = await TestDatabase.CreateInitializedAsync();
        var session = CreateSession();
        var outputPath = Path.Combine(database.DirectoryPath, "export.json");
        var exportService = new JsonExportService(database.Repository);

        await database.Repository.SaveCompletedSessionAsync(session, CreateEvents(session.Id));
        await exportService.ExportAllSessionsAsync(outputPath);

        await using var stream = File.OpenRead(outputPath);
        using var document = await JsonDocument.ParseAsync(stream);

        Assert.AreEqual("TypingTrainer", document.RootElement.GetProperty("app").GetString());
        var sessions = document.RootElement.GetProperty("sessions");
        Assert.AreEqual(1, sessions.GetArrayLength());
        Assert.AreEqual(session.Id, sessions[0].GetProperty("id").GetGuid());
        Assert.AreEqual(2, sessions[0].GetProperty("events").GetArrayLength());
    }

    private static StoredPracticeSession CreateSession(DateTimeOffset? startedAtUtc = null)
    {
        var started = startedAtUtc ?? DateTimeOffset.UtcNow.AddMinutes(-1);
        var ended = started.AddMinutes(1);

        return new StoredPracticeSession(
            Guid.NewGuid(),
            started,
            ended,
            "fixed",
            "ab",
            TargetLength: 2,
            RawWpm: 40,
            NetWpm: 35,
            Accuracy: 0.5,
            Consistency: null,
            TotalKeypresses: 2,
            CorrectKeypresses: 1,
            IncorrectKeypresses: 1,
            CorrectedErrors: 0,
            UncorrectedErrors: 1,
            DurationMs: 60_000);
    }

    private static StoredKeyEvent[] CreateEvents(Guid sessionId)
    {
        return
        [
            new StoredKeyEvent(
                Id: null,
                sessionId,
                Position: 0,
                ExpectedChar: 'a',
                ActualChar: 'a',
                EventKind: "Character",
                IsCorrect: true,
                WasCorrection: false,
                TimestampTicks: 1,
                ElapsedMs: 0,
                DeltaPreviousMs: null),
            new StoredKeyEvent(
                Id: null,
                sessionId,
                Position: 1,
                ExpectedChar: 'b',
                ActualChar: 'x',
                EventKind: "Character",
                IsCorrect: false,
                WasCorrection: false,
                TimestampTicks: 2,
                ElapsedMs: 100,
                DeltaPreviousMs: 100)
        ];
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(string directoryPath, SqliteConnectionFactory connectionFactory)
        {
            DirectoryPath = directoryPath;
            ConnectionFactory = connectionFactory;
            Repository = new PracticeSessionRepository(connectionFactory);
            LearningProgress = new LearningProgressRepository(connectionFactory);
        }

        public string DirectoryPath { get; }

        public SqliteConnectionFactory ConnectionFactory { get; }

        public PracticeSessionRepository Repository { get; }

        public LearningProgressRepository LearningProgress { get; }

        public static async Task<TestDatabase> CreateInitializedAsync()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "TypingTrainer.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            var databasePath = Path.Combine(directoryPath, "typingtrainer.db");
            var connectionFactory = new SqliteConnectionFactory(new FixedDatabasePath(databasePath));
            var initializer = new DatabaseInitializer(connectionFactory, new MigrationRunner());

            await initializer.InitializeAsync();

            return new TestDatabase(directoryPath, connectionFactory);
        }

        public async Task<IReadOnlyList<string>> GetTableNamesAsync()
        {
            await using var connection = await ConnectionFactory.OpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";

            var tableNames = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                tableNames.Add(reader.GetString(0));
            }

            return tableNames;
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
}
