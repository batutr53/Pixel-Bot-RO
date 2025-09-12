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
            Console.WriteLine($"[ScreenCapture] CaptureWindow called: HWND=0x{hwnd:X8}, Region=({x},{y},{width},{height})");
            
            if (hwnd == IntPtr.Zero)
            {
                Console.WriteLine($"[ScreenCapture] Invalid window handle (Zero)");
                return null;
            }

            // Get window DC
            var hdcSrc = GetWindowDC(hwnd);
            if (hdcSrc == IntPtr.Zero)
            {
                Console.WriteLine($"[ScreenCapture] Failed to get window DC for handle 0x{hwnd:X8}");
                return null;
            }
            
            Console.WriteLine($"[ScreenCapture] Window DC obtained successfully: 0x{hdcSrc:X8}");

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
                Console.WriteLine($"[ScreenCapture] Bitmap created successfully: {bitmap.Width}x{bitmap.Height}");

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