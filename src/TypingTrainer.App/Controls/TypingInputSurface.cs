using Microsoft.UI.Xaml.Controls;

namespace TypingTrainer.App.Controls;

public sealed class TypingInputSurface : UserControl
{
    public TypingInputSurface()
    {
        IsTabStop = true;
        UseSystemFocusVisuals = true;
    }
}
