using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TypingTrainer.App.ViewModels;

namespace TypingTrainer.App.Views;

public sealed partial class SessionDetailPage : Page
{
    public SessionDetailPage()
    {
        InitializeComponent();
        ViewModel = new SessionDetailViewModel(
            App.Services.SessionDetailQueryService,
            App.Services.AppSettingsRepository);
        DataContext = ViewModel;
    }

    public SessionDetailViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Guid sessionId)
        {
            await ViewModel.LoadAsync(sessionId);
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(typeof(DashboardPage));
    }

    private void ReplayMistakesButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(typeof(PracticePage));
    }

    private bool NavigateTo(Type pageType, object? parameter = null)
    {
        return MainWindow.Instance?.NavigateTo(pageType, parameter)
            ?? Frame.Navigate(pageType, parameter);
    }
}
