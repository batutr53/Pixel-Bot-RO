using System.Drawing;

namespace PixelAutomation.Core.Interfaces;

public interface IProbe
{
    string Name { get; }
    ProbeKind Kind { get; }
    Point Location { get; }
    Size? Size { get; }
    int Box { get; }
    ProbeMode Mode { get; }
    ColorMetric Metric { get; }
    Color ReferenceColor { get; }
    Color? ToColor { get; }
    int Tolerance { get; }
    int? DebounceMs { get; }
    
    ProbeResult Evaluate(Bitmap bitmap, ProbeResult? previousResult = null);
}

public enum ProbeKind
{
    Point,
    Rect
}

public enum ProbeMode
{
    Level,
    Edge
}

public enum ColorMetric
{
    RGB,
    HSV,
    DeltaE
}

public class ProbeResult
{
    public bool Triggered { get; init; }
    public Color CurrentColor { get; init; }
    public double Distance { get; init; }
    public DateTime Timestamp { get; init; }
    public string? EdgeDirection { get; init; }
}