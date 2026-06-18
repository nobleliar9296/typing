using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System.Text;
using TypingTrainer.Data.Models;
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
    private static readonly SolidColorBrush CorrectedMistakeBrush = new(Color.FromArgb(255, 32, 145, 108));
    private static readonly SolidColorBrush IncorrectBrush = new(Color.FromArgb(255, 196, 43, 55));
    private static readonly SolidColorBrush FallbackTargetBrush = new(Color.FromArgb(255, 18, 18, 18));
    private static readonly SolidColorBrush FallbackCursorBrush = new(Color.FromArgb(255, 0, 120, 212));

    private readonly Grid _host = new() { Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)) };
    private readonly TranslateTransform _cursorTransform = new();
    private readonly Border _cursor = new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        IsHitTestVisible = false,
        RenderTransformOrigin = new Windows.Foundation.Point(0, 0),
        Visibility = Visibility.Collapsed
    };
    private readonly TextBlock _textBlock = new()
    {
        FontFamily = new FontFamily("Cascadia Mono, Consolas"),
        FontSize = 34,
        LineHeight = 48,
        TextWrapping = TextWrapping.NoWrap
    };
    private readonly TextBlock _measureTextBlock = new()
    {
        FontFamily = new FontFamily("Cascadia Mono, Consolas"),
        FontSize = 34,
        LineHeight = 48,
        TextWrapping = TextWrapping.NoWrap
    };
    private Storyboard? _cursorStoryboard;
    private bool _hasCursorPosition;
    private Windows.Foundation.Rect? _lastCursorBounds;
    private PracticeTextLayoutSnapshot? _layoutSnapshot;

    public PracticeTextPresenter()
    {
        _cursor.RenderTransform = _cursorTransform;
        Canvas.SetZIndex(_cursor, 2);
        Canvas.SetZIndex(_textBlock, 1);
        _host.Children.Add(_cursor);
        _host.Children.Add(_textBlock);
        Content = _host;
        ApplyDisplayScale();
        SizeChanged += (_, _) => Render();
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
        return GetEstimatedCursorBounds().Y;
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
        var fontFamily = AppSettings.NormalizePracticeFontFamily(FontFamilyName);
        _textBlock.FontFamily = new FontFamily($"{fontFamily}, Consolas");
        _textBlock.FontSize = 34 * scale;
        _textBlock.LineHeight = 48 * scale;
        _measureTextBlock.FontFamily = _textBlock.FontFamily;
        _measureTextBlock.FontSize = _textBlock.FontSize;
        _measureTextBlock.LineHeight = _textBlock.LineHeight;
        _layoutSnapshot = null;
        UpdateCursor(animate: false);
    }

    private void Render()
    {
        _textBlock.Inlines.Clear();

        if (State is null)
        {
            _layoutSnapshot = null;
            _cursor.Visibility = Visibility.Collapsed;
            return;
        }

        var textBuilder = new StringBuilder();
        VisualCharacterState? activeState = null;

        _layoutSnapshot = BuildLayoutSnapshot();
        var layout = _layoutSnapshot.Layout;

        for (var index = 0; index < State.Characters.Count; index++)
        {
            var character = State.Characters[index];
            var visualState = GetVisualState(character);

            if (index > 0
                && layout[index].Line > layout[index - 1].Line
                && State.Characters[index - 1].ExpectedChar != '\n')
            {
                textBuilder.Append('\n');
            }

            if (visualState == VisualCharacterState.Current)
            {
                FlushRun(textBuilder, activeState);
                activeState = visualState;
            }

            if (activeState is not null && activeState != visualState)
            {
                FlushRun(textBuilder, activeState);
            }

            activeState = visualState;
            textBuilder.Append(GetDisplayChar(character));
        }

        FlushRun(textBuilder, activeState);
        UpdateCursor();
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

    private void UpdateCursor(bool animate = true)
    {
        if (State is null || State.IsComplete || State.CurrentExpectedCharacter is null)
        {
            _cursor.Visibility = Visibility.Collapsed;
            _hasCursorPosition = false;
            _lastCursorBounds = null;
            return;
        }

        var style = AppSettings.NormalizeCursorStyle(CursorStyle);
        var bounds = GetEstimatedCursorBounds();
        var cursorShape = CreateCursorShape(bounds, style);

        ConfigureCursorStyle(style);
        MoveCursor(cursorShape, animate && _hasCursorPosition);
        _hasCursorPosition = true;
        _cursor.Visibility = Visibility.Visible;
    }

    private Windows.Foundation.Rect GetEstimatedCursorBounds()
    {
        if (State is null || State.TargetText.Length == 0)
        {
            return new Windows.Foundation.Rect(0, 0, 0, 0);
        }

        var snapshot = GetLayoutSnapshot();
        var cursor = Math.Clamp(State.CursorIndex, 0, State.TargetText.Length);
        var position = GetCursorPosition(cursor, snapshot);

        return new Windows.Foundation.Rect(
            position.Column * snapshot.CharacterWidth,
            position.Line * snapshot.LineHeight,
            snapshot.CharacterWidth,
            snapshot.LineHeight);
    }

    private CharacterLayoutPosition GetCursorPosition(int cursor, PracticeTextLayoutSnapshot snapshot)
    {
        if (State is null || State.Characters.Count == 0 || snapshot.Layout.Length == 0)
        {
            return new CharacterLayoutPosition(0, 0);
        }

        if (cursor < snapshot.Layout.Length)
        {
            return snapshot.Layout[cursor];
        }

        var lastCharacter = State.Characters[^1];
        var lastPosition = snapshot.Layout[^1];
        if (lastCharacter.ExpectedChar == '\n')
        {
            return new CharacterLayoutPosition(lastPosition.Line + 1, 0);
        }

        var nextColumn = lastPosition.Column + 1;
        if (nextColumn >= snapshot.ColumnCapacity)
        {
            return new CharacterLayoutPosition(lastPosition.Line + 1, 0);
        }

        return new CharacterLayoutPosition(lastPosition.Line, nextColumn);
    }

    private PracticeTextLayoutSnapshot GetLayoutSnapshot()
    {
        _layoutSnapshot ??= BuildLayoutSnapshot();
        return _layoutSnapshot;
    }

    private PracticeTextLayoutSnapshot BuildLayoutSnapshot()
    {
        var scale = Math.Clamp(DisplayScale, 0.5, 1.3);
        var characterWidth = MeasureCharacterWidth();
        var lineHeight = 48 * scale;
        var columnCapacity = GetColumnCapacity(characterWidth);
        return new PracticeTextLayoutSnapshot(
            BuildCharacterLayout(columnCapacity),
            characterWidth,
            lineHeight,
            columnCapacity);
    }

    private CharacterLayoutPosition[] BuildCharacterLayout(int maxColumns)
    {
        if (State is null || State.Characters.Count == 0)
        {
            return [];
        }

        var layout = new CharacterLayoutPosition[State.Characters.Count];
        var line = 0;
        var start = 0;

        while (start < State.Characters.Count)
        {
            if (State.Characters[start].ExpectedChar == '\n')
            {
                layout[start] = new CharacterLayoutPosition(line, 0);
                line++;
                start++;
                continue;
            }

            var lineEnd = FindLineEnd(start, maxColumns);
            if (lineEnd <= start)
            {
                lineEnd = Math.Min(start + 1, State.Characters.Count);
            }

            for (var index = start; index < lineEnd; index++)
            {
                layout[index] = new CharacterLayoutPosition(line, index - start);
            }

            start = lineEnd;
            if (start < State.Characters.Count && State.Characters[start].ExpectedChar != '\n')
            {
                line++;
            }
        }

        return layout;
    }

    private int FindLineEnd(int start, int maxColumns)
    {
        if (State is null)
        {
            return start;
        }

        var count = State.Characters.Count;
        var hardLimit = Math.Min(start + maxColumns, count);
        for (var index = start; index < hardLimit; index++)
        {
            if (State.Characters[index].ExpectedChar == '\n')
            {
                return index;
            }
        }

        if (hardLimit >= count)
        {
            return count;
        }

        var lastSpace = -1;
        for (var index = start; index < hardLimit; index++)
        {
            if (State.Characters[index].ExpectedChar == ' ')
            {
                lastSpace = index;
            }
        }

        return lastSpace > start ? lastSpace + 1 : hardLimit;
    }

    private int GetColumnCapacity(double characterWidth)
    {
        var availableWidth = GetAvailableTextWidth();
        return Math.Max(1, (int)Math.Floor(availableWidth / Math.Max(characterWidth, 1)));
    }

    private double MeasureCharacterWidth()
    {
        var fallbackWidth = Math.Max(1, _textBlock.FontSize * 0.58);
        const int sampleLength = 40;

        _measureTextBlock.Text = new string('m', sampleLength);
        _measureTextBlock.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));

        var measuredWidth = _measureTextBlock.DesiredSize.Width / sampleLength;
        return double.IsFinite(measuredWidth) && measuredWidth > 0 ? measuredWidth : fallbackWidth;
    }

    private double GetAvailableTextWidth()
    {
        if (ActualWidth > 1)
        {
            return ActualWidth;
        }

        if (_textBlock.ActualWidth > 1)
        {
            return _textBlock.ActualWidth;
        }

        return double.IsFinite(MaxWidth) && MaxWidth > 1 ? MaxWidth : 1040;
    }

    private Windows.Foundation.Rect CreateCursorShape(Windows.Foundation.Rect characterBounds, string style)
    {
        var scale = Math.Clamp(DisplayScale, 0.5, 1.3);
        var fontSize = 34 * scale;
        var lineHeight = characterBounds.Height;
        var characterWidth = Math.Max(characterBounds.Width, fontSize * 0.45);

        return style switch
        {
            AppSettings.BarCursorStyle => new Windows.Foundation.Rect(
                Math.Max(0, characterBounds.X - (1.5 * scale)),
                characterBounds.Y + Math.Max(0, (lineHeight - (fontSize * 1.08)) / 2),
                Math.Max(2, 3 * scale),
                fontSize * 1.08),
            AppSettings.BlockCursorStyle or AppSettings.OutlineCursorStyle => new Windows.Foundation.Rect(
                characterBounds.X,
                characterBounds.Y + Math.Max(0, (lineHeight - (fontSize * 1.12)) / 2),
                characterWidth,
                fontSize * 1.12),
            _ => new Windows.Foundation.Rect(
                characterBounds.X,
                characterBounds.Y + Math.Max(0, lineHeight - (7 * scale)),
                characterWidth,
                Math.Max(3, 4 * scale))
        };
    }

    private void ConfigureCursorStyle(string style)
    {
        var cursorColor = GetCursorColor();
        var cursorBrush = new SolidColorBrush(cursorColor);

        _cursor.BorderBrush = null;
        _cursor.Background = cursorBrush;
        _cursor.BorderThickness = new Thickness(0);
        _cursor.CornerRadius = new CornerRadius(2);
        _cursor.Opacity = 1;
        Canvas.SetZIndex(_cursor, 2);

        switch (style)
        {
            case AppSettings.BlockCursorStyle:
                _cursor.Background = new SolidColorBrush(Color.FromArgb(52, cursorColor.R, cursorColor.G, cursorColor.B));
                _cursor.CornerRadius = new CornerRadius(4);
                Canvas.SetZIndex(_cursor, 0);
                break;
            case AppSettings.OutlineCursorStyle:
                _cursor.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                _cursor.BorderBrush = cursorBrush;
                _cursor.BorderThickness = new Thickness(2);
                _cursor.CornerRadius = new CornerRadius(4);
                break;
            case AppSettings.BarCursorStyle:
                _cursor.CornerRadius = new CornerRadius(2);
                break;
        }
    }

    private void MoveCursor(Windows.Foundation.Rect bounds, bool animate)
    {
        var startBounds = _lastCursorBounds
            ?? new Windows.Foundation.Rect(_cursorTransform.X, _cursorTransform.Y, _cursor.Width, _cursor.Height);
        _cursorStoryboard?.Stop();
        _cursorStoryboard = null;

        if (!animate)
        {
            ApplyCursorBounds(bounds);
            _lastCursorBounds = bounds;
            return;
        }

        ApplyCursorBounds(startBounds);
        _cursorStoryboard = new Storyboard();
        AddCursorAnimation(_cursorStoryboard, _cursorTransform, nameof(TranslateTransform.X), startBounds.X, bounds.X, enableDependentAnimation: false);
        AddCursorAnimation(_cursorStoryboard, _cursorTransform, nameof(TranslateTransform.Y), startBounds.Y, bounds.Y, enableDependentAnimation: false);
        AddCursorAnimation(_cursorStoryboard, _cursor, nameof(Width), startBounds.Width, bounds.Width, enableDependentAnimation: true);
        AddCursorAnimation(_cursorStoryboard, _cursor, nameof(Height), startBounds.Height, bounds.Height, enableDependentAnimation: true);
        _cursorStoryboard.Begin();
        _lastCursorBounds = bounds;
    }

    private void ApplyCursorBounds(Windows.Foundation.Rect bounds)
    {
        _cursorTransform.X = bounds.X;
        _cursorTransform.Y = bounds.Y;
        _cursor.Width = bounds.Width;
        _cursor.Height = bounds.Height;
    }

    private static void AddCursorAnimation(Storyboard storyboard, DependencyObject target, string property, double from, double to, bool enableDependentAnimation)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(95)),
            EnableDependentAnimation = enableDependentAnimation,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        storyboard.Children.Add(animation);
    }

    private static char GetDisplayChar(CharacterSnapshot character)
    {
        return character.ExpectedChar == ' ' ? '\u00B7' : character.ExpectedChar;
    }

    private static VisualCharacterState GetVisualState(CharacterSnapshot character)
    {
        return character.State switch
        {
            CharacterState.Correct when character.HadIncorrectInput => VisualCharacterState.CorrectedMistake,
            CharacterState.Correct => VisualCharacterState.Correct,
            CharacterState.Incorrect => VisualCharacterState.Incorrect,
            CharacterState.Current => VisualCharacterState.Current,
            _ => VisualCharacterState.Pending
        };
    }

    private Brush GetBrush(VisualCharacterState state)
    {
        return state switch
        {
            VisualCharacterState.CorrectedMistake => CorrectedMistakeBrush,
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

    private static Color GetCursorColor()
    {
        if (Application.Current?.Resources.TryGetValue("SystemAccentColor", out var accentColor) == true
            && accentColor is Color color)
        {
            return color;
        }

        if (Application.Current?.Resources.TryGetValue("AccentFillColorDefaultBrush", out var accentBrush) == true
            && accentBrush is SolidColorBrush solidColorBrush)
        {
            return solidColorBrush.Color;
        }

        return FallbackCursorBrush.Color;
    }

    private enum VisualCharacterState
    {
        Pending,
        Current,
        Correct,
        CorrectedMistake,
        Incorrect
    }

    private readonly record struct CharacterLayoutPosition(int Line, int Column);

    private sealed record PracticeTextLayoutSnapshot(
        CharacterLayoutPosition[] Layout,
        double CharacterWidth,
        double LineHeight,
        int ColumnCapacity);
}
