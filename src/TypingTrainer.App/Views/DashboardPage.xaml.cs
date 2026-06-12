using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TypingTrainer.App.ViewModels;
using TypingTrainer.Data.Models;

namespace TypingTrainer.App.Views;

public sealed partial class DashboardPage : Page
{
    private bool _isLoaded;

    public DashboardPage()
    {
        InitializeComponent();
        ViewModel = new DashboardViewModel(
            App.Services.AnalyticsQueryService,
            App.Services.AppSettingsRepository);
        DataContext = ViewModel;
    }

    public DashboardViewModel ViewModel { get; }

    private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        await ViewModel.LoadAsync();
    }

    private async void SaveGoalsButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SaveGoalsAsync();
    }

    private void PracticeButton_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(PracticePage));
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(SettingsPage));
    }

    private async void RangeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        ViewModel.SelectedRange = RangeComboBox.SelectedIndex switch
        {
            1 => AnalyticsRange.Last30Days,
            2 => AnalyticsRange.AllTime,
            _ => AnalyticsRange.Last7Days
        };

        await ViewModel.LoadAsync();
    }
}
