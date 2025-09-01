using System.Drawing;
using System.Drawing.Imaging;
using Vanara.PInvoke;
using PixelAutomation.Core.Interfaces;
using static Vanara.PInvoke.User32;
using static Vanara.PInvoke.Gdi32;

namespace PixelAutomation.Capture.Win.Backends;

public class GetPixelBackend : ICaptureBackend
{
    private IntPtr _hwnd;
    private int _dpi = 96;

    public string Name => "GetPixel";
    public bool IsAvailable => true;
    public CaptureBackendType Type => CaptureBackendType.GetPixel;

    public Task<bool> InitializeAsync(IntPtr hwnd)
    {
        _hwnd = hwnd;
        return Task.FromResult(hwnd != IntPtr.Zero);
    }

    public Task<Bitmap?> CaptureAsync(Rectangle? roi = null)
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
            Console.WriteLine($"GetPixel capture failed: {ex.Message}");
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