using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.Core.Content;
using TypingTrainer.Data.Content;
using TypingTrainer.Data.Database;
using TypingTrainer.Data.Models;
using TypingTrainer.Data.Repositories;
using TypingTrainer.Data.Services;

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
    public async Task TextFileImportService_StripsPunctuationWhenRequested()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        var filePath = database.CreateTextFile(
            "punctuation.txt",
            "Hello, world! It's fine--really; type it now with punctuation removed.");

        await database.ImportService.ImportTextFileAsync(
            filePath,
            new TextImportOptions(
                "No Punctuation",
                MinParagraphCharacters: 20,
                StripPunctuation: true));

        var paragraph = await database.ContentQuery.GetNextParagraphAsync(
            new ParagraphPracticeQuery(
                TargetCharacters: 200,
                AllowCapitalLetters: true,
                AllowNumbers: true,
                AllowPunctuation: false,
                UseImportedContent: true,
                UseBuiltInContent: false));

        Assert.IsNotNull(paragraph);
        Assert.IsFalse(paragraph.ContainsPunctuation);
        Assert.IsFalse(paragraph.Text.Any(char.IsPunctuation));
        Assert.AreEqual("Hello world It s fine really type it now with punctuation removed", paragraph.Text);
    }

    [TestMethod]
    public async Task TextFileImportService_PreviewsOriginalAndCleanedSamples()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        var filePath = database.CreateTextFile(
            "preview.txt",
            "Hello, WORLD! Caf\u00E9 notes should become clean typing text.");

        var preview = await database.ImportService.PreviewTextFileAsync(
            filePath,
            new TextImportOptions(
                "Preview",
                NormalizeWhitespace: true,
                LowercaseWhenImported: true,
                NormalizeToAscii: true,
                StripPunctuation: true));

        StringAssert.Contains(preview.OriginalSample, "Hello, WORLD!");
        Assert.AreEqual("hello world cafe notes should become clean typing text", preview.CleanedSample);
        CollectionAssert.Contains(preview.CleanupNotes.ToArray(), "Punctuation removed");
    }

    [TestMethod]
    public async Task TextFileImportService_PreviewMatchesImportedCleanup()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        var filePath = database.CreateTextFile(
            "preview-match.txt",
            "Hello, world! This imported line should match the preview after cleanup.");
        var options = new TextImportOptions(
            "Preview Match",
            MinParagraphCharacters: 20,
            NormalizeWhitespace: true,
            LowercaseWhenImported: true,
            NormalizeToAscii: true,
            StripPunctuation: true);

        var preview = await database.ImportService.PreviewTextFileAsync(filePath, options);
        await database.ImportService.ImportTextFileAsync(filePath, options);
        var paragraph = await database.ContentQuery.GetNextParagraphAsync(
            new ParagraphPracticeQuery(
                TargetCharacters: 200,
                AllowCapitalLetters: false,
                AllowNumbers: true,
                AllowPunctuation: false,
                UseImportedContent: true,
                UseBuiltInContent: false));

        Assert.IsNotNull(paragraph);
        Assert.AreEqual(paragraph.Text, preview.CleanedSample);
    }

    [TestMethod]
    public async Task TextFileImportService_PreviewMissingFileReturnsSafeStatus()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        var missingPath = Path.Combine(database.DirectoryPath, "missing.txt");

        var preview = await database.ImportService.PreviewTextFileAsync(
            missingPath,
            new TextImportOptions("Missing"));

        Assert.AreEqual(string.Empty, preview.OriginalSample);
        Assert.AreEqual(string.Empty, preview.CleanedSample);
        StringAssert.Contains(preview.CleanupNotes.Single(), "File not found");
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
    public async Task ContentQueryService_StripsImportedPunctuationWhenDisallowed()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        var filePath = database.CreateTextFile(
            "existing-punctuation.txt",
            "this imported paragraph has commas, periods, and apostrophes so it should still work in paragraph mode");

        await database.ImportService.ImportTextFileAsync(
            filePath,
            new TextImportOptions("Existing Punctuation", MinParagraphCharacters: 20));

        var paragraph = await database.ContentQuery.GetNextParagraphAsync(
            new ParagraphPracticeQuery(
                TargetCharacters: 120,
                AllowCapitalLetters: false,
                AllowNumbers: true,
                AllowPunctuation: false,
                UseImportedContent: true,
                UseBuiltInContent: false));

        Assert.IsNotNull(paragraph);
        Assert.IsFalse(paragraph.ContainsPunctuation);
        Assert.IsFalse(paragraph.Text.Any(char.IsPunctuation));
        StringAssert.Contains(paragraph.Text, "commas periods and apostrophes");
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
    public async Task ContentQueryService_RenameAndEnableContentPack_Persists()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        var filePath = database.CreateTextFile("rename.txt", "this paragraph has enough text for import and rename testing");
        await database.ImportService.ImportTextFileAsync(filePath, new TextImportOptions("Original", MinParagraphCharacters: 20));
        var pack = (await database.ContentQuery.GetContentPacksAsync()).Single();

        await database.ContentQuery.RenameContentPackAsync(pack.Id, "Renamed");
        await database.ContentQuery.SetContentPackEnabledAsync(pack.Id, false);

        var updated = (await database.ContentQuery.GetContentPacksAsync()).Single();
        Assert.AreEqual("Renamed", updated.Name);
        Assert.IsFalse(updated.Enabled);
    }

    [TestMethod]
    public async Task ContentQueryService_DisabledPack_IsNotSelectedForPractice()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        var filePath = database.CreateTextFile("disabled.txt", "this paragraph has enough text for import and disable testing");
        await database.ImportService.ImportTextFileAsync(filePath, new TextImportOptions("Disabled", MinParagraphCharacters: 20));
        var pack = (await database.ContentQuery.GetContentPacksAsync()).Single();

        await database.ContentQuery.SetContentPackEnabledAsync(pack.Id, false);
        var paragraph = await database.ContentQuery.GetNextParagraphAsync(new ParagraphPracticeQuery(80, true, true, true, true, false));

        Assert.IsNull(paragraph);
    }

    [TestMethod]
    public async Task ContentQueryService_ContentPackPreview_ReturnsParagraphs()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        var filePath = database.CreateTextFile("preview.txt", "this paragraph has enough text for import and preview testing");
        await database.ImportService.ImportTextFileAsync(filePath, new TextImportOptions("Preview", MinParagraphCharacters: 20));
        var pack = (await database.ContentQuery.GetContentPacksAsync()).Single();

        var preview = await database.ContentQuery.GetContentPackPreviewAsync(pack.Id, 5);

        Assert.AreEqual(1, preview.Count);
        StringAssert.Contains(preview[0].Text, "preview");
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
        Assert.IsFalse(settings.ZenModeEnabled);
        Assert.AreEqual(0, settings.CountdownSeconds);
        Assert.IsFalse(settings.KeySoundEnabled);
        Assert.IsFalse(settings.MistakeSoundEnabled);
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
        Assert.AreEqual(AppSettings.DefaultTrainingFocus, settings.GoalTrainingFocus);
        Assert.AreEqual(15, settings.GoalTargetSessionMinutes);
        Assert.AreEqual(1000, settings.GoalTargetEssayWords);
        Assert.AreEqual(AppSettings.DefaultFontFamily, settings.PracticeFontFamily);
        Assert.AreEqual(AppSettings.DefaultThemePreset, settings.ThemePreset);
        Assert.AreEqual(AppSettings.DefaultDifficultyPreset, settings.DifficultyPreset);
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
            AutoSaveCompletedSessions = false,
            ZenModeEnabled = true,
            CountdownSeconds = 3,
            KeySoundEnabled = true,
            MistakeSoundEnabled = true,
            ThemePreset = "Dark",
            DifficultyPreset = "Speed Words"
        };

        await database.SettingsRepository.SaveSettingsAsync(settings);
        var stored = await database.SettingsRepository.GetSettingsAsync();

        Assert.AreEqual("Paragraph", stored.DefaultLessonMode);
        Assert.AreEqual(333, stored.LessonLengthCharacters);
        Assert.IsTrue(stored.AllowNumbers);
        Assert.IsFalse(stored.AutoSaveCompletedSessions);
        Assert.IsTrue(stored.ZenModeEnabled);
        Assert.AreEqual(3, stored.CountdownSeconds);
        Assert.IsTrue(stored.KeySoundEnabled);
        Assert.IsTrue(stored.MistakeSoundEnabled);
        Assert.AreEqual("Dark", stored.ThemePreset);
        Assert.AreEqual("Speed Words", stored.DifficultyPreset);
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
    public async Task AppSettingsRepository_CountdownSeconds_ClampStoredValues()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        await database.SetRawSettingAsync("typing.countdownSeconds", "99");

        var settings = await database.SettingsRepository.GetSettingsAsync();

        Assert.AreEqual(3, settings.CountdownSeconds);
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
    public async Task AppSettingsRepository_SaveTrainingAndReadabilitySettings_Persists()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();

        await database.SettingsRepository.SaveSettingsAsync(AppSettings.Defaults with
        {
            GoalTrainingFocus = "Speed",
            GoalTargetSessionMinutes = 25,
            GoalTargetEssayWords = 1500,
            PracticeFontFamily = "Consolas",
            PracticeLineWidth = "Wide",
            PracticeTextContrast = "High",
            PracticeCursorStyle = "Bold"
        });
        var settings = await database.SettingsRepository.GetSettingsAsync();

        Assert.AreEqual("Speed", settings.GoalTrainingFocus);
        Assert.AreEqual(25, settings.GoalTargetSessionMinutes);
        Assert.AreEqual(1500, settings.GoalTargetEssayWords);
        Assert.AreEqual("Consolas", settings.PracticeFontFamily);
        Assert.AreEqual("Wide", settings.PracticeLineWidth);
        Assert.AreEqual("High", settings.PracticeTextContrast);
        Assert.AreEqual("Bold", settings.PracticeCursorStyle);
    }

    [TestMethod]
    public async Task LocalDataBackupService_BackupCreatesValidDatabaseCopy()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        var backupPath = Path.Combine(database.DirectoryPath, "backup.db");

        await database.BackupService.BackupAsync(backupPath);

        Assert.IsTrue(File.Exists(backupPath));
    }

    [TestMethod]
    public async Task LocalDataBackupService_RestoreRejectsInvalidDatabase()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        var invalidPath = database.CreateTextFile("invalid.db", "not sqlite");

        await Assert.ThrowsExceptionAsync<SqliteException>(() => database.BackupService.RestoreAsync(invalidPath));
    }

    [TestMethod]
    public async Task LocalDataBackupService_RestoreAcceptsValidBackup()
    {
        await using var database = await ContentTestDatabase.CreateInitializedAsync();
        var backupPath = Path.Combine(database.DirectoryPath, "valid-backup.db");
        await database.BackupService.BackupAsync(backupPath);

        await database.BackupService.RestoreAsync(backupPath);

        Assert.IsTrue(File.Exists(database.DatabasePath));
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
            DatabasePath = Path.Combine(directoryPath, "typingtrainer.db");
            ConnectionFactory = connectionFactory;
            ImportRepository = new ContentImportRepository(connectionFactory);
            ImportService = new TextFileImportService(ImportRepository);
            ContentQuery = new ContentQueryService(connectionFactory, new BuiltInParagraphProvider());
            SettingsRepository = new AppSettingsRepository(connectionFactory);
            BackupService = new LocalDataBackupService(new FixedDatabasePath(DatabasePath));
        }

        public string DirectoryPath { get; }

        public string DatabasePath { get; }

        public SqliteConnectionFactory ConnectionFactory { get; }

        public ContentImportRepository ImportRepository { get; }

        public TextFileImportService ImportService { get; }

        public ContentQueryService ContentQuery { get; }

        public AppSettingsRepository SettingsRepository { get; }

        public LocalDataBackupService BackupService { get; }

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
