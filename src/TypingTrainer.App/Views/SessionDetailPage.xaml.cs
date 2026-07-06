using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TypingTrainer.App.Services;
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
        ApplyResponsiveLayout(ActualWidth, ActualHeight);
        if (e.Parameter is Guid sessionId)
        {
            await ViewModel.LoadAsync(sessionId);
        }
    }

    private void SessionDetailPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout(e.NewSize.Width, e.NewSize.Height);
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

    private void ApplyResponsiveLayout(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var metrics = AppResponsiveLayoutMetrics.FromViewport(width, height);
        var cardPadding = new Thickness(Math.Clamp(14 * metrics.Scale, 10, 14));

        SessionDetailRootGrid.RowSpacing = metrics.RootRowSpacing;
        SessionDetailScrollViewer.Margin = new Thickness(
            metrics.PageHorizontalPadding,
            metrics.PageTopPadding,
            metrics.PageHorizontalPadding,
            metrics.PageBottomPadding);
        SessionDetailContentPanel.Spacing = metrics.ContentSpacing;

        ApplySummaryLayout(metrics, cardPadding);
        ApplyTwoColumnLayout(ConsistencyChartsGrid, SessionNetWpmChartCard, SessionAccuracyChartCard, metrics.CompactWidth);
        SessionNetWpmChartCard.Padding = new Thickness(Math.Clamp(12 * metrics.Scale, 8, 12));
        SessionAccuracyChartCard.Padding = new Thickness(Math.Clamp(12 * metrics.Scale, 8, 12));

        ApplyTwoColumnLayout(TimelineMistakesGrid, (FrameworkElement)TimelineMistakesGrid.Children[0], (FrameworkElement)TimelineMistakesGrid.Children[1], metrics.CompactWidth);
        TimelineListView.MaxHeight = metrics.TableMaxHeight;
        MistakesListView.MaxHeight = metrics.TableMaxHeight;

        ApplyTwoColumnLayout(SlowestListsGrid, (FrameworkElement)SlowestListsGrid.Children[0], (FrameworkElement)SlowestListsGrid.Children[1], metrics.CompactWidth);
        SessionDetailActionsPanel.Orientation = metrics.NarrowWidth ? Orientation.Vertical : Orientation.Horizontal;
    }

    private void ApplySummaryLayout(AppResponsiveLayoutMetrics metrics, Thickness cardPadding)
    {
        var cards = new FrameworkElement[]
        {
            NetWpmSummaryCard,
            AccuracySummaryCard,
            DurationSummaryCard,
            MistakesSummaryCard,
            QualitySummaryCard
        };
        var columns = metrics.VeryNarrowWidth ? 1 : metrics.CompactWidth ? 2 : 5;
        ConfigureColumns(SessionSummaryGrid, columns);
        ConfigureRows(SessionSummaryGrid, (int)Math.Ceiling(cards.Length / (double)columns));

        for (var index = 0; index < cards.Length; index++)
        {
            PositionGridItem(cards[index], index / columns, index % columns);
        }

        foreach (var card in cards.OfType<Border>())
        {
            card.Padding = cardPadding;
        }
    }

    private void ApplyTwoColumnLayout(Grid grid, FrameworkElement first, FrameworkElement second, bool stack)
    {
        if (stack)
        {
            ConfigureColumns(grid, 1);
            ConfigureRows(grid, 2);
            PositionGridItem(first, 0, 0);
            PositionGridItem(second, 1, 0);
        }
        else
        {
            ConfigureColumns(grid, 2);
            ConfigureRows(grid, 1);
            PositionGridItem(first, 0, 0);
            PositionGridItem(second, 0, 1);
        }
    }

    private static void ConfigureColumns(Grid grid, int count)
    {
        grid.ColumnDefinitions.Clear();
        for (var index = 0; index < count; index++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
    }

    private static void ConfigureRows(Grid grid, int count)
    {
        grid.RowDefinitions.Clear();
        for (var index = 0; index < count; index++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
    }

    private static void PositionGridItem(FrameworkElement element, int row, int column)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        Grid.SetColumnSpan(element, 1);
    }
}
