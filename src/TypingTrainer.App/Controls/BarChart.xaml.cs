using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TypingTrainer.App.ViewModels;

namespace TypingTrainer.App.Controls;

public sealed partial class BarChart : UserControl
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points),
        typeof(IReadOnlyList<ChartPointViewModel>),
        typeof(BarChart),
        new PropertyMetadata(null, OnPointsChanged));

    public BarChart()
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
        var maximum = Math.Max(1, points.Max(point => point.Value));
        var slotWidth = width / points.Length;
        var barWidth = Math.Max(4, slotWidth * 0.62);

        for (var index = 0; index < points.Length; index++)
        {
            var barHeight = points[index].Value / maximum * (height - 20);
            var bar = new Rectangle
            {
                Width = barWidth,
                Height = Math.Max(2, barHeight),
                Fill = new SolidColorBrush(Colors.SeaGreen),
                RadiusX = 2,
                RadiusY = 2
            };
            Canvas.SetLeft(bar, (slotWidth * index) + ((slotWidth - barWidth) / 2));
            Canvas.SetTop(bar, height - bar.Height - 10);
            ChartCanvas.Children.Add(bar);
        }
    }
}
