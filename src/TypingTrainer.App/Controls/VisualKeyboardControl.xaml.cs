using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TypingTrainer.Core.Keyboard;
using Windows.UI;

namespace TypingTrainer.App.Controls;

public sealed partial class VisualKeyboardControl : UserControl
{
    public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(
        nameof(Layout),
        typeof(VisualKeyboardLayout),
        typeof(VisualKeyboardControl),
        new PropertyMetadata(null, OnLayoutChanged));

    public static readonly DependencyProperty HighlightedKeyIdProperty = DependencyProperty.Register(
        nameof(HighlightedKeyId),
        typeof(string),
        typeof(VisualKeyboardControl),
        new PropertyMetadata(null, OnHighlightChanged));

    public static readonly DependencyProperty HighlightedShiftKeyIdProperty = DependencyProperty.Register(
        nameof(HighlightedShiftKeyId),
        typeof(string),
        typeof(VisualKeyboardControl),
        new PropertyMetadata(null, OnHighlightChanged));

    public static readonly DependencyProperty ShowFingerColorsProperty = DependencyProperty.Register(
        nameof(ShowFingerColors),
        typeof(bool),
        typeof(VisualKeyboardControl),
        new PropertyMetadata(true, OnAppearanceChanged));

    public static readonly DependencyProperty ShowFingerLabelsProperty = DependencyProperty.Register(
        nameof(ShowFingerLabels),
        typeof(bool),
        typeof(VisualKeyboardControl),
        new PropertyMetadata(false, OnAppearanceChanged));

    public static readonly DependencyProperty KeyboardScaleProperty = DependencyProperty.Register(
        nameof(KeyboardScale),
        typeof(double),
        typeof(VisualKeyboardControl),
        new PropertyMetadata(1.0, OnAppearanceChanged));

    private static readonly SolidColorBrush NeutralKeyBrush = Brush(82, 88, 92);
    private static readonly SolidColorBrush NeutralBorderBrush = Brush(54, 57, 60);
    private static readonly SolidColorBrush NormalTextBrush = Brush(224, 226, 228);
    private static readonly SolidColorBrush HighlightBackgroundBrush = Brush(34, 83, 125);
    private static readonly SolidColorBrush HighlightBorderBrush = Brush(102, 185, 255);
    private static readonly SolidColorBrush ShiftHighlightBackgroundBrush = Brush(66, 70, 78);
    private static readonly SolidColorBrush ShiftHighlightBorderBrush = Brush(232, 191, 87);
    private static readonly SolidColorBrush HighlightTextBrush = new(Colors.White);

    private readonly Dictionary<string, Border> _keyBorders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, VisualKeyboardKey> _keys = new(StringComparer.Ordinal);

    public VisualKeyboardControl()
    {
        InitializeComponent();
    }

    public VisualKeyboardLayout? Layout
    {
        get => (VisualKeyboardLayout?)GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    public string? HighlightedKeyId
    {
        get => (string?)GetValue(HighlightedKeyIdProperty);
        set => SetValue(HighlightedKeyIdProperty, value);
    }

    public string? HighlightedShiftKeyId
    {
        get => (string?)GetValue(HighlightedShiftKeyIdProperty);
        set => SetValue(HighlightedShiftKeyIdProperty, value);
    }

    public bool ShowFingerColors
    {
        get => (bool)GetValue(ShowFingerColorsProperty);
        set => SetValue(ShowFingerColorsProperty, value);
    }

    public bool ShowFingerLabels
    {
        get => (bool)GetValue(ShowFingerLabelsProperty);
        set => SetValue(ShowFingerLabelsProperty, value);
    }

    public double KeyboardScale
    {
        get => (double)GetValue(KeyboardScaleProperty);
        set => SetValue(KeyboardScaleProperty, value);
    }

    private static void OnLayoutChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is VisualKeyboardControl control)
        {
            control.RebuildKeyboard();
        }
    }

    private static void OnHighlightChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is VisualKeyboardControl control)
        {
            control.UpdateKeyStyles();
        }
    }

    private static void OnAppearanceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is VisualKeyboardControl control)
        {
            control.RebuildKeyboard();
        }
    }

    private void RebuildKeyboard()
    {
        RowsPanel.Children.Clear();
        _keyBorders.Clear();
        _keys.Clear();

        if (Layout is null)
        {
            return;
        }

        var scale = GetScale();
        KeyboardFrame.Padding = new Thickness(12 * scale);
        RowsPanel.Spacing = scale < 0.75 ? 0 : 1;

        foreach (var row in Layout.Rows)
        {
            var rowGrid = new Grid
            {
                ColumnSpacing = scale < 0.75 ? 0 : 1,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            for (var index = 0; index < row.Keys.Count; index++)
            {
                var key = row.Keys[index];
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(Math.Max(0.25, key.WidthUnits), GridUnitType.Star)
                });

                var keyVisual = CreateKeyVisual(key, scale);
                Grid.SetColumn(keyVisual, index);
                rowGrid.Children.Add(keyVisual);

                _keys[key.Id] = key;
                _keyBorders[key.Id] = keyVisual;
            }

            RowsPanel.Children.Add(rowGrid);
        }

        UpdateKeyStyles();
    }

    private Border CreateKeyVisual(VisualKeyboardKey key, double scale)
    {
        var labels = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 0
        };

        if (!string.IsNullOrEmpty(key.SecondaryLabel))
        {
            labels.Children.Add(CreateLabel(key.SecondaryLabel, 12 * scale, 0.74));
        }

        labels.Children.Add(CreateLabel(key.PrimaryLabel, (key.Role == KeyRole.Character ? 17 : 13) * scale, 1.0));

        if (ShowFingerLabels)
        {
            labels.Children.Add(CreateLabel(GetFingerLabel(key.Finger), 9 * scale, 0.68));
        }

        return new Border
        {
            MinHeight = 58 * scale,
            Padding = new Thickness(4 * scale, 4 * scale, 4 * scale, 4 * scale),
            CornerRadius = new CornerRadius(1),
            Child = labels
        };
    }

    private static TextBlock CreateLabel(string text, double fontSize, double opacity)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = NormalTextBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1,
            Opacity = opacity
        };
    }

    private void UpdateKeyStyles()
    {
        var scale = GetScale();
        foreach (var (keyId, border) in _keyBorders)
        {
            var key = _keys[keyId];
            var isCurrent = string.Equals(keyId, HighlightedKeyId, StringComparison.Ordinal);
            var isShift = string.Equals(keyId, HighlightedShiftKeyId, StringComparison.Ordinal);

            border.Background = isCurrent
                ? HighlightBackgroundBrush
                : isShift
                    ? ShiftHighlightBackgroundBrush
                    : GetNormalKeyBrush(key);
            border.BorderBrush = isCurrent
                ? HighlightBorderBrush
                : isShift
                    ? ShiftHighlightBorderBrush
                    : NeutralBorderBrush;
            border.BorderThickness = isCurrent
                ? new Thickness(3 * scale)
                : isShift
                    ? new Thickness(2 * scale)
                    : new Thickness(1);
            border.Opacity = isCurrent || isShift ? 1.0 : 0.76;
            SetTextBrush(border, isCurrent || isShift ? HighlightTextBrush : NormalTextBrush);
        }
    }

    private Brush GetNormalKeyBrush(VisualKeyboardKey key)
    {
        return ShowFingerColors ? GetFingerBrush(key.Finger) : NeutralKeyBrush;
    }

    private static SolidColorBrush GetFingerBrush(FingerAssignment finger)
    {
        return finger switch
        {
            FingerAssignment.LeftPinky => Brush(94, 126, 86),
            FingerAssignment.LeftRing => Brush(120, 125, 45),
            FingerAssignment.LeftMiddle => Brush(158, 126, 44),
            FingerAssignment.LeftIndex => Brush(87, 106, 100),
            FingerAssignment.LeftThumb => Brush(126, 71, 65),
            FingerAssignment.RightThumb => Brush(102, 123, 84),
            FingerAssignment.RightIndex => Brush(138, 92, 104),
            FingerAssignment.RightMiddle => Brush(155, 126, 45),
            FingerAssignment.RightRing => Brush(126, 128, 43),
            FingerAssignment.RightPinky => Brush(94, 126, 86),
            _ => NeutralKeyBrush
        };
    }

    private static void SetTextBrush(DependencyObject dependencyObject, Brush brush)
    {
        if (dependencyObject is TextBlock textBlock)
        {
            textBlock.Foreground = brush;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(dependencyObject);
        for (var index = 0; index < childCount; index++)
        {
            SetTextBrush(VisualTreeHelper.GetChild(dependencyObject, index), brush);
        }
    }

    private static string GetFingerLabel(FingerAssignment finger)
    {
        return finger switch
        {
            FingerAssignment.LeftPinky => "LP",
            FingerAssignment.LeftRing => "LR",
            FingerAssignment.LeftMiddle => "LM",
            FingerAssignment.LeftIndex => "LI",
            FingerAssignment.LeftThumb => "LT",
            FingerAssignment.RightThumb => "RT",
            FingerAssignment.RightIndex => "RI",
            FingerAssignment.RightMiddle => "RM",
            FingerAssignment.RightRing => "RR",
            FingerAssignment.RightPinky => "RP",
            _ => string.Empty
        };
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(Color.FromArgb(255, red, green, blue));
    }

    private double GetScale()
    {
        return Math.Clamp(KeyboardScale, 0.58, 1.0);
    }
}
