using System.Drawing;
using Vanara.PInvoke;
using static Vanara.PInvoke.User32;
using static Vanara.PInvoke.Gdi32;

namespace PixelAutomation.Tool.Overlay.WPF.Services;

public static class ColorSampler
{
    public static Color GetColorAt(IntPtr hwnd, int clientX, int clientY)
    {
        try
        {
            // Apply MuMu Player offset compensation
            var compensatedX = clientX;
            var compensatedY = clientY;
            
            var processName = GetProcessName(hwnd);
            if (processName.Contains("NemuPlayer") || processName.Contains("MuMuPlayer") || processName.Contains("MuMuNxDevice"))
            {
                // Same offset as CoordinatePicker - but REVERSE for color sampling
                compensatedX = clientX + 8;
                compensatedY = clientY + 50;
                
                // Debug color sampling coordinates
                if (clientX < 1000 && clientY < 1000) // Avoid spam, only log reasonable coordinates
                {
                    System.Console.WriteLine($"ColorSampler MuMu OFFSET: Original({clientX},{clientY}) -> Compensated({compensatedX},{compensatedY}) Process:{processName}");
                }
            }
            
            // Convert compensated client coordinates to screen coordinates
            var point = new POINT { x = compensatedX, y = compensatedY };
            ClientToScreen(hwnd, ref point);
            
            // Get pixel color from screen
            var hdc = GetDC(IntPtr.Zero);
            try
            {
                var colorRef = GetPixel(hdc, point.x, point.y);
                
                // Extract RGB from COLORREF
                var r = (byte)(colorRef & 0xFF);
                var g = (byte)((colorRef >> 8) & 0xFF);
                var b = (byte)((colorRef >> 16) & 0xFF);
                
                return Color.FromArgb(r, g, b);
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, hdc);
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Color sampling failed: {ex.Message}");
            return Color.Black;
        }
    }

    public static Color GetAverageColorInArea(IntPtr hwnd, int clientX, int clientY, int boxSize = 3)
    {
        try
        {
            int totalR = 0, totalG = 0, totalB = 0;
            int count = 0;
            int halfBox = boxSize / 2;

            for (int dy = -halfBox; dy <= halfBox; dy++)
            {
                for (int dx = -halfBox; dx <= halfBox; dx++)
                {
                    var color = GetColorAt(hwnd, clientX + dx, clientY + dy);
                    totalR += color.R;
                    totalG += color.G;
                    totalB += color.B;
                    count++;
                }
            }

            if (count == 0)
                return Color.Black;

            return Color.FromArgb(totalR / count, totalG / count, totalB / count);
        }
        catch
        {
            return Color.Black;
        }
    }

    public static double CalculateColorDistance(Color c1, Color c2)
    {
        var dr = c1.R - c2.R;
        var dg = c1.G - c2.G;
        var db = c1.B - c2.B;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    public static bool IsColorMatch(Color current, Color expected, int tolerance)
    {
        var distance = CalculateColorDistance(current, expected);
        return distance <= tolerance;
    }

    public static BarAnalysisResult AnalyzeHpMpBar(IntPtr hwnd, int x, int y, int width, int height)
    {
        try
        {
            var fullColors = new List<Color>();
            var emptyColors = new List<Color>();
            
            // Sample the bar horizontally to find full vs empty colors
            int centerY = y + height / 2;
            
            for (int sampleX = x; sampleX < x + width; sampleX += Math.Max(1, width / 20)) // 20 samples across width
            {
                var color = GetColorAt(hwnd, sampleX, centerY);
                
                // Assume first 20% is full, last 20% might be empty
                if (sampleX < x + width * 0.3)
                    fullColors.Add(color);
                else if (sampleX > x + width * 0.7)
                    emptyColors.Add(color);
            }
            
            // Calculate average colors
            var fullColor = fullColors.Count > 0 ? 
                Color.FromArgb(
                    (int)fullColors.Average(c => c.R),
                    (int)fullColors.Average(c => c.G), 
                    (int)fullColors.Average(c => c.B)) : Color.Red;
                    
            var emptyColor = emptyColors.Count > 0 ?
                Color.FromArgb(
                    (int)emptyColors.Average(c => c.R),
                    (int)emptyColors.Average(c => c.G),
                    (int)emptyColors.Average(c => c.B)) : Color.Black;
            
            return new BarAnalysisResult
            {
                FullColor = fullColor,
                EmptyColor = emptyColor,
                BarWidth = width,
                BarHeight = height
            };
        }
        catch
        {
            return new BarAnalysisResult
            {
                FullColor = Color.Red,
                EmptyColor = Color.Black,
                BarWidth = width,
                BarHeight = height
            };
        }
    }

    public static double CalculateBarPercentage(IntPtr hwnd, int x, int y, int width, int height, Color fullColor, Color emptyColor)
    {
        try
        {
            int filledPixels = 0;
            int totalPixels = 0;
            int centerY = y + height / 2;
            
            // Scan horizontally across the bar
            for (int sampleX = x; sampleX < x + width; sampleX += 2) // Every 2nd pixel
            {
                var pixelColor = GetColorAt(hwnd, sampleX, centerY);
                
                // Check if closer to full or empty color
                var distanceToFull = CalculateColorDistance(pixelColor, fullColor);
                var distanceToEmpty = CalculateColorDistance(pixelColor, emptyColor);
                
                if (distanceToFull < distanceToEmpty)
                {
                    filledPixels++;
                }
                totalPixels++;
            }
            
            return totalPixels > 0 ? (double)filledPixels / totalPixels * 100.0 : 0.0;
        }
        catch
        {
            return 100.0; // Safe default
        }
    }
    
    private static string GetProcessName(IntPtr hwnd)
    {
        try
        {
            GetWindowThreadProcessId(hwnd, out var processId);
            var process = System.Diagnostics.Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return "Unknown";
        }
    }
}

public class BarAnalysisResult
{
    public Color FullColor { get; set; }
    public Color EmptyColor { get; set; }
    public int BarWidth { get; set; }
    public int BarHeight { get; set; }
}