using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using TypingTrainer.App.Views;

namespace TypingTrainer.App.Controls;

public sealed partial class AppTopBar : UserControl
{
    public static readonly DependencyProperty ActivePageProperty = DependencyProperty.Register(
        nameof(ActivePage),
        typeof(string),
        typeof(AppTopBar),
        new PropertyMetadata(string.Empty, OnActivePageChanged));

    public AppTopBar()
    {
        InitializeComponent();
        UpdateActivePage();
    }

    public string ActivePage
    {
        get => (string)GetValue(ActivePageProperty);
        set => SetValue(ActivePageProperty, value);
    }

    private static void OnActivePageChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is AppTopBar topBar)
        {
            topBar.UpdateActivePage();
        }
    }

    private void PracticeButton_Click(object sender, RoutedEventArgs e)
    {
        Navigate(typeof(PracticePage));
    }

    private void DashboardButton_Click(object sender, RoutedEventArgs e)
    {
        Navigate(typeof(DashboardPage));
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        Navigate(typeof(SettingsPage));
    }

    private void Navigate(Type pageType)
    {
        var frame = FindParentFrame();
        if (frame?.CurrentSourcePageType != pageType)
        {
            frame?.Navigate(pageType);
        }
    }

    private Frame? FindParentFrame()
    {
        DependencyObject? current = this;
        while (current is not null)
        {
            if (current is Frame frame)
            {
                return frame;
            }

            if (current is Page page)
            {
                return page.Frame;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void UpdateActivePage()
    {
        UpdateButton(PracticeButton, PracticeUnderline, "Practice");
        UpdateButton(DashboardButton, DashboardUnderline, "Dashboard");
        UpdateButton(SettingsButton, SettingsUnderline, "Settings");
    }

    private void UpdateButton(Button button, FrameworkElement underline, string pageName)
    {
        var isActive = string.Equals(ActivePage, pageName, StringComparison.OrdinalIgnoreCase);
        button.IsHitTestVisible = !isActive;
        button.Opacity = isActive ? 1.0 : 0.78;
        button.FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal;
        underline.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
    }
}
