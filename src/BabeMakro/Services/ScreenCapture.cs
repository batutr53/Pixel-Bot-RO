using System.Drawing;
using System.Drawing.Imaging;
using Vanara.PInvoke;
using static Vanara.PInvoke.User32;
using static Vanara.PInvoke.Gdi32;

namespace PixelAutomation.Tool.Overlay.WPF.Services;

public static class ScreenCapture
{
    public static Bitmap? CaptureWindow(IntPtr hwnd, int x, int y, int width, int height)
    {
        try
        {
            if (hwnd == IntPtr.Zero) return null;

            // Get window DC
            var hdcSrc = GetWindowDC(hwnd);
            if (hdcSrc == IntPtr.Zero) return null;

            try
            {
                // Create compatible DC and bitmap
                var hdcDest = CreateCompatibleDC(hdcSrc);
                var hBitmap = CreateCompatibleBitmap(hdcSrc, width, height);
                var hOld = SelectObject(hdcDest, (IntPtr)hBitmap.DangerousGetHandle());

                // Copy the portion of the window
                BitBlt(hdcDest, 0, 0, width, height, hdcSrc, x, y, RasterOperationMode.SRCCOPY);

                // Convert to managed bitmap
                var bitmap = Image.FromHbitmap(hBitmap.DangerousGetHandle());

                // Cleanup
                SelectObject(hdcDest, hOld);
                DeleteObject(hBitmap);
                DeleteDC(hdcDest);

                return bitmap;
            }
            finally
            {
                ReleaseDC(hwnd, hdcSrc);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ScreenCapture error: {ex.Message}");
            return null;
        }
    }

    public static Bitmap? CaptureScreen(int x, int y, int width, int height)
    {
        try
        {
            var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height));
            }
            return bitmap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CaptureScreen error: {ex.Message}");
            return null;
        }
    }
}