using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TypingTrainer.App.ViewModels;
using Windows.UI;

namespace TypingTrainer.App.Controls;

public sealed partial class BarChart : UserControl
{
    private const double PlotLeft = 58;
    private const double PlotRight = 18;
    private const double PlotTop = 26;
    private const double PlotBottom = 42;

    private static readonly SolidColorBrush AxisBrush = Brush(92, 96, 100);
    private static readonly SolidColorBrush GridBrush = Brush(58, 62, 66);
    private static readonly SolidColorBrush LabelBrush = Brush(202, 205, 208);
    private static readonly SolidColorBrush BarBrush = Brush(47, 151, 94);

    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points),
        typeof(IReadOnlyList<ChartPointViewModel>),
        typeof(BarChart),
        new PropertyMetadata(null, OnPointsChanged));

    public static readonly DependencyProperty ValueSuffixProperty = DependencyProperty.Register(
        nameof(ValueSuffix),
        typeof(string),
        typeof(BarChart),
        new PropertyMetadata(string.Empty, OnAppearanceChanged));

    public BarChart()
    {
        InitializeComponent();
    }

    public IReadOnlyList<ChartPointViewModel>? Points
    {
        get => (IReadOnlyList<ChartPointViewModel>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public string ValueSuffix
    {
        get => (string)GetValue(ValueSuffixProperty);
        set => SetValue(ValueSuffixProperty, value);
    }

    private static void OnPointsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is BarChart chart)
        {
            chart.Render();
        }
    }

    private static void OnAppearanceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is BarChart chart)
        {
            chart.Render();
        }
    }

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Render();
    }

    private void Render()
    {
        ChartCanvas.Children.Clear();
        var points = Points?.Where(point => !double.IsNaN(point.Value)).ToArray() ?? [];
        EmptyText.Visibility = points.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (points.Length == 0 || ChartCanvas.ActualWidth <= 0 || ChartCanvas.ActualHeight <= 0)
        {
            return;
        }

        var width = ChartCanvas.ActualWidth;
        var height = ChartCanvas.ActualHeight;
        var plotWidth = Math.Max(1, width - PlotLeft - PlotRight);
        var plotHeight = Math.Max(1, height - PlotTop - PlotBottom);
        var maximum = GetNiceMaximum(Math.Max(1, points.Max(point => point.Value)));
        var slotWidth = plotWidth / points.Length;
        var barWidth = Math.Clamp(slotWidth * 0.56, 10, 120);

        DrawAxes(height, plotWidth, plotHeight, maximum);

        for (var index = 0; index < points.Length; index++)
        {
            var barHeight = points[index].Value / maximum * plotHeight;
            var barLeft = PlotLeft + (slotWidth * index) + ((slotWidth - barWidth) / 2);
            var barTop = PlotTop + plotHeight - Math.Max(2, barHeight);
            var bar = new Rectangle
            {
                Width = barWidth,
                Height = Math.Max(2, barHeight),
                Fill = BarBrush,
                RadiusX = 2,
                RadiusY = 2
            };
            Canvas.SetLeft(bar, barLeft);
            Canvas.SetTop(bar, barTop);
            ChartCanvas.Children.Add(bar);

            if (ShouldShowLabel(index, points.Length))
            {
                AddText(points[index].Label, PlotLeft + slotWidth * index + slotWidth / 2 - 34, height - PlotBottom + 13, 68, TextAlignment.Center, 11);
                AddText(FormatValue(points[index].Value), barLeft - 14, Math.Max(2, barTop - 22), barWidth + 28, TextAlignment.Center, 11);
            }
        }
    }

    private void DrawAxes(double height, double plotWidth, double plotHeight, double maximum)
    {
        var baselineY = height - PlotBottom;
        var midY = PlotTop + plotHeight / 2;

        DrawHorizontalGridLine(PlotTop, plotWidth);
        DrawHorizontalGridLine(midY, plotWidth);
        DrawHorizontalGridLine(baselineY, plotWidth, isAxis: true);
        DrawVerticalAxis(baselineY);

        AddText(FormatValue(maximum), 0, PlotTop - 9, PlotLeft - 8, TextAlignment.Right, 11);
        AddText(FormatValue(maximum / 2), 0, midY - 9, PlotLeft - 8, TextAlignment.Right, 11);
        AddText(FormatValue(0), 0, baselineY - 9, PlotLeft - 8, TextAlignment.Right, 11);
        AddText("Date", PlotLeft + plotWidth - 42, baselineY + 13, 42, TextAlignment.Right, 11);
    }

    private void DrawHorizontalGridLine(double y, double plotWidth, bool isAxis = false)
    {
        ChartCanvas.Children.Add(new Line
        {
            X1 = PlotLeft,
            Y1 = y,
            X2 = PlotLeft + plotWidth,
            Y2 = y,
            Stroke = isAxis ? AxisBrush : GridBrush,
            StrokeThickness = 1,
            Opacity = isAxis ? 0.75 : 0.55
        });
    }

    private void DrawVerticalAxis(double baselineY)
    {
        ChartCanvas.Children.Add(new Line
        {
            X1 = PlotLeft,
            Y1 = PlotTop,
            X2 = PlotLeft,
            Y2 = baselineY,
            Stroke = AxisBrush,
            StrokeThickness = 1,
            Opacity = 0.75
        });
    }

    private void AddText(
        string text,
        double left,
        double top,
        double width,
        TextAlignment textAlignment,
        double fontSize)
    {
        var label = new TextBlock
        {
            Text = text,
            Width = width,
            FontSize = fontSize,
            Foreground = LabelBrush,
            TextAlignment = textAlignment,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        };
        Canvas.SetLeft(label, Math.Max(0, left));
        Canvas.SetTop(label, top);
        ChartCanvas.Children.Add(label);
    }

    private string FormatValue(double value)
    {
        var format = value >= 10 || Math.Abs(value % 1) < 0.05 ? "0" : "0.0";
        return value.ToString(format, System.Globalization.CultureInfo.InvariantCulture) + ValueSuffix;
    }

    private static bool ShouldShowLabel(int index, int count)
    {
        if (count <= 7)
        {
            return true;
        }

        var stride = Math.Max(1, (int)Math.Ceiling(count / 6.0));
        return index == 0 || index == count - 1 || index % stride == 0;
    }

    private static double GetNiceMaximum(double value)
    {
        if (value <= 10)
        {
            return Math.Ceiling(value);
        }

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
        var normalized = value / magnitude;
        var niceNormalized = normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
        return niceNormalized * magnitude;
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(Color.FromArgb(255, red, green, blue));
    }
}
