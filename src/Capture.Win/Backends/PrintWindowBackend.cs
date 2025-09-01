using System.Drawing;
using System.Drawing.Imaging;
using Vanara.PInvoke;
using PixelAutomation.Core.Interfaces;
using static Vanara.PInvoke.User32;
using static Vanara.PInvoke.Gdi32;

namespace PixelAutomation.Capture.Win.Backends;

public class PrintWindowBackend : ICaptureBackend
{
    private IntPtr _hwnd;
    private RECT _windowRect;
    private int _dpi = 96;

    public string Name => "PrintWindow";
    public bool IsAvailable => true;
    public CaptureBackendType Type => CaptureBackendType.PrintWindow;

    public Task<bool> InitializeAsync(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return Task.FromResult(false);

        _hwnd = hwnd;
        GetWindowRect(_hwnd, out _windowRect);
        return Task.FromResult(true);
    }

    public Task<Bitmap?> CaptureAsync(Rectangle? roi = null)
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
                        bitmap.Dispose();
                        return Task.FromResult<Bitmap?>(null);
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
                
                var cropped = new Bitmap(roiRect.Width, roiRect.Height);
                using (var g = Graphics.FromImage(cropped))
                {
                    g.DrawImage(bitmap, 0, 0, roiRect, GraphicsUnit.Pixel);
                }
                bitmap.Dispose();
                return Task.FromResult<Bitmap?>(cropped);
            }

            return Task.FromResult<Bitmap?>(bitmap);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PrintWindow capture failed: {ex.Message}");
            return Task.FromResult<Bitmap?>(null);
        }
    }

    public void UpdateDpi(int dpi)
    {
        _dpi = dpi;
    }

    public void Dispose()
    {
    }
}