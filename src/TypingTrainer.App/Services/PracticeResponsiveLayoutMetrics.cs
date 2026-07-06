namespace TypingTrainer.App.Services;

internal sealed record PracticeResponsiveLayoutMetrics(
    double ViewportWidth,
    double ViewportHeight,
    double Scale,
    double KeyboardScale,
    bool CompactStats,
    bool CompactHeader,
    bool VeryCompactHeader,
    bool StackedSelectors,
    bool UseShortClipboardText,
    double PageHorizontalPadding,
    double PageTopPadding,
    double ContentSpacing,
    double RootRowSpacing,
    double HeaderRowSpacing,
    double HeaderColumnSpacing,
    double SelectorSpacing,
    double LessonModeWidth,
    double LessonSizeWidth,
    double ClipboardPaddingHorizontal,
    double ClipboardPaddingVertical,
    double InputHorizontalPadding,
    double InputTopPadding,
    double InputBorderMaxWidth,
    double PracticeTextDisplayScale,
    double PracticeTextMaxWidth,
    double KeyboardMaxWidth,
    double HeaderHeightFallback,
    double KeyboardHeightEstimate,
    double StatusAllowance,
    double CompactStatsAllowance,
    double AvailableTextHeight,
    double FourLineTextHeight,
    double MinimumTextHeight,
    double TextHeightCap,
    double PracticeTextMaxHeight,
    double PracticeTextMinHeight,
    double CompactStatsRowSpacing,
    double RailStatsWidth,
    double StatsPanelSpacing,
    double TypingGridColumnSpacing,
    double MetadataFontSize,
    double KpiValueFontSize,
    double KpiLabelFontSize,
    double KpiTilePaddingHorizontal,
    double KpiTilePaddingVertical,
    double KpiTileMinHeight,
    double? KpiTileWidth,
    double ReviewMargin,
    double ReviewPadding,
    double ReviewMaxWidth,
    double ReviewMaxHeight)
{
    public static PracticeResponsiveLayoutMetrics FromViewport(
        double width,
        double height,
        double headerActualHeight,
        bool statsVisible,
        double practiceTextScale,
        double visualKeyboardScale,
        double practiceLineWidthMax)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Viewport width and height must be positive.");
        }

        var widthScale = width switch
        {
            < 720 => 0.70,
            < 920 => 0.78,
            < 1200 => 0.88,
            _ => 1.00
        };
        var heightScale = height switch
        {
            < 700 => 0.64,
            < 820 => 0.74,
            < 980 => 0.86,
            _ => 1.00
        };

        var minimumScale = width < 760 || height < 700 ? 0.62 : 0.68;
        var scale = Math.Clamp(Math.Min(widthScale, heightScale), minimumScale, 1.0);
        var compactStats = width < 1120;
        var compactHeader = width < 1100;
        var veryCompactHeader = width < 920;
        var stackedSelectors = width < 980;
        var keyboardMinimumScale = width <= 900 || height < 720 ? 0.45 : 0.58;
        var keyboardScale = Math.Clamp(scale * (height < 900 ? 0.90 : 1.0), keyboardMinimumScale, 1.0);

        var pageHorizontalPadding = Math.Clamp(32 * scale, width < 760 ? 10 : 16, 32);
        var pageTopPadding = Math.Clamp(12 * scale, 8, 14);
        var contentSpacing = Math.Clamp(12 * scale, 8, 14);
        var rootRowSpacing = Math.Clamp(10 * scale, 6, 12);
        var headerRowSpacing = Math.Clamp(7 * scale, 4, 8);
        var headerColumnSpacing = Math.Clamp(16 * scale, 10, 16);
        var selectorSpacing = stackedSelectors ? Math.Clamp(7 * scale, 5, 8) : Math.Clamp(14 * scale, 9, 14);
        var lessonModeWidth = Math.Clamp(160 * scale, stackedSelectors ? 148 : 132, 160);
        var lessonSizeWidth = Math.Clamp(132 * scale, stackedSelectors ? 124 : 104, 132);

        var inputHorizontalPadding = Math.Clamp(28 * scale, width < 760 ? 10 : 14, 28);
        var inputTopPadding = Math.Clamp(24 * scale, 12, 24);
        var inputBorderMaxWidth = width < 1200 ? double.PositiveInfinity : 1100;
        var practiceTextDisplayScale = scale * Math.Clamp(practiceTextScale, 0.5, 1.5);
        var availablePracticeTextWidth = Math.Max(280, width - (2 * pageHorizontalPadding) - (2 * inputHorizontalPadding));
        var practiceTextMaxWidth = Math.Min(practiceLineWidthMax, availablePracticeTextWidth);
        var keyboardMaxWidth = width <= 900 ? Math.Max(320, width - (pageHorizontalPadding * 2)) : 1280;

        var compactStatsRowSpacing = Math.Clamp(8 * scale, 6, 10);
        var kpiTileMinHeight = Math.Clamp(58 * scale, 48, 58);
        var compactStatsAllowance = compactStats && statsVisible
            ? (2 * kpiTileMinHeight) + (2 * compactStatsRowSpacing)
            : 0;
        var headerHeightFallback = compactHeader ? 92 * scale : 58 * scale;
        var headerHeight = headerActualHeight > 0 ? headerActualHeight : headerHeightFallback;
        var keyboardHeightEstimate = (306 * keyboardScale) + 18;
        var statusAllowance = 34 * scale;
        var availableTextHeight = height
            - pageTopPadding
            - rootRowSpacing
            - headerHeight
            - keyboardHeightEstimate
            - statusAllowance
            - compactStatsAllowance;
        var fourLineTextHeight = 4 * 48 * practiceTextDisplayScale;
        var minimumTextHeight = Math.Min(fourLineTextHeight, Math.Max(144, height * 0.24));
        var textHeightCap = Math.Max(minimumTextHeight, height < 760 ? 260 : height < 900 ? 300 : 360);
        var practiceTextMaxHeight = Math.Clamp(availableTextHeight, minimumTextHeight, textHeightCap);
        var practiceTextMinHeight = Math.Min(practiceTextMaxHeight, minimumTextHeight);

        var kpiTileWidth = GetCompactKpiTileWidth(width, pageHorizontalPadding, scale, compactStats);
        var reviewMargin = Math.Clamp(48 * scale, width < 760 || height < 720 ? 12 : 24, 48);
        var reviewPadding = Math.Clamp(28 * scale, 16, 28);
        var reviewMaxWidth = Math.Max(280, Math.Min(1040, width - (2 * reviewMargin)));
        var reviewMaxHeight = Math.Max(260, height - (2 * reviewMargin) - (84 * scale));

        return new PracticeResponsiveLayoutMetrics(
            width,
            height,
            scale,
            keyboardScale * Math.Clamp(visualKeyboardScale, 0.5, 1.5),
            compactStats,
            compactHeader,
            veryCompactHeader,
            stackedSelectors,
            width < 780,
            pageHorizontalPadding,
            pageTopPadding,
            contentSpacing,
            rootRowSpacing,
            headerRowSpacing,
            headerColumnSpacing,
            selectorSpacing,
            lessonModeWidth,
            lessonSizeWidth,
            Math.Clamp(12 * scale, 8, 12),
            Math.Clamp(6 * scale, 4, 6),
            inputHorizontalPadding,
            inputTopPadding,
            inputBorderMaxWidth,
            practiceTextDisplayScale,
            practiceTextMaxWidth,
            keyboardMaxWidth,
            headerHeightFallback,
            keyboardHeightEstimate,
            statusAllowance,
            compactStatsAllowance,
            availableTextHeight,
            fourLineTextHeight,
            minimumTextHeight,
            textHeightCap,
            practiceTextMaxHeight,
            practiceTextMinHeight,
            compactStatsRowSpacing,
            Math.Clamp(132 * scale, width < 820 ? 92 : 108, 132),
            Math.Clamp(8 * scale, compactStats ? 6 : 5, 8),
            Math.Clamp(12 * scale, 8, 14),
            Math.Clamp(14 * scale, 12, 14),
            Math.Clamp(24 * scale, 18, 24),
            Math.Clamp(11 * scale, 9, 11),
            Math.Clamp(10 * scale, 6, 10),
            Math.Clamp(8 * scale, 5, 8),
            kpiTileMinHeight,
            kpiTileWidth,
            reviewMargin,
            reviewPadding,
            reviewMaxWidth,
            reviewMaxHeight);
    }

    private static double? GetCompactKpiTileWidth(double width, double pageHorizontalPadding, double scale, bool compactStats)
    {
        if (!compactStats)
        {
            return null;
        }

        var availableWidth = Math.Max(0, width - (2 * pageHorizontalPadding));
        var spacing = Math.Clamp(8 * scale, 6, 10);
        return Math.Clamp((availableWidth - (2 * spacing)) / 3, 96, 176);
    }
}
