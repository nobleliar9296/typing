using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.Core.Content;
using TypingTrainer.Data.Content;
using TypingTrainer.Data.Database;
using TypingTrainer.Data.Models;
using TypingTrainer.Data.Repositories;

namespace TypingTrainer.Data.Tests;

[TestClass]
public sealed class ContentServicesTests
{
    [TestMethod]
    public async Task TextFileImportService_ImportsParagraphsFromTxt()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        var filePath = database.CreateTextFile("import.txt", "this paragraph has enough text for import and should become one typing paragraph");

        var result = await database.ImportService.ImportTextFileAsync(
            filePath,
            new TextImportOptions("Import", MinParagraphCharacters: 20));

        Assert.AreEqual(1, result.ParagraphsImported);
        Assert.AreEqual(1, (await database.ContentQuery.GetContentPacksAsync()).Single().ParagraphCount);
    }

    [TestMethod]
    public async Task TextFileImportService_DoesNotImportTinyParagraphs()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        var filePath = database.CreateTextFile("tiny.txt", "tiny");

        var result = await database.ImportService.ImportTextFileAsync(
            filePath,
            new TextImportOptions("Tiny", MinParagraphCharacters: 20));

        Assert.AreEqual(0, result.ParagraphsImported);
    }

    [TestMethod]
    public async Task TextFileImportService_SplitsLargeParagraphs()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        var text = string.Join(' ', Enumerable.Repeat("steady", 80));
        var filePath = database.CreateTextFile("large.txt", text);

        var result = await database.ImportService.ImportTextFileAsync(
            filePath,
            new TextImportOptions("Large", MinParagraphCharacters: 20, MaxParagraphCharacters: 120));

        Assert.IsTrue(result.ParagraphsImported > 1);
    }

    [TestMethod]
    public async Task TextFileImportService_CanImportLargeFile()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        var filePath = Path.Combine(database.DirectoryPath, "many.txt");
        await using (var writer = new StreamWriter(filePath))
        {
            for (var index = 0; index < 10_000; index++)
            {
                await writer.WriteLineAsync("this generated paragraph is long enough for import and remains local to the temp test database");
                await writer.WriteLineAsync();
            }
        }

        var result = await database.ImportService.ImportTextFileAsync(
            filePath,
            new TextImportOptions("Many", MinParagraphCharacters: 20));

        Assert.AreEqual(10_000, result.ParagraphsImported);
    }

    [TestMethod]
    public async Task TextFileImportService_NormalizesImportedTextToAscii()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        var filePath = database.CreateTextFile(
            "quotes.txt",
            "\u201cCaf\u00E9 notes\u201d don\u2019t stop\u2014they continue\u2026 with na\u00EFve examples");

        await database.ImportService.ImportTextFileAsync(
            filePath,
            new TextImportOptions("Quotes", MinParagraphCharacters: 20));

        var paragraph = await database.ContentQuery.GetNextParagraphAsync(
            new ParagraphPracticeQuery(
                TargetCharacters: 200,
                AllowCapitalLetters: true,
                AllowNumbers: true,
                AllowPunctuation: true,
                UseImportedContent: true,
                UseBuiltInContent: false));

        Assert.IsNotNull(paragraph);
        Assert.IsTrue(paragraph.Text.All(character => character <= 0x7F));
        StringAssert.Contains(paragraph.Text, "\"Cafe notes\"");
        StringAssert.Contains(paragraph.Text, "don't stop-they continue...");
        StringAssert.Contains(paragraph.Text, "naive");
    }

    [TestMethod]
    public async Task ContentQueryService_ReturnsParagraphMatchingSettings()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        var filePath = database.CreateTextFile("settings.txt", """
            This Paragraph Has Capitals And Punctuation.

            this paragraph has lowercase words and should match
            """);

        await database.ImportService.ImportTextFileAsync(
            filePath,
            new TextImportOptions("Settings", MinParagraphCharacters: 20));

        var paragraph = await database.ContentQuery.GetNextParagraphAsync(
            new ParagraphPracticeQuery(
                TargetCharacters: 80,
                AllowCapitalLetters: false,
                AllowNumbers: false,
                AllowPunctuation: false,
                UseImportedContent: true,
                UseBuiltInContent: false));

        Assert.IsNotNull(paragraph);
        Assert.IsFalse(paragraph.ContainsCapitalLetters);
        Assert.IsFalse(paragraph.ContainsPunctuation);
    }

    [TestMethod]
    public async Task ContentQueryService_UpdatesUseCount()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        var filePath = database.CreateTextFile("use.txt", "this paragraph has enough text for import and use count update");

        await database.ImportService.ImportTextFileAsync(
            filePath,
            new TextImportOptions("Use", MinParagraphCharacters: 20));

        _ = await database.ContentQuery.GetNextParagraphAsync(
            new ParagraphPracticeQuery(80, true, true, true, true, false));
        var paragraph = await database.ContentQuery.GetNextParagraphAsync(
            new ParagraphPracticeQuery(80, true, true, true, true, false));

        Assert.IsNotNull(paragraph);
        Assert.AreEqual(1, paragraph.UseCount);
        Assert.IsNotNull(paragraph.LastUsedAtUtc);
    }

    [TestMethod]
    public async Task ContentQueryService_GetParagraphs_ReturnsMultipleParagraphsForLongTarget()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        var filePath = database.CreateTextFile("long.txt", """
            this first imported paragraph has enough lowercase words for a longer practice lesson

            this second imported paragraph should be joined when the target length needs more text

            this third imported paragraph gives the query service another chunk for essay practice
            """);

        await database.ImportService.ImportTextFileAsync(
            filePath,
            new TextImportOptions("Long", MinParagraphCharacters: 20));

        var paragraphs = await database.ContentQuery.GetParagraphsAsync(
            new ParagraphPracticeQuery(
                TargetCharacters: 180,
                AllowCapitalLetters: false,
                AllowNumbers: false,
                AllowPunctuation: false,
                UseImportedContent: true,
                UseBuiltInContent: false));

        Assert.IsTrue(paragraphs.Count > 1);
        Assert.IsTrue(paragraphs.Sum(paragraph => paragraph.CharacterCount) >= 180);
    }

    [TestMethod]
    public async Task ContentQueryService_DeleteContentPack_RemovesParagraphs()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        var filePath = database.CreateTextFile("delete.txt", "this paragraph has enough text for import and delete testing");

        await database.ImportService.ImportTextFileAsync(
            filePath,
            new TextImportOptions("Delete", MinParagraphCharacters: 20));
        var pack = (await database.ContentQuery.GetContentPacksAsync()).Single();

        await database.ContentQuery.DeleteContentPackAsync(pack.Id);

        Assert.AreEqual(0, (await database.ContentQuery.GetContentPacksAsync()).Count);
        Assert.IsNull(await database.ContentQuery.GetNextParagraphAsync(new ParagraphPracticeQuery(80, true, true, true, true, false)));
    }

    [TestMethod]
    public async Task AppSettingsRepository_ReturnsDefaultsWhenEmpty()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();

        var settings = await database.SettingsRepository.GetSettingsAsync();

        Assert.AreEqual(AppSettings.AutoLessonMode, settings.DefaultLessonMode);
        Assert.AreEqual(220, settings.LessonLengthCharacters);
        Assert.IsTrue(settings.UseImportedContent);
        Assert.IsFalse(settings.RequireCorrectKeyToAdvance);
        Assert.IsTrue(settings.ShowVisualKeyboard);
        Assert.IsTrue(settings.ShowFingerColors);
        Assert.IsFalse(settings.ShowFingerLabels);
        Assert.AreEqual(AppSettings.QwertyKeyboardLayout, settings.VisualKeyboardLayout);
        Assert.AreEqual(100, settings.PracticeTextScalePercent);
        Assert.AreEqual(100, settings.VisualKeyboardScalePercent);
        Assert.AreEqual(60, settings.GoalTargetNetWpm);
        Assert.AreEqual(95, settings.GoalTargetAccuracyPercent);
        Assert.AreEqual(75, settings.GoalWeeklyPracticeMinutes);
        Assert.IsTrue(settings.NormalizeImportedTextToAscii);
        Assert.IsFalse(settings.LowercaseImportedText);
        Assert.IsTrue(settings.NormalizeImportedWhitespace);
    }

    [TestMethod]
    public async Task AppSettingsRepository_PersistsSettings()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        var settings = AppSettings.Defaults with
        {
            DefaultLessonMode = "Paragraph",
            LessonLengthCharacters = 333,
            AllowNumbers = true,
            AutoSaveCompletedSessions = false
        };

        await database.SettingsRepository.SaveSettingsAsync(settings);
        var stored = await database.SettingsRepository.GetSettingsAsync();

        Assert.AreEqual("Paragraph", stored.DefaultLessonMode);
        Assert.AreEqual(333, stored.LessonLengthCharacters);
        Assert.IsTrue(stored.AllowNumbers);
        Assert.IsFalse(stored.AutoSaveCompletedSessions);
    }

    [TestMethod]
    public async Task AppSettingsRepository_DefaultRequireCorrectKeyToAdvance_IsFalse()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();

        var settings = await database.SettingsRepository.GetSettingsAsync();

        Assert.IsFalse(settings.RequireCorrectKeyToAdvance);
    }

    [TestMethod]
    public async Task AppSettingsRepository_SaveRequireCorrectKeyToAdvance_PersistsTrue()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();

        await database.SettingsRepository.SaveSettingsAsync(AppSettings.Defaults with { RequireCorrectKeyToAdvance = true });
        var settings = await database.SettingsRepository.GetSettingsAsync();

        Assert.IsTrue(settings.RequireCorrectKeyToAdvance);
    }

    [TestMethod]
    public async Task AppSettingsRepository_SaveRequireCorrectKeyToAdvance_PersistsFalse()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();

        await database.SettingsRepository.SaveSettingsAsync(AppSettings.Defaults with { RequireCorrectKeyToAdvance = true });
        await database.SettingsRepository.SaveSettingsAsync(AppSettings.Defaults with { RequireCorrectKeyToAdvance = false });
        var settings = await database.SettingsRepository.GetSettingsAsync();

        Assert.IsFalse(settings.RequireCorrectKeyToAdvance);
    }

    [TestMethod]
    public async Task AppSettingsRepository_DefaultVisualKeyboardSettings_AreEnabledWithLabelsOff()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();

        var settings = await database.SettingsRepository.GetSettingsAsync();

        Assert.IsTrue(settings.ShowVisualKeyboard);
        Assert.IsTrue(settings.ShowFingerColors);
        Assert.IsFalse(settings.ShowFingerLabels);
        Assert.AreEqual(AppSettings.QwertyKeyboardLayout, settings.VisualKeyboardLayout);
    }

    [TestMethod]
    public async Task AppSettingsRepository_SaveVisualKeyboardSettings_Persists()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();

        await database.SettingsRepository.SaveSettingsAsync(AppSettings.Defaults with
        {
            ShowVisualKeyboard = false,
            ShowFingerColors = false,
            ShowFingerLabels = true,
            VisualKeyboardLayout = AppSettings.QwertyKeyboardLayout
        });
        var settings = await database.SettingsRepository.GetSettingsAsync();

        Assert.IsFalse(settings.ShowVisualKeyboard);
        Assert.IsFalse(settings.ShowFingerColors);
        Assert.IsTrue(settings.ShowFingerLabels);
        Assert.AreEqual(AppSettings.QwertyKeyboardLayout, settings.VisualKeyboardLayout);
    }

    [TestMethod]
    public async Task AppSettingsRepository_DefaultDisplayScaleSettings_AreOneHundredPercent()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();

        var settings = await database.SettingsRepository.GetSettingsAsync();

        Assert.AreEqual(100, settings.PracticeTextScalePercent);
        Assert.AreEqual(100, settings.VisualKeyboardScalePercent);
    }

    [TestMethod]
    public async Task AppSettingsRepository_SaveDisplayScaleSettings_Persists()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();

        await database.SettingsRepository.SaveSettingsAsync(AppSettings.Defaults with
        {
            PracticeTextScalePercent = 115,
            VisualKeyboardScalePercent = 85
        });
        var settings = await database.SettingsRepository.GetSettingsAsync();

        Assert.AreEqual(115, settings.PracticeTextScalePercent);
        Assert.AreEqual(85, settings.VisualKeyboardScalePercent);
    }

    [TestMethod]
    public async Task AppSettingsRepository_DisplayScaleSettings_ClampStoredValues()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        await database.SetRawSettingAsync("practice.textScalePercent", "10");
        await database.SetRawSettingAsync("visualKeyboard.scalePercent", "200");

        var settings = await database.SettingsRepository.GetSettingsAsync();

        Assert.AreEqual(70, settings.PracticeTextScalePercent);
        Assert.AreEqual(130, settings.VisualKeyboardScalePercent);
    }

    [TestMethod]
    public async Task AppSettingsRepository_DefaultGoalSettings_AreReturnedWhenEmpty()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();

        var settings = await database.SettingsRepository.GetSettingsAsync();

        Assert.AreEqual(60, settings.GoalTargetNetWpm);
        Assert.AreEqual(95, settings.GoalTargetAccuracyPercent);
        Assert.AreEqual(75, settings.GoalWeeklyPracticeMinutes);
    }

    [TestMethod]
    public async Task AppSettingsRepository_SaveGoalSettings_Persists()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();

        await database.SettingsRepository.SaveSettingsAsync(AppSettings.Defaults with
        {
            GoalTargetNetWpm = 72,
            GoalTargetAccuracyPercent = 97,
            GoalWeeklyPracticeMinutes = 120
        });
        var settings = await database.SettingsRepository.GetSettingsAsync();

        Assert.AreEqual(72, settings.GoalTargetNetWpm);
        Assert.AreEqual(97, settings.GoalTargetAccuracyPercent);
        Assert.AreEqual(120, settings.GoalWeeklyPracticeMinutes);
    }

    [TestMethod]
    public async Task AppSettingsRepository_DefaultImportCleanupSettings_AreReturnedWhenEmpty()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();

        var settings = await database.SettingsRepository.GetSettingsAsync();

        Assert.IsTrue(settings.NormalizeImportedTextToAscii);
        Assert.IsFalse(settings.LowercaseImportedText);
        Assert.IsTrue(settings.NormalizeImportedWhitespace);
    }

    [TestMethod]
    public async Task AppSettingsRepository_SaveImportCleanupSettings_Persists()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();

        await database.SettingsRepository.SaveSettingsAsync(AppSettings.Defaults with
        {
            NormalizeImportedTextToAscii = false,
            LowercaseImportedText = true,
            NormalizeImportedWhitespace = false
        });
        var settings = await database.SettingsRepository.GetSettingsAsync();

        Assert.IsFalse(settings.NormalizeImportedTextToAscii);
        Assert.IsTrue(settings.LowercaseImportedText);
        Assert.IsFalse(settings.NormalizeImportedWhitespace);
    }

    [TestMethod]
    public void TextFileImportService_Source_DoesNotUseWholeFileReads()
    {
        var sourcePath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "TypingTrainer.Data",
            "Content",
            "TextFileImportService.cs");
        var source = string.Join('\n', File.ReadLines(sourcePath));

        Assert.IsFalse(source.Contains("File.Read" + "AllText", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("Read" + "AllLines", StringComparison.Ordinal));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TypingTrainer.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root could not be found.");
    }

    private sealed class ContentTestDatabase : IAsyncDisposable
    {
        private ContentTestDatabase(string directoryPath, SqliteConnectionFactory connectionFactory)
        {
            DirectoryPath = directoryPath;
            ConnectionFactory = connectionFactory;
            ImportRepository = new ContentImportRepository(connectionFactory);
            ImportService = new TextFileImportService(ImportRepository);
            ContentQuery = new ContentQueryService(connectionFactory, new BuiltInParagraphProvider());
            SettingsRepository = new AppSettingsRepository(connectionFactory);
        }

        public string DirectoryPath { get; }

        public SqliteConnectionFactory ConnectionFactory { get; }

        public ContentImportRepository ImportRepository { get; }

        public TextFileImportService ImportService { get; }

        public ContentQueryService ContentQuery { get; }

        public AppSettingsRepository SettingsRepository { get; }

        public static async Task<ContentTestDatabase> CreateInitializedAsync()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "TypingTrainer.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            var databasePath = Path.Combine(directoryPath, "typingtrainer.db");
            var connectionFactory = new SqliteConnectionFactory(new FixedDatabasePath(databasePath));
            var initializer = new DatabaseInitializer(connectionFactory, new MigrationRunner());
            await initializer.InitializeAsync();

            return new ContentTestDatabase(directoryPath, connectionFactory);
        }

        public string CreateTextFile(string fileName, string content)
        {
            var path = Path.Combine(DirectoryPath, fileName);
            using var writer = new StreamWriter(path);
            writer.Write(content);
            return path;
        }

        public async Task SetRawSettingAsync(string key, string value)
        {
            await using var connection = await ConnectionFactory.OpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO app_settings (key, value)
                VALUES ($key, $value)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                """;
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);
            await command.ExecuteNonQueryAsync();
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
