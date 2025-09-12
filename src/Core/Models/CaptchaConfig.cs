using System.Drawing;
using PixelAutomation.Core.Interfaces;

namespace PixelAutomation.Core.Models;

public class CaptchaConfig
{
    public bool Enabled { get; set; } = false;
    public Rectangle CaptchaArea { get; set; } = new(400, 350, 300, 100);
    public Point TextBoxLocation { get; set; } = new(700, 460);
    public Point SubmitButtonLocation { get; set; } = new(700, 490);
    public int DetectionIntervalMs { get; set; } = 5000;
    public int SolveTimeoutMs { get; set; } = 30000;
    public CaptchaOptions ProcessingOptions { get; set; } = new();
    public List<CaptchaTemplate> Templates { get; set; } = new();
}

public class CaptchaTemplate
{
    public string Name { get; set; } = "";
    public Rectangle SearchArea { get; set; }
    public Color[] DetectionColors { get; set; } = Array.Empty<Color>();
    public int ColorTolerance { get; set; } = 30;
    public string? ExpectedPattern { get; set; }
}

public class CaptchaDetectionEvent
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Rectangle DetectedArea { get; init; }
    public double Confidence { get; init; }
    public string? DetectionMethod { get; init; }
}

public class CaptchaSolveEvent
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string? SolvedText { get; init; }
    public bool Success { get; init; }
    public TimeSpan SolveTime { get; init; }
    public string? ErrorMessage { get; init; }
}