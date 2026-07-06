using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using TypingTrainer.App.Views;

namespace TypingTrainer.App;

public sealed partial class MainWindow : Window
{
    public static MainWindow? Instance { get; private set; }

#if DEBUG
    private readonly Services.DpiDebugLogger _dpiDebugLogger;
#endif

    public MainWindow()
    {
        InitializeComponent();
        Instance = this;
        TopBar.NavigateRequested += TopBar_NavigateRequested;
        RootFrame.Navigated += RootFrame_Navigated;
#if DEBUG
        _dpiDebugLogger = Services.DpiDebugLogger.Attach(this);
#endif
        NavigateToCore(typeof(PracticePage), null, useTransition: false);
    }

    public bool NavigateTo(Type pageType, object? parameter = null)
    {
        return NavigateToCore(pageType, parameter, useTransition: true);
    }

    private bool NavigateToCore(Type pageType, object? parameter, bool useTransition)
    {
        if (RootFrame.CurrentSourcePageType == pageType && parameter is null)
        {
            return false;
        }

        var transition = useTransition
            ? CreateNavigationTransition(RootFrame.CurrentSourcePageType, pageType)
            : new SuppressNavigationTransitionInfo();
        return RootFrame.Navigate(pageType, parameter, transition);
    }

    private void TopBar_NavigateRequested(Type pageType)
    {
        NavigateTo(pageType);
    }

    private void RootFrame_Navigated(object sender, NavigationEventArgs e)
    {
        TopBar.ActivePage = GetActivePageName(RootFrame.CurrentSourcePageType);
    }

    private static NavigationTransitionInfo CreateNavigationTransition(Type? currentPageType, Type targetPageType)
    {
        if (currentPageType is null || currentPageType == targetPageType)
        {
            return new SuppressNavigationTransitionInfo();
        }

        var effect = GetNavigationOrder(targetPageType) > GetNavigationOrder(currentPageType)
            ? SlideNavigationTransitionEffect.FromRight
            : SlideNavigationTransitionEffect.FromLeft;
        return new SlideNavigationTransitionInfo { Effect = effect };
    }

    private static int GetNavigationOrder(Type pageType)
    {
        if (pageType == typeof(PracticePage))
        {
            return 0;
        }

        if (pageType == typeof(DashboardPage))
        {
            return 1;
        }

        if (pageType == typeof(SessionDetailPage))
        {
            return 2;
        }

        return pageType == typeof(SettingsPage) ? 3 : 1;
    }

    private static string GetActivePageName(Type? pageType)
    {
        if (pageType == typeof(PracticePage))
        {
            return "Practice";
        }

        if (pageType == typeof(SettingsPage))
        {
            return "Settings";
        }

        return "Dashboard";
    }
}
