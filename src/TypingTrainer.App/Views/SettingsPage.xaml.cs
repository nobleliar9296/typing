using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TypingTrainer.App.ViewModels;
using TypingTrainer.Data.Models;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace TypingTrainer.App.Views;

public sealed partial class SettingsPage : Page
{
    private bool _isLoaded;

    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = new SettingsViewModel(
            App.Services.AppSettingsRepository,
            App.Services.TextFileImportService,
            App.Services.ContentQueryService,
            App.Services.JsonExportService,
            App.Services.PracticeSessionRepository);
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
            _ => 0
        };
        _isLoaded = true;
    }

    private void PracticeButton_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(PracticePage));
    }

    private void DashboardButton_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(DashboardPage));
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
            _ => AppSettings.AutoLessonMode
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

    private async void ExportSessionsButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ExportSessionsAsync();
    }

    private async void DeleteHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.DeletePracticeHistoryAsync();
    }

    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SaveAsync();
    }
}
