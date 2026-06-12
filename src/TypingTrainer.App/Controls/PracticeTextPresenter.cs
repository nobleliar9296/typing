using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System.Text;
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

    public static readonly DependencyProperty FontFamilyNameProperty = DependencyProperty.Register(
        nameof(FontFamilyName),
        typeof(string),
        typeof(PracticeTextPresenter),
        new PropertyMetadata("Cascadia Mono", OnDisplayScaleChanged));

    public static readonly DependencyProperty TextContrastProperty = DependencyProperty.Register(
        nameof(TextContrast),
        typeof(string),
        typeof(PracticeTextPresenter),
        new PropertyMetadata("Normal", OnStateChanged));

    public static readonly DependencyProperty CursorStyleProperty = DependencyProperty.Register(
        nameof(CursorStyle),
        typeof(string),
        typeof(PracticeTextPresenter),
        new PropertyMetadata("Underline", OnStateChanged));

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

    public string FontFamilyName
    {
        get => (string)GetValue(FontFamilyNameProperty);
        set => SetValue(FontFamilyNameProperty, value);
    }

    public string TextContrast
    {
        get => (string)GetValue(TextContrastProperty);
        set => SetValue(TextContrastProperty, value);
    }

    public string CursorStyle
    {
        get => (string)GetValue(CursorStyleProperty);
        set => SetValue(CursorStyleProperty, value);
    }

    public double GetEstimatedCursorOffsetY()
    {
        if (State is null || State.TargetText.Length == 0)
        {
            return 0;
        }

        var scale = Math.Clamp(DisplayScale, 0.5, 1.3);
        var fontSize = 34 * scale;
        var lineHeight = 48 * scale;
        var availableWidth = Math.Max(ActualWidth, 1);
        var estimatedCharactersPerLine = Math.Max(12, (int)Math.Floor(availableWidth / (fontSize * 0.58)));
        var cursor = Math.Clamp(State.CursorIndex, 0, State.TargetText.Length);
        var line = 0;
        var column = 0;

        for (var index = 0; index < cursor; index++)
        {
            if (State.TargetText[index] == '\n')
            {
                line++;
                column = 0;
                continue;
            }

            column++;
            if (column >= estimatedCharactersPerLine)
            {
                line++;
                column = 0;
            }
        }

        return Math.Max(0, line * lineHeight);
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
        var scale = Math.Clamp(DisplayScale, 0.5, 1.3);
        _textBlock.FontFamily = new FontFamily(string.IsNullOrWhiteSpace(FontFamilyName) ? "Cascadia Mono, Consolas" : $"{FontFamilyName}, Consolas");
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
        if (string.Equals(CursorStyle, "Bold", StringComparison.OrdinalIgnoreCase))
        {
            _textBlock.Inlines.Add(new Run
            {
                Text = expectedChar.ToString(),
                Foreground = CurrentBrush,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold
            });
            return;
        }

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

    private Brush GetBrush(CharacterState state)
    {
        return state switch
        {
            CharacterState.Correct => CorrectBrush,
            CharacterState.Incorrect => IncorrectBrush,
            CharacterState.Current => CurrentBrush,
            _ => string.Equals(TextContrast, "High", StringComparison.OrdinalIgnoreCase)
                ? new SolidColorBrush(Color.FromArgb(255, 128, 132, 136))
                : PendingBrush
        };
    }
}
