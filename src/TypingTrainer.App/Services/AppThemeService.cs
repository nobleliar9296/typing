using Microsoft.UI.Xaml;

namespace TypingTrainer.App.Services;

public static class AppThemeService
{
    public static void Apply(string? themePreset)
    {
        if (App.MainWindowInstance?.Content is not FrameworkElement root)
        {
            return;
        }

        root.RequestedTheme = themePreset switch
        {
            "Dark" => ElementTheme.Dark,
            "Light" => ElementTheme.Light,
            _ => ElementTheme.Default
        };
    }
}
