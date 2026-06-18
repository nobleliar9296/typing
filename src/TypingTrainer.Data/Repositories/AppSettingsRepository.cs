using Microsoft.Data.Sqlite;
using TypingTrainer.Data.Database;
using TypingTrainer.Data.Models;

namespace TypingTrainer.Data.Repositories;

public sealed class AppSettingsRepository : IAppSettingsRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public AppSettingsRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT key, value FROM app_settings;";

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            values[reader.GetString(0)] = reader.GetString(1);
        }

        var defaults = AppSettings.Defaults;
        return new AppSettings(
            GetString(values, "DefaultLessonMode", defaults.DefaultLessonMode),
            GetClampedInt(values, "LessonLengthCharacters", defaults.LessonLengthCharacters, 20, 5000),
            GetBool(values, "AllowCapitalLetters", defaults.AllowCapitalLetters),
            GetBool(values, "AllowNumbers", defaults.AllowNumbers),
            GetBool(values, "AllowPunctuation", defaults.AllowPunctuation),
            GetBool(values, "UseImportedContent", defaults.UseImportedContent),
            GetBool(values, "UseBuiltInContent", defaults.UseBuiltInContent),
            GetBool(values, "BackspaceAllowed", defaults.BackspaceAllowed),
            GetBool(values, "AutoSaveCompletedSessions", defaults.AutoSaveCompletedSessions),
            GetBool(values, "typing.requireCorrectKeyToAdvance", defaults.RequireCorrectKeyToAdvance),
            GetBool(values, "typing.zenMode", defaults.ZenModeEnabled),
            GetClampedInt(values, "typing.countdownSeconds", defaults.CountdownSeconds, 0, 3),
            GetBool(values, "typing.keySoundEnabled", defaults.KeySoundEnabled),
            GetBool(values, "typing.mistakeSoundEnabled", defaults.MistakeSoundEnabled),
            GetBool(values, "visualKeyboard.showKeyboard", defaults.ShowVisualKeyboard),
            GetBool(values, "visualKeyboard.showFingerColors", defaults.ShowFingerColors),
            GetBool(values, "visualKeyboard.showFingerLabels", defaults.ShowFingerLabels),
            GetString(values, "visualKeyboard.layout", defaults.VisualKeyboardLayout),
            GetClampedInt(values, "practice.textScalePercent", defaults.PracticeTextScalePercent, 70, 130),
            GetClampedInt(values, "visualKeyboard.scalePercent", defaults.VisualKeyboardScalePercent, 70, 130),
            GetClampedInt(values, "goals.targetNetWpm", defaults.GoalTargetNetWpm, 10, 250),
            GetClampedInt(values, "goals.targetAccuracyPercent", defaults.GoalTargetAccuracyPercent, 50, 100),
            GetClampedInt(values, "goals.weeklyPracticeMinutes", defaults.GoalWeeklyPracticeMinutes, 0, 10_080),
            GetBool(values, "content.normalizeImportedTextToAscii", defaults.NormalizeImportedTextToAscii),
            GetBool(values, "content.lowercaseImportedText", defaults.LowercaseImportedText),
            GetBool(values, "content.normalizeWhitespace", defaults.NormalizeImportedWhitespace),
            GetString(values, "goals.trainingFocus", defaults.GoalTrainingFocus),
            GetClampedInt(values, "goals.targetSessionMinutes", defaults.GoalTargetSessionMinutes, 5, 60),
            GetClampedInt(values, "goals.targetEssayWords", defaults.GoalTargetEssayWords, 100, 3000),
            AppSettings.NormalizePracticeFontFamily(GetString(values, "practice.fontFamily", defaults.PracticeFontFamily)),
            GetString(values, "practice.lineWidth", defaults.PracticeLineWidth),
            GetString(values, "practice.textContrast", defaults.PracticeTextContrast),
            AppSettings.NormalizeCursorStyle(GetString(values, "practice.cursorStyle", defaults.PracticeCursorStyle)),
            GetString(values, "practice.themePreset", defaults.ThemePreset),
            GetString(values, "practice.difficultyPreset", defaults.DifficultyPreset));
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await UpsertAsync(connection, (SqliteTransaction)transaction, "DefaultLessonMode", settings.DefaultLessonMode, cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "LessonLengthCharacters", settings.LessonLengthCharacters.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "AllowCapitalLetters", Bool(settings.AllowCapitalLetters), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "AllowNumbers", Bool(settings.AllowNumbers), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "AllowPunctuation", Bool(settings.AllowPunctuation), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "UseImportedContent", Bool(settings.UseImportedContent), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "UseBuiltInContent", Bool(settings.UseBuiltInContent), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "BackspaceAllowed", Bool(settings.BackspaceAllowed), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "AutoSaveCompletedSessions", Bool(settings.AutoSaveCompletedSessions), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "typing.requireCorrectKeyToAdvance", Bool(settings.RequireCorrectKeyToAdvance), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "typing.zenMode", Bool(settings.ZenModeEnabled), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "typing.countdownSeconds", settings.CountdownSeconds.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "typing.keySoundEnabled", Bool(settings.KeySoundEnabled), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "typing.mistakeSoundEnabled", Bool(settings.MistakeSoundEnabled), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "visualKeyboard.showKeyboard", Bool(settings.ShowVisualKeyboard), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "visualKeyboard.showFingerColors", Bool(settings.ShowFingerColors), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "visualKeyboard.showFingerLabels", Bool(settings.ShowFingerLabels), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "visualKeyboard.layout", settings.VisualKeyboardLayout, cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "practice.textScalePercent", settings.PracticeTextScalePercent.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "visualKeyboard.scalePercent", settings.VisualKeyboardScalePercent.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "goals.targetNetWpm", settings.GoalTargetNetWpm.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "goals.targetAccuracyPercent", settings.GoalTargetAccuracyPercent.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "goals.weeklyPracticeMinutes", settings.GoalWeeklyPracticeMinutes.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "content.normalizeImportedTextToAscii", Bool(settings.NormalizeImportedTextToAscii), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "content.lowercaseImportedText", Bool(settings.LowercaseImportedText), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "content.normalizeWhitespace", Bool(settings.NormalizeImportedWhitespace), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "goals.trainingFocus", settings.GoalTrainingFocus, cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "goals.targetSessionMinutes", settings.GoalTargetSessionMinutes.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "goals.targetEssayWords", settings.GoalTargetEssayWords.ToString(), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "practice.fontFamily", AppSettings.NormalizePracticeFontFamily(settings.PracticeFontFamily), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "practice.lineWidth", settings.PracticeLineWidth, cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "practice.textContrast", settings.PracticeTextContrast, cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "practice.cursorStyle", AppSettings.NormalizeCursorStyle(settings.PracticeCursorStyle), cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "practice.themePreset", settings.ThemePreset, cancellationToken).ConfigureAwait(false);
        await UpsertAsync(connection, (SqliteTransaction)transaction, "practice.difficultyPreset", settings.DifficultyPreset, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string key,
        string value,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO app_settings (key, value)
            VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string GetString(Dictionary<string, string> values, string key, string fallback)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static int GetClampedInt(
        Dictionary<string, string> values,
        string key,
        int fallback,
        int minimum,
        int maximum)
    {
        return values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? Math.Clamp(parsed, minimum, maximum)
            : fallback;
    }

    private static bool GetBool(Dictionary<string, string> values, string key, bool fallback)
    {
        return values.TryGetValue(key, out var value)
            ? value == "1" || bool.TryParse(value, out var parsed) && parsed
            : fallback;
    }

    private static string Bool(bool value)
    {
        return value ? "1" : "0";
    }
}
