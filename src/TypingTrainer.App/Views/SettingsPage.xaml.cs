using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        AppSettings.HighContrastThemePreset
    };

    private bool _isLoaded;

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
        await ViewModel.LoadAsync();
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
        DifficultyPresetComboBox.SelectedIndex = ViewModel.DifficultyPreset switch
        {
            "Speed Words" => 1,
            "Clean Copy" => 2,
            "Symbols" => 3,
            _ => 0
        };
        _isLoaded = true;
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
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ImportAsync();
    }

    private void CancelImportButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelImport();
    }

    private async void RefreshPacksButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshContentPacksAsync();
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
        await ViewModel.DeleteSelectedPackAsync();
    }

    private async void SavePackButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SaveSelectedPackAsync();
    }

    private async void PreviewPackButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.PreviewSelectedPackAsync();
    }

    private async void ExportSessionsButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ExportSessionsAsync();
    }

    private async void DeleteHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.DeletePracticeHistoryAsync();
    }

    private async void BackupDatabaseButton_Click(object sender, RoutedEventArgs e)
    {
        await App.Services.SessionPersistenceQueue.FlushAsync();
        await ViewModel.BackupDatabaseAsync();
    }

    private async void RestoreDatabaseButton_Click(object sender, RoutedEventArgs e)
    {
        await App.Services.SessionPersistenceQueue.FlushAsync();
        await ViewModel.RestoreDatabaseAsync();
    }

}
