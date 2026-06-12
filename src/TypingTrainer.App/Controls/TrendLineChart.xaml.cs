using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TypingTrainer.App.ViewModels;
using Windows.Foundation;

namespace TypingTrainer.App.Controls;

public sealed partial class TrendLineChart : UserControl
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points),
        typeof(IReadOnlyList<ChartPointViewModel>),
        typeof(TrendLineChart),
        new PropertyMetadata(null, OnPointsChanged));

    public TrendLineChart()
    {
        InitializeComponent();
    }

    public IReadOnlyList<ChartPointViewModel>? Points
    {
        get => (IReadOnlyList<ChartPointViewModel>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    private static void OnPointsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
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
        var maximum = Math.Max(1, points.Max(point => point.Value));
        var step = points.Length == 1 ? width : width / (points.Length - 1);
        var pointCollection = new PointCollection();

        for (var index = 0; index < points.Length; index++)
        {
            var x = points.Length == 1 ? width / 2 : step * index;
            var y = height - (points[index].Value / maximum * (height - 24)) - 12;
            pointCollection.Add(new Point(x, y));
        }

        ChartCanvas.Children.Add(new Line
        {
            X1 = 0,
            Y1 = height - 12,
            X2 = width,
            Y2 = height - 12,
            Stroke = new SolidColorBrush(Colors.Gray),
            StrokeThickness = 1,
            Opacity = 0.45
        });

        ChartCanvas.Children.Add(new Polyline
        {
            Points = pointCollection,
            Stroke = new SolidColorBrush(Colors.DodgerBlue),
            StrokeThickness = 3
        });

        foreach (var point in pointCollection)
        {
            var marker = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(Colors.DodgerBlue)
            };
            Canvas.SetLeft(marker, point.X - 3);
            Canvas.SetTop(marker, point.Y - 3);
            ChartCanvas.Children.Add(marker);
        }
    }
}
