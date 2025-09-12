using System;
using System.Threading.Tasks;
using Vanara.PInvoke;
using static Vanara.PInvoke.User32;

namespace PixelAutomation.Tool.Overlay.WPF.Services;

public static class KeyboardInput
{
    public static void SendText(IntPtr hwnd, string text)
    {
        if (hwnd == IntPtr.Zero || string.IsNullOrEmpty(text)) return;

        try
        {
            // Clear any existing text first
            SendKey(hwnd, VK.VK_CONTROL, VK.VK_A); // Select All
            System.Threading.Thread.Sleep(50);
            SendKey(hwnd, VK.VK_DELETE); // Delete
            System.Threading.Thread.Sleep(100);

            // Send each character
            foreach (char c in text)
            {
                SendChar(hwnd, c);
                System.Threading.Thread.Sleep(50);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"KeyboardInput error: {ex.Message}");
        }
    }

    private static void SendChar(IntPtr hwnd, char c)
    {
        try
        {
            // Use WM_CHAR to send character directly to window
            SendMessage(hwnd, WindowMessage.WM_CHAR, (IntPtr)c, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SendChar error for '{c}': {ex.Message}");
        }
    }

    public static void SendKey(IntPtr hwnd, params VK[] keys)
    {
        try
        {
            // Send key down events
            foreach (var key in keys)
            {
                PostMessage(hwnd, WindowMessage.WM_KEYDOWN, (IntPtr)key, IntPtr.Zero);
            }

            System.Threading.Thread.Sleep(50);

            // Send key up events in reverse order
            for (int i = keys.Length - 1; i >= 0; i--)
            {
                PostMessage(hwnd, WindowMessage.WM_KEYUP, (IntPtr)keys[i], IntPtr.Zero);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SendKey error: {ex.Message}");
        }
    }
}