using System.Drawing;
using System.Drawing.Imaging;
using Vanara.PInvoke;
using PixelAutomation.Core.Interfaces;
using static Vanara.PInvoke.User32;
using static Vanara.PInvoke.Gdi32;

namespace PixelAutomation.Capture.Win.Backends;

public class WindowsGraphicsCaptureBackend : ICaptureBackend
{
    private IntPtr _hwnd;
    private int _dpi = 96;
    private bool _useWgc = false;

    public string Name => _useWgc ? "Windows Graphics Capture" : "Windows Graphics Capture (PrintWindow Fallback)";
    public bool IsAvailable => true; // Always available through fallback
    public CaptureBackendType Type => CaptureBackendType.WindowsGraphicsCapture;

    public Task<bool> InitializeAsync(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return Task.FromResult(false);

        _hwnd = hwnd;

        // Try to detect if WGC is available (Windows 10 1903+ with proper APIs)
        // For now, we'll always fall back to PrintWindow since WGC requires additional SDKs
        _useWgc = false;

        return Task.FromResult(true);
    }

    public Task<Bitmap?> CaptureAsync(Rectangle? roi = null)
    {
        if (_useWgc)
        {
            // This would be the WGC implementation
            return CaptureWithWgcAsync(roi);
        }
        else
        {
            // Fallback to PrintWindow method
            return CaptureWithPrintWindowAsync(roi);
        }
    }

    private Task<Bitmap?> CaptureWithWgcAsync(Rectangle? roi = null)
    {
        // WGC implementation would go here
        // For now, fallback to PrintWindow
        return CaptureWithPrintWindowAsync(roi);
    }

    private Task<Bitmap?> CaptureWithPrintWindowAsync(Rectangle? roi = null)
    {
        try
        {
            GetClientRect(_hwnd, out var clientRect);

            int width = clientRect.right - clientRect.left;
            int height = clientRect.bottom - clientRect.top;

            if (width <= 0 || height <= 0)
                return Task.FromResult<Bitmap?>(null);

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                var hdc = graphics.GetHdc();
                try
                {
                    bool success = PrintWindow(_hwnd, hdc, PW.PW_RENDERFULLCONTENT);
                    if (!success)
                    {
                        // Try alternative PrintWindow flags
                        success = PrintWindow(_hwnd, hdc, PW.PW_CLIENTONLY);
                        if (!success)
                        {
                            bitmap.Dispose();
                            return CaptureWithBitBltFallback(roi);
                        }
                    }
                }
                finally
                {
                    graphics.ReleaseHdc(hdc);
                }
            }

            if (roi.HasValue && roi.Value.Width > 0 && roi.Value.Height > 0)
            {
                var roiRect = roi.Value;
                if (roiRect.X < 0) roiRect.X = 0;
                if (roiRect.Y < 0) roiRect.Y = 0;
                if (roiRect.Right > width) roiRect.Width = width - roiRect.X;
                if (roiRect.Bottom > height) roiRect.Height = height - roiRect.Y;

                if (roiRect.Width > 0 && roiRect.Height > 0)
                {
                    var cropped = new Bitmap(roiRect.Width, roiRect.Height);
                    using (var g = Graphics.FromImage(cropped))
                    {
                        g.DrawImage(bitmap, 0, 0, roiRect, GraphicsUnit.Pixel);
                    }
                    bitmap.Dispose();
                    return Task.FromResult<Bitmap?>(cropped);
                }
            }

            return Task.FromResult<Bitmap?>(bitmap);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WindowsGraphicsCapture PrintWindow fallback failed: {ex.Message}");
            return CaptureWithBitBltFallback(roi);
        }
    }

    private Task<Bitmap?> CaptureWithBitBltFallback(Rectangle? roi = null)
    {
        try
        {
            GetClientRect(_hwnd, out var clientRect);
            var clientPoint = new POINT { x = 0, y = 0 };
            ClientToScreen(_hwnd, ref clientPoint);

            var captureRect = roi ?? new Rectangle(0, 0,
                clientRect.right - clientRect.left,
                clientRect.bottom - clientRect.top);

            if (captureRect.Width <= 0 || captureRect.Height <= 0)
                return Task.FromResult<Bitmap?>(null);

            var screenX = clientPoint.x + captureRect.X;
            var screenY = clientPoint.y + captureRect.Y;

            var bitmap = new Bitmap(captureRect.Width, captureRect.Height, PixelFormat.Format32bppArgb);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                var hdc = GetDC(IntPtr.Zero);
                var memDC = graphics.GetHdc();

                try
                {
                    BitBlt(memDC, 0, 0, captureRect.Width, captureRect.Height,
                           hdc, screenX, screenY, RasterOperationMode.SRCCOPY);
                }
                finally
                {
                    graphics.ReleaseHdc(memDC);
                    ReleaseDC(IntPtr.Zero, hdc);
                }
            }

            return Task.FromResult<Bitmap?>(bitmap);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WindowsGraphicsCapture BitBlt fallback failed: {ex.Message}");
            return Task.FromResult<Bitmap?>(null);
        }
    }

    public void UpdateDpi(int dpi)
    {
        _dpi = dpi;
    }

    public void Dispose()
    {
        // Cleanup resources if any were allocated
    }
}