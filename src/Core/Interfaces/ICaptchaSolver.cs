using System.Drawing;

namespace PixelAutomation.Core.Interfaces;

public interface ICaptchaSolver : IDisposable
{
    string Name { get; }
    bool IsAvailable { get; }
    
    Task<string?> SolveCaptchaAsync(Bitmap captchaImage, CaptchaOptions? options = null);
    Task<bool> InitializeAsync();
}

public interface ICaptchaDetector
{
    string Name { get; }
    Task<bool> DetectCaptchaAsync(Bitmap screenCapture, Rectangle searchArea);
    Task<Rectangle?> FindCaptchaAreaAsync(Bitmap screenCapture, Rectangle searchArea);
}

public class CaptchaOptions
{
    public CaptchaProcessingMode ProcessingMode { get; set; } = CaptchaProcessingMode.Enhanced;
    public TesseractPageSegmentationMode PsmMode { get; set; } = TesseractPageSegmentationMode.SingleWord;
    public bool UseGrayscale { get; set; } = true;
    public bool UseHistogramEqualization { get; set; } = true;
    public double ContrastFactor { get; set; } = 3.5;
    public double SharpnessFactor { get; set; } = 3.0;
    public double BrightnessFactor { get; set; } = 1.3;
    public int ScaleFactor { get; set; } = 4;
}

public enum CaptchaProcessingMode
{
    Original,
    Enhanced,
    MultipleAttempts
}

public enum TesseractPageSegmentationMode
{
    FullPage = 3,
    SingleBlock = 6,
    SingleLine = 7,
    SingleWord = 8,
    RawLine = 13
}

public class CaptchaResult
{
    public bool Success { get; init; }
    public string? Text { get; init; }
    public double Confidence { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
    public TimeSpan ProcessingTime { get; init; }
}