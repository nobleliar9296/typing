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

    private static readonly SolidColorBrush CorrectBrush = new(Color.FromArgb(255, 132, 136, 140));
    private static readonly SolidColorBrush LatestCorrectBrush = new(Color.FromArgb(255, 32, 145, 108));
    private static readonly SolidColorBrush IncorrectBrush = new(Color.FromArgb(255, 196, 43, 55));
    private static readonly SolidColorBrush FallbackTargetBrush = new(Color.FromArgb(255, 18, 18, 18));

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
        VisualCharacterState? activeState = null;
        var latestCorrectPosition = GetLatestCorrectPosition(State);

        foreach (var character in State.Characters)
        {
            var visualState = GetVisualState(character, latestCorrectPosition);

            if (visualState == VisualCharacterState.Current)
            {
                FlushRun(textBuilder, activeState);
                activeState = null;
                AddCurrentRun(character.ExpectedChar);
                continue;
            }

            if (activeState is not null && activeState != visualState)
            {
                FlushRun(textBuilder, activeState);
            }

            activeState = visualState;
            textBuilder.Append(GetDisplayChar(character));
        }

        FlushRun(textBuilder, activeState);
    }

    private void FlushRun(StringBuilder textBuilder, VisualCharacterState? state)
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
                Foreground = GetBrush(VisualCharacterState.Current),
                FontWeight = Microsoft.UI.Text.FontWeights.Bold
            });
            return;
        }

        var underline = new Underline
        {
            Foreground = GetBrush(VisualCharacterState.Current)
        };

        underline.Inlines.Add(new Run { Text = expectedChar.ToString() });
        _textBlock.Inlines.Add(underline);
    }

    private static char GetDisplayChar(CharacterSnapshot character)
    {
        return character.ExpectedChar;
    }

    private static VisualCharacterState GetVisualState(CharacterSnapshot character, int? latestCorrectPosition)
    {
        return character.State switch
        {
            CharacterState.Correct when latestCorrectPosition == character.Position => VisualCharacterState.LatestCorrect,
            CharacterState.Correct => VisualCharacterState.Correct,
            CharacterState.Incorrect => VisualCharacterState.Incorrect,
            CharacterState.Current => VisualCharacterState.Current,
            _ => VisualCharacterState.Pending
        };
    }

    private static int? GetLatestCorrectPosition(TypingStateSnapshot state)
    {
        var latestPosition = Math.Min(state.CursorIndex - 1, state.Characters.Count - 1);
        return latestPosition >= 0 && state.Characters[latestPosition].State == CharacterState.Correct
            ? latestPosition
            : null;
    }

    private Brush GetBrush(VisualCharacterState state)
    {
        return state switch
        {
            VisualCharacterState.LatestCorrect => LatestCorrectBrush,
            VisualCharacterState.Correct => CorrectBrush,
            VisualCharacterState.Incorrect => IncorrectBrush,
            _ => GetTargetBrush()
        };
    }

    private static Brush GetTargetBrush()
    {
        if (Application.Current?.Resources.TryGetValue("TextFillColorPrimaryBrush", out var brush) == true
            && brush is Brush targetBrush)
        {
            return targetBrush;
        }

        return FallbackTargetBrush;
    }

    private enum VisualCharacterState
    {
        Pending,
        Current,
        Correct,
        LatestCorrect,
        Incorrect
    }
}
