using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.App.Services;
using Windows.UI;

namespace TypingTrainer.App.Tests;

[TestClass]
public sealed class ThemeContrastTests
{
    [TestMethod]
    public void ReadableTextColor_MeetsContrastForCustomControlBackgrounds()
    {
        var backgrounds = new[]
        {
            Color.FromArgb(255, 82, 88, 92),
            Color.FromArgb(255, 34, 83, 125),
            Color.FromArgb(255, 66, 70, 78),
            Color.FromArgb(255, 94, 126, 86),
            Color.FromArgb(255, 120, 125, 45),
            Color.FromArgb(255, 158, 126, 44),
            Color.FromArgb(255, 126, 71, 65),
            Color.FromArgb(255, 145, 62, 66),
            Color.FromArgb(255, 242, 245, 248),
            Color.FromArgb(255, 255, 255, 255),
            Color.FromArgb(255, 24, 28, 34)
        };

        foreach (var background in backgrounds)
        {
            var foreground = ThemeContrast.ReadableTextColor(background);

            Assert.IsTrue(
                ThemeContrast.HasReadableContrast(foreground, background),
                $"Foreground {Format(foreground)} is not readable on background {Format(background)}.");
        }
    }

    private static string Format(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
