using Microsoft.UI.Xaml;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using TypingTrainer.Data.Content;
using TypingTrainer.Data.Models;
using TypingTrainer.Data.Repositories;
using TypingTrainer.Data.Services;

namespace TypingTrainer.App.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly IAppSettingsRepository _settingsRepository;
    private readonly ITextFileImportService _textFileImportService;
    private readonly IContentQueryService _contentQueryService;
    private readonly IJsonExportService _jsonExportService;
    private readonly IPracticeSessionRepository _practiceSessionRepository;
    private readonly ILocalDataBackupService _localDataBackupService;
    private CancellationTokenSource? _importCancellation;
    private AppSettings _settings = AppSettings.Defaults;
    private IReadOnlyList<ContentPackRow> _contentPacks = Array.Empty<ContentPackRow>();
    private ContentPackRow? _selectedContentPack;
    private string _importFilePath = string.Empty;
    private string _importPackName = string.Empty;
    private string _importStatus = string.Empty;
    private bool _isImporting;
    private bool _isLoadingSettings;
    private bool _hasLoadedSettings;
    private CancellationTokenSource? _settingsAutosaveCancellation;
    private string _settingsStatus = string.Empty;
    private string _selectedContentPackName = string.Empty;
    private bool _selectedContentPackEnabled;
    private IReadOnlyList<ContentPreviewRow> _contentPreviewRows = Array.Empty<ContentPreviewRow>();
    private string _backupPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TypingTrainer",
        "typingtrainer-backup.db");
    private string _exportPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TypingTrainer",
        "sessions-export.json");

    public SettingsViewModel(
        IAppSettingsRepository settingsRepository,
        ITextFileImportService textFileImportService,
        IContentQueryService contentQueryService,
        IJsonExportService jsonExportService,
        IPracticeSessionRepository practiceSessionRepository,
        ILocalDataBackupService localDataBackupService)
    {
        _settingsRepository = settingsRepository;
        _textFileImportService = textFileImportService;
        _contentQueryService = contentQueryService;
        _jsonExportService = jsonExportService;
        _practiceSessionRepository = practiceSessionRepository;
        _localDataBackupService = localDataBackupService;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DefaultLessonMode
    {
        get => _settings.DefaultLessonMode;
        set => UpdateSettings(_settings with { DefaultLessonMode = value });
    }

    public int LessonLengthCharacters
    {
        get => _settings.LessonLengthCharacters;
        set => UpdateSettings(_settings with { LessonLengthCharacters = Math.Clamp(value, 20, 5000) });
    }

    public bool AllowCapitalLetters
    {
        get => _settings.AllowCapitalLetters;
        set => UpdateSettings(_settings with { AllowCapitalLetters = value });
    }

    public bool AllowNumbers
    {
        get => _settings.AllowNumbers;
        set => UpdateSettings(_settings with { AllowNumbers = value });
    }

    public bool AllowPunctuation
    {
        get => _settings.AllowPunctuation;
        set => UpdateSettings(_settings with { AllowPunctuation = value });
    }

    public bool UseImportedContent
    {
        get => _settings.UseImportedContent;
        set => UpdateSettings(_settings with { UseImportedContent = value });
    }

    public bool UseBuiltInContent
    {
        get => _settings.UseBuiltInContent;
        set => UpdateSettings(_settings with { UseBuiltInContent = value });
    }

    public bool BackspaceAllowed
    {
        get => _settings.BackspaceAllowed;
        set => UpdateSettings(_settings with { BackspaceAllowed = value });
    }

    public bool AutoSaveCompletedSessions
    {
        get => _settings.AutoSaveCompletedSessions;
        set => UpdateSettings(_settings with { AutoSaveCompletedSessions = value });
    }

    public bool RequireCorrectKeyToAdvance
    {
        get => _settings.RequireCorrectKeyToAdvance;
        set => UpdateSettings(_settings with { RequireCorrectKeyToAdvance = value });
    }

    public bool ShowVisualKeyboard
    {
        get => _settings.ShowVisualKeyboard;
        set => UpdateSettings(_settings with { ShowVisualKeyboard = value });
    }

    public bool ShowFingerColors
    {
        get => _settings.ShowFingerColors;
        set => UpdateSettings(_settings with { ShowFingerColors = value });
    }

    public bool ShowFingerLabels
    {
        get => _settings.ShowFingerLabels;
        set => UpdateSettings(_settings with { ShowFingerLabels = value });
    }

    public string VisualKeyboardLayout
    {
        get => _settings.VisualKeyboardLayout;
        set => UpdateSettings(_settings with { VisualKeyboardLayout = value });
    }

    public int PracticeTextScalePercent
    {
        get => _settings.PracticeTextScalePercent;
        set => UpdateSettings(_settings with { PracticeTextScalePercent = Math.Clamp(value, 70, 130) });
    }

    public int VisualKeyboardScalePercent
    {
        get => _settings.VisualKeyboardScalePercent;
        set => UpdateSettings(_settings with { VisualKeyboardScalePercent = Math.Clamp(value, 70, 130) });
    }

    public string PracticeTextScaleText => $"{PracticeTextScalePercent}%";

    public string VisualKeyboardScaleText => $"{VisualKeyboardScalePercent}%";

    public string GoalTrainingFocus
    {
        get => _settings.GoalTrainingFocus;
        set => UpdateSettings(_settings with { GoalTrainingFocus = value });
    }

    public int GoalTargetSessionMinutes
    {
        get => _settings.GoalTargetSessionMinutes;
        set => UpdateSettings(_settings with { GoalTargetSessionMinutes = Math.Clamp(value, 5, 60) });
    }

    public int GoalTargetEssayWords
    {
        get => _settings.GoalTargetEssayWords;
        set => UpdateSettings(_settings with { GoalTargetEssayWords = Math.Clamp(value, 100, 3000) });
    }

    public string PracticeFontFamily
    {
        get => _settings.PracticeFontFamily;
        set => UpdateSettings(_settings with { PracticeFontFamily = value });
    }

    public string PracticeLineWidth
    {
        get => _settings.PracticeLineWidth;
        set => UpdateSettings(_settings with { PracticeLineWidth = value });
    }

    public string PracticeTextContrast
    {
        get => _settings.PracticeTextContrast;
        set => UpdateSettings(_settings with { PracticeTextContrast = value });
    }

    public string PracticeCursorStyle
    {
        get => _settings.PracticeCursorStyle;
        set => UpdateSettings(_settings with { PracticeCursorStyle = value });
    }

    public bool NormalizeImportedTextToAscii
    {
        get => _settings.NormalizeImportedTextToAscii;
        set => UpdateSettings(_settings with { NormalizeImportedTextToAscii = value });
    }

    public bool LowercaseImportedText
    {
        get => _settings.LowercaseImportedText;
        set => UpdateSettings(_settings with { LowercaseImportedText = value });
    }

    public bool NormalizeImportedWhitespace
    {
        get => _settings.NormalizeImportedWhitespace;
        set => UpdateSettings(_settings with { NormalizeImportedWhitespace = value });
    }

    public string ImportFilePath
    {
        get => _importFilePath;
        set
        {
            if (_importFilePath != value)
            {
                _importFilePath = value;
                if (string.IsNullOrWhiteSpace(ImportPackName) && !string.IsNullOrWhiteSpace(value))
                {
                    ImportPackName = Path.GetFileNameWithoutExtension(value);
                }

                OnPropertyChanged();
            }
        }
    }

    public string ImportPackName
    {
        get => _importPackName;
        set
        {
            if (_importPackName != value)
            {
                _importPackName = value;
                OnPropertyChanged();
            }
        }
    }

    public string ImportStatus
    {
        get => _importStatus;
        private set
        {
            if (_importStatus != value)
            {
                _importStatus = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsImporting
    {
        get => _isImporting;
        private set
        {
            if (_isImporting != value)
            {
                _isImporting = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ImportProgressVisibility));
            }
        }
    }

    public Visibility ImportProgressVisibility => IsImporting ? Visibility.Visible : Visibility.Collapsed;

    public IReadOnlyList<ContentPackDisplayRow> ContentPackRows => ContentPacks
        .Select(pack => new ContentPackDisplayRow(
            pack.Id,
            pack.Name,
            pack.SourceFileName ?? "-",
            pack.ParagraphCount.ToString(CultureInfo.InvariantCulture),
            pack.FileSizeBytes is long bytes ? FormatFileSize(bytes) : "-",
            pack.CreatedAtUtc.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
            pack.Enabled ? "Enabled" : "Disabled"))
        .ToArray();

    public IReadOnlyList<ContentPackRow> ContentPacks
    {
        get => _contentPacks;
        private set
        {
            _contentPacks = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ContentPackRows));
        }
    }

    public ContentPackRow? SelectedContentPack
    {
        get => _selectedContentPack;
        set
        {
            if (!Equals(_selectedContentPack, value))
            {
                _selectedContentPack = value;
                _selectedContentPackName = value?.Name ?? string.Empty;
                _selectedContentPackEnabled = value?.Enabled ?? false;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedContentPackName));
                OnPropertyChanged(nameof(SelectedContentPackEnabled));
            }
        }
    }

    public string SelectedContentPackName
    {
        get => _selectedContentPackName;
        set
        {
            if (_selectedContentPackName != value)
            {
                _selectedContentPackName = value;
                OnPropertyChanged();
            }
        }
    }

    public bool SelectedContentPackEnabled
    {
        get => _selectedContentPackEnabled;
        set
        {
            if (_selectedContentPackEnabled != value)
            {
                _selectedContentPackEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public IReadOnlyList<ContentPreviewRow> ContentPreviewRows
    {
        get => _contentPreviewRows;
        private set
        {
            _contentPreviewRows = value;
            OnPropertyChanged();
        }
    }

    public string SettingsStatus
    {
        get => _settingsStatus;
        private set
        {
            if (_settingsStatus != value)
            {
                _settingsStatus = value;
                OnPropertyChanged();
            }
        }
    }

    public string ExportPath
    {
        get => _exportPath;
        set
        {
            if (_exportPath != value)
            {
                _exportPath = value;
                OnPropertyChanged();
            }
        }
    }

    public string BackupPath
    {
        get => _backupPath;
        set
        {
            if (_backupPath != value)
            {
                _backupPath = value;
                OnPropertyChanged();
            }
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _isLoadingSettings = true;
        try
        {
            _settings = await _settingsRepository.GetSettingsAsync(cancellationToken);
            await RefreshContentPacksAsync(cancellationToken);
            _hasLoadedSettings = true;
            SettingsStatus = "Settings autosave is on.";
            OnAllSettingsChanged();
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        _settingsAutosaveCancellation?.Cancel();
        SettingsStatus = "Saving...";
        await _settingsRepository.SaveSettingsAsync(_settings, cancellationToken);
        SettingsStatus = "Settings saved.";
    }

    public async Task RefreshContentPacksAsync(CancellationToken cancellationToken = default)
    {
        ContentPacks = await _contentQueryService.GetContentPacksAsync(cancellationToken);
    }

    public async Task ImportAsync(CancellationToken cancellationToken = default)
    {
        if (IsImporting || string.IsNullOrWhiteSpace(ImportFilePath))
        {
            return;
        }

        _importCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsImporting = true;
        ImportStatus = "Starting import...";

        try
        {
            var progress = new Progress<TextImportProgress>(value =>
            {
                ImportStatus = value.TotalBytes is long totalBytes && totalBytes > 0
                    ? $"Imported {value.ParagraphsImported} paragraphs ({value.BytesRead * 100 / totalBytes}%)."
                    : $"Imported {value.ParagraphsImported} paragraphs.";
            });
            var result = await _textFileImportService.ImportTextFileAsync(
                ImportFilePath,
                new TextImportOptions(
                    string.IsNullOrWhiteSpace(ImportPackName) ? Path.GetFileNameWithoutExtension(ImportFilePath) : ImportPackName,
                    MinParagraphCharacters: 80,
                    MaxParagraphCharacters: 900,
                    NormalizeWhitespace: _settings.NormalizeImportedWhitespace,
                    LowercaseWhenImported: _settings.LowercaseImportedText,
                    NormalizeToAscii: _settings.NormalizeImportedTextToAscii),
                progress,
                _importCancellation.Token);

            ImportStatus = $"Imported {result.ParagraphsImported} paragraphs.";
            await RefreshContentPacksAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            ImportStatus = "Import canceled.";
        }
        catch (Exception ex)
        {
            ImportStatus = $"Import failed: {ex.Message}";
        }
        finally
        {
            IsImporting = false;
            _importCancellation.Dispose();
            _importCancellation = null;
        }
    }

    public void CancelImport()
    {
        _importCancellation?.Cancel();
    }

    public async Task DeleteSelectedPackAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedContentPack is null)
        {
            return;
        }

        await _contentQueryService.DeleteContentPackAsync(SelectedContentPack.Id, cancellationToken);
        SelectedContentPack = null;
        ContentPreviewRows = Array.Empty<ContentPreviewRow>();
        await RefreshContentPacksAsync(cancellationToken);
        SettingsStatus = "Content pack deleted.";
    }

    public async Task SaveSelectedPackAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedContentPack is null)
        {
            return;
        }

        await _contentQueryService.RenameContentPackAsync(SelectedContentPack.Id, SelectedContentPackName, cancellationToken);
        await _contentQueryService.SetContentPackEnabledAsync(SelectedContentPack.Id, SelectedContentPackEnabled, cancellationToken);
        await RefreshContentPacksAsync(cancellationToken);
        SettingsStatus = "Content pack updated.";
    }

    public async Task PreviewSelectedPackAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedContentPack is null)
        {
            ContentPreviewRows = Array.Empty<ContentPreviewRow>();
            return;
        }

        var preview = await _contentQueryService.GetContentPackPreviewAsync(SelectedContentPack.Id, 5, cancellationToken);
        ContentPreviewRows = preview
            .Select(item => new ContentPreviewRow(item.Title, item.Text))
            .ToArray();
    }

    public async Task ExportSessionsAsync(CancellationToken cancellationToken = default)
    {
        await _jsonExportService.ExportAllSessionsAsync(ExportPath, cancellationToken);
        SettingsStatus = "Sessions exported.";
    }

    public async Task DeletePracticeHistoryAsync(CancellationToken cancellationToken = default)
    {
        await _practiceSessionRepository.DeleteAllAsync(cancellationToken);
        SettingsStatus = "Practice history deleted.";
    }

    public async Task BackupDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await _localDataBackupService.BackupAsync(BackupPath, cancellationToken);
        SettingsStatus = "Database backup saved.";
    }

    public async Task RestoreDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await _localDataBackupService.RestoreAsync(BackupPath, cancellationToken);
        SettingsStatus = "Database restored. Restart the app to reload all services.";
    }

    private void UpdateSettings(AppSettings settings)
    {
        if (_settings == settings)
        {
            return;
        }

        _settings = settings;
        OnAllSettingsChanged();
        QueueSettingsAutosave();
    }

    private void OnAllSettingsChanged()
    {
        OnPropertyChanged(nameof(DefaultLessonMode));
        OnPropertyChanged(nameof(LessonLengthCharacters));
        OnPropertyChanged(nameof(AllowCapitalLetters));
        OnPropertyChanged(nameof(AllowNumbers));
        OnPropertyChanged(nameof(AllowPunctuation));
        OnPropertyChanged(nameof(UseImportedContent));
        OnPropertyChanged(nameof(UseBuiltInContent));
        OnPropertyChanged(nameof(BackspaceAllowed));
        OnPropertyChanged(nameof(AutoSaveCompletedSessions));
        OnPropertyChanged(nameof(RequireCorrectKeyToAdvance));
        OnPropertyChanged(nameof(ShowVisualKeyboard));
        OnPropertyChanged(nameof(ShowFingerColors));
        OnPropertyChanged(nameof(ShowFingerLabels));
        OnPropertyChanged(nameof(VisualKeyboardLayout));
        OnPropertyChanged(nameof(PracticeTextScalePercent));
        OnPropertyChanged(nameof(VisualKeyboardScalePercent));
        OnPropertyChanged(nameof(PracticeTextScaleText));
        OnPropertyChanged(nameof(VisualKeyboardScaleText));
        OnPropertyChanged(nameof(GoalTrainingFocus));
        OnPropertyChanged(nameof(GoalTargetSessionMinutes));
        OnPropertyChanged(nameof(GoalTargetEssayWords));
        OnPropertyChanged(nameof(PracticeFontFamily));
        OnPropertyChanged(nameof(PracticeLineWidth));
        OnPropertyChanged(nameof(PracticeTextContrast));
        OnPropertyChanged(nameof(PracticeCursorStyle));
        OnPropertyChanged(nameof(NormalizeImportedTextToAscii));
        OnPropertyChanged(nameof(LowercaseImportedText));
        OnPropertyChanged(nameof(NormalizeImportedWhitespace));
    }

    private static string FormatFileSize(long bytes)
    {
        return bytes >= 1024 * 1024
            ? $"{bytes / (1024.0 * 1024.0):0.0} MB"
            : bytes >= 1024
                ? $"{bytes / 1024.0:0.0} KB"
                : $"{bytes} B";
    }

    private void QueueSettingsAutosave()
    {
        if (_isLoadingSettings || !_hasLoadedSettings)
        {
            return;
        }

        SettingsStatus = "Saving...";
        _settingsAutosaveCancellation?.Cancel();

        var cancellation = new CancellationTokenSource();
        _settingsAutosaveCancellation = cancellation;
        _ = SaveSettingsAfterDebounceAsync(cancellation);
    }

    private async Task SaveSettingsAfterDebounceAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(500, cancellation.Token);
            await _settingsRepository.SaveSettingsAsync(_settings, cancellation.Token);

            if (ReferenceEquals(_settingsAutosaveCancellation, cancellation))
            {
                SettingsStatus = "Settings saved.";
            }
        }
        catch (OperationCanceledException)
        {
            // A newer settings change replaced this pending save.
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(_settingsAutosaveCancellation, cancellation))
            {
                SettingsStatus = $"Settings autosave failed: {ex.Message}";
            }
        }
        finally
        {
            if (ReferenceEquals(_settingsAutosaveCancellation, cancellation))
            {
                _settingsAutosaveCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record ContentPackDisplayRow(
    Guid Id,
    string Name,
    string SourceFileName,
    string ParagraphCount,
    string FileSize,
    string CreatedAt,
    string Enabled);

public sealed record ContentPreviewRow(
    string Title,
    string Text);
