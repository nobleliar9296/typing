namespace TypingTrainer.App.Services;

internal sealed record AppResponsiveLayoutMetrics(
    double ViewportWidth,
    double ViewportHeight,
    double Scale,
    bool CompactWidth,
    bool NarrowWidth,
    bool VeryNarrowWidth,
    bool ShortHeight,
    double PageHorizontalPadding,
    double PageTopPadding,
    double PageBottomPadding,
    double RootRowSpacing,
    double ContentSpacing,
    double CardSpacing,
    double CardPadding,
    double HeaderColumnSpacing,
    double HeaderControlSpacing,
    double FilterControlWidth,
    double SmallFilterControlWidth,
    double FormLabelWidth,
    double FormControlWidth,
    double CompactFormControlWidth,
    double TableMaxHeight,
    double ChartMinHeight,
    double CalendarTileWidth,
    double MaxContentWidth)
{
    public static AppResponsiveLayoutMetrics FromViewport(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Viewport width and height must be positive.");
        }

        var widthScale = width switch
        {
            < 560 => 0.72,
            < 760 => 0.80,
            < 980 => 0.88,
            < 1180 => 0.94,
            _ => 1.0
        };
        var heightScale = height switch
        {
            < 620 => 0.72,
            < 760 => 0.82,
            < 900 => 0.92,
            _ => 1.0
        };

        var scale = Math.Clamp(Math.Min(widthScale, heightScale), 0.70, 1.0);
        var compactWidth = width < 980;
        var narrowWidth = width < 720;
        var veryNarrowWidth = width < 560;
        var shortHeight = height < 720;

        var horizontalPadding = Math.Clamp(32 * scale, veryNarrowWidth ? 10 : 16, 32);
        var topPadding = Math.Clamp(24 * scale, shortHeight ? 10 : 14, 24);
        var bottomPadding = Math.Clamp(24 * scale, 12, 24);
        var contentSpacing = Math.Clamp(24 * scale, shortHeight ? 12 : 16, 24);

        return new AppResponsiveLayoutMetrics(
            width,
            height,
            scale,
            compactWidth,
            narrowWidth,
            veryNarrowWidth,
            shortHeight,
            horizontalPadding,
            topPadding,
            bottomPadding,
            Math.Clamp(24 * scale, 12, 24),
            contentSpacing,
            Math.Clamp(18 * scale, 12, 18),
            Math.Clamp(18 * scale, 12, 18),
            Math.Clamp(24 * scale, 10, 24),
            Math.Clamp(8 * scale, 6, 8),
            Math.Clamp(180 * scale, narrowWidth ? 132 : 150, 180),
            Math.Clamp(160 * scale, narrowWidth ? 120 : 136, 160),
            Math.Clamp(180 * scale, narrowWidth ? 118 : 142, 180),
            Math.Clamp(220 * scale, narrowWidth ? 148 : 176, 220),
            Math.Clamp(160 * scale, narrowWidth ? 112 : 132, 160),
            shortHeight ? 190 : 260,
            shortHeight ? 190 : 230,
            Math.Clamp(122 * scale, veryNarrowWidth ? 96 : 108, 122),
            compactWidth ? double.PositiveInfinity : 1120);
    }
}
