using System.Drawing;
using Vanara.PInvoke;
using static Vanara.PInvoke.User32;
using static Vanara.PInvoke.Gdi32;

namespace PixelAutomation.Tool.Overlay.WPF.Services;

public static class TestColorSampler
{
    public static Color GetColorAtWithOffset(IntPtr hwnd, int clientX, int clientY, int offsetX, int offsetY)
    {
        try
        {
            // Apply custom offset for testing
            var compensatedX = clientX + offsetX;
            var compensatedY = clientY + offsetY;
            
            // Convert compensated client coordinates to screen coordinates
            var point = new POINT { x = compensatedX, y = compensatedY };
            ClientToScreen(hwnd, ref point);
            
            // Get pixel color from screen
            var hdc = GetDC(IntPtr.Zero);
            try
            {
                var colorRef = GetPixel(hdc, point.x, point.y);
                
                // Extract RGB from COLORREF
                var r = (byte)(colorRef & 0xFF);
                var g = (byte)((colorRef >> 8) & 0xFF);
                var b = (byte)((colorRef >> 16) & 0xFF);
                
                return Color.FromArgb(r, g, b);
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, hdc);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get color at ({clientX},{clientY}) with offset ({offsetX},{offsetY}): {ex.Message}");
        }
    }
}