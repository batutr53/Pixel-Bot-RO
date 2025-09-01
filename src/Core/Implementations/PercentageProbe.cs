using System.Drawing;
using PixelAutomation.Core.Interfaces;

namespace PixelAutomation.Core.Implementations;

public class PercentageProbe : IPercentageProbe
{
    public string Name { get; }
    public ProbeKind Kind => ProbeKind.Point;
    public Point Location { get; private set; }
    public Size? Size => null;
    public int Box { get; }
    public ProbeMode Mode { get; }
    public ColorMetric Metric { get; }
    public Color ReferenceColor { get; }
    public Color? ToColor { get; }
    public int Tolerance { get; }
    public int? DebounceMs { get; }
    
    public int StartX { get; }
    public int EndX { get; }
    public double MonitorPercentage { get; }
    public int CalculatedX => StartX + (int)((MonitorPercentage / 100.0) * (EndX - StartX));
    public PercentageProbeType ProbeType { get; }

    private readonly int _y;
    private double _lastPercentage = 100.0;

    public PercentageProbe(
        string name,
        int startX,
        int endX,
        int y,
        double monitorPercentage,
        PercentageProbeType probeType,
        Color expectedColor,
        Color? emptyColor = null,
        int tolerance = 30,
        int box = 1,
        ProbeMode mode = ProbeMode.Edge,
        ColorMetric metric = ColorMetric.RGB,
        int? debounceMs = 100)
    {
        Name = name;
        StartX = startX;
        EndX = endX;
        _y = y;
        MonitorPercentage = monitorPercentage;
        ProbeType = probeType;
        ReferenceColor = expectedColor;
        ToColor = emptyColor;
        Tolerance = tolerance;
        Box = box;
        Mode = mode;
        Metric = metric;
        DebounceMs = debounceMs;
        
        Location = new Point(CalculatedX, y);
    }

    public ProbeResult Evaluate(Bitmap bitmap, ProbeResult? previousResult = null)
    {
        // Update location based on current percentage
        Location = new Point(CalculatedX, _y);
        
        var currentColor = GetPixelColor(bitmap, CalculatedX, _y);
        var expectedDistance = CalculateColorDistance(currentColor, ReferenceColor);
        
        // Calculate current percentage by scanning the bar
        var currentPercentage = CalculateCurrentPercentage(bitmap);
        
        bool triggered = false;
        bool thresholdCrossed = false;
        string? edgeDirection = null;
        string? crossDirection = null;

        if (Mode == ProbeMode.Level)
        {
            // Level mode: trigger when color doesn't match expected at the percentage position
            triggered = expectedDistance > Tolerance;
        }
        else if (Mode == ProbeMode.Edge && previousResult != null)
        {
            var prevPercentageResult = previousResult as PercentageProbeResult;
            var wasTriggered = previousResult.Triggered;
            var isTriggered = expectedDistance > Tolerance;
            
            // Edge detection for color change
            if (wasTriggered != isTriggered)
            {
                triggered = true;
                edgeDirection = isTriggered ? "falling" : "rising";
            }
            
            // Threshold crossing detection
            if (prevPercentageResult != null)
            {
                var prevPercentage = prevPercentageResult.CurrentPercentage;
                if ((prevPercentage >= MonitorPercentage && currentPercentage < MonitorPercentage) ||
                    (prevPercentage < MonitorPercentage && currentPercentage >= MonitorPercentage))
                {
                    thresholdCrossed = true;
                    crossDirection = currentPercentage < MonitorPercentage ? "below" : "above";
                    triggered = true;
                }
            }
        }

        _lastPercentage = currentPercentage;

        return new PercentageProbeResult
        {
            Triggered = triggered,
            CurrentColor = currentColor,
            Distance = expectedDistance,
            Timestamp = DateTime.UtcNow,
            EdgeDirection = edgeDirection,
            CurrentPercentage = currentPercentage,
            ThresholdCrossed = thresholdCrossed,
            CrossDirection = crossDirection
        };
    }

    private Color GetPixelColor(Bitmap bitmap, int x, int y)
    {
        if (x < 0 || x >= bitmap.Width || y < 0 || y >= bitmap.Height)
            return Color.Black;

        if (Box <= 1)
        {
            return bitmap.GetPixel(x, y);
        }

        // Average color in box area
        int totalR = 0, totalG = 0, totalB = 0;
        int count = 0;
        int halfBox = Box / 2;

        for (int dy = -halfBox; dy <= halfBox; dy++)
        {
            for (int dx = -halfBox; dx <= halfBox; dx++)
            {
                int px = x + dx;
                int py = y + dy;
                if (px >= 0 && px < bitmap.Width && py >= 0 && py < bitmap.Height)
                {
                    var pixel = bitmap.GetPixel(px, py);
                    totalR += pixel.R;
                    totalG += pixel.G;
                    totalB += pixel.B;
                    count++;
                }
            }
        }

        return count > 0 ? Color.FromArgb(totalR / count, totalG / count, totalB / count) : Color.Black;
    }

    private double CalculateCurrentPercentage(Bitmap bitmap)
    {
        int filledPixels = 0;
        int totalPixels = 0;

        // Scan horizontally across the bar to find filled vs empty pixels
        for (int x = StartX; x < EndX; x++)
        {
            var pixelColor = GetPixelColor(bitmap, x, _y);
            var distanceToExpected = CalculateColorDistance(pixelColor, ReferenceColor);
            
            totalPixels++;
            if (distanceToExpected <= Tolerance)
            {
                filledPixels++;
            }
        }

        return totalPixels > 0 ? (double)filledPixels / totalPixels * 100.0 : 0.0;
    }

    private double CalculateColorDistance(Color c1, Color c2)
    {
        return Metric switch
        {
            ColorMetric.RGB => Math.Sqrt(Math.Pow(c1.R - c2.R, 2) + Math.Pow(c1.G - c2.G, 2) + Math.Pow(c1.B - c2.B, 2)),
            ColorMetric.HSV => CalculateHsvDistance(c1, c2),
            ColorMetric.DeltaE => CalculateDeltaE(c1, c2),
            _ => Math.Sqrt(Math.Pow(c1.R - c2.R, 2) + Math.Pow(c1.G - c2.G, 2) + Math.Pow(c1.B - c2.B, 2))
        };
    }

    private double CalculateHsvDistance(Color c1, Color c2)
    {
        var hsv1 = RgbToHsv(c1);
        var hsv2 = RgbToHsv(c2);
        
        var dh = Math.Min(Math.Abs(hsv1.h - hsv2.h), 360 - Math.Abs(hsv1.h - hsv2.h));
        var ds = Math.Abs(hsv1.s - hsv2.s);
        var dv = Math.Abs(hsv1.v - hsv2.v);
        
        return Math.Sqrt(dh * dh + ds * ds * 100 + dv * dv * 100);
    }

    private double CalculateDeltaE(Color c1, Color c2)
    {
        var dr = c1.R - c2.R;
        var dg = c1.G - c2.G;
        var db = c1.B - c2.B;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    private (double h, double s, double v) RgbToHsv(Color color)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double h = 0;
        if (delta != 0)
        {
            if (max == r)
                h = 60 * (((g - b) / delta) % 6);
            else if (max == g)
                h = 60 * (((b - r) / delta) + 2);
            else if (max == b)
                h = 60 * (((r - g) / delta) + 4);
        }

        double s = max == 0 ? 0 : delta / max;
        double v = max;

        return (h < 0 ? h + 360 : h, s, v);
    }
}