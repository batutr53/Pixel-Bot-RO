using System.Drawing;

namespace PixelAutomation.Core.Interfaces;

public interface ICaptureBackend : IDisposable
{
    string Name { get; }
    bool IsAvailable { get; }
    CaptureBackendType Type { get; }
    
    Task<bool> InitializeAsync(IntPtr hwnd);
    Task<Bitmap?> CaptureAsync(Rectangle? roi = null);
    void UpdateDpi(int dpi);
}

public enum CaptureBackendType
{
    WindowsGraphicsCapture,
    PrintWindow,
    GetPixel
}

public interface ICaptureFactory
{
    ICaptureBackend CreateBackend(CaptureBackendType type);
    ICaptureBackend CreateWithFallback(CaptureBackendType preferred);
}