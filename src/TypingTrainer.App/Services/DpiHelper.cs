using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace TypingTrainer.App.Services;

internal static class DpiHelper
{
    public const int DefaultDpi = 96;

    public static IntPtr GetWindowHandle(Window window)
    {
        return WindowNative.GetWindowHandle(window);
    }

    public static uint GetWindowDpi(Window window)
    {
        return GetWindowDpi(GetWindowHandle(window));
    }

    public static uint GetWindowDpi(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return DefaultDpi;
        }

        try
        {
            var dpi = GetDpiForWindow(hwnd);
            return dpi == 0 ? DefaultDpi : dpi;
        }
        catch (EntryPointNotFoundException)
        {
            return DefaultDpi;
        }
    }

    public static double GetScaleFactor(Window window)
    {
        return GetScaleFactor(GetWindowDpi(window));
    }

    public static double GetScaleFactor(uint dpi)
    {
        return dpi <= 0 ? 1.0 : dpi / (double)DefaultDpi;
    }

    public static double LogicalToPhysical(double logicalPixels, double scaleFactor)
    {
        return logicalPixels * scaleFactor;
    }

    public static double PhysicalToLogical(double physicalPixels, double scaleFactor)
    {
        return scaleFactor <= 0 ? physicalPixels : physicalPixels / scaleFactor;
    }

    public static RectInt32 GetWindowBoundsPhysical(Window window)
    {
        return GetWindowBoundsPhysical(GetWindowHandle(window));
    }

    public static RectInt32 GetWindowBoundsPhysical(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect))
        {
            return new RectInt32(0, 0, 0, 0);
        }

        return new RectInt32(
            rect.Left,
            rect.Top,
            Math.Max(0, rect.Right - rect.Left),
            Math.Max(0, rect.Bottom - rect.Top));
    }

    public static string GetDpiAwarenessDescription(Window window)
    {
        return GetDpiAwarenessDescription(GetWindowHandle(window));
    }

    public static string GetDpiAwarenessDescription(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return "Unknown";
        }

        try
        {
            var context = GetWindowDpiAwarenessContext(hwnd);
            if (AreDpiAwarenessContextsEqual(context, DpiAwarenessContextPerMonitorAwareV2))
            {
                return "PerMonitorV2";
            }

            if (AreDpiAwarenessContextsEqual(context, DpiAwarenessContextPerMonitorAware))
            {
                return "PerMonitor";
            }

            if (AreDpiAwarenessContextsEqual(context, DpiAwarenessContextSystemAware))
            {
                return "System";
            }

            if (AreDpiAwarenessContextsEqual(context, DpiAwarenessContextUnaware))
            {
                return "Unaware";
            }
        }
        catch (EntryPointNotFoundException)
        {
            return "Unknown";
        }

        return "Unknown";
    }

    private static readonly IntPtr DpiAwarenessContextUnaware = new(-1);
    private static readonly IntPtr DpiAwarenessContextSystemAware = new(-2);
    private static readonly IntPtr DpiAwarenessContextPerMonitorAware = new(-3);
    private static readonly IntPtr DpiAwarenessContextPerMonitorAwareV2 = new(-4);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool AreDpiAwarenessContextsEqual(IntPtr dpiContextA, IntPtr dpiContextB);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRect
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;
    }
}

internal readonly record struct RectInt32(int X, int Y, int Width, int Height);
