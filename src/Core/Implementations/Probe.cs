using System.Drawing;
using PixelAutomation.Core.Interfaces;

namespace PixelAutomation.Core.Implementations;

public class Probe : IProbe
{
    public string Name { get; }
    public ProbeKind Kind { get; }
    public Point Location { get; }
    public Size? Size { get; }
    public int Box { get; }
    public ProbeMode Mode { get; }
    public ColorMetric Metric { get; }
    public Color ReferenceColor { get; }
    public Color? ToColor { get; }
    public int Tolerance { get; }
    public int? DebounceMs { get; }

    public Probe(string name, ProbeKind kind, Point location, Size? size = null, 
                 int box = 3, ProbeMode mode = ProbeMode.Level, ColorMetric metric = ColorMetric.RGB,
                 Color? referenceColor = null, Color? toColor = null, int tolerance = 20, int? debounceMs = null)
    {
        Name = name;
        Kind = kind;
        Location = location;
        Size = size;
        Box = box;
        Mode = mode;
        Metric = metric;
        ReferenceColor = referenceColor ?? Color.Black;
        ToColor = toColor;
        Tolerance = tolerance;
        DebounceMs = debounceMs;
    }

    public ProbeResult Evaluate(Bitmap bitmap, ProbeResult? previousResult = null)
    {
        var currentColor = GetAverageColor(bitmap);
        var distance = CalculateColorDistance(currentColor, ReferenceColor);

        bool triggered = false;
        string? edgeDirection = null;

        if (Mode == ProbeMode.Level)
        {
            triggered = distance <= Tolerance;
        }
        else if (Mode == ProbeMode.Edge && previousResult != null)
        {
            var wasClose = previousResult.Distance <= Tolerance;
            var isClose = distance <= Tolerance;
            
            if (ToColor.HasValue)
            {
                var toDistance = CalculateColorDistance(currentColor, ToColor.Value);
                if (wasClose && toDistance <= Tolerance)
                {
                    triggered = true;
                    edgeDirection = "transition";
                }
            }
            else
            {
                if (wasClose && !isClose)
                {
                    triggered = true;
                    edgeDirection = "falling";
                }
                else if (!wasClose && isClose)
                {
                    triggered = true;
                    edgeDirection = "rising";
                }
            }
        }

        return new ProbeResult
        {
            Triggered = triggered,
            CurrentColor = currentColor,
            Distance = distance,
            Timestamp = DateTime.UtcNow,
            EdgeDirection = edgeDirection
        };
    }

    private Color GetAverageColor(Bitmap bitmap)
    {
        int totalR = 0, totalG = 0, totalB = 0;
        int count = 0;

        if (Kind == ProbeKind.Point)
        {
            int halfBox = Box / 2;
            int startX = Math.Max(0, Location.X - halfBox);
            int startY = Math.Max(0, Location.Y - halfBox);
            int endX = Math.Min(bitmap.Width - 1, Location.X + halfBox);
            int endY = Math.Min(bitmap.Height - 1, Location.Y + halfBox);

            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    totalR += pixel.R;
                    totalG += pixel.G;
                    totalB += pixel.B;
                    count++;
                }
            }
        }
        else if (Kind == ProbeKind.Rect && Size.HasValue)
        {
            int startX = Math.Max(0, Location.X);
            int startY = Math.Max(0, Location.Y);
            int endX = Math.Min(bitmap.Width - 1, Location.X + Size.Value.Width);
            int endY = Math.Min(bitmap.Height - 1, Location.Y + Size.Value.Height);

            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    totalR += pixel.R;
                    totalG += pixel.G;
                    totalB += pixel.B;
                    count++;
                }
            }
        }

        if (count == 0)
            return Color.Black;

        return Color.FromArgb(totalR / count, totalG / count, totalB / count);
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