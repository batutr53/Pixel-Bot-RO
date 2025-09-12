using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Vanara.PInvoke;
using System.Diagnostics;

namespace PixelAutomation.Tool.Overlay.WPF.Services;

/// <summary>
/// High-performance color sampling cache that dramatically reduces Win32 API calls
/// by caching both individual pixel colors and window regions with smart expiry policies.
/// </summary>
public class ColorSamplingCache : IDisposable
{
    private readonly ConcurrentDictionary<string, CachedSample> _colorCache = new();
    private readonly ConcurrentDictionary<string, CachedRegion> _regionCache = new();
    private readonly System.Threading.Timer _cleanupTimer;
    private readonly object _cacheLock = new object();
    private bool _disposed = false;

    // Performance tracking
    private long _cacheHits = 0;
    private long _cacheMisses = 0;
    private long _totalRequests = 0;
    private long _apiCallsSaved = 0;

    // Configuration
    public TimeSpan DefaultColorCacheDuration { get; set; } = TimeSpan.FromMilliseconds(25); // Half the 50ms HP/MP interval
    public TimeSpan DefaultRegionCacheDuration { get; set; } = TimeSpan.FromMilliseconds(50); // Match FastColorSampler
    public int MaxCacheSize { get; set; } = 10000; // Maximum cached entries
    public int NearbyPixelThreshold { get; set; } = 5; // Group nearby pixels
    
    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
        IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);
    
    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);
    
    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    
    private const uint SRCCOPY = 0x00CC0020;

    /// <summary>
    /// Represents a cached color sample
    /// </summary>
    public struct CachedSample
    {
        public Color Color { get; set; }
        public DateTime Timestamp { get; set; }
        public TimeSpan ValidDuration { get; set; }
        public int HitCount { get; set; }
        
        public bool IsValid => DateTime.Now - Timestamp < ValidDuration;
        public bool IsExpired => !IsValid;
    }

    /// <summary>
    /// Represents a cached window region
    /// </summary>
    public class CachedRegion : IDisposable
    {
        public Bitmap? Bitmap { get; set; }
        public Rectangle Bounds { get; set; }
        public DateTime Timestamp { get; set; }
        public TimeSpan ValidDuration { get; set; }
        public int HitCount { get; set; }
        public IntPtr SourceWindow { get; set; }
        
        public bool IsValid => DateTime.Now - Timestamp < ValidDuration;
        public bool IsExpired => !IsValid;

        public void Dispose()
        {
            Bitmap?.Dispose();
            Bitmap = null;
        }
    }

    /// <summary>
    /// Performance statistics for monitoring cache effectiveness
    /// </summary>
    public class CacheStats
    {
        public long TotalRequests { get; set; }
        public long CacheHits { get; set; }
        public long CacheMisses { get; set; }
        public double HitRate => TotalRequests > 0 ? (double)CacheHits / TotalRequests * 100 : 0;
        public long ApiCallsSaved { get; set; }
        public double ApiReductionPercentage => TotalRequests > 0 ? (double)ApiCallsSaved / TotalRequests * 100 : 0;
        public int ColorCacheSize { get; set; }
        public int RegionCacheSize { get; set; }
        public long MemoryUsageBytes { get; set; }
    }

    public ColorSamplingCache()
    {
        // Cleanup expired entries every 5 seconds
        _cleanupTimer = new System.Threading.Timer(CleanupExpiredEntries, null, 
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            
        Debug.WriteLine("[ColorSamplingCache] Initialized - Smart caching enabled");
    }

    /// <summary>
    /// Gets a color from cache or samples it with smart caching strategies
    /// </summary>
    /// <param name="hwnd">Window handle</param>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <param name="cacheDuration">How long to cache this color (optional)</param>
    /// <param name="priority">Cache priority: High = longer cache, Low = shorter cache</param>
    /// <returns>The color at the specified location</returns>
    public Color GetCachedColor(IntPtr hwnd, int x, int y, 
        TimeSpan? cacheDuration = null, CachePriority priority = CachePriority.Normal)
    {
        if (_disposed) return ColorSampler.GetColorAt(hwnd, x, y);
        
        Interlocked.Increment(ref _totalRequests);
        
        // Smart cache duration based on priority
        var duration = cacheDuration ?? GetSmartCacheDuration(priority);
        
        // Create cache key - group nearby pixels together
        var cacheKey = CreatePixelCacheKey(hwnd, x, y);
        
        // Try to get from cache first
        if (_colorCache.TryGetValue(cacheKey, out var cachedSample))
        {
            if (cachedSample.IsValid)
            {
                Interlocked.Increment(ref _cacheHits);
                Interlocked.Increment(ref _apiCallsSaved);
                
                // Update hit count and extend cache slightly on frequent access
                var updatedSample = cachedSample;
                updatedSample.HitCount++;
                if (updatedSample.HitCount > 5) // Frequently accessed pixels get longer cache
                {
                    updatedSample.ValidDuration = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * 1.5);
                }
                _colorCache.TryUpdate(cacheKey, updatedSample, cachedSample);
                
                return cachedSample.Color;
            }
            else
            {
                // Remove expired entry
                _colorCache.TryRemove(cacheKey, out _);
            }
        }
        
        Interlocked.Increment(ref _cacheMisses);
        
        // Sample the color using the most efficient method available
        Color sampledColor = SampleColorEfficiently(hwnd, x, y);
        
        // Cache the result
        var newSample = new CachedSample
        {
            Color = sampledColor,
            Timestamp = DateTime.Now,
            ValidDuration = duration,
            HitCount = 1
        };
        
        _colorCache.AddOrUpdate(cacheKey, newSample, (key, existing) => newSample);
        
        return sampledColor;
    }

    /// <summary>
    /// Batch sample multiple colors efficiently by caching window region
    /// </summary>
    /// <param name="hwnd">Window handle</param>
    /// <param name="coordinates">Array of coordinates to sample</param>
    /// <param name="cacheDuration">How long to cache the region</param>
    /// <returns>Dictionary mapping coordinates to colors</returns>
    public Dictionary<Point, Color> BatchSampleColors(IntPtr hwnd, Point[] coordinates, 
        TimeSpan? cacheDuration = null)
    {
        if (_disposed || coordinates.Length == 0)
        {
            return coordinates.ToDictionary(p => p, p => ColorSampler.GetColorAt(hwnd, p.X, p.Y));
        }
        
        var results = new Dictionary<Point, Color>();
        var duration = cacheDuration ?? DefaultRegionCacheDuration;
        
        // Calculate bounding rectangle for all coordinates
        var minX = coordinates.Min(p => p.X);
        var minY = coordinates.Min(p => p.Y);
        var maxX = coordinates.Max(p => p.X);
        var maxY = coordinates.Max(p => p.Y);
        
        var bounds = new Rectangle(
            Math.Max(0, minX - 10), // Add small padding
            Math.Max(0, minY - 10), 
            Math.Min(1920, maxX - minX + 20), 
            Math.Min(1080, maxY - minY + 20)
        );
        
        // Try to get region from cache
        var regionKey = CreateRegionCacheKey(hwnd, bounds);
        CachedRegion? cachedRegion = null;
        
        if (_regionCache.TryGetValue(regionKey, out cachedRegion))
        {
            if (cachedRegion.IsValid && cachedRegion.Bitmap != null)
            {
                // Use cached region
                foreach (var coord in coordinates)
                {
                    var localX = coord.X - cachedRegion.Bounds.X;
                    var localY = coord.Y - cachedRegion.Bounds.Y;
                    
                    if (localX >= 0 && localY >= 0 && 
                        localX < cachedRegion.Bitmap.Width && localY < cachedRegion.Bitmap.Height)
                    {
                        results[coord] = cachedRegion.Bitmap.GetPixel(localX, localY);
                        Interlocked.Increment(ref _cacheHits);
                        Interlocked.Increment(ref _apiCallsSaved);
                    }
                    else
                    {
                        results[coord] = ColorSampler.GetColorAt(hwnd, coord.X, coord.Y);
                        Interlocked.Increment(ref _cacheMisses);
                    }
                }
                
                cachedRegion.HitCount++;
                return results;
            }
            else
            {
                // Remove expired region
                _regionCache.TryRemove(regionKey, out var expired);
                expired?.Dispose();
            }
        }
        
        // Capture region and cache it
        var bitmap = CaptureWindowRegion(hwnd, bounds);
        if (bitmap != null)
        {
            var newRegion = new CachedRegion
            {
                Bitmap = bitmap,
                Bounds = bounds,
                Timestamp = DateTime.Now,
                ValidDuration = duration,
                HitCount = 1,
                SourceWindow = hwnd
            };
            
            _regionCache.AddOrUpdate(regionKey, newRegion, (key, existing) =>
            {
                existing?.Dispose();
                return newRegion;
            });
            
            // Sample from the captured region
            foreach (var coord in coordinates)
            {
                var localX = coord.X - bounds.X;
                var localY = coord.Y - bounds.Y;
                
                if (localX >= 0 && localY >= 0 && localX < bitmap.Width && localY < bitmap.Height)
                {
                    results[coord] = bitmap.GetPixel(localX, localY);
                }
                else
                {
                    results[coord] = Color.Black; // Fallback
                }
            }
        }
        else
        {
            // Fallback to individual sampling
            foreach (var coord in coordinates)
            {
                results[coord] = ColorSampler.GetColorAt(hwnd, coord.X, coord.Y);
            }
        }
        
        return results;
    }

    /// <summary>
    /// Invalidates cache for a specific window (e.g., when window focus changes)
    /// </summary>
    /// <param name="hwnd">Window handle to invalidate</param>
    public void InvalidateWindow(IntPtr hwnd)
    {
        var toRemove = new List<string>();
        
        // Invalidate color cache
        foreach (var kvp in _colorCache)
        {
            if (kvp.Key.StartsWith($"{hwnd}_"))
            {
                toRemove.Add(kvp.Key);
            }
        }
        
        foreach (var key in toRemove)
        {
            _colorCache.TryRemove(key, out _);
        }
        
        // Invalidate region cache
        toRemove.Clear();
        foreach (var kvp in _regionCache)
        {
            if (kvp.Key.StartsWith($"{hwnd}_"))
            {
                toRemove.Add(kvp.Key);
            }
        }
        
        foreach (var key in toRemove)
        {
            if (_regionCache.TryRemove(key, out var region))
            {
                region?.Dispose();
            }
        }
        
        Debug.WriteLine($"[ColorSamplingCache] Invalidated cache for window {hwnd}");
    }

    /// <summary>
    /// Gets current performance statistics
    /// </summary>
    public CacheStats GetPerformanceStats()
    {
        long memoryUsage = 0;
        foreach (var region in _regionCache.Values)
        {
            if (region.Bitmap != null)
            {
                memoryUsage += region.Bitmap.Width * region.Bitmap.Height * 4; // RGBA
            }
        }
        memoryUsage += _colorCache.Count * 32; // Rough estimate for color cache entries
        
        return new CacheStats
        {
            TotalRequests = _totalRequests,
            CacheHits = _cacheHits,
            CacheMisses = _cacheMisses,
            ApiCallsSaved = _apiCallsSaved,
            ColorCacheSize = _colorCache.Count,
            RegionCacheSize = _regionCache.Count,
            MemoryUsageBytes = memoryUsage
        };
    }

    #region Private Methods
    
    private TimeSpan GetSmartCacheDuration(CachePriority priority)
    {
        return priority switch
        {
            CachePriority.Low => TimeSpan.FromMilliseconds(15),        // 15ms for low priority
            CachePriority.Normal => DefaultColorCacheDuration,         // 25ms default
            CachePriority.High => TimeSpan.FromMilliseconds(40),       // 40ms for HP/MP critical
            CachePriority.VeryHigh => TimeSpan.FromMilliseconds(60),   // 60ms for static UI elements
            _ => DefaultColorCacheDuration
        };
    }
    
    private string CreatePixelCacheKey(IntPtr hwnd, int x, int y)
    {
        // Group nearby pixels to reduce cache fragmentation
        var groupedX = (x / NearbyPixelThreshold) * NearbyPixelThreshold;
        var groupedY = (y / NearbyPixelThreshold) * NearbyPixelThreshold;
        return $"{hwnd}_{groupedX}_{groupedY}";
    }
    
    private string CreateRegionCacheKey(IntPtr hwnd, Rectangle bounds)
    {
        return $"{hwnd}_{bounds.X}_{bounds.Y}_{bounds.Width}_{bounds.Height}";
    }
    
    private Color SampleColorEfficiently(IntPtr hwnd, int x, int y)
    {
        // For single pixel sampling, use the static ColorSampler which handles MuMu offsets
        return ColorSampler.GetColorAt(hwnd, x, y);
    }
    
    private Bitmap? CaptureWindowRegion(IntPtr hwnd, Rectangle bounds)
    {
        try
        {
            var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                var hdcDest = graphics.GetHdc();
                try
                {
                    var hdcSrc = GetDC(hwnd);
                    if (hdcSrc != IntPtr.Zero)
                    {
                        BitBlt(hdcDest, 0, 0, bounds.Width, bounds.Height,
                               hdcSrc, bounds.X, bounds.Y, SRCCOPY);
                        ReleaseDC(hwnd, hdcSrc);
                        return bitmap;
                    }
                }
                finally
                {
                    graphics.ReleaseHdc(hdcDest);
                }
            }
            
            bitmap.Dispose();
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ColorSamplingCache] Region capture failed: {ex.Message}");
            return null;
        }
    }
    
    private void CleanupExpiredEntries(object? state)
    {
        if (_disposed) return;
        
        try
        {
            var removedColors = 0;
            var removedRegions = 0;
            
            // Cleanup color cache
            var expiredColors = new List<string>();
            foreach (var kvp in _colorCache)
            {
                if (kvp.Value.IsExpired)
                {
                    expiredColors.Add(kvp.Key);
                }
            }
            
            foreach (var key in expiredColors)
            {
                if (_colorCache.TryRemove(key, out _))
                {
                    removedColors++;
                }
            }
            
            // Cleanup region cache
            var expiredRegions = new List<string>();
            foreach (var kvp in _regionCache)
            {
                if (kvp.Value.IsExpired)
                {
                    expiredRegions.Add(kvp.Key);
                }
            }
            
            foreach (var key in expiredRegions)
            {
                if (_regionCache.TryRemove(key, out var region))
                {
                    region?.Dispose();
                    removedRegions++;
                }
            }
            
            // Enforce cache size limits
            if (_colorCache.Count > MaxCacheSize)
            {
                var excess = _colorCache.Count - MaxCacheSize;
                var oldestEntries = _colorCache
                    .OrderBy(kvp => kvp.Value.Timestamp)
                    .Take(excess)
                    .Select(kvp => kvp.Key)
                    .ToList();
                    
                foreach (var key in oldestEntries)
                {
                    _colorCache.TryRemove(key, out _);
                    removedColors++;
                }
            }
            
            if (removedColors > 0 || removedRegions > 0)
            {
                Debug.WriteLine($"[ColorSamplingCache] Cleanup: {removedColors} colors, {removedRegions} regions removed");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ColorSamplingCache] Cleanup error: {ex.Message}");
        }
    }
    
    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        _cleanupTimer?.Dispose();
        
        // Dispose all cached regions
        foreach (var region in _regionCache.Values)
        {
            region?.Dispose();
        }
        
        _colorCache.Clear();
        _regionCache.Clear();
        
        Debug.WriteLine($"[ColorSamplingCache] Disposed - Final stats: {_cacheHits} hits, {_cacheMisses} misses, {_apiCallsSaved} API calls saved");
    }
}

/// <summary>
/// Cache priority levels affecting cache duration
/// </summary>
public enum CachePriority
{
    Low,        // 15ms - Fast-changing content
    Normal,     // 25ms - Default monitoring
    High,       // 40ms - HP/MP critical monitoring  
    VeryHigh    // 60ms - Static UI elements
}