using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Vanara.PInvoke;
using System.Diagnostics;

namespace PixelAutomation.Tool.Overlay.WPF.Services;

/// <summary>
/// Optimized FastColorSampler that integrates with ColorSamplingCache for maximum performance.
/// Replaces the original FastColorSampler with intelligent caching and batch operations.
/// </summary>
public class OptimizedFastColorSampler : IDisposable
{
    private readonly ColorSamplingCache _cache;
    private readonly FastColorSampler? _fallbackSampler;
    private bool _disposed = false;
    
    // Performance tracking
    private long _batchOperations = 0;
    private long _individualOperations = 0;
    private readonly Stopwatch _performanceTimer = new();

    public OptimizedFastColorSampler()
    {
        _cache = new ColorSamplingCache
        {
            DefaultColorCacheDuration = TimeSpan.FromMilliseconds(25), // 25ms cache for responsive monitoring
            DefaultRegionCacheDuration = TimeSpan.FromMilliseconds(50), // 50ms region cache
            MaxCacheSize = 15000, // Increased for multiple clients
            NearbyPixelThreshold = 3 // Tighter grouping for accuracy
        };
        
        // Keep original as fallback
        _fallbackSampler = new FastColorSampler();
        
        Debug.WriteLine("[OptimizedFastColorSampler] Initialized with intelligent caching");
    }

    /// <summary>
    /// Captures window - now uses intelligent caching instead of always capturing
    /// </summary>
    /// <param name="hwnd">Window handle</param>
    public void CaptureWindow(IntPtr hwnd)
    {
        // With caching, we don't need to explicitly capture - it's done on-demand
        // This method is kept for compatibility but is now a no-op
        _performanceTimer.Restart();
    }

    /// <summary>
    /// Gets color at specific coordinates using intelligent caching
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <param name="priority">Cache priority for this sample</param>
    /// <returns>Color at the specified location</returns>
    public Color GetColorAt(int x, int y, CachePriority priority = CachePriority.Normal)
    {
        if (_disposed) return Color.Black;
        
        Interlocked.Increment(ref _individualOperations);
        
        // Use cached sampling with priority-based cache duration
        var cacheDuration = GetCacheDurationForPriority(priority);
        return _cache.GetCachedColor(GetCurrentWindowHandle(), x, y, cacheDuration, priority);
    }

    /// <summary>
    /// Batch operation to get multiple colors efficiently
    /// </summary>
    /// <param name="points">Array of points to sample</param>
    /// <param name="priority">Cache priority for these samples</param>
    /// <returns>Dictionary mapping points to colors</returns>
    public Dictionary<Point, Color> GetMultipleColors(Point[] points, CachePriority priority = CachePriority.Normal)
    {
        if (_disposed || points.Length == 0) 
            return new Dictionary<Point, Color>();
        
        Interlocked.Increment(ref _batchOperations);
        
        var cacheDuration = GetCacheDurationForPriority(priority);
        return _cache.BatchSampleColors(GetCurrentWindowHandle(), points, cacheDuration);
    }

    /// <summary>
    /// Optimized method for HP/MP monitoring that uses high-priority caching
    /// </summary>
    /// <param name="hwnd">Window handle</param>
    /// <param name="hpPoint">HP monitoring point</param>
    /// <param name="mpPoint">MP monitoring point</param>
    /// <returns>Tuple of (HP Color, MP Color)</returns>
    public (Color hpColor, Color mpColor) GetHpMpColors(IntPtr hwnd, Point hpPoint, Point mpPoint)
    {
        if (_disposed) return (Color.Black, Color.Black);
        
        var points = new[] { hpPoint, mpPoint };
        var colors = _cache.BatchSampleColors(hwnd, points, TimeSpan.FromMilliseconds(40));
        
        return (
            colors.GetValueOrDefault(hpPoint, Color.Black),
            colors.GetValueOrDefault(mpPoint, Color.Black)
        );
    }

    /// <summary>
    /// Optimized method for party heal system that batches all member HP checks
    /// </summary>
    /// <param name="hwnd">Window handle</param>
    /// <param name="memberPoints">Array of member HP points</param>
    /// <returns>Array of HP colors for each member</returns>
    public Color[] GetPartyHpColors(IntPtr hwnd, Point[] memberPoints)
    {
        if (_disposed || memberPoints.Length == 0) 
            return Array.Empty<Color>();
        
        var colors = _cache.BatchSampleColors(hwnd, memberPoints, TimeSpan.FromMilliseconds(50));
        
        return memberPoints.Select(point => colors.GetValueOrDefault(point, Color.Black)).ToArray();
    }

    /// <summary>
    /// Optimized method for BabeBot system with lower frequency caching
    /// </summary>
    /// <param name="hwnd">Window handle</param>
    /// <param name="hpPoint">HP threshold point</param>
    /// <param name="mpPoint">MP threshold point</param>
    /// <returns>Tuple of (HP Color, MP Color)</returns>
    public (Color hpColor, Color mpColor) GetBabeBotColors(IntPtr hwnd, Point hpPoint, Point mpPoint)
    {
        if (_disposed) return (Color.Black, Color.Black);
        
        var points = new[] { hpPoint, mpPoint };
        var colors = _cache.BatchSampleColors(hwnd, points, TimeSpan.FromMilliseconds(60));
        
        return (
            colors.GetValueOrDefault(hpPoint, Color.Black),
            colors.GetValueOrDefault(mpPoint, Color.Black)
        );
    }

    /// <summary>
    /// Calibration method that efficiently samples HP bar at multiple percentages
    /// </summary>
    /// <param name="hwnd">Window handle</param>
    /// <param name="y">Y coordinate of HP bar</param>
    /// <param name="minX">Minimum X coordinate</param>
    /// <param name="maxX">Maximum X coordinate</param>
    /// <param name="percentages">Array of percentages to sample (5, 10, 15, ... 95)</param>
    /// <returns>Dictionary mapping percentages to colors</returns>
    public Dictionary<int, Color> CalibrateHpBarColors(IntPtr hwnd, int y, int minX, int maxX, int[] percentages)
    {
        if (_disposed || percentages.Length == 0) 
            return new Dictionary<int, Color>();
        
        // Calculate points for each percentage
        var points = new Point[percentages.Length];
        for (int i = 0; i < percentages.Length; i++)
        {
            var percentage = percentages[i];
            var x = minX + (int)((maxX - minX) * (percentage / 100.0));
            points[i] = new Point(x, y);
        }
        
        // Batch sample all points with longer cache duration (calibration doesn't change often)
        var colors = _cache.BatchSampleColors(hwnd, points, TimeSpan.FromMilliseconds(500));
        
        var result = new Dictionary<int, Color>();
        for (int i = 0; i < percentages.Length; i++)
        {
            result[percentages[i]] = colors.GetValueOrDefault(points[i], Color.Black);
        }
        
        return result;
    }

    /// <summary>
    /// Invalidates cache for the current window (useful when window focus changes)
    /// </summary>
    /// <param name="hwnd">Window handle to invalidate</param>
    public void InvalidateCache(IntPtr hwnd)
    {
        _cache?.InvalidateWindow(hwnd);
    }

    /// <summary>
    /// Gets comprehensive performance statistics
    /// </summary>
    /// <returns>Performance statistics including cache metrics</returns>
    public OptimizedPerformanceStats GetPerformanceStats()
    {
        var cacheStats = _cache?.GetPerformanceStats() ?? new ColorSamplingCache.CacheStats();
        
        return new OptimizedPerformanceStats
        {
            BatchOperations = _batchOperations,
            IndividualOperations = _individualOperations,
            CacheHitRate = cacheStats.HitRate,
            ApiCallsSaved = cacheStats.ApiCallsSaved,
            ApiReductionPercentage = cacheStats.ApiReductionPercentage,
            MemoryUsageMB = cacheStats.MemoryUsageBytes / (1024.0 * 1024.0),
            ColorCacheSize = cacheStats.ColorCacheSize,
            RegionCacheSize = cacheStats.RegionCacheSize
        };
    }

    #region Private Methods
    
    private IntPtr _currentWindowHandle = IntPtr.Zero;
    
    /// <summary>
    /// Gets the current window handle - this needs to be set by the client
    /// </summary>
    private IntPtr GetCurrentWindowHandle()
    {
        return _currentWindowHandle;
    }
    
    /// <summary>
    /// Sets the current window handle for caching operations
    /// </summary>
    /// <param name="hwnd">Window handle</param>
    public void SetCurrentWindow(IntPtr hwnd)
    {
        _currentWindowHandle = hwnd;
    }
    
    private TimeSpan GetCacheDurationForPriority(CachePriority priority)
    {
        return priority switch
        {
            CachePriority.Low => TimeSpan.FromMilliseconds(15),
            CachePriority.Normal => TimeSpan.FromMilliseconds(25),
            CachePriority.High => TimeSpan.FromMilliseconds(40),
            CachePriority.VeryHigh => TimeSpan.FromMilliseconds(100),
            _ => TimeSpan.FromMilliseconds(25)
        };
    }
    
    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        _cache?.Dispose();
        _fallbackSampler?.Dispose();
        
        var stats = GetPerformanceStats();
        Debug.WriteLine($"[OptimizedFastColorSampler] Disposed - Final stats:");
        Debug.WriteLine($"  Batch operations: {stats.BatchOperations}");
        Debug.WriteLine($"  Individual operations: {stats.IndividualOperations}");
        Debug.WriteLine($"  Cache hit rate: {stats.CacheHitRate:F1}%");
        Debug.WriteLine($"  API calls saved: {stats.ApiCallsSaved}");
        Debug.WriteLine($"  API reduction: {stats.ApiReductionPercentage:F1}%");
        Debug.WriteLine($"  Memory usage: {stats.MemoryUsageMB:F1} MB");
    }
}

/// <summary>
/// Performance statistics for the optimized color sampler
/// </summary>
public class OptimizedPerformanceStats
{
    public long BatchOperations { get; set; }
    public long IndividualOperations { get; set; }
    public double CacheHitRate { get; set; }
    public long ApiCallsSaved { get; set; }
    public double ApiReductionPercentage { get; set; }
    public double MemoryUsageMB { get; set; }
    public int ColorCacheSize { get; set; }
    public int RegionCacheSize { get; set; }
    
    public long TotalOperations => BatchOperations + IndividualOperations;
}