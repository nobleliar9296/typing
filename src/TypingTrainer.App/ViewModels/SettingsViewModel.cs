using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
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
    private CancellationTokenSource? _importCancellation;
    private AppSettings _settings = AppSettings.Defaults;
    private IReadOnlyList<ContentPackRow> _contentPacks = Array.Empty<ContentPackRow>();
    private ContentPackRow? _selectedContentPack;
    private string _importFilePath = string.Empty;
    private string _importPackName = string.Empty;
    private string _importStatus = string.Empty;
    private bool _isImporting;
    private string _settingsStatus = string.Empty;
    private string _exportPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TypingTrainer",
        "sessions-export.json");

    public SettingsViewModel(
        IAppSettingsRepository settingsRepository,
        ITextFileImportService textFileImportService,
        IContentQueryService contentQueryService,
        IJsonExportService jsonExportService,
        IPracticeSessionRepository practiceSessionRepository)
    {
        _settingsRepository = settingsRepository;
        _textFileImportService = textFileImportService;
        _contentQueryService = contentQueryService;
        _jsonExportService = jsonExportService;
        _practiceSessionRepository = practiceSessionRepository;
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
            pack.CreatedAtUtc.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.InvariantCulture)))
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
                OnPropertyChanged();
            }
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

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _settings = await _settingsRepository.GetSettingsAsync(cancellationToken);
        await RefreshContentPacksAsync(cancellationToken);
        OnAllSettingsChanged();
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
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
                    NormalizeWhitespace: true,
                    LowercaseWhenImported: false),
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
        await RefreshContentPacksAsync(cancellationToken);
        SettingsStatus = "Content pack deleted.";
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

    private void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
        OnAllSettingsChanged();
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
    string CreatedAt);
