using System.Drawing;
using Microsoft.Extensions.Logging;
using PixelAutomation.Core.Interfaces;

namespace PixelAutomation.Core.Implementations;

public class ColorBasedCaptchaDetector : ICaptchaDetector
{
    private readonly ILogger<ColorBasedCaptchaDetector> _logger;
    
    // Common captcha background/border colors
    private readonly Color[] _captchaColors = new[]
    {
        Color.FromArgb(255, 255, 255), // White background
        Color.FromArgb(0, 0, 0),       // Black border
        Color.FromArgb(192, 192, 192), // Light gray
        Color.FromArgb(128, 128, 128), // Gray
        Color.FromArgb(255, 255, 0),   // Yellow
        Color.FromArgb(255, 0, 0),     // Red
        Color.FromArgb(0, 255, 0),     // Green
        Color.FromArgb(0, 0, 255)      // Blue
    };

    public string Name => "Color-based Captcha Detector";

    public ColorBasedCaptchaDetector(ILogger<ColorBasedCaptchaDetector> logger)
    {
        _logger = logger;
    }

    public async Task<bool> DetectCaptchaAsync(Bitmap screenCapture, Rectangle searchArea)
    {
        try
        {
            var area = await FindCaptchaAreaAsync(screenCapture, searchArea);
            return area.HasValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting captcha");
            return false;
        }
    }

    public async Task<Rectangle?> FindCaptchaAreaAsync(Bitmap screenCapture, Rectangle searchArea)
    {
        return await Task.Run(() => FindCaptchaAreaSync(screenCapture, searchArea));
    }

    private Rectangle? FindCaptchaAreaSync(Bitmap screenCapture, Rectangle searchArea)
    {
        try
        {
            // Ensure search area is within image bounds
            var clampedArea = Rectangle.Intersect(searchArea, 
                new Rectangle(0, 0, screenCapture.Width, screenCapture.Height));
                
            if (clampedArea.IsEmpty)
                return null;

            // Method 1: Look for rectangular regions with captcha-like characteristics
            var candidateArea = FindRectangularPattern(screenCapture, clampedArea);
            if (candidateArea.HasValue)
            {
                _logger.LogDebug("Found captcha candidate using rectangular pattern detection");
                return candidateArea;
            }

            // Method 2: Look for color patterns typical of captcha
            candidateArea = FindColorPattern(screenCapture, clampedArea);
            if (candidateArea.HasValue)
            {
                _logger.LogDebug("Found captcha candidate using color pattern detection");
                return candidateArea;
            }

            // Method 3: Look for contrast changes (captcha vs background)
            candidateArea = FindContrastPattern(screenCapture, clampedArea);
            if (candidateArea.HasValue)
            {
                _logger.LogDebug("Found captcha candidate using contrast pattern detection");
                return candidateArea;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in captcha area detection");
            return null;
        }
    }

    private Rectangle? FindRectangularPattern(Bitmap image, Rectangle searchArea)
    {
        try
        {
            // Look for rectangular regions that might be captcha boxes
            // Common captcha dimensions: width 200-400px, height 50-150px
            var minWidth = 100;
            var maxWidth = 500;
            var minHeight = 30;
            var maxHeight = 200;

            // Sample points within the search area to find potential captcha regions
            var samplePoints = GenerateSamplePoints(searchArea, 20, 10);
            
            foreach (var point in samplePoints)
            {
                if (point.X >= image.Width || point.Y >= image.Height)
                    continue;

                var backgroundColor = image.GetPixel(point.X, point.Y);
                
                // Look for rectangular regions with consistent background
                var rect = FindConsistentColorRegion(image, point, backgroundColor, 
                    minWidth, maxWidth, minHeight, maxHeight);
                    
                if (rect.HasValue && IsCaptchaLikeRegion(image, rect.Value))
                {
                    return rect;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error in rectangular pattern detection");
            return null;
        }
    }

    private Rectangle? FindColorPattern(Bitmap image, Rectangle searchArea)
    {
        try
        {
            // Look for common captcha color patterns
            var colorCounts = new Dictionary<Color, List<Point>>();
            
            // Sample the search area
            for (int x = searchArea.Left; x < searchArea.Right; x += 5)
            {
                for (int y = searchArea.Top; y < searchArea.Bottom; y += 5)
                {
                    if (x >= image.Width || y >= image.Height)
                        continue;

                    var pixel = image.GetPixel(x, y);
                    var nearestCaptchaColor = FindNearestCaptchaColor(pixel);
                    
                    if (nearestCaptchaColor.HasValue)
                    {
                        if (!colorCounts.ContainsKey(nearestCaptchaColor.Value))
                            colorCounts[nearestCaptchaColor.Value] = new List<Point>();
                            
                        colorCounts[nearestCaptchaColor.Value].Add(new Point(x, y));
                    }
                }
            }

            // Find the color with the most occurrences that might indicate a captcha
            var dominantColor = colorCounts
                .Where(kvp => kvp.Value.Count > 10) // Minimum threshold
                .OrderByDescending(kvp => kvp.Value.Count)
                .FirstOrDefault();

            if (dominantColor.Key != default && dominantColor.Value.Count > 0)
            {
                // Calculate bounding rectangle of the dominant color
                var points = dominantColor.Value;
                var minX = points.Min(p => p.X);
                var maxX = points.Max(p => p.X);
                var minY = points.Min(p => p.Y);
                var maxY = points.Max(p => p.Y);
                
                var rect = new Rectangle(minX, minY, maxX - minX, maxY - minY);
                
                // Validate if this looks like a captcha region
                if (rect.Width >= 100 && rect.Width <= 500 && 
                    rect.Height >= 30 && rect.Height <= 200)
                {
                    return rect;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error in color pattern detection");
            return null;
        }
    }

    private Rectangle? FindContrastPattern(Bitmap image, Rectangle searchArea)
    {
        try
        {
            // Look for areas with high contrast (text on background)
            var contrastMap = new double[searchArea.Width / 4, searchArea.Height / 4];
            
            for (int x = 0; x < contrastMap.GetLength(0); x++)
            {
                for (int y = 0; y < contrastMap.GetLength(1); y++)
                {
                    var actualX = searchArea.Left + x * 4;
                    var actualY = searchArea.Top + y * 4;
                    
                    if (actualX + 8 >= image.Width || actualY + 8 >= image.Height)
                        continue;

                    contrastMap[x, y] = CalculateLocalContrast(image, actualX, actualY, 8);
                }
            }

            // Find regions with consistently high contrast
            var threshold = 50.0; // Contrast threshold
            var highContrastRegions = new List<Rectangle>();
            
            for (int x = 0; x < contrastMap.GetLength(0) - 5; x++)
            {
                for (int y = 0; y < contrastMap.GetLength(1) - 3; y++)
                {
                    var avgContrast = 0.0;
                    var count = 0;
                    
                    // Check a 5x3 region for high contrast
                    for (int dx = 0; dx < 5; dx++)
                    {
                        for (int dy = 0; dy < 3; dy++)
                        {
                            if (x + dx < contrastMap.GetLength(0) && y + dy < contrastMap.GetLength(1))
                            {
                                avgContrast += contrastMap[x + dx, y + dy];
                                count++;
                            }
                        }
                    }
                    
                    if (count > 0 && avgContrast / count > threshold)
                    {
                        var rect = new Rectangle(
                            searchArea.Left + x * 4,
                            searchArea.Top + y * 4,
                            5 * 4,
                            3 * 4
                        );
                        highContrastRegions.Add(rect);
                    }
                }
            }

            // Merge nearby high contrast regions
            if (highContrastRegions.Count > 0)
            {
                var merged = MergeNearbyRectangles(highContrastRegions);
                var bestCandidate = merged
                    .Where(r => r.Width >= 100 && r.Width <= 500 && 
                               r.Height >= 30 && r.Height <= 200)
                    .OrderByDescending(r => r.Width * r.Height)
                    .FirstOrDefault();
                    
                if (bestCandidate != default)
                    return bestCandidate;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error in contrast pattern detection");
            return null;
        }
    }

    private List<Point> GenerateSamplePoints(Rectangle area, int countX, int countY)
    {
        var points = new List<Point>();
        var stepX = Math.Max(1, area.Width / countX);
        var stepY = Math.Max(1, area.Height / countY);
        
        for (int x = area.Left; x < area.Right; x += stepX)
        {
            for (int y = area.Top; y < area.Bottom; y += stepY)
            {
                points.Add(new Point(x, y));
            }
        }
        
        return points;
    }

    private Rectangle? FindConsistentColorRegion(Bitmap image, Point startPoint, Color backgroundColor, 
        int minWidth, int maxWidth, int minHeight, int maxHeight)
    {
        try
        {
            var tolerance = 30;
            
            // Expand from start point to find the extent of similar color
            int left = startPoint.X, right = startPoint.X;
            int top = startPoint.Y, bottom = startPoint.Y;
            
            // Expand horizontally
            while (left > 0 && ColorDistance(image.GetPixel(left - 1, startPoint.Y), backgroundColor) < tolerance)
                left--;
            while (right < image.Width - 1 && ColorDistance(image.GetPixel(right + 1, startPoint.Y), backgroundColor) < tolerance)
                right++;
                
            // Expand vertically
            while (top > 0 && ColorDistance(image.GetPixel(startPoint.X, top - 1), backgroundColor) < tolerance)
                top--;
            while (bottom < image.Height - 1 && ColorDistance(image.GetPixel(startPoint.X, bottom + 1), backgroundColor) < tolerance)
                bottom++;
                
            var width = right - left + 1;
            var height = bottom - top + 1;
            
            if (width >= minWidth && width <= maxWidth && height >= minHeight && height <= maxHeight)
            {
                return new Rectangle(left, top, width, height);
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }

    private bool IsCaptchaLikeRegion(Bitmap image, Rectangle rect)
    {
        try
        {
            // Check if the region has characteristics typical of a captcha
            var sampleCount = 0;
            var textLikePixels = 0;
            
            // Sample pixels within the rectangle
            for (int x = rect.Left; x < rect.Right; x += Math.Max(1, rect.Width / 20))
            {
                for (int y = rect.Top; y < rect.Bottom; y += Math.Max(1, rect.Height / 10))
                {
                    if (x >= image.Width || y >= image.Height)
                        continue;
                        
                    var pixel = image.GetPixel(x, y);
                    sampleCount++;
                    
                    // Check if pixel looks like text (dark on light or light on dark)
                    var intensity = (pixel.R + pixel.G + pixel.B) / 3;
                    if (intensity < 100 || intensity > 200) // Dark or light pixels
                        textLikePixels++;
                }
            }
            
            // If at least 30% of sampled pixels look like text, consider it a captcha candidate
            return sampleCount > 0 && (double)textLikePixels / sampleCount > 0.3;
        }
        catch
        {
            return false;
        }
    }

    private Color? FindNearestCaptchaColor(Color pixel)
    {
        var minDistance = double.MaxValue;
        Color? nearestColor = null;
        
        foreach (var captchaColor in _captchaColors)
        {
            var distance = ColorDistance(pixel, captchaColor);
            if (distance < minDistance && distance < 50) // Tolerance threshold
            {
                minDistance = distance;
                nearestColor = captchaColor;
            }
        }
        
        return nearestColor;
    }

    private double ColorDistance(Color c1, Color c2)
    {
        var dr = c1.R - c2.R;
        var dg = c1.G - c2.G;
        var db = c1.B - c2.B;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    private double CalculateLocalContrast(Bitmap image, int centerX, int centerY, int radius)
    {
        try
        {
            var minIntensity = 255.0;
            var maxIntensity = 0.0;
            
            for (int x = Math.Max(0, centerX - radius); x < Math.Min(image.Width, centerX + radius); x++)
            {
                for (int y = Math.Max(0, centerY - radius); y < Math.Min(image.Height, centerY + radius); y++)
                {
                    var pixel = image.GetPixel(x, y);
                    var intensity = (pixel.R + pixel.G + pixel.B) / 3.0;
                    
                    minIntensity = Math.Min(minIntensity, intensity);
                    maxIntensity = Math.Max(maxIntensity, intensity);
                }
            }
            
            return maxIntensity - minIntensity;
        }
        catch
        {
            return 0.0;
        }
    }

    private List<Rectangle> MergeNearbyRectangles(List<Rectangle> rectangles)
    {
        var merged = new List<Rectangle>();
        var used = new bool[rectangles.Count];
        
        for (int i = 0; i < rectangles.Count; i++)
        {
            if (used[i])
                continue;
                
            var current = rectangles[i];
            used[i] = true;
            
            // Find nearby rectangles to merge with
            for (int j = i + 1; j < rectangles.Count; j++)
            {
                if (used[j])
                    continue;
                    
                var other = rectangles[j];
                
                // Check if rectangles are close enough to merge
                var distance = CalculateRectangleDistance(current, other);
                if (distance < 20) // Merge threshold
                {
                    current = Rectangle.Union(current, other);
                    used[j] = true;
                }
            }
            
            merged.Add(current);
        }
        
        return merged;
    }

    private double CalculateRectangleDistance(Rectangle r1, Rectangle r2)
    {
        var center1 = new Point(r1.Left + r1.Width / 2, r1.Top + r1.Height / 2);
        var center2 = new Point(r2.Left + r2.Width / 2, r2.Top + r2.Height / 2);
        
        var dx = center1.X - center2.X;
        var dy = center1.Y - center2.Y;
        
        return Math.Sqrt(dx * dx + dy * dy);
    }
}