using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TypingTrainer.App.Services;
using TypingTrainer.App.ViewModels;
using TypingTrainer.Data.Models;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace TypingTrainer.App.Views;

public sealed partial class SettingsPage : Page
{
    private static readonly string[] ThemePresets =
    {
        AppSettings.SystemThemePreset,
        AppSettings.DarkThemePreset,
        AppSettings.LightThemePreset,
        AppSettings.InkThemePreset,
        AppSettings.ForestThemePreset,
        AppSettings.DawnThemePreset,
        AppSettings.MonochromeThemePreset,
        AppSettings.HighContrastThemePreset
    };

    private static readonly string[] CursorStyles =
    {
        AppSettings.UnderlineCursorStyle,
        AppSettings.BarCursorStyle,
        AppSettings.BlockCursorStyle,
        AppSettings.OutlineCursorStyle
    };

    private static readonly string[] FontFamilies = AppSettings.PracticeFontFamilies.ToArray();

    private bool _isLoaded;
    private readonly SettingsActionExecutor _actionExecutor = new();

    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = new SettingsViewModel(
            App.Services.AppSettingsRepository,
            App.Services.TextFileImportService,
            App.Services.ContentQueryService,
            App.Services.JsonExportService,
            App.Services.PracticeSessionRepository,
            App.Services.LocalDataBackupService);
        DataContext = ViewModel;
    }

    public SettingsViewModel ViewModel { get; }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        var loaded = await RunSettingsActionAsync(
            () => ViewModel.LoadAsync(),
            "Settings could not be loaded.",
            "SettingsPage.Load");
        if (!loaded)
        {
            return;
        }

        DefaultLessonModeComboBox.SelectedIndex = ViewModel.DefaultLessonMode switch
        {
            "Fixed" => 1,
            "Adaptive" => 2,
            "Paragraph" => 3,
            "WeakKeys" => 4,
            "WeakBigrams" => 5,
            "Review" => 6,
            "Clipboard" => 7,
            _ => 0
        };
        TrainingFocusComboBox.SelectedIndex = ViewModel.GoalTrainingFocus switch
        {
            "AccuracyFirst" or "Accuracy First" or "Accuracy" => 1,
            "SpeedFirst" or "Speed First" or "Speed" => 2,
            "WeakLeftHand" or "Weak Left Hand" => 3,
            "WeakRightHand" or "Weak Right Hand" => 4,
            "Punctuation" => 5,
            _ => 0
        };
        var themePresetIndex = Array.IndexOf(ThemePresets, ViewModel.ThemePreset);
        ThemePresetComboBox.SelectedIndex = themePresetIndex >= 0 ? themePresetIndex : 0;
        var cursorStyleIndex = Array.IndexOf(CursorStyles, AppSettings.NormalizeCursorStyle(ViewModel.PracticeCursorStyle));
        CursorStyleComboBox.SelectedIndex = cursorStyleIndex >= 0 ? cursorStyleIndex : 0;
        var fontFamilyIndex = Array.IndexOf(FontFamilies, AppSettings.NormalizePracticeFontFamily(ViewModel.PracticeFontFamily));
        FontFamilyComboBox.SelectedIndex = fontFamilyIndex >= 0 ? fontFamilyIndex : 0;
        DifficultyPresetComboBox.SelectedIndex = ViewModel.DifficultyPreset switch
        {
            "Speed Words" => 1,
            "Clean Copy" => 2,
            "Symbols" => 3,
            _ => 0
        };
        _isLoaded = true;
        ApplyResponsiveLayout(ActualWidth, ActualHeight);
    }

    private void SettingsPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout(e.NewSize.Width, e.NewSize.Height);
    }

    private void DefaultLessonModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        ViewModel.DefaultLessonMode = DefaultLessonModeComboBox.SelectedIndex switch
        {
            1 => "Fixed",
            2 => "Adaptive",
            3 => "Paragraph",
            4 => "WeakKeys",
            5 => "WeakBigrams",
            6 => "Review",
            7 => "Clipboard",
            _ => AppSettings.AutoLessonMode
        };
    }

    private void TrainingFocusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        ViewModel.GoalTrainingFocus = TrainingFocusComboBox.SelectedIndex switch
        {
            1 => "AccuracyFirst",
            2 => "SpeedFirst",
            3 => "WeakLeftHand",
            4 => "WeakRightHand",
            5 => "Punctuation",
            _ => "Balanced"
        };
    }

    private void ThemePresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        ViewModel.ThemePreset = ThemePresetComboBox.SelectedIndex >= 0 && ThemePresetComboBox.SelectedIndex < ThemePresets.Length
            ? ThemePresets[ThemePresetComboBox.SelectedIndex]
            : AppSettings.DefaultThemePreset;
        AppThemeService.Apply(ViewModel.ThemePreset);
    }

    private void CursorStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        ViewModel.PracticeCursorStyle = CursorStyleComboBox.SelectedIndex >= 0 && CursorStyleComboBox.SelectedIndex < CursorStyles.Length
            ? CursorStyles[CursorStyleComboBox.SelectedIndex]
            : AppSettings.DefaultCursorStyle;
    }

    private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        ViewModel.PracticeFontFamily = FontFamilyComboBox.SelectedIndex >= 0 && FontFamilyComboBox.SelectedIndex < FontFamilies.Length
            ? FontFamilies[FontFamilyComboBox.SelectedIndex]
            : AppSettings.DefaultFontFamily;
    }

    private void DifficultyPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        ViewModel.DifficultyPreset = DifficultyPresetComboBox.SelectedIndex switch
        {
            1 => "Speed Words",
            2 => "Clean Copy",
            3 => "Symbols",
            _ => "Custom"
        };
    }

    private async void BrowseImportButton_Click(object sender, RoutedEventArgs e)
    {
        await RunSettingsActionAsync(
            async () =>
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add(".txt");

                if (App.MainWindowInstance is not null)
                {
                    InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindowInstance));
                }

                var file = await picker.PickSingleFileAsync();
                if (file is not null)
                {
                    ViewModel.ImportFilePath = file.Path;
                }
            },
            "File selection failed. Try again.",
            "SettingsPage.BrowseImport");
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        await RunImportActionAsync(
            () => ViewModel.ImportAsync(),
            "Import failed. Check the selected file and try again.",
            "SettingsPage.Import");
    }

    private void CancelImportButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelImport();
    }

    private async void RefreshPacksButton_Click(object sender, RoutedEventArgs e)
    {
        await RunSettingsActionAsync(
            () => ViewModel.RefreshContentPacksAsync(),
            "Content packs could not be refreshed.",
            "SettingsPage.RefreshPacks");
    }

    private void ContentPacksListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ContentPacksListView.SelectedItem is ContentPackDisplayRow selected)
        {
            ViewModel.SelectedContentPack = ViewModel.ContentPacks.FirstOrDefault(pack => pack.Id == selected.Id);
        }
    }

    private async void DeletePackButton_Click(object sender, RoutedEventArgs e)
    {
        await RunSettingsActionAsync(
            () => ViewModel.DeleteSelectedPackAsync(),
            "Content pack could not be deleted.",
            "SettingsPage.DeletePack");
    }

    private async void SavePackButton_Click(object sender, RoutedEventArgs e)
    {
        await RunSettingsActionAsync(
            () => ViewModel.SaveSelectedPackAsync(),
            "Content pack could not be saved.",
            "SettingsPage.SavePack");
    }

    private async void PreviewPackButton_Click(object sender, RoutedEventArgs e)
    {
        await RunSettingsActionAsync(
            () => ViewModel.PreviewSelectedPackAsync(),
            "Content pack preview could not be loaded.",
            "SettingsPage.PreviewPack");
    }

    private async void ExportSessionsButton_Click(object sender, RoutedEventArgs e)
    {
        await RunSettingsActionAsync(
            () => ViewModel.ExportSessionsAsync(),
            "Export failed. Check the selected location and try again.",
            "SettingsPage.ExportSessions");
    }

    private async void DeleteHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        await RunSettingsActionAsync(
            () => ViewModel.DeletePracticeHistoryAsync(),
            "Practice history could not be deleted.",
            "SettingsPage.DeleteHistory");
    }

    private async void BackupDatabaseButton_Click(object sender, RoutedEventArgs e)
    {
        await RunSettingsActionAsync(
            async () =>
            {
                await App.Services.SessionPersistenceQueue.FlushAsync();
                await ViewModel.BackupDatabaseAsync();
            },
            "Backup failed. Check the selected location and try again.",
            "SettingsPage.BackupDatabase");
    }

    private async void RestoreDatabaseButton_Click(object sender, RoutedEventArgs e)
    {
        await RunSettingsActionAsync(
            async () =>
            {
                await App.Services.SessionPersistenceQueue.FlushAsync();
                await ViewModel.RestoreDatabaseAsync();
            },
            "Restore failed. Check the selected backup and try again.",
            "SettingsPage.RestoreDatabase");
    }

    private Task<bool> RunSettingsActionAsync(
        Func<Task> operation,
        string fallbackFailureStatus,
        string logSource)
    {
        return _actionExecutor.ExecuteAsync(
            operation,
            () => ViewModel.SettingsStatus,
            ViewModel.SetSettingsStatus,
            fallbackFailureStatus,
            logSource);
    }

    private Task<bool> RunImportActionAsync(
        Func<Task> operation,
        string fallbackFailureStatus,
        string logSource)
    {
        return _actionExecutor.ExecuteAsync(
            operation,
            () => ViewModel.ImportStatus,
            ViewModel.SetImportStatus,
            fallbackFailureStatus,
            logSource);
    }

    private void ApplyResponsiveLayout(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var metrics = AppResponsiveLayoutMetrics.FromViewport(width, height);
        var compact = metrics.CompactWidth;
        var narrow = metrics.NarrowWidth;
        var cardPadding = new Thickness(metrics.CardPadding);

        SettingsRootGrid.RowSpacing = metrics.RootRowSpacing;
        SettingsHeaderPanel.Margin = new Thickness(
            metrics.PageHorizontalPadding,
            metrics.PageTopPadding,
            metrics.PageHorizontalPadding,
            0);
        SettingsScrollViewer.Margin = new Thickness(
            metrics.PageHorizontalPadding,
            0,
            metrics.PageHorizontalPadding,
            metrics.PageBottomPadding);
        SettingsContentPanel.Spacing = metrics.CardSpacing;
        SettingsContentPanel.MaxWidth = metrics.MaxContentWidth;

        SettingsStatusCard.Padding = new Thickness(Math.Clamp(12 * metrics.Scale, 8, 12));
        SettingsSectionsGrid.ColumnSpacing = compact ? 0 : metrics.CardSpacing;
        SettingsSectionsGrid.RowSpacing = metrics.CardSpacing;
        SettingsPrimaryColumn.Width = new GridLength(1, GridUnitType.Star);
        SettingsSecondaryColumn.Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);

        PositionSettingsCards(compact);

        TypingSettingsCard.Padding = cardPadding;
        PracticeDisplayCard.Padding = cardPadding;
        ContentSettingsCard.Padding = cardPadding;
        DataSettingsCard.Padding = cardPadding;

        TypingSettingsGrid.ColumnSpacing = Math.Clamp(14 * metrics.Scale, 8, 14);
        TypingSettingsGrid.RowSpacing = Math.Clamp(12 * metrics.Scale, 8, 12);
        TypingSettingsLabelColumn.Width = new GridLength(metrics.FormLabelWidth);
        DefaultLessonModeComboBox.Width = metrics.FormControlWidth;
        TrainingFocusComboBox.Width = metrics.FormControlWidth;
        LessonLengthNumberBox.Width = metrics.CompactFormControlWidth;
        SessionMinutesNumberBox.Width = metrics.CompactFormControlWidth;
        EssayWordsNumberBox.Width = metrics.CompactFormControlWidth;

        DisplaySettingsGrid.ColumnSpacing = Math.Clamp(12 * metrics.Scale, 8, 12);
        DisplaySettingsGrid.RowSpacing = Math.Clamp(12 * metrics.Scale, 8, 12);
        DisplaySettingsLabelColumn.Width = new GridLength(Math.Min(150, metrics.FormLabelWidth));
        PracticeTextContrastTextBox.Width = metrics.CompactFormControlWidth;
        CursorStyleComboBox.Width = metrics.FilterControlWidth;
        ThemePresetComboBox.Width = metrics.FilterControlWidth;
        DifficultyPresetComboBox.Width = metrics.FilterControlWidth;

        var verticalOptions = metrics.VeryNarrowWidth;
        SoundOptionsPanel.Orientation = verticalOptions ? Orientation.Vertical : Orientation.Horizontal;
        FingerOptionsPanel.Orientation = narrow ? Orientation.Vertical : Orientation.Horizontal;
        ContrastCursorPanel.Orientation = narrow ? Orientation.Vertical : Orientation.Horizontal;
        ThemePanel.Orientation = narrow ? Orientation.Vertical : Orientation.Horizontal;
        ImportActionButtonsPanel.Orientation = narrow ? Orientation.Vertical : Orientation.Horizontal;
        DifficultyPanel.Orientation = narrow ? Orientation.Vertical : Orientation.Horizontal;
        ContentSourcePanel.Orientation = narrow ? Orientation.Vertical : Orientation.Horizontal;
        AllowedCharactersPanel.Orientation = narrow ? Orientation.Vertical : Orientation.Horizontal;
        ExportButtonsPanel.Orientation = narrow ? Orientation.Vertical : Orientation.Horizontal;
        BackupButtonsPanel.Orientation = narrow ? Orientation.Vertical : Orientation.Horizontal;

        ContentPacksListView.MaxHeight = metrics.TableMaxHeight;
        ContentPreviewListView.MaxHeight = metrics.ShortHeight ? 140 : 180;

        ApplyStatusLayout(narrow);
        ApplyImportLayout(narrow);
    }

    private void PositionSettingsCards(bool compact)
    {
        if (compact)
        {
            Grid.SetRow(TypingSettingsCard, 0);
            Grid.SetColumn(TypingSettingsCard, 0);
            Grid.SetRow(PracticeDisplayCard, 1);
            Grid.SetColumn(PracticeDisplayCard, 0);
            Grid.SetRow(ContentSettingsCard, 2);
            Grid.SetColumn(ContentSettingsCard, 0);
            Grid.SetRow(DataSettingsCard, 3);
            Grid.SetColumn(DataSettingsCard, 0);
        }
        else
        {
            Grid.SetRow(TypingSettingsCard, 0);
            Grid.SetColumn(TypingSettingsCard, 0);
            Grid.SetRow(PracticeDisplayCard, 0);
            Grid.SetColumn(PracticeDisplayCard, 1);
            Grid.SetRow(ContentSettingsCard, 1);
            Grid.SetColumn(ContentSettingsCard, 0);
            Grid.SetRow(DataSettingsCard, 1);
            Grid.SetColumn(DataSettingsCard, 1);
        }
    }

    private void ApplyStatusLayout(bool narrow)
    {
        if (narrow)
        {
            SettingsStatusInfoColumn.Width = new GridLength(1, GridUnitType.Star);
            SettingsStatusTextColumn.Width = new GridLength(1, GridUnitType.Star);
            Grid.SetRow(SettingsStatusTextBlock, 1);
            Grid.SetColumn(SettingsStatusTextBlock, 0);
        }
        else
        {
            SettingsStatusInfoColumn.Width = new GridLength(1, GridUnitType.Star);
            SettingsStatusTextColumn.Width = GridLength.Auto;
            Grid.SetRow(SettingsStatusTextBlock, 0);
            Grid.SetColumn(SettingsStatusTextBlock, 1);
        }
    }

    private void ApplyImportLayout(bool narrow)
    {
        if (narrow)
        {
            Grid.SetRow(BrowseImportButton, 1);
            Grid.SetColumn(BrowseImportButton, 0);
            BrowseImportButton.HorizontalAlignment = HorizontalAlignment.Left;

            ImportPreviewOriginalColumn.Width = new GridLength(1, GridUnitType.Star);
            ImportPreviewCleanedColumn.Width = new GridLength(0);
            Grid.SetRow(ImportCleanedPreviewTextBox, 1);
            Grid.SetColumn(ImportCleanedPreviewTextBox, 0);
        }
        else
        {
            Grid.SetRow(BrowseImportButton, 0);
            Grid.SetColumn(BrowseImportButton, 1);
            BrowseImportButton.HorizontalAlignment = HorizontalAlignment.Stretch;

            ImportPreviewOriginalColumn.Width = new GridLength(1, GridUnitType.Star);
            ImportPreviewCleanedColumn.Width = new GridLength(1, GridUnitType.Star);
            Grid.SetRow(ImportCleanedPreviewTextBox, 0);
            Grid.SetColumn(ImportCleanedPreviewTextBox, 1);
        }
    }
}
