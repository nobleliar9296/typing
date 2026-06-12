using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TypingTrainer.App.ViewModels;
using TypingTrainer.Core.Lessons;
using Windows.System;

namespace TypingTrainer.App.Views;

public sealed partial class PracticePage : Page
{
    private bool _isLoaded;

    public PracticePage()
    {
        InitializeComponent();
        ViewModel = new PracticeViewModel(
            App.Services.SessionPersistenceQueue,
            App.Services.LessonService);
        DataContext = ViewModel;

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

    private void DashboardButton_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(DashboardPage));
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(SettingsPage));
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
        if (!_isLoaded)
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

    private void PracticeRoot_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            InputSurface.Focus(FocusState.Pointer);
        }
    }

    private void QueueScrollToCursor()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var textLength = Math.Max(1, ViewModel.CurrentState.TargetText.Length);
            var ratio = Math.Clamp(ViewModel.CurrentState.CursorIndex / (double)textLength, 0, 1);
            var offset = PracticeTextScrollViewer.ScrollableHeight * ratio;
            PracticeTextScrollViewer.ChangeView(null, offset, null, disableAnimation: true);
        });
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
