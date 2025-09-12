using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using Vanara.PInvoke;

namespace PixelAutomation.Tool.Overlay.WPF.Services;

public static class WindowHelper
{
    public static string GetWindowTitle(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return "None";

        var length = User32.GetWindowTextLength(hwnd);
        if (length == 0)
            return "Untitled";

        var sb = new StringBuilder(length + 1);
        User32.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static void SetWindowTransparent(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var extendedStyle = User32.GetWindowLong(hwnd, User32.WindowLongFlags.GWL_EXSTYLE);
        User32.SetWindowLong(hwnd, User32.WindowLongFlags.GWL_EXSTYLE,
            extendedStyle | (int)User32.WindowStylesEx.WS_EX_TRANSPARENT);
    }

    public static void SetWindowInteractive(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var extendedStyle = User32.GetWindowLong(hwnd, User32.WindowLongFlags.GWL_EXSTYLE);
        User32.SetWindowLong(hwnd, User32.WindowLongFlags.GWL_EXSTYLE,
            extendedStyle & ~(int)User32.WindowStylesEx.WS_EX_TRANSPARENT);
    }
}