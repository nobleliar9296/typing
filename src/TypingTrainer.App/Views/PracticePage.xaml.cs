using System.Diagnostics;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TypingTrainer.App.ViewModels;
using TypingTrainer.Core.Lessons;
using Windows.System;
using Windows.UI;

namespace TypingTrainer.App.Views;

public sealed partial class PracticePage : Page
{
    private bool _isLoaded;
    private bool _suppressLessonSelectionChanges;
    private readonly DispatcherTimer _mistakeFeedbackTimer = new() { Interval = TimeSpan.FromMilliseconds(240) };
    private readonly SolidColorBrush _mistakeBorderBrush = new(Color.FromArgb(255, 196, 43, 55));
    private Brush? _defaultInputBorderBrush;

    public PracticePage()
    {
        InitializeComponent();
        ViewModel = new PracticeViewModel(
            App.Services.SessionPersistenceQueue,
            App.Services.LessonService);
        DataContext = ViewModel;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        _mistakeFeedbackTimer.Tick += MistakeFeedbackTimer_Tick;

        var restartAccelerator = new KeyboardAccelerator
        {
            Key = VirtualKey.R,
            Modifiers = VirtualKeyModifiers.Control
        };

        restartAccelerator.Invoked += RestartAccelerator_Invoked;
        KeyboardAccelerators.Add(restartAccelerator);
    }

    public PracticeViewModel ViewModel { get; }

    private async void PracticePage_Loaded(object sender, RoutedEventArgs e)
    {
        _defaultInputBorderBrush ??= InputBorder.BorderBrush;

        if (!_isLoaded)
        {
            await ViewModel.InitializeAsync();
            LessonModeComboBox.SelectedIndex = ViewModel.SelectedLessonMode switch
            {
                LessonMode.Paragraph => 1,
                LessonMode.WeakKeys => 2,
                LessonMode.WeakBigrams => 3,
                LessonMode.Review => 4,
                LessonMode.Fixed => 5,
                _ => 0
            };
            _isLoaded = true;
        }

        ApplyResponsiveLayout(ActualWidth, ActualHeight);
        InputSurface.Focus(FocusState.Programmatic);
        QueueScrollToCursor();
    }

    private void InputSurface_Tapped(object sender, TappedRoutedEventArgs e)
    {
        InputSurface.Focus(FocusState.Pointer);
        e.Handled = true;
    }

    private void InputSurface_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        InputSurface.Focus(FocusState.Pointer);
    }

    private void InputSurface_CharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
    {
        var character = (char)args.Character;

        if (!char.IsControl(character))
        {
            ViewModel.HandleCharacter(character);
            QueueScrollToCursor();
            args.Handled = true;
        }
    }

    private void InputSurface_KeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Escape)
        {
            ViewModel.HandleEscape(Stopwatch.GetTimestamp());
            InputSurface.Focus(FocusState.Programmatic);
            args.Handled = true;
        }
        else if (args.Key == VirtualKey.Back)
        {
            ViewModel.HandleBackspace();
            QueueScrollToCursor();
            args.Handled = true;
        }
    }

    private void RestartAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ViewModel.StartNewLesson();
        InputSurface.Focus(FocusState.Programmatic);
        QueueScrollToCursor();
        args.Handled = true;
    }

    private void RestartButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.StartNewLesson();
        InputSurface.Focus(FocusState.Programmatic);
        QueueScrollToCursor();
    }

    private async void NextLessonButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.GenerateNextLessonAsync();
        InputSurface.Focus(FocusState.Programmatic);
        QueueScrollToCursor();
    }

    private async void PracticeMistakesButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.PracticeMistakesAsync();
        _suppressLessonSelectionChanges = true;
        LessonModeComboBox.SelectedIndex = 4;
        _suppressLessonSelectionChanges = false;
        InputSurface.Focus(FocusState.Programmatic);
        QueueScrollToCursor();
    }

    private async void ViewDashboardButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await App.Services.SessionPersistenceQueue.FlushAsync();
        }
        catch
        {
            // Dashboard remains read-only and can still show the last successfully saved history.
        }

        Frame.Navigate(typeof(DashboardPage));
    }

    private async void LessonModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _suppressLessonSelectionChanges)
        {
            return;
        }

        var mode = LessonModeComboBox.SelectedIndex switch
        {
            1 => LessonMode.Paragraph,
            2 => LessonMode.WeakKeys,
            3 => LessonMode.WeakBigrams,
            4 => LessonMode.Review,
            5 => LessonMode.Fixed,
            _ => LessonMode.Adaptive
        };

        await ViewModel.ChangeLessonModeAsync(mode);
        InputSurface.Focus(FocusState.Programmatic);
        QueueScrollToCursor();
    }

    private async void LessonSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        var size = LessonSizeComboBox.SelectedIndex switch
        {
            1 => PracticeLessonSize.Medium,
            2 => PracticeLessonSize.Long,
            _ => PracticeLessonSize.Small
        };

        await ViewModel.ChangeLessonSizeAsync(size);
        InputSurface.Focus(FocusState.Programmatic);
        QueueScrollToCursor();
    }

    private void PracticeRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout(e.NewSize.Width, e.NewSize.Height);
    }

    private void PracticeRoot_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            InputSurface.Focus(FocusState.Pointer);
        }
    }

    private void ApplyResponsiveLayout(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var widthScale = width switch
        {
            < 720 => 0.70,
            < 920 => 0.78,
            < 1200 => 0.88,
            _ => 1.00
        };
        var heightScale = height switch
        {
            < 700 => 0.64,
            < 820 => 0.74,
            < 980 => 0.86,
            _ => 1.00
        };
        var scale = Math.Clamp(Math.Min(widthScale, heightScale), 0.68, 1.0);
        var keyboardScale = Math.Clamp(scale * (height < 900 ? 0.94 : 1.0), 0.58, 1.0);
        var compactHeader = width < 980;
        var veryCompactHeader = width < 720;
        var stackedSelectors = width < 760;

        var pageHorizontalPadding = Math.Clamp(32 * scale, 16, 32);
        var pageTopPadding = Math.Clamp(24 * scale, 12, 28);
        var pageBottomPadding = Math.Clamp(18 * scale, 8, 24);
        PracticeRoot.Padding = new Thickness(0);
        HeaderGrid.Margin = new Thickness(pageHorizontalPadding, pageTopPadding, pageHorizontalPadding, 0);
        PracticeContentPanel.Margin = new Thickness(pageHorizontalPadding, 0, pageHorizontalPadding, pageBottomPadding);
        PracticeRoot.RowSpacing = Math.Clamp(18 * scale, 10, 20);

        HeaderGrid.RowSpacing = Math.Clamp(10 * scale, 7, 12);
        HeaderTopGrid.ColumnSpacing = Math.Clamp(16 * scale, 10, 16);
        HeaderTopGrid.RowSpacing = Math.Clamp(8 * scale, 6, 8);
        LessonSelectorsPanel.Orientation = stackedSelectors ? Orientation.Vertical : Orientation.Horizontal;
        LessonSelectorsPanel.Spacing = stackedSelectors ? Math.Clamp(7 * scale, 5, 8) : Math.Clamp(14 * scale, 9, 14);

        ArrangeHeaderTop(compactHeader, veryCompactHeader);
        ArrangeContext(compactHeader, veryCompactHeader);
        ArrangeKpis(compactHeader, scale);
        SetHeaderTypography(scale);
        SetKpiTileSizing(scale);

        InputBorder.Padding = new Thickness(Math.Clamp(32 * scale, 16, 32));
        InputBorder.MaxWidth = width < 1200 ? double.PositiveInfinity : 1100;
        PracticeTextPresenter.DisplayScale = scale * ViewModel.PracticeTextScale;
        PracticeTextPresenter.MaxWidth = ViewModel.PracticeLineWidthMax;
        VisualKeyboard.KeyboardScale = keyboardScale * ViewModel.VisualKeyboardScale;
        VisualKeyboard.MaxWidth = width < 900 ? double.PositiveInfinity : 1280;

        var headerHeight = HeaderGrid.ActualHeight > 0
            ? HeaderGrid.ActualHeight
            : compactHeader ? 245 * scale : 178 * scale;
        var keyboardHeightEstimate = (306 * keyboardScale) + 18;
        var statusAllowance = 62 * scale;
        var availableTextHeight = height
            - 43
            - HeaderGrid.Margin.Top
            - PracticeContentPanel.Margin.Bottom
            - PracticeRoot.RowSpacing
            - headerHeight
            - keyboardHeightEstimate
            - statusAllowance;
        var maxTextHeight = Math.Clamp(availableTextHeight, 120, height < 900 ? 300 : 360);
        PracticeTextScrollViewer.MaxHeight = maxTextHeight;
        PracticeTextScrollViewer.MinHeight = Math.Min(maxTextHeight, Math.Clamp(190 * scale, 120, 190));
    }

    private void ArrangeHeaderTop(bool compactHeader, bool veryCompactHeader)
    {
        if (veryCompactHeader)
        {
            HeaderSpacerColumn.Width = new GridLength(0);
            HeaderControlsColumn.Width = new GridLength(1, GridUnitType.Star);

            Grid.SetRow(TitlePanel, 0);
            Grid.SetColumn(TitlePanel, 0);
            Grid.SetColumnSpan(TitlePanel, 3);
            Grid.SetRow(LessonSelectorsPanel, 1);
            Grid.SetColumn(LessonSelectorsPanel, 0);
            Grid.SetColumnSpan(LessonSelectorsPanel, 3);
            LessonSelectorsPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            return;
        }

        if (compactHeader)
        {
            HeaderSpacerColumn.Width = new GridLength(1, GridUnitType.Star);
            HeaderControlsColumn.Width = GridLength.Auto;

            Grid.SetRow(TitlePanel, 0);
            Grid.SetColumn(TitlePanel, 0);
            Grid.SetColumnSpan(TitlePanel, 3);
            Grid.SetRow(LessonSelectorsPanel, 1);
            Grid.SetColumn(LessonSelectorsPanel, 2);
            Grid.SetColumnSpan(LessonSelectorsPanel, 1);
            LessonSelectorsPanel.HorizontalAlignment = HorizontalAlignment.Right;
            return;
        }

        HeaderSpacerColumn.Width = new GridLength(1, GridUnitType.Star);
        HeaderControlsColumn.Width = GridLength.Auto;

        Grid.SetRow(TitlePanel, 0);
        Grid.SetColumn(TitlePanel, 0);
        Grid.SetColumnSpan(TitlePanel, 1);
        Grid.SetRow(LessonSelectorsPanel, 0);
        Grid.SetColumn(LessonSelectorsPanel, 2);
        Grid.SetColumnSpan(LessonSelectorsPanel, 1);
        LessonSelectorsPanel.HorizontalAlignment = HorizontalAlignment.Right;
    }

    private void ArrangeContext(bool compactHeader, bool veryCompactHeader)
    {
        if (veryCompactHeader)
        {
            ContextColumn0.Width = new GridLength(1, GridUnitType.Star);
            ContextColumn1.Width = new GridLength(0);
            ContextColumn2.Width = new GridLength(0);
            ContextColumn3.Width = new GridLength(0);
            MoveContextText(LessonReasonText, 0, 0, 1);
            MoveContextText(FocusKeysText, 1, 0, 1);
            MoveContextText(FocusBigramsText, 2, 0, 1);
            MoveContextText(LessonContentText, 3, 0, 1);
            return;
        }

        if (compactHeader)
        {
            ContextColumn0.Width = new GridLength(1, GridUnitType.Star);
            ContextColumn1.Width = new GridLength(1, GridUnitType.Star);
            ContextColumn2.Width = new GridLength(0);
            ContextColumn3.Width = new GridLength(0);
            MoveContextText(LessonReasonText, 0, 0, 1);
            MoveContextText(FocusKeysText, 0, 1, 1);
            MoveContextText(FocusBigramsText, 1, 0, 1);
            MoveContextText(LessonContentText, 1, 1, 1);
            return;
        }

        ContextColumn0.Width = new GridLength(1.1, GridUnitType.Star);
        ContextColumn1.Width = new GridLength(1, GridUnitType.Star);
        ContextColumn2.Width = new GridLength(1, GridUnitType.Star);
        ContextColumn3.Width = new GridLength(1.4, GridUnitType.Star);
        MoveContextText(LessonReasonText, 0, 0, 1);
        MoveContextText(FocusKeysText, 0, 1, 1);
        MoveContextText(FocusBigramsText, 0, 2, 1);
        MoveContextText(LessonContentText, 0, 3, 1);
    }

    private static void MoveContextText(FrameworkElement element, int row, int column, int columnSpan)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        Grid.SetColumnSpan(element, columnSpan);
    }

    private void ArrangeKpis(bool compactHeader, double scale)
    {
        var columns = compactHeader ? 3 : 6;
        var columnDefinitions = new[] { KpiColumn0, KpiColumn1, KpiColumn2, KpiColumn3, KpiColumn4, KpiColumn5 };
        for (var index = 0; index < columnDefinitions.Length; index++)
        {
            columnDefinitions[index].Width = index < columns
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(0);
        }

        KpiRow1.Height = compactHeader ? GridLength.Auto : new GridLength(0);
        StatsGrid.ColumnSpacing = Math.Clamp(8 * scale, 5, 8);
        StatsGrid.RowSpacing = compactHeader ? Math.Clamp(8 * scale, 5, 8) : 0;

        var tiles = new[] { RawWpmTile, NetWpmTile, AccuracyTile, ElapsedTile, ErrorsTile, ProgressTile };
        for (var index = 0; index < tiles.Length; index++)
        {
            Grid.SetRow(tiles[index], compactHeader ? index / 3 : 0);
            Grid.SetColumn(tiles[index], compactHeader ? index % 3 : index);
        }
    }

    private void SetHeaderTypography(double scale)
    {
        TitleText.FontSize = Math.Clamp(28 * scale, 22, 28);
        LessonTitleText.FontSize = Math.Clamp(14 * scale, 12, 14);
        LessonReasonText.FontSize = Math.Clamp(14 * scale, 12, 14);
        FocusKeysText.FontSize = Math.Clamp(14 * scale, 12, 14);
        FocusBigramsText.FontSize = Math.Clamp(14 * scale, 12, 14);
        LessonContentText.FontSize = Math.Clamp(14 * scale, 12, 14);

        foreach (var valueText in new[] { RawWpmValueText, NetWpmValueText, AccuracyValueText, ElapsedValueText, ErrorsValueText, ProgressValueText })
        {
            valueText.FontSize = Math.Clamp(24 * scale, 18, 24);
        }

        foreach (var labelText in new[] { RawWpmLabelText, NetWpmLabelText, AccuracyLabelText, ElapsedLabelText, ErrorsLabelText, ProgressLabelText, CharactersText })
        {
            labelText.FontSize = Math.Clamp(11 * scale, 9, 11);
        }
    }

    private void SetKpiTileSizing(double scale)
    {
        foreach (var tile in new[] { RawWpmTile, NetWpmTile, AccuracyTile, ElapsedTile, ErrorsTile, ProgressTile })
        {
            tile.Padding = new Thickness(Math.Clamp(12 * scale, 8, 12), Math.Clamp(10 * scale, 7, 10), Math.Clamp(12 * scale, 8, 12), Math.Clamp(10 * scale, 7, 10));
            tile.MinHeight = Math.Clamp(68 * scale, 54, 68);
        }
    }

    private void QueueScrollToCursor()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var cursorOffset = PracticeTextPresenter.GetEstimatedCursorOffsetY();
            var viewportAnchor = Math.Max(0, PracticeTextScrollViewer.ViewportHeight * 0.38);
            var offset = Math.Clamp(
                cursorOffset - viewportAnchor,
                0,
                Math.Max(0, PracticeTextScrollViewer.ScrollableHeight));
            PracticeTextScrollViewer.ChangeView(null, offset, null, disableAnimation: true);
        });
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PracticeViewModel.TypingFeedbackVisibility)
            && ViewModel.TypingFeedbackVisibility == Visibility.Visible)
        {
            FlashInputBorder();
        }
    }

    private void FlashInputBorder()
    {
        _defaultInputBorderBrush ??= InputBorder.BorderBrush;
        InputBorder.BorderBrush = _mistakeBorderBrush;
        _mistakeFeedbackTimer.Stop();
        _mistakeFeedbackTimer.Start();
    }

    private void MistakeFeedbackTimer_Tick(object? sender, object e)
    {
        _mistakeFeedbackTimer.Stop();
        if (_defaultInputBorderBrush is not null)
        {
            InputBorder.BorderBrush = _defaultInputBorderBrush;
        }
    }

    private static bool IsInteractiveElement(DependencyObject? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is ButtonBase
                or ComboBox
                or TextBox
                or NumberBox
                or ToggleSwitch
                or CheckBox
                or RadioButton
                or Slider
                or ScrollBar)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}
