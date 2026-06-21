using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using TypingTrainer.Data.Models;
using Windows.UI;

namespace TypingTrainer.App.Services;

public static class AppThemeService
{
    private static readonly string[] ResourceOverrideKeys =
    {
        "SystemAccentColor",
        "SystemAccentColorLight1",
        "SystemAccentColorLight2",
        "SystemAccentColorLight3",
        "SystemAccentColorDark1",
        "SystemAccentColorDark2",
        "SystemAccentColorDark3",
        "AccentFillColorDefaultBrush",
        "AccentFillColorSecondaryBrush",
        "AccentFillColorTertiaryBrush",
        "ApplicationPageBackgroundThemeBrush",
        "SolidBackgroundFillColorBaseBrush",
        "CardBackgroundFillColorDefaultBrush",
        "ControlStrokeColorDefaultBrush",
        "TextFillColorPrimaryBrush",
        "TextFillColorSecondaryBrush",
        "TextFillColorTertiaryBrush",
        "SystemFillColorCriticalBrush",
        "SystemFillColorSuccessBrush"
    };

    public static void Apply(string? themePreset)
    {
        if (App.MainWindowInstance?.Content is not FrameworkElement root)
        {
            return;
        }

        var preset = GetPreset(themePreset);
        root.RequestedTheme = preset.RequestedTheme;
        ApplyResources(preset);
    }

    private static ThemePreset GetPreset(string? themePreset)
    {
        return Normalize(themePreset) switch
        {
            AppSettings.DarkThemePreset => new ThemePreset(ElementTheme.Dark),
            AppSettings.LightThemePreset => new ThemePreset(ElementTheme.Light),
            AppSettings.InkThemePreset => new ThemePreset(
                ElementTheme.Dark,
                Accent: Color(0x4A, 0xA3, 0xFF),
                Background: Color(0x10, 0x13, 0x18),
                Surface: Color(0x15, 0x19, 0x20),
                Card: Color(0x1B, 0x21, 0x2A),
                Stroke: Color(0x33, 0x3C, 0x49)),
            AppSettings.ForestThemePreset => new ThemePreset(
                ElementTheme.Dark,
                Accent: Color(0x5C, 0xC9, 0x83),
                Background: Color(0x0F, 0x16, 0x12),
                Surface: Color(0x14, 0x1D, 0x17),
                Card: Color(0x19, 0x25, 0x1D),
                Stroke: Color(0x32, 0x42, 0x36)),
            AppSettings.DawnThemePreset => new ThemePreset(
                ElementTheme.Light,
                Accent: Color(0x0B, 0x6B, 0x74),
                Background: Color(0xF6, 0xF8, 0xFA),
                Surface: Color(0xFF, 0xFF, 0xFF),
                Card: Color(0xFF, 0xFF, 0xFF),
                Stroke: Color(0xD2, 0xDA, 0xE2)),
            AppSettings.MonochromeThemePreset => new ThemePreset(
                ElementTheme.Dark,
                Accent: Color(0xC8, 0xC8, 0xC8),
                Background: Color(0x10, 0x10, 0x10),
                Surface: Color(0x16, 0x16, 0x16),
                Card: Color(0x1D, 0x1D, 0x1D),
                Stroke: Color(0x3C, 0x3C, 0x3C)),
            AppSettings.HighContrastThemePreset => new ThemePreset(
                ElementTheme.Dark,
                Accent: Color(0xFF, 0xCC, 0x33),
                Background: Color(0x00, 0x00, 0x00),
                Surface: Color(0x08, 0x08, 0x08),
                Card: Color(0x10, 0x10, 0x10),
                Stroke: Color(0xE5, 0xE5, 0xE5)),
            _ => new ThemePreset(ElementTheme.Default)
        };
    }

    private static string Normalize(string? themePreset)
    {
        return themePreset?.Trim() switch
        {
            AppSettings.DarkThemePreset => AppSettings.DarkThemePreset,
            AppSettings.LightThemePreset => AppSettings.LightThemePreset,
            AppSettings.InkThemePreset => AppSettings.InkThemePreset,
            AppSettings.ForestThemePreset => AppSettings.ForestThemePreset,
            AppSettings.DawnThemePreset => AppSettings.DawnThemePreset,
            AppSettings.MonochromeThemePreset => AppSettings.MonochromeThemePreset,
            AppSettings.HighContrastThemePreset => AppSettings.HighContrastThemePreset,
            _ => AppSettings.SystemThemePreset
        };
    }

    private static void ApplyResources(ThemePreset preset)
    {
        var resources = Application.Current.Resources;
        ClearResourceOverrides(resources);

        if (preset.Accent is not null)
        {
            var accent = preset.Accent.Value;
            resources["SystemAccentColor"] = accent;
            resources["SystemAccentColorLight1"] = Adjust(accent, 1.18);
            resources["SystemAccentColorLight2"] = Adjust(accent, 1.35);
            resources["SystemAccentColorLight3"] = Adjust(accent, 1.55);
            resources["SystemAccentColorDark1"] = Adjust(accent, 0.86);
            resources["SystemAccentColorDark2"] = Adjust(accent, 0.72);
            resources["SystemAccentColorDark3"] = Adjust(accent, 0.58);
            resources["AccentFillColorDefaultBrush"] = new SolidColorBrush(accent);
            resources["AccentFillColorSecondaryBrush"] = new SolidColorBrush(WithAlpha(accent, 0xE6));
            resources["AccentFillColorTertiaryBrush"] = new SolidColorBrush(WithAlpha(accent, 0xCC));
        }

        SetBrush(resources, "ApplicationPageBackgroundThemeBrush", preset.Background);
        SetBrush(resources, "SolidBackgroundFillColorBaseBrush", preset.Surface);
        SetBrush(resources, "CardBackgroundFillColorDefaultBrush", preset.Card);
        SetBrush(resources, "ControlStrokeColorDefaultBrush", preset.Stroke);
        SetTextBrushes(resources, preset.RequestedTheme);
    }

    private static void ClearResourceOverrides(ResourceDictionary resources)
    {
        foreach (var key in ResourceOverrideKeys)
        {
            if (resources.Keys.Contains(key))
            {
                resources.Remove(key);
            }
        }
    }

    private static void SetBrush(ResourceDictionary resources, string key, Color? color)
    {
        if (color is not null)
        {
            resources[key] = new SolidColorBrush(color.Value);
        }
    }

    private static void SetTextBrushes(ResourceDictionary resources, ElementTheme theme)
    {
        if (theme == ElementTheme.Default)
        {
            return;
        }

        var light = theme == ElementTheme.Light;
        resources["TextFillColorPrimaryBrush"] = ThemeContrast.PrimaryTextBrush(light);
        resources["TextFillColorSecondaryBrush"] = ThemeContrast.SecondaryTextBrush(light);
        resources["TextFillColorTertiaryBrush"] = ThemeContrast.TertiaryTextBrush(light);
        resources["SystemFillColorCriticalBrush"] = ThemeContrast.CriticalTextBrush(light);
        resources["SystemFillColorSuccessBrush"] = ThemeContrast.SuccessTextBrush(light);
    }

    private static Color Color(byte red, byte green, byte blue)
    {
        return Windows.UI.Color.FromArgb(0xFF, red, green, blue);
    }

    private static Color WithAlpha(Color color, byte alpha)
    {
        return Windows.UI.Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static Color Adjust(Color color, double factor)
    {
        return Windows.UI.Color.FromArgb(
            color.A,
            Clamp(color.R * factor),
            Clamp(color.G * factor),
            Clamp(color.B * factor));
    }

    private static byte Clamp(double value)
    {
        return (byte)Math.Max(0, Math.Min(255, Math.Round(value)));
    }

    private sealed record ThemePreset(
        ElementTheme RequestedTheme,
        Color? Accent = null,
        Color? Background = null,
        Color? Surface = null,
        Color? Card = null,
        Color? Stroke = null);
}
