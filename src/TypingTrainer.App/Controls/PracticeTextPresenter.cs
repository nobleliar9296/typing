using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using TypingTrainer.Core.Typing;
using Windows.UI;

namespace TypingTrainer.App.Controls;

public sealed class PracticeTextPresenter : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(TypingStateSnapshot),
        typeof(PracticeTextPresenter),
        new PropertyMetadata(null, OnStateChanged));

    public static readonly DependencyProperty DisplayScaleProperty = DependencyProperty.Register(
        nameof(DisplayScale),
        typeof(double),
        typeof(PracticeTextPresenter),
        new PropertyMetadata(1.0, OnDisplayScaleChanged));

    private static readonly SolidColorBrush CorrectBrush = new(Color.FromArgb(255, 32, 145, 108));
    private static readonly SolidColorBrush IncorrectBrush = new(Color.FromArgb(255, 196, 43, 55));
    private static readonly SolidColorBrush CurrentBrush = new(Color.FromArgb(255, 0, 95, 184));
    private static readonly SolidColorBrush PendingBrush = new(Color.FromArgb(255, 88, 92, 95));

    private readonly TextBlock _textBlock = new()
    {
        FontFamily = new FontFamily("Cascadia Mono, Consolas"),
        FontSize = 34,
        LineHeight = 48,
        TextWrapping = TextWrapping.Wrap
    };

    public PracticeTextPresenter()
    {
        Content = _textBlock;
        ApplyDisplayScale();
    }

    public TypingStateSnapshot? State
    {
        get => (TypingStateSnapshot?)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public double DisplayScale
    {
        get => (double)GetValue(DisplayScaleProperty);
        set => SetValue(DisplayScaleProperty, value);
    }

    private static void OnStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is PracticeTextPresenter presenter)
        {
            presenter.Render();
        }
    }

    private static void OnDisplayScaleChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is PracticeTextPresenter presenter)
        {
            presenter.ApplyDisplayScale();
        }
    }

    private void ApplyDisplayScale()
    {
        var scale = Math.Clamp(DisplayScale, 0.68, 1.0);
        _textBlock.FontSize = 34 * scale;
        _textBlock.LineHeight = 48 * scale;
    }

    private void Render()
    {
        _textBlock.Inlines.Clear();

        if (State is null)
        {
            return;
        }

        var textBuilder = new StringBuilder();
        CharacterState? activeState = null;

        foreach (var character in State.Characters)
        {
            if (character.State == CharacterState.Current)
            {
                FlushRun(textBuilder, activeState);
                activeState = null;
                AddCurrentRun(character.ExpectedChar);
                continue;
            }

            if (activeState is not null && activeState != character.State)
            {
                FlushRun(textBuilder, activeState);
            }

            activeState = character.State;
            textBuilder.Append(GetDisplayChar(character));
        }

        FlushRun(textBuilder, activeState);
    }

    private void FlushRun(StringBuilder textBuilder, CharacterState? state)
    {
        if (state is null || textBuilder.Length == 0)
        {
            return;
        }

        _textBlock.Inlines.Add(new Run
        {
            Text = textBuilder.ToString(),
            Foreground = GetBrush(state.Value)
        });

        textBuilder.Clear();
    }

    private void AddCurrentRun(char expectedChar)
    {
        var underline = new Underline
        {
            Foreground = CurrentBrush
        };

        underline.Inlines.Add(new Run { Text = expectedChar.ToString() });
        _textBlock.Inlines.Add(underline);
    }

    private static char GetDisplayChar(CharacterSnapshot character)
    {
        return character.ActualChar ?? character.ExpectedChar;
    }

    private static Brush GetBrush(CharacterState state)
    {
        return state switch
        {
            CharacterState.Correct => CorrectBrush,
            CharacterState.Incorrect => IncorrectBrush,
            CharacterState.Current => CurrentBrush,
            _ => PendingBrush
        };
    }
}
