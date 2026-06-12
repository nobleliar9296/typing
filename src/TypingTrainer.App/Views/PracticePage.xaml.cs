using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.ComponentModel;
using System.Diagnostics;
using TypingTrainer.App.Controls;
using TypingTrainer.App.ViewModels;
using TypingTrainer.Core.Lessons;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using WinRT.Interop;

namespace TypingTrainer.App.Views;

public sealed partial class PracticePage : Page
{
    private bool _isLoaded;
    private bool _suppressLessonSelectionChanges;
    private readonly DispatcherTimer _mistakeFeedbackTimer = new() { Interval = TimeSpan.FromMilliseconds(240) };
    private readonly SolidColorBrush _mistakeBorderBrush = new(Color.FromArgb(255, 196, 43, 55));
    private Brush? _defaultInputBorderBrush;
    private double? _lastScrollTarget;

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
                LessonMode.Clipboard => 5,
                LessonMode.Fixed => 6,
                _ => 0
            };
            _isLoaded = true;
        }

        ApplyResponsiveLayout(ActualWidth, ActualHeight);
        FocusTypingSurface(FocusState.Programmatic);
        QueueScrollToCursor(animate: false);
    }

    private void InputSurface_Tapped(object sender, TappedRoutedEventArgs e)
    {
        FocusTypingSurface(FocusState.Pointer);
        e.Handled = true;
    }

    private void InputSurface_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        FocusTypingSurface(FocusState.Pointer);
    }

    private void InputSurface_CharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
    {
        if (ViewModel.ReviewPopupVisibility == Visibility.Visible)
        {
            args.Handled = true;
            return;
        }

        var character = (char)args.Character;

        if (!char.IsControl(character))
        {
            PlayInputSound(ViewModel.HandleCharacter(character));
            QueueScrollToCursor();
            args.Handled = true;
        }
    }

    private void InputSurface_KeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (ViewModel.ReviewPopupVisibility == Visibility.Visible)
        {
            args.Handled = true;
            return;
        }

        if (args.Key == VirtualKey.Escape)
        {
            ViewModel.HandleEscape(Stopwatch.GetTimestamp());
            FocusTypingSurface(FocusState.Programmatic);
            args.Handled = true;
        }
        else if (args.Key == VirtualKey.Back)
        {
            PlayInputSound(ViewModel.HandleBackspace());
            QueueScrollToCursor();
            args.Handled = true;
        }
    }

    private void RestartAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ViewModel.StartNewLesson();
        FocusTypingSurface(FocusState.Programmatic);
        QueueScrollToCursor(animate: false);
        args.Handled = true;
    }

    private void RestartButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.StartNewLesson();
        FocusTypingSurface(FocusState.Programmatic);
        QueueScrollToCursor(animate: false);
    }

    private void CloseReviewButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DismissReviewPopup();
        FocusTypingSurface(FocusState.Programmatic);
    }

    private async void NextLessonButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.GenerateNextLessonAsync();
        FocusTypingSurface(FocusState.Programmatic);
        QueueScrollToCursor(animate: false);
    }

    private async void ClipboardLessonButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dataPackageView = Clipboard.GetContent();
            if (!dataPackageView.Contains(StandardDataFormats.Text))
            {
                ViewModel.SetClipboardUnavailable();
                return;
            }

            var text = await dataPackageView.GetTextAsync();
            await ViewModel.StartClipboardLessonAsync(text);
            _suppressLessonSelectionChanges = true;
            LessonModeComboBox.SelectedIndex = 5;
            _suppressLessonSelectionChanges = false;
            FocusTypingSurface(FocusState.Programmatic);
            QueueScrollToCursor(animate: false);
        }
        catch
        {
            ViewModel.SetClipboardUnavailable();
        }
    }

    private async void PracticeMistakesButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.PracticeMistakesAsync();
        _suppressLessonSelectionChanges = true;
        LessonModeComboBox.SelectedIndex = 4;
        _suppressLessonSelectionChanges = false;
        FocusTypingSurface(FocusState.Programmatic);
        QueueScrollToCursor(animate: false);
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

        NavigateTo(typeof(DashboardPage));
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
            5 => LessonMode.Clipboard,
            6 => LessonMode.Fixed,
            _ => LessonMode.Adaptive
        };

        await ViewModel.ChangeLessonModeAsync(mode);
        FocusTypingSurface(FocusState.Programmatic);
        QueueScrollToCursor(animate: false);
    }

    private async void ExportSummaryButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker
        {
            SuggestedFileName = $"typing-trainer-review-{DateTime.Now:yyyyMMdd-HHmm}",
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeChoices.Add("Text review", [".txt"]);

        if (App.MainWindowInstance is not null)
        {
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindowInstance));
        }

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        await FileIO.WriteTextAsync(file, ViewModel.CreateReviewSummaryText());
    }

    private void SessionNetWpmChart_PointSelected(object sender, ChartPointSelectedEventArgs e)
    {
        if (ViewModel.StartRetryFromNetWpmPoint(e.Index))
        {
            FocusTypingSurface(FocusState.Programmatic);
            QueueScrollToCursor(animate: false);
        }
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
        FocusTypingSurface(FocusState.Programmatic);
        QueueScrollToCursor(animate: false);
    }

    private void PracticeRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout(e.NewSize.Width, e.NewSize.Height);
    }

    private void PracticeRoot_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.ReviewPopupVisibility == Visibility.Visible)
        {
            return;
        }

        if (!IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            FocusTypingSurface(FocusState.Pointer);
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
        var pageTopPadding = Math.Clamp(12 * scale, 8, 14);
        PracticeRoot.Padding = new Thickness(0);
        HeaderGrid.Margin = new Thickness(pageHorizontalPadding, pageTopPadding, pageHorizontalPadding, 0);
        PracticeContentPanel.Margin = new Thickness(pageHorizontalPadding, 0, pageHorizontalPadding, 0);
        PracticeContentPanel.Spacing = Math.Clamp(12 * scale, 8, 14);
        PracticeRoot.RowSpacing = Math.Clamp(10 * scale, 6, 12);

        HeaderGrid.RowSpacing = Math.Clamp(7 * scale, 4, 8);
        HeaderTopGrid.ColumnSpacing = Math.Clamp(16 * scale, 10, 16);
        HeaderTopGrid.RowSpacing = 0;
        LessonSelectorsPanel.Orientation = stackedSelectors ? Orientation.Vertical : Orientation.Horizontal;
        LessonSelectorsPanel.Spacing = stackedSelectors ? Math.Clamp(7 * scale, 5, 8) : Math.Clamp(14 * scale, 9, 14);

        ArrangeHeaderTop(stackedSelectors);
        ArrangeContext(compactHeader, veryCompactHeader);
        ArrangeStatsColumns(width, scale);
        SetHeaderTypography(scale);
        SetKpiTileSizing(scale);

        var inputHorizontalPadding = Math.Clamp(28 * scale, 14, 28);
        var inputTopPadding = Math.Clamp(24 * scale, 12, 24);
        InputBorder.Padding = new Thickness(inputHorizontalPadding, inputTopPadding, inputHorizontalPadding, 0);
        InputBorder.MaxWidth = width < 1200 ? double.PositiveInfinity : 1100;
        PracticeTextPresenter.DisplayScale = scale * ViewModel.PracticeTextScale;
        PracticeTextPresenter.MaxWidth = ViewModel.PracticeLineWidthMax;
        VisualKeyboard.KeyboardScale = keyboardScale * ViewModel.VisualKeyboardScale;
        VisualKeyboard.MaxWidth = width < 900 ? double.PositiveInfinity : 1280;

        var headerHeight = HeaderGrid.ActualHeight > 0
            ? HeaderGrid.ActualHeight
            : compactHeader ? 92 * scale : 58 * scale;
        var keyboardHeightEstimate = (306 * keyboardScale) + 18;
        var statusAllowance = 34 * scale;
        var availableTextHeight = height
            - HeaderGrid.Margin.Top
            - PracticeRoot.RowSpacing
            - headerHeight
            - keyboardHeightEstimate
            - statusAllowance;
        var fourLineTextHeight = 4 * 48 * PracticeTextPresenter.DisplayScale;
        var maxTextHeight = Math.Clamp(
            availableTextHeight,
            fourLineTextHeight,
            Math.Max(fourLineTextHeight, height < 900 ? 300 : 360));
        PracticeTextScrollViewer.MaxHeight = maxTextHeight;
        PracticeTextScrollViewer.MinHeight = Math.Min(maxTextHeight, fourLineTextHeight);
    }

    private void ArrangeHeaderTop(bool stackedSelectors)
    {
        HeaderTitleColumn.Width = new GridLength(0);
        HeaderSpacerColumn.Width = new GridLength(1, GridUnitType.Star);
        HeaderControlsColumn.Width = stackedSelectors
            ? new GridLength(1, GridUnitType.Star)
            : GridLength.Auto;

        Grid.SetRow(LessonSelectorsPanel, 0);
        Grid.SetColumn(LessonSelectorsPanel, stackedSelectors ? 0 : 2);
        Grid.SetColumnSpan(LessonSelectorsPanel, stackedSelectors ? 3 : 1);
        LessonSelectorsPanel.HorizontalAlignment = stackedSelectors
            ? HorizontalAlignment.Stretch
            : HorizontalAlignment.Right;
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

    private void ArrangeStatsColumns(double width, double scale)
    {
        var minimumRailWidth = width < 820 ? 92 : 108;
        var statsWidth = Math.Clamp(132 * scale, minimumRailWidth, 132);
        LeftStatsColumn.Width = new GridLength(statsWidth);
        RightStatsColumn.Width = new GridLength(statsWidth);
        PracticeTypingGrid.ColumnSpacing = Math.Clamp(12 * scale, 8, 14);
        LeftStatsPanel.Spacing = Math.Clamp(8 * scale, 5, 8);
        RightStatsPanel.Spacing = Math.Clamp(8 * scale, 5, 8);
    }

    private void SetHeaderTypography(double scale)
    {
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
            tile.Padding = new Thickness(Math.Clamp(10 * scale, 6, 10), Math.Clamp(8 * scale, 5, 8), Math.Clamp(10 * scale, 6, 10), Math.Clamp(8 * scale, 5, 8));
            tile.MinHeight = Math.Clamp(58 * scale, 48, 58);
        }
    }

    private bool NavigateTo(Type pageType, object? parameter = null)
    {
        return MainWindow.Instance?.NavigateTo(pageType, parameter)
            ?? Frame.Navigate(pageType, parameter);
    }

    private void FocusTypingSurface(FocusState focusState)
    {
        InputSurface.Focus(focusState);
    }

    private void PlayInputSound(PracticeInputFeedback feedback)
    {
        try
        {
            if (feedback == PracticeInputFeedback.Mistake && ViewModel.MistakeSoundEnabled)
            {
                ElementSoundPlayer.Play(ElementSoundKind.Focus);
            }
            else if (feedback == PracticeInputFeedback.Key && ViewModel.KeySoundEnabled)
            {
                ElementSoundPlayer.Play(ElementSoundKind.Invoke);
            }
        }
        catch
        {
            // Sound feedback is optional; platform sound failures should never affect typing.
        }
    }

    private void QueueScrollToCursor(bool animate = true)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var cursorOffset = PracticeTextPresenter.GetEstimatedCursorOffsetY();
            var viewportAnchor = Math.Max(0, PracticeTextScrollViewer.ViewportHeight * 0.38);
            var offset = Math.Clamp(
                cursorOffset - viewportAnchor,
                0,
                Math.Max(0, PracticeTextScrollViewer.ScrollableHeight));
            var targetOffset = offset;

            if (animate && _lastScrollTarget is double previousOffset)
            {
                targetOffset = previousOffset + ((offset - previousOffset) * 0.58);
            }

            _lastScrollTarget = targetOffset;

            if (Math.Abs(PracticeTextScrollViewer.VerticalOffset - targetOffset) < 0.5)
            {
                return;
            }

            PracticeTextScrollViewer.ChangeView(null, targetOffset, null, disableAnimation: !animate);
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
