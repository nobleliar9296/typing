using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TypingTrainer.App.Services;
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
            App.Services.KeyboardHeatmapQueryService,
            App.Services.TrainingHistoryQueryService,
            App.Services.AppSettingsRepository);
        DataContext = ViewModel;
    }

    public DashboardViewModel ViewModel { get; }

    private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        ApplyResponsiveLayout(ActualWidth, ActualHeight);
        await ViewModel.LoadAsync();
    }

    private void DashboardPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout(e.NewSize.Width, e.NewSize.Height);
    }

    private async void SaveGoalsButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SaveGoalsAsync();
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

    private async void ModeFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (ModeFilterComboBox.SelectedItem is ComboBoxItem item
            && item.Content is string content)
        {
            ViewModel.SelectedModeFilter = content;
        }

        await ViewModel.LoadAsync();
    }

    private void KeyFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (KeyFilterComboBox.SelectedItem is ComboBoxItem item
            && item.Content is string content)
        {
            ViewModel.SelectedKeyFilter = content;
        }
    }

    private void RecentSessionsListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RecentSessionDisplayRow row)
        {
            NavigateTo(typeof(SessionDetailPage), row.SessionId);
        }
    }

    private void PersonalBestListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PersonalBestDisplayRow { SessionId: Guid sessionId })
        {
            NavigateTo(typeof(SessionDetailPage), sessionId);
        }
    }

    private void StartDailyPlanButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(typeof(PracticePage), ViewModel.CreateDailyPlanLaunchRequest());
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
        var cardPadding = new Thickness(metrics.CardPadding);

        DashboardRootGrid.RowSpacing = metrics.RootRowSpacing;
        DashboardHeaderGrid.Margin = new Thickness(
            metrics.PageHorizontalPadding,
            metrics.PageTopPadding,
            metrics.PageHorizontalPadding,
            0);
        DashboardHeaderGrid.ColumnSpacing = metrics.HeaderColumnSpacing;
        DashboardHeaderGrid.RowSpacing = metrics.HeaderControlSpacing;
        DashboardScrollViewer.Margin = new Thickness(
            metrics.PageHorizontalPadding,
            0,
            metrics.PageHorizontalPadding,
            metrics.PageBottomPadding);
        DashboardContentPanel.Spacing = metrics.ContentSpacing;

        ApplyHeaderLayout(metrics);

        GoalPanelCard.Padding = cardPadding;
        GoalHeaderGrid.ColumnSpacing = Math.Clamp(16 * metrics.Scale, 8, 16);
        GoalMetricsGrid.ColumnSpacing = Math.Clamp(12 * metrics.Scale, 8, 12);
        GoalMetricsGrid.RowSpacing = Math.Clamp(12 * metrics.Scale, 8, 12);
        ApplyGoalInputLayout(metrics);

        DailyCoachCard.Padding = cardPadding;
        AchievementsCard.Padding = cardPadding;
        ApplyTwoColumnLayout(DailyCoachGrid, DailyCoachCard, AchievementsCard, metrics.CompactWidth);
        DailyPlanListView.MaxHeight = metrics.TableMaxHeight;
        AchievementsListView.MaxHeight = metrics.TableMaxHeight;

        ApplySummaryLayout(metrics);
        ApplyTwoColumnLayout(QualityBestGrid, TrainingQualityCard, PersonalBestsCard, metrics.CompactWidth);
        TrainingQualityCard.Padding = cardPadding;
        PersonalBestsCard.Padding = cardPadding;
        PersonalBestListView.MaxHeight = metrics.ShortHeight ? 240 : 330;

        ApplyTrendLayout(metrics);
        ApplyTwoColumnLayout(WeakKeyGrid, (FrameworkElement)WeakKeyGrid.Children[0], (FrameworkElement)WeakKeyGrid.Children[1], metrics.CompactWidth);
        ApplyTwoColumnLayout(BigramGrid, (FrameworkElement)BigramGrid.Children[0], (FrameworkElement)BigramGrid.Children[1], metrics.CompactWidth);

        HeatmapListView.MaxHeight = metrics.TableMaxHeight;
        HandFingerListView.MaxHeight = metrics.ShortHeight ? 180 : 220;
    }

    private void ApplyHeaderLayout(AppResponsiveLayoutMetrics metrics)
    {
        if (metrics.VeryNarrowWidth)
        {
            DashboardHeaderTitleColumn.Width = new GridLength(1, GridUnitType.Star);
            DashboardHeaderModeColumn.Width = new GridLength(0);
            DashboardHeaderRangeColumn.Width = new GridLength(0);
            DashboardHeaderKeysColumn.Width = new GridLength(0);

            PositionHeaderItem(DashboardTitlePanel, 0, 0);
            PositionHeaderItem(ModeFilterPanel, 1, 0);
            PositionHeaderItem(RangeFilterPanel, 2, 0);
            PositionHeaderItem(KeyFilterPanel, 3, 0);
            SetFilterStretch(metrics, stretch: true);
            return;
        }

        if (metrics.CompactWidth)
        {
            DashboardHeaderTitleColumn.Width = new GridLength(1, GridUnitType.Star);
            DashboardHeaderModeColumn.Width = new GridLength(1, GridUnitType.Star);
            DashboardHeaderRangeColumn.Width = new GridLength(1, GridUnitType.Star);
            DashboardHeaderKeysColumn.Width = new GridLength(0);

            PositionHeaderItem(DashboardTitlePanel, 0, 0, 3);
            PositionHeaderItem(ModeFilterPanel, 1, 0);
            PositionHeaderItem(RangeFilterPanel, 1, 1);
            PositionHeaderItem(KeyFilterPanel, 1, 2);
            SetFilterStretch(metrics, stretch: true);
            return;
        }

        DashboardHeaderTitleColumn.Width = new GridLength(1, GridUnitType.Star);
        DashboardHeaderModeColumn.Width = GridLength.Auto;
        DashboardHeaderRangeColumn.Width = GridLength.Auto;
        DashboardHeaderKeysColumn.Width = GridLength.Auto;

        PositionHeaderItem(DashboardTitlePanel, 0, 0);
        PositionHeaderItem(ModeFilterPanel, 0, 1);
        PositionHeaderItem(RangeFilterPanel, 0, 2);
        PositionHeaderItem(KeyFilterPanel, 0, 3);
        SetFilterStretch(metrics, stretch: false);
    }

    private void ApplyGoalInputLayout(AppResponsiveLayoutMetrics metrics)
    {
        if (metrics.NarrowWidth)
        {
            ConfigureColumns(GoalInputsGrid, 1);
            PositionGridItem(GoalTargetWpmNumberBox, 0, 0);
            PositionGridItem(GoalTargetAccuracyNumberBox, 1, 0);
            PositionGridItem(GoalWeeklyMinutesNumberBox, 2, 0);
            PositionGridItem(SaveGoalsButton, 3, 0);
            SetGoalInputStretch(true);
        }
        else if (metrics.CompactWidth)
        {
            ConfigureColumns(GoalInputsGrid, 2);
            PositionGridItem(GoalTargetWpmNumberBox, 0, 0);
            PositionGridItem(GoalTargetAccuracyNumberBox, 0, 1);
            PositionGridItem(GoalWeeklyMinutesNumberBox, 1, 0);
            PositionGridItem(SaveGoalsButton, 1, 1);
            SetGoalInputStretch(true);
        }
        else
        {
            GoalInputsGrid.ColumnDefinitions.Clear();
            GoalInputsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            GoalInputsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            GoalInputsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            GoalInputsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            PositionGridItem(GoalTargetWpmNumberBox, 0, 0);
            PositionGridItem(GoalTargetAccuracyNumberBox, 0, 1);
            PositionGridItem(GoalWeeklyMinutesNumberBox, 0, 2);
            PositionGridItem(SaveGoalsButton, 0, 3);
            SetGoalInputStretch(false);
        }
    }

    private void ApplySummaryLayout(AppResponsiveLayoutMetrics metrics)
    {
        var cards = new FrameworkElement[]
        {
            SessionsSummaryCard,
            PracticeTimeSummaryCard,
            AverageWpmSummaryCard,
            BestWpmSummaryCard,
            AccuracySummaryCard,
            ErrorsSummaryCard
        };
        var columns = metrics.VeryNarrowWidth ? 1 : metrics.CompactWidth ? 2 : 3;
        ConfigureColumns(SummaryMetricsGrid, columns);
        ConfigureRows(SummaryMetricsGrid, (int)Math.Ceiling(cards.Length / (double)columns));

        for (var index = 0; index < cards.Length; index++)
        {
            PositionGridItem(cards[index], index / columns, index % columns);
        }

        foreach (var card in cards.OfType<Border>())
        {
            card.Padding = new Thickness(metrics.CardPadding);
        }
    }

    private void ApplyTrendLayout(AppResponsiveLayoutMetrics metrics)
    {
        if (metrics.CompactWidth)
        {
            ConfigureColumns(TrendChartsGrid, 1);
            ConfigureRows(TrendChartsGrid, 3);
            PositionGridItem(NetWpmChartPanel, 0, 0);
            PositionGridItem(AccuracyChartPanel, 1, 0);
            PositionGridItem(PracticeTimeChartPanel, 2, 0);
            Grid.SetColumnSpan(PracticeTimeChartPanel, 1);
        }
        else
        {
            ConfigureColumns(TrendChartsGrid, 2);
            ConfigureRows(TrendChartsGrid, 2);
            PositionGridItem(NetWpmChartPanel, 0, 0);
            PositionGridItem(AccuracyChartPanel, 0, 1);
            PositionGridItem(PracticeTimeChartPanel, 1, 0);
            Grid.SetColumnSpan(PracticeTimeChartPanel, 2);
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

    private static void PositionHeaderItem(FrameworkElement element, int row, int column, int columnSpan = 1)
    {
        PositionGridItem(element, row, column);
        Grid.SetColumnSpan(element, columnSpan);
    }

    private static void PositionGridItem(FrameworkElement element, int row, int column)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        Grid.SetColumnSpan(element, 1);
    }

    private void SetFilterStretch(AppResponsiveLayoutMetrics metrics, bool stretch)
    {
        var width = stretch ? double.NaN : metrics.FilterControlWidth;
        var alignment = stretch ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;

        ModeFilterComboBox.Width = width;
        RangeComboBox.Width = width;
        KeyFilterComboBox.Width = stretch ? double.NaN : metrics.SmallFilterControlWidth;
        ModeFilterComboBox.HorizontalAlignment = alignment;
        RangeComboBox.HorizontalAlignment = alignment;
        KeyFilterComboBox.HorizontalAlignment = alignment;
    }

    private void SetGoalInputStretch(bool stretch)
    {
        var width = stretch ? double.NaN : 0;
        var alignment = stretch ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;

        GoalTargetWpmNumberBox.Width = stretch ? width : double.NaN;
        GoalTargetAccuracyNumberBox.Width = stretch ? width : double.NaN;
        GoalWeeklyMinutesNumberBox.Width = stretch ? width : double.NaN;
        GoalTargetWpmNumberBox.HorizontalAlignment = alignment;
        GoalTargetAccuracyNumberBox.HorizontalAlignment = alignment;
        GoalWeeklyMinutesNumberBox.HorizontalAlignment = alignment;
        SaveGoalsButton.HorizontalAlignment = stretch ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
    }
}
