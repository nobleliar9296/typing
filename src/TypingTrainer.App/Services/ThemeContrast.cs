using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace TypingTrainer.App.Services;

internal static class ThemeContrast
{
    private static readonly Color BlackTextColor = Color.FromArgb(255, 0, 0, 0);
    private static readonly Color WhiteTextColor = Color.FromArgb(255, 255, 255, 255);

    public static bool IsLightTheme(FrameworkElement? element)
    {
        if (element?.ActualTheme is ElementTheme.Light)
        {
            return true;
        }

        if (element?.ActualTheme is ElementTheme.Dark)
        {
            return false;
        }

        if (element?.RequestedTheme is ElementTheme.Light)
        {
            return true;
        }

        if (element?.RequestedTheme is ElementTheme.Dark)
        {
            return false;
        }

        if (App.MainWindowInstance?.Content is FrameworkElement root)
        {
            if (root.ActualTheme is ElementTheme.Light)
            {
                return true;
            }

            if (root.ActualTheme is ElementTheme.Dark)
            {
                return false;
            }

            if (root.RequestedTheme is ElementTheme.Light)
            {
                return true;
            }

            if (root.RequestedTheme is ElementTheme.Dark)
            {
                return false;
            }
        }

        return Application.Current?.RequestedTheme == ApplicationTheme.Light;
    }

    public static SolidColorBrush PrimaryTextBrush(FrameworkElement? element)
    {
        return PrimaryTextBrush(IsLightTheme(element));
    }

    public static SolidColorBrush PrimaryTextBrush(bool isLightTheme)
    {
        return isLightTheme ? Brush(18, 18, 18) : Brush(246, 248, 251);
    }

    public static SolidColorBrush SecondaryTextBrush(FrameworkElement? element)
    {
        return SecondaryTextBrush(IsLightTheme(element));
    }

    public static SolidColorBrush SecondaryTextBrush(bool isLightTheme)
    {
        return isLightTheme ? Brush(73, 80, 89) : Brush(198, 204, 212);
    }

    public static SolidColorBrush TertiaryTextBrush(FrameworkElement? element)
    {
        return TertiaryTextBrush(IsLightTheme(element));
    }

    public static SolidColorBrush TertiaryTextBrush(bool isLightTheme)
    {
        return isLightTheme ? Brush(99, 109, 121) : Brush(158, 167, 178);
    }

    public static SolidColorBrush CriticalTextBrush(FrameworkElement? element)
    {
        return CriticalTextBrush(IsLightTheme(element));
    }

    public static SolidColorBrush CriticalTextBrush(bool isLightTheme)
    {
        return isLightTheme ? Brush(171, 31, 45) : Brush(255, 107, 115);
    }

    public static SolidColorBrush SuccessTextBrush(FrameworkElement? element)
    {
        return SuccessTextBrush(IsLightTheme(element));
    }

    public static SolidColorBrush SuccessTextBrush(bool isLightTheme)
    {
        return isLightTheme ? Brush(13, 115, 83) : Brush(61, 220, 151);
    }

    public static SolidColorBrush AxisBrush(FrameworkElement? element)
    {
        return IsLightTheme(element)
            ? Brush(104, 114, 126)
            : Brush(142, 148, 155);
    }

    public static SolidColorBrush GridBrush(FrameworkElement? element)
    {
        return IsLightTheme(element)
            ? Brush(211, 218, 226)
            : Brush(58, 62, 66);
    }

    public static SolidColorBrush ChartPositiveBrush(FrameworkElement? element)
    {
        return IsLightTheme(element)
            ? Brush(20, 122, 72)
            : Brush(47, 151, 94);
    }

    public static SolidColorBrush ChartLineBrush(FrameworkElement? element)
    {
        return IsLightTheme(element)
            ? Brush(10, 101, 184)
            : Brush(38, 151, 255);
    }

    public static SolidColorBrush ChartMarkerBrush(FrameworkElement? element)
    {
        return IsLightTheme(element)
            ? Brush(10, 116, 214)
            : Brush(77, 174, 255);
    }

    public static SolidColorBrush ChartHoverBrush(FrameworkElement? element)
    {
        return IsLightTheme(element)
            ? Brush(7, 94, 171)
            : Brush(120, 205, 255);
    }

    public static SolidColorBrush TooltipBackgroundBrush(FrameworkElement? element)
    {
        return IsLightTheme(element)
            ? Brush(255, 255, 255)
            : Brush(24, 28, 34);
    }

    public static SolidColorBrush ReadableTextBrush(Color background)
    {
        return new SolidColorBrush(ReadableTextColor(background));
    }

    public static Color ReadableTextColor(Color background)
    {
        return ContrastRatio(BlackTextColor, background) >= ContrastRatio(WhiteTextColor, background)
            ? BlackTextColor
            : WhiteTextColor;
    }

    public static bool HasReadableContrast(Color foreground, Color background, double minimumRatio = 4.5)
    {
        return ContrastRatio(foreground, background) >= minimumRatio;
    }

    public static double ContrastRatio(Color foreground, Color background)
    {
        var lighter = Math.Max(RelativeLuminance(foreground), RelativeLuminance(background));
        var darker = Math.Min(RelativeLuminance(foreground), RelativeLuminance(background));
        return (lighter + 0.05) / (darker + 0.05);
    }

    public static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        return new SolidColorBrush(Color.FromArgb(255, red, green, blue));
    }

    private static double RelativeLuminance(Color color)
    {
        return 0.2126 * ToLinear(color.R)
            + 0.7152 * ToLinear(color.G)
            + 0.0722 * ToLinear(color.B);
    }

    private static double ToLinear(byte component)
    {
        var value = component / 255.0;
        return value <= 0.03928
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
