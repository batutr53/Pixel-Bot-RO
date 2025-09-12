using System;
using System.Threading.Tasks;
using Vanara.PInvoke;
using static Vanara.PInvoke.User32;

namespace PixelAutomation.Tool.Overlay.WPF.Services;

public static class MouseClick
{
    public static void Click(IntPtr hwnd, int x, int y)
    {
        if (hwnd == IntPtr.Zero) return;

        try
        {
            // Convert client coordinates to screen coordinates
            var clientPoint = new POINT { x = x, y = y };
            ClientToScreen(hwnd, ref clientPoint);

            // Save current cursor position
            GetCursorPos(out var originalPos);

            // Move to target position and click
            SetCursorPos(clientPoint.x, clientPoint.y);
            
            // Send mouse down and up events
            mouse_event(MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
            System.Threading.Thread.Sleep(50);
            mouse_event(MOUSEEVENTF.MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);

            // Restore cursor position
            System.Threading.Thread.Sleep(100);
            SetCursorPos(originalPos.x, originalPos.y);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MouseClick error: {ex.Message}");
        }
    }
}