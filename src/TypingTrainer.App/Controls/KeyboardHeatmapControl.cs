using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TypingTrainer.App.Services;
using TypingTrainer.App.ViewModels;
using TypingTrainer.Core.Keyboard;
using Windows.UI;

namespace TypingTrainer.App.Controls;

public sealed class KeyboardHeatmapControl : UserControl
{
    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
        nameof(Rows),
        typeof(IReadOnlyList<KeyboardHeatmapDisplayRow>),
        typeof(KeyboardHeatmapControl),
        new PropertyMetadata(Array.Empty<KeyboardHeatmapDisplayRow>(), OnRowsChanged));

    private static readonly SolidColorBrush TrayBrush = new(Color.FromArgb(255, 35, 35, 35));
    private static readonly SolidColorBrush EmptyBrush = new(Color.FromArgb(255, 70, 76, 70));
    private static readonly SolidColorBrush StrongBrush = new(Color.FromArgb(255, 42, 132, 84));
    private static readonly SolidColorBrush MediumBrush = new(Color.FromArgb(255, 132, 112, 40));
    private static readonly SolidColorBrush WeakBrush = new(Color.FromArgb(255, 145, 62, 66));
    private static readonly SolidColorBrush KeyBorderBrush = new(Color.FromArgb(255, 38, 38, 38));

    private readonly VisualKeyboardLayout _layout = QwertyVisualKeyboardLayout.Create();
    private readonly Border _root = new()
    {
        Padding = new Thickness(10),
        CornerRadius = new CornerRadius(8),
        Background = TrayBrush,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    public KeyboardHeatmapControl()
    {
        Content = _root;
        IsTabStop = false;
        ActualThemeChanged += (_, _) => Render();
        Render();
    }

    public IReadOnlyList<KeyboardHeatmapDisplayRow> Rows
    {
        get => (IReadOnlyList<KeyboardHeatmapDisplayRow>)GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    private static void OnRowsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is KeyboardHeatmapControl control)
        {
            control.Render();
        }
    }

    private void Render()
    {
        var heatmap = (Rows ?? Array.Empty<KeyboardHeatmapDisplayRow>())
            .GroupBy(row => NormalizeHeatmapLabel(row.Key), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(row => row.WeaknessPercent)
                    .ThenByDescending(row => ParseSampleCount(row.Samples))
                    .First(),
                StringComparer.OrdinalIgnoreCase);
        var panel = new StackPanel { Spacing = 2 };
        _root.Background = ThemeContrast.IsLightTheme(this)
            ? ThemeContrast.Brush(242, 245, 248)
            : TrayBrush;

        foreach (var row in _layout.Rows)
        {
            var rowGrid = new Grid { ColumnSpacing = 2, Height = 34 };
            for (var index = 0; index < row.Keys.Count; index++)
            {
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(Math.Max(0.8, row.Keys[index].WidthUnits), GridUnitType.Star)
                });
            }

            for (var index = 0; index < row.Keys.Count; index++)
            {
                var key = row.Keys[index];
                var label = GetHeatmapLabel(key);
                heatmap.TryGetValue(label, out var value);
                var keyBrush = value is null ? EmptyBrush : GetHeatBrush(value.WeaknessPercent);
                var keyBorder = new Border
                {
                    Background = keyBrush,
                    BorderBrush = ThemeContrast.IsLightTheme(this) ? ThemeContrast.Brush(213, 220, 228) : KeyBorderBrush,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(4),
                    Child = new TextBlock
                    {
                        Text = key.PrimaryLabel,
                        FontSize = key.WidthUnits > 1.5 ? 10 : 12,
                        Foreground = ThemeContrast.ReadableTextBrush(keyBrush.Color),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis
                    }
                };
                ToolTipService.SetToolTip(keyBorder, value is null
                    ? $"{label}: no samples"
                    : $"{label}: {value.Accuracy}, {value.MedianLatencyMs} ms, {value.Samples} samples");
                Grid.SetColumn(keyBorder, index);
                rowGrid.Children.Add(keyBorder);
            }

            panel.Children.Add(rowGrid);
        }

        _root.Child = panel;
    }

    private static SolidColorBrush GetHeatBrush(double weaknessPercent)
    {
        return weaknessPercent switch
        {
            >= 55 => WeakBrush,
            >= 25 => MediumBrush,
            _ => StrongBrush
        };
    }

    private static string GetHeatmapLabel(VisualKeyboardKey key)
    {
        if (key.Role == KeyRole.Space)
        {
            return "Space";
        }

        return key.PrimaryLabel.Length == 1
            ? key.PrimaryLabel.ToLowerInvariant()
            : key.PrimaryLabel;
    }

    private static string NormalizeHeatmapLabel(string label)
    {
        return label.Length == 1
            ? label.ToLowerInvariant()
            : label;
    }

    private static int ParseSampleCount(string samples)
    {
        return int.TryParse(samples, out var value) ? value : 0;
    }
}
