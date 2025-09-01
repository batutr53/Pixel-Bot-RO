using PixelAutomation.Core.Interfaces;
using PixelAutomation.Capture.Win.Backends;

namespace PixelAutomation.Capture.Win;

public class CaptureFactory : ICaptureFactory
{
    public ICaptureBackend CreateBackend(CaptureBackendType type)
    {
        return type switch
        {
            CaptureBackendType.WindowsGraphicsCapture => new WindowsGraphicsCaptureBackend(),
            CaptureBackendType.PrintWindow => new PrintWindowBackend(),
            CaptureBackendType.GetPixel => new GetPixelBackend(),
            _ => throw new NotSupportedException($"Backend type {type} is not supported")
        };
    }

    public ICaptureBackend CreateWithFallback(CaptureBackendType preferred)
    {
        var backend = CreateBackend(preferred);
        
        if (backend.IsAvailable)
            return backend;

        var fallbackOrder = preferred switch
        {
            CaptureBackendType.WindowsGraphicsCapture => new[]
            {
                CaptureBackendType.PrintWindow,
                CaptureBackendType.GetPixel
            },
            CaptureBackendType.PrintWindow => new[]
            {
                CaptureBackendType.GetPixel
            },
            _ => Array.Empty<CaptureBackendType>()
        };

        foreach (var fallbackType in fallbackOrder)
        {
            var fallback = CreateBackend(fallbackType);
            if (fallback.IsAvailable)
            {
                backend.Dispose();
                Console.WriteLine($"Using fallback backend: {fallback.Name}");
                return fallback;
            }
            fallback.Dispose();
        }

        return backend;
    }
}