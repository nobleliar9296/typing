using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.ComponentModel;
using System.Diagnostics;
using TypingTrainer.App.Controls;
using TypingTrainer.App.Navigation;
using TypingTrainer.App.Services;
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
    private PracticeLaunchRequest? _pendingLaunchRequest;
    private KeyboardSoundPlayer? _keyboardSoundPlayer;
    private bool _suppressNextEnterCharacterReceived;

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
        Unloaded += PracticePage_Unloaded;
    }

    public PracticeViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is PracticeLaunchRequest request)
        {
            _pendingLaunchRequest = request;
            if (_isLoaded)
            {
                await ApplyLaunchRequestAsync(request);
            }
        }
    }

    private async void PracticePage_Loaded(object sender, RoutedEventArgs e)
    {
        _defaultInputBorderBrush ??= InputBorder.BorderBrush;

        if (!_isLoaded)
        {
            await ViewModel.InitializeAsync();
            SyncLessonSelectors();
            _isLoaded = true;
        }

        if (_pendingLaunchRequest is not null)
        {
            await ApplyLaunchRequestAsync(_pendingLaunchRequest);
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

        if (_suppressNextEnterCharacterReceived)
        {
            _suppressNextEnterCharacterReceived = false;
            if (IsLineBreakCharacter(character))
            {
                args.Handled = true;
                return;
            }
        }

        if (IsTypingCharacter(character))
        {
            PlayInputSound(ViewModel.HandleCharacter(NormalizeTypingCharacter(character)));
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
        else if (args.Key == VirtualKey.Enter)
        {
            _suppressNextEnterCharacterReceived = true;
            PlayInputSound(ViewModel.HandleCharacter('\n'));
            QueueScrollToCursor();
            args.Handled = true;
        }
    }

    private static bool IsTypingCharacter(char character)
    {
        return !char.IsControl(character) || IsLineBreakCharacter(character);
    }

    private static bool IsLineBreakCharacter(char character)
    {
        return character is '\r' or '\n';
    }

    private static char NormalizeTypingCharacter(char character)
    {
        return character == '\r' ? '\n' : character;
    }

    private async void RestartAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        await ViewModel.StartNewLessonAsync();
        FocusTypingSurface(FocusState.Programmatic);
        QueueScrollToCursor(animate: false);
        args.Handled = true;
    }

    private async void RestartButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.StartNewLessonAsync();
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
        SyncLessonSelectors();
        FocusTypingSurface(FocusState.Programmatic);
        QueueScrollToCursor(animate: false);
    }

    private async void RecommendedFollowUpButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.StartRecommendedFollowUpAsync();
        SyncLessonSelectors();
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
        if (!_isLoaded || _suppressLessonSelectionChanges)
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

    private async Task ApplyLaunchRequestAsync(PracticeLaunchRequest request)
    {
        _pendingLaunchRequest = null;
        await ViewModel.ApplyLaunchRequestAsync(request);
        SyncLessonSelectors();
        FocusTypingSurface(FocusState.Programmatic);
        QueueScrollToCursor(animate: false);
    }

    private void SyncLessonSelectors()
    {
        _suppressLessonSelectionChanges = true;
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
        LessonSizeComboBox.SelectedIndex = ViewModel.SelectedLessonSize switch
        {
            PracticeLessonSize.Medium => 1,
            PracticeLessonSize.Long => 2,
            _ => 0
        };
        _suppressLessonSelectionChanges = false;
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

        var metrics = PracticeResponsiveLayoutMetrics.FromViewport(
            width,
            height,
            HeaderGrid.ActualHeight,
            ViewModel.PracticeStatsVisibility == Visibility.Visible,
            ViewModel.PracticeTextScale,
            ViewModel.VisualKeyboardScale,
            ViewModel.PracticeLineWidthMax);

        PracticeRoot.Padding = new Thickness(0);
        HeaderGrid.Margin = new Thickness(metrics.PageHorizontalPadding, metrics.PageTopPadding, metrics.PageHorizontalPadding, 0);
        PracticeContentPanel.Margin = new Thickness(metrics.PageHorizontalPadding, 0, metrics.PageHorizontalPadding, 0);
        PracticeContentPanel.Spacing = metrics.ContentSpacing;
        PracticeRoot.RowSpacing = metrics.RootRowSpacing;

        HeaderGrid.RowSpacing = metrics.HeaderRowSpacing;
        HeaderTopGrid.ColumnSpacing = metrics.HeaderColumnSpacing;
        HeaderTopGrid.RowSpacing = 0;
        LessonSelectorsPanel.Orientation = metrics.StackedSelectors ? Orientation.Vertical : Orientation.Horizontal;
        LessonSelectorsPanel.Spacing = metrics.SelectorSpacing;
        LessonModeComboBox.Width = metrics.LessonModeWidth;
        LessonSizeComboBox.Width = metrics.LessonSizeWidth;
        ClipboardLessonButton.Content = metrics.UseShortClipboardText ? "Copied text" : "Practice copied text";
        ClipboardLessonButton.Padding = new Thickness(
            metrics.ClipboardPaddingHorizontal,
            metrics.ClipboardPaddingVertical,
            metrics.ClipboardPaddingHorizontal,
            metrics.ClipboardPaddingVertical);

        ArrangeHeaderTop(metrics.StackedSelectors);
        ArrangeContext(metrics.CompactHeader, metrics.VeryCompactHeader);
        ArrangeStatsColumns(metrics);
        SetHeaderTypography(metrics);
        SetKpiTileSizing(metrics);

        InputBorder.Padding = new Thickness(metrics.InputHorizontalPadding, metrics.InputTopPadding, metrics.InputHorizontalPadding, 0);
        InputBorder.MaxWidth = metrics.InputBorderMaxWidth;
        PracticeTextPresenter.DisplayScale = metrics.PracticeTextDisplayScale;
        PracticeTextPresenter.MaxWidth = metrics.PracticeTextMaxWidth;
        PracticeTextPresenter.RefreshLayout();
        VisualKeyboard.KeyboardScale = metrics.KeyboardScale;
        VisualKeyboard.MaxWidth = metrics.KeyboardMaxWidth;
        ArrangeReviewPopup(metrics);

        PracticeTextScrollViewer.MaxHeight = metrics.PracticeTextMaxHeight;
        PracticeTextScrollViewer.MinHeight = metrics.PracticeTextMinHeight;
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

    private void ArrangeStatsColumns(PracticeResponsiveLayoutMetrics metrics)
    {
        if (metrics.CompactStats)
        {
            LeftStatsColumn.Width = new GridLength(0);
            TextColumn.Width = new GridLength(1, GridUnitType.Star);
            RightStatsColumn.Width = new GridLength(0);
            PracticeTypingGrid.ColumnSpacing = 0;
            PracticeTypingGrid.RowSpacing = metrics.CompactStatsRowSpacing;
            ArrangeStatPanel(LeftStatsPanel, row: 0, column: 0, columnSpan: 3, Orientation.Horizontal, HorizontalAlignment.Left);
            ArrangeTypingSurface(row: 1, column: 0, columnSpan: 3);
            ArrangeStatPanel(RightStatsPanel, row: 2, column: 0, columnSpan: 3, Orientation.Horizontal, HorizontalAlignment.Left);
            LeftStatsPanel.Spacing = metrics.StatsPanelSpacing;
            RightStatsPanel.Spacing = metrics.StatsPanelSpacing;
            return;
        }

        LeftStatsColumn.Width = new GridLength(metrics.RailStatsWidth);
        TextColumn.Width = new GridLength(1, GridUnitType.Star);
        RightStatsColumn.Width = new GridLength(metrics.RailStatsWidth);
        PracticeTypingGrid.ColumnSpacing = metrics.TypingGridColumnSpacing;
        PracticeTypingGrid.RowSpacing = 0;
        ArrangeStatPanel(LeftStatsPanel, row: 0, column: 0, columnSpan: 1, Orientation.Vertical, HorizontalAlignment.Stretch);
        ArrangeTypingSurface(row: 0, column: 1, columnSpan: 1);
        ArrangeStatPanel(RightStatsPanel, row: 0, column: 2, columnSpan: 1, Orientation.Vertical, HorizontalAlignment.Stretch);
        LeftStatsPanel.Spacing = metrics.StatsPanelSpacing;
        RightStatsPanel.Spacing = metrics.StatsPanelSpacing;
    }

    private static void ArrangeStatPanel(StackPanel panel, int row, int column, int columnSpan, Orientation orientation, HorizontalAlignment horizontalAlignment)
    {
        panel.Orientation = orientation;
        panel.HorizontalAlignment = horizontalAlignment;
        Grid.SetRow(panel, row);
        Grid.SetColumn(panel, column);
        Grid.SetColumnSpan(panel, columnSpan);
    }

    private void ArrangeTypingSurface(int row, int column, int columnSpan)
    {
        Grid.SetRow(InputSurface, row);
        Grid.SetColumn(InputSurface, column);
        Grid.SetColumnSpan(InputSurface, columnSpan);
    }

    private void SetHeaderTypography(PracticeResponsiveLayoutMetrics metrics)
    {
        LessonReasonText.FontSize = metrics.MetadataFontSize;
        FocusKeysText.FontSize = metrics.MetadataFontSize;
        FocusBigramsText.FontSize = metrics.MetadataFontSize;
        LessonContentText.FontSize = metrics.MetadataFontSize;

        foreach (var valueText in new[] { RawWpmValueText, NetWpmValueText, AccuracyValueText, ElapsedValueText, ErrorsValueText, ProgressValueText })
        {
            valueText.FontSize = metrics.KpiValueFontSize;
        }

        foreach (var labelText in new[] { RawWpmLabelText, NetWpmLabelText, AccuracyLabelText, ElapsedLabelText, ErrorsLabelText, ProgressLabelText, CharactersText })
        {
            labelText.FontSize = metrics.KpiLabelFontSize;
        }
    }

    private void SetKpiTileSizing(PracticeResponsiveLayoutMetrics metrics)
    {
        foreach (var tile in new[] { RawWpmTile, NetWpmTile, AccuracyTile, ElapsedTile, ErrorsTile, ProgressTile })
        {
            tile.Padding = new Thickness(
                metrics.KpiTilePaddingHorizontal,
                metrics.KpiTilePaddingVertical,
                metrics.KpiTilePaddingHorizontal,
                metrics.KpiTilePaddingVertical);
            tile.MinHeight = metrics.KpiTileMinHeight;
            tile.MinWidth = metrics.KpiTileWidth.HasValue ? 96 : 0;
            tile.Width = metrics.KpiTileWidth ?? double.NaN;
        }
    }

    private void ArrangeReviewPopup(PracticeResponsiveLayoutMetrics metrics)
    {
        ReviewDialogBorder.Margin = new Thickness(metrics.ReviewMargin);
        ReviewDialogBorder.Padding = new Thickness(metrics.ReviewPadding);
        ReviewDialogBorder.MaxWidth = metrics.ReviewMaxWidth;
        ReviewScrollViewer.MaxHeight = metrics.ReviewMaxHeight;
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
        var fallbackSoundKind = feedback switch
        {
            PracticeInputFeedback.Mistake when ViewModel.MistakeSoundEnabled => ElementSoundKind.Focus,
            PracticeInputFeedback.Correction when ViewModel.KeySoundEnabled => ElementSoundKind.MovePrevious,
            PracticeInputFeedback.Key when ViewModel.KeySoundEnabled => ElementSoundKind.Invoke,
            _ => (ElementSoundKind?)null
        };

        if (fallbackSoundKind is not ElementSoundKind kind)
        {
            return;
        }

        try
        {
            switch (feedback)
            {
                case PracticeInputFeedback.Mistake:
                    KeyboardSoundPlayer.PlayMistake();
                    break;
                case PracticeInputFeedback.Correction:
                    KeyboardSoundPlayer.PlayCorrection();
                    break;
                case PracticeInputFeedback.Key:
                    KeyboardSoundPlayer.PlayKey();
                    break;
            }
        }
        catch
        {
            PlayTypingElementSound(kind);
        }
    }

    private KeyboardSoundPlayer KeyboardSoundPlayer => _keyboardSoundPlayer ??= new KeyboardSoundPlayer();

    private static void PlayTypingElementSound(ElementSoundKind soundKind)
    {
        try
        {
            var previousState = ElementSoundPlayer.State;
            ElementSoundPlayer.State = ElementSoundPlayerState.On;
            ElementSoundPlayer.Play(soundKind);
            ElementSoundPlayer.State = previousState;
        }
        catch
        {
            // Typing sounds are optional; platform sound failures should never affect typing.
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

    private void PracticePage_Unloaded(object sender, RoutedEventArgs e)
    {
        _keyboardSoundPlayer?.Dispose();
        _keyboardSoundPlayer = null;
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
