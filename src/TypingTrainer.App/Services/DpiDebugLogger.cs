#if DEBUG
using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace TypingTrainer.App.Services;

internal sealed class DpiDebugLogger : IDisposable
{
    private readonly Window _window;
    private readonly IntPtr _hwnd;
    private readonly AppWindow? _appWindow;
    private uint _lastDpi;
    private bool _disposed;

    private DpiDebugLogger(Window window)
    {
        _window = window;
        _hwnd = DpiHelper.GetWindowHandle(window);
        _lastDpi = DpiHelper.GetWindowDpi(_hwnd);

        _window.SizeChanged += Window_SizeChanged;
        _window.Closed += Window_Closed;

        if (_hwnd != IntPtr.Zero)
        {
            var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            if (_appWindow is not null)
            {
                _appWindow.Changed += AppWindow_Changed;
            }
        }
    }

    public static DpiDebugLogger Attach(Window window)
    {
        var logger = new DpiDebugLogger(window);
        logger.LogState("Initial");
        return logger;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _window.SizeChanged -= Window_SizeChanged;
        _window.Closed -= Window_Closed;
        if (_appWindow is not null)
        {
            _appWindow.Changed -= AppWindow_Changed;
        }
    }

    private void Window_SizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        LogState("Window size changed");
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        Dispose();
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        LogState("AppWindow changed");
    }

    private void LogState(string reason)
    {
        if (_disposed || _hwnd == IntPtr.Zero)
        {
            return;
        }

        var dpi = DpiHelper.GetWindowDpi(_hwnd);
        var scale = DpiHelper.GetScaleFactor(dpi);
        var awareness = DpiHelper.GetDpiAwarenessDescription(_hwnd);
        var bounds = DpiHelper.GetWindowBoundsPhysical(_hwnd);
        var displayText = GetDisplayText();
        var dpiChangeText = dpi == _lastDpi
            ? string.Empty
            : $" DPI changed {_lastDpi}->{dpi}.";

        _lastDpi = dpi;
        Debug.WriteLine(
            $"[DPI] {reason}.{dpiChangeText} Awareness={awareness}; DPI={dpi}; Scale={scale:0.###}; WindowPhysical={bounds.X},{bounds.Y},{bounds.Width}x{bounds.Height}; {displayText}");
    }

    private string GetDisplayText()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return "Display=unknown";
        }

        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        var display = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
        if (display is null)
        {
            return "Display=unknown";
        }

        var workArea = display.WorkArea;
        return $"WorkAreaPhysical={workArea.X},{workArea.Y},{workArea.Width}x{workArea.Height}";
    }
}
#endif
