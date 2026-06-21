using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TypingTrainer.App.Services;
using TypingTrainer.App.ViewModels;
using Windows.Foundation;

namespace TypingTrainer.App.Controls;

public sealed partial class TrendLineChart : UserControl
{
    private const double PlotLeft = 58;
    private const double PlotRight = 18;
    private const double PlotTop = 40;
    private const double PlotBottom = 42;

    private RenderedPoint[] _renderedPoints = [];
    private double _baselineY;

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

    public static readonly DependencyProperty ChartTitleProperty = DependencyProperty.Register(
        nameof(ChartTitle),
        typeof(string),
        typeof(TrendLineChart),
        new PropertyMetadata(string.Empty, OnAppearanceChanged));

    public static readonly DependencyProperty XAxisLabelProperty = DependencyProperty.Register(
        nameof(XAxisLabel),
        typeof(string),
        typeof(TrendLineChart),
        new PropertyMetadata("Date", OnAppearanceChanged));

    public static readonly DependencyProperty YAxisLabelProperty = DependencyProperty.Register(
        nameof(YAxisLabel),
        typeof(string),
        typeof(TrendLineChart),
        new PropertyMetadata(string.Empty, OnAppearanceChanged));

    public TrendLineChart()
    {
        InitializeComponent();
        ActualThemeChanged += (_, _) => Render();
    }

    public event EventHandler<ChartPointSelectedEventArgs>? PointSelected;

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

    public string ChartTitle
    {
        get => (string)GetValue(ChartTitleProperty);
        set => SetValue(ChartTitleProperty, value);
    }

    public string XAxisLabel
    {
        get => (string)GetValue(XAxisLabelProperty);
        set => SetValue(XAxisLabelProperty, value);
    }

    public string YAxisLabel
    {
        get => (string)GetValue(YAxisLabelProperty);
        set => SetValue(YAxisLabelProperty, value);
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

    private void ChartCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_renderedPoints.Length == 0)
        {
            ClearHover();
            return;
        }

        var pointerPosition = e.GetCurrentPoint(ChartCanvas).Position;
        var plotRight = ChartCanvas.ActualWidth - PlotRight;
        if (pointerPosition.X < PlotLeft - 10 || pointerPosition.X > plotRight + 10)
        {
            ClearHover();
            return;
        }

        var nearest = _renderedPoints
            .OrderBy(point => Math.Abs(point.Location.X - pointerPosition.X))
            .First();
        ShowHover(nearest);
    }

    private void ChartCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        ClearHover();
    }

    private void ChartCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_renderedPoints.Length == 0)
        {
            return;
        }

        var pointerPosition = e.GetCurrentPoint(ChartCanvas).Position;
        var nearest = _renderedPoints
            .OrderBy(point => Math.Abs(point.Location.X - pointerPosition.X))
            .First();
        ShowHover(nearest);
        PointSelected?.Invoke(
            this,
            new ChartPointSelectedEventArgs(nearest.Index, nearest.Point.Label, nearest.Point.Value));
    }

    private void Render()
    {
        ChartCanvas.Children.Clear();
        HoverCanvas.Children.Clear();
        _renderedPoints = [];
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
        var renderedPoints = new RenderedPoint[points.Length];

        DrawAxes(width, height, plotWidth, plotHeight, maximum);

        for (var index = 0; index < points.Length; index++)
        {
            var x = points.Length == 1 ? PlotLeft + plotWidth / 2 : PlotLeft + step * index;
            var y = PlotTop + plotHeight - (points[index].Value / maximum * plotHeight);
            var location = new Point(x, y);
            pointCollection.Add(location);
            renderedPoints[index] = new RenderedPoint(index, location, points[index]);
        }

        _renderedPoints = renderedPoints;

        ChartCanvas.Children.Add(new Polyline
        {
            Points = pointCollection,
            Stroke = ThemeContrast.ChartLineBrush(this),
            StrokeThickness = 3
        });

        var shouldShowAllMarkers = pointCollection.Count <= 80;
        for (var index = 0; index < pointCollection.Count; index++)
        {
            var point = pointCollection[index];

            if (shouldShowAllMarkers || ShouldShowLabel(index, points.Length))
            {
                var marker = new Ellipse
                {
                    Width = 7,
                    Height = 7,
                    Fill = ThemeContrast.ChartMarkerBrush(this)
                };
                Canvas.SetLeft(marker, point.X - 3.5);
                Canvas.SetTop(marker, point.Y - 3.5);
                ChartCanvas.Children.Add(marker);
            }

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
        _baselineY = baselineY;

        if (!string.IsNullOrWhiteSpace(ChartTitle))
        {
            AddText(ChartTitle, PlotLeft, 7, Math.Max(80, width - PlotLeft - PlotRight), TextAlignment.Left, 13);
        }

        DrawHorizontalGridLine(PlotTop, plotWidth);
        DrawHorizontalGridLine(midY, plotWidth);
        DrawHorizontalGridLine(baselineY, plotWidth, isAxis: true);
        DrawVerticalAxis(baselineY);

        AddText(FormatValue(maximum), 0, PlotTop - 9, PlotLeft - 8, TextAlignment.Right, 11);
        AddText(FormatValue(maximum / 2), 0, midY - 9, PlotLeft - 8, TextAlignment.Right, 11);
        AddText(FormatValue(0), 0, baselineY - 9, PlotLeft - 8, TextAlignment.Right, 11);
        AddText(XAxisLabel, PlotLeft + plotWidth - 64, baselineY + 13, 64, TextAlignment.Right, 11);

        if (!string.IsNullOrWhiteSpace(YAxisLabel))
        {
            AddText(YAxisLabel, PlotLeft, PlotTop - 19, 92, TextAlignment.Left, 11);
        }
    }

    private void DrawHorizontalGridLine(double y, double plotWidth, bool isAxis = false)
    {
        ChartCanvas.Children.Add(new Line
        {
            X1 = PlotLeft,
            Y1 = y,
            X2 = PlotLeft + plotWidth,
            Y2 = y,
            Stroke = isAxis ? ThemeContrast.AxisBrush(this) : ThemeContrast.GridBrush(this),
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
            Stroke = ThemeContrast.AxisBrush(this),
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
            Foreground = ThemeContrast.SecondaryTextBrush(this),
            TextAlignment = textAlignment,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        };
        Canvas.SetLeft(label, Math.Max(0, left));
        Canvas.SetTop(label, top);
        ChartCanvas.Children.Add(label);
    }

    private void ShowHover(RenderedPoint renderedPoint)
    {
        HoverCanvas.Children.Clear();

        HoverCanvas.Children.Add(new Line
        {
            X1 = renderedPoint.Location.X,
            Y1 = PlotTop,
            X2 = renderedPoint.Location.X,
            Y2 = _baselineY,
            Stroke = ThemeContrast.ChartHoverBrush(this),
            StrokeThickness = 1,
            Opacity = 0.85
        });

        var marker = new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = ThemeContrast.ChartHoverBrush(this),
            Stroke = ThemeContrast.ChartLineBrush(this),
            StrokeThickness = 2
        };
        Canvas.SetLeft(marker, renderedPoint.Location.X - 6);
        Canvas.SetTop(marker, renderedPoint.Location.Y - 6);
        HoverCanvas.Children.Add(marker);

        var tooltipWidth = 154.0;
        var tooltipHeight = 52.0;
        var tooltipLeft = Math.Clamp(
            renderedPoint.Location.X + 12,
            2,
            Math.Max(2, ChartCanvas.ActualWidth - tooltipWidth - 2));
        var tooltipTop = Math.Clamp(
            renderedPoint.Location.Y - tooltipHeight - 10,
            2,
            Math.Max(2, ChartCanvas.ActualHeight - tooltipHeight - 2));

        var tooltip = new Border
        {
            Width = tooltipWidth,
            Padding = new Thickness(10, 7, 10, 7),
            Background = ThemeContrast.TooltipBackgroundBrush(this),
            BorderBrush = ThemeContrast.ChartHoverBrush(this),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock
                    {
                        Text = renderedPoint.Point.Label,
                        FontSize = 11,
                        Foreground = ThemeContrast.SecondaryTextBrush(this),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxLines = 1
                    },
                    new TextBlock
                    {
                        Text = FormatValue(renderedPoint.Point.Value),
                        FontSize = 14,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = ThemeContrast.ChartLineBrush(this)
                    }
                }
            }
        };

        Canvas.SetLeft(tooltip, tooltipLeft);
        Canvas.SetTop(tooltip, tooltipTop);
        HoverCanvas.Children.Add(tooltip);
    }

    private void ClearHover()
    {
        HoverCanvas.Children.Clear();
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
    private readonly record struct RenderedPoint(int Index, Point Location, ChartPointViewModel Point);
}

public sealed class ChartPointSelectedEventArgs : EventArgs
{
    public ChartPointSelectedEventArgs(int index, string label, double value)
    {
        Index = index;
        Label = label;
        Value = value;
    }

    public int Index { get; }

    public string Label { get; }

    public double Value { get; }
}
