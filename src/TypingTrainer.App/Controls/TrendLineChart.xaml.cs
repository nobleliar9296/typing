using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TypingTrainer.App.ViewModels;
using Windows.Foundation;
using Windows.UI;

namespace TypingTrainer.App.Controls;

public sealed partial class TrendLineChart : UserControl
{
    private const double PlotLeft = 58;
    private const double PlotRight = 18;
    private const double PlotTop = 26;
    private const double PlotBottom = 42;

    private static readonly SolidColorBrush AxisBrush = Brush(92, 96, 100);
    private static readonly SolidColorBrush GridBrush = Brush(58, 62, 66);
    private static readonly SolidColorBrush LabelBrush = Brush(202, 205, 208);
    private static readonly SolidColorBrush LineBrush = Brush(38, 151, 255);
    private static readonly SolidColorBrush MarkerBrush = Brush(77, 174, 255);

    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points),
        typeof(IReadOnlyList<ChartPointViewModel>),
        typeof(TrendLineChart),
        new PropertyMetadata(null, OnPointsChanged));

    public static readonly DependencyProperty ValueSuffixProperty = DependencyProperty.Register(
        nameof(ValueSuffix),
        typeof(string),
        typeof(TrendLineChart),
        new PropertyMetadata(string.Empty, OnAppearanceChanged));

    public static readonly DependencyProperty MaximumValueProperty = DependencyProperty.Register(
        nameof(MaximumValue),
        typeof(double),
        typeof(TrendLineChart),
        new PropertyMetadata(double.NaN, OnAppearanceChanged));

    public TrendLineChart()
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

    public double MaximumValue
    {
        get => (double)GetValue(MaximumValueProperty);
        set => SetValue(MaximumValueProperty, value);
    }

    private static void OnPointsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is TrendLineChart chart)
        {
            chart.Render();
        }
    }

    private static void OnAppearanceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is TrendLineChart chart)
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
        var dataMaximum = Math.Max(1, points.Max(point => point.Value));
        var maximum = double.IsNaN(MaximumValue)
            ? GetNiceMaximum(dataMaximum)
            : Math.Max(1, MaximumValue);
        var step = points.Length == 1 ? 0 : plotWidth / (points.Length - 1);
        var pointCollection = new PointCollection();

        DrawAxes(width, height, plotWidth, plotHeight, maximum);

        for (var index = 0; index < points.Length; index++)
        {
            var x = points.Length == 1 ? PlotLeft + plotWidth / 2 : PlotLeft + step * index;
            var y = PlotTop + plotHeight - (points[index].Value / maximum * plotHeight);
            pointCollection.Add(new Point(x, y));
        }

        ChartCanvas.Children.Add(new Polyline
        {
            Points = pointCollection,
            Stroke = LineBrush,
            StrokeThickness = 3
        });

        for (var index = 0; index < pointCollection.Count; index++)
        {
            var point = pointCollection[index];
            var marker = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = MarkerBrush
            };
            Canvas.SetLeft(marker, point.X - 3.5);
            Canvas.SetTop(marker, point.Y - 3.5);
            ChartCanvas.Children.Add(marker);

            if (ShouldShowLabel(index, points.Length))
            {
                AddText(points[index].Label, point.X - 34, height - PlotBottom + 13, 68, TextAlignment.Center, 11);
                AddText(FormatValue(points[index].Value), point.X - 38, Math.Max(2, point.Y - 24), 76, TextAlignment.Center, 11);
            }
        }
    }

    private void DrawAxes(double width, double height, double plotWidth, double plotHeight, double maximum)
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
