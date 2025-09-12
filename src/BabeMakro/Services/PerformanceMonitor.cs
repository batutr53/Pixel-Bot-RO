using System.Diagnostics;
using System.Collections.Concurrent;

namespace PixelAutomation.Tool.Overlay.WPF.Services;

/// <summary>
/// Performance monitoring system to track optimization effectiveness
/// </summary>
public class PerformanceMonitor
{
    private static readonly PerformanceMonitor _instance = new();
    public static PerformanceMonitor Instance => _instance;
    
    private readonly ConcurrentDictionary<string, PerformanceMetric> _metrics = new();
    private readonly ConcurrentDictionary<string, MovingAverage> _averages = new();
    private readonly Timer _reportTimer;
    private readonly DateTime _startTime = DateTime.Now;
    
    public PerformanceMonitor()
    {
        // Report metrics every 30 seconds
        _reportTimer = new Timer(ReportMetrics, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        
        // Initialize system metrics
        InitializeSystemMetrics();
    }
    
    private void InitializeSystemMetrics()
    {
        RegisterMetric("system.memory_mb", "System Memory Usage (MB)");
        RegisterMetric("system.gc_collections", "GC Collections");
        RegisterMetric("system.cpu_percent", "CPU Usage %");
        
        // Timer optimization metrics
        RegisterMetric("timer.consolidated_count", "Active Consolidated Timers");
        RegisterMetric("timer.reduction_ratio", "Timer Reduction Ratio");
        
        // Object pooling metrics
        RegisterMetric("pool.color_array_hits", "Color Array Pool Hits");
        RegisterMetric("pool.point_array_hits", "Point Array Pool Hits");
        RegisterMetric("pool.list_hits", "List Pool Hits");
        RegisterMetric("pool.allocation_reduction", "Allocation Reduction %");
        
        // Color sampling cache metrics
        RegisterMetric("cache.color_hit_rate", "Color Cache Hit Rate %");
        RegisterMetric("cache.api_calls_saved", "Win32 API Calls Saved");
        RegisterMetric("cache.memory_usage_kb", "Cache Memory Usage (KB)");
        
        // Image processing metrics
        RegisterMetric("image.parallel_speedup", "Parallel Processing Speedup");
        RegisterMetric("image.processing_time_ms", "Image Processing Time (ms)");
        RegisterMetric("image.throughput_fps", "Image Processing FPS");
        
        // Automation performance metrics
        RegisterMetric("automation.hp_checks_per_sec", "HP Checks/sec");
        RegisterMetric("automation.mp_checks_per_sec", "MP Checks/sec");
        RegisterMetric("automation.party_heal_latency_ms", "Party Heal Latency (ms)");
        RegisterMetric("automation.attack_latency_ms", "Attack Latency (ms)");
    }
    
    public void RegisterMetric(string key, string description)
    {
        _metrics.TryAdd(key, new PerformanceMetric(key, description));
        _averages.TryAdd(key, new MovingAverage(60)); // 60-sample moving average
    }
    
    public void RecordValue(string key, double value)
    {
        if (_metrics.TryGetValue(key, out var metric))
        {
            metric.RecordValue(value);
            
            if (_averages.TryGetValue(key, out var average))
            {
                average.AddValue(value);
            }
        }
    }
    
    public void IncrementCounter(string key, long increment = 1)
    {
        if (_metrics.TryGetValue(key, out var metric))
        {
            metric.IncrementCounter(increment);
        }
    }
    
    public void StartTimer(string key)
    {
        if (_metrics.TryGetValue(key, out var metric))
        {
            metric.StartTimer();
        }
    }
    
    public void StopTimer(string key)
    {
        if (_metrics.TryGetValue(key, out var metric))
        {
            metric.StopTimer();
        }
    }
    
    public PerformanceSnapshot GetSnapshot()
    {
        var snapshot = new PerformanceSnapshot
        {
            Timestamp = DateTime.Now,
            UptimeMinutes = (DateTime.Now - _startTime).TotalMinutes,
            Metrics = new Dictionary<string, MetricSnapshot>()
        };
        
        foreach (var kvp in _metrics)
        {
            var metric = kvp.Value;
            var average = _averages.GetValueOrDefault(kvp.Key);
            
            snapshot.Metrics[kvp.Key] = new MetricSnapshot
            {
                Key = kvp.Key,
                Description = metric.Description,
                CurrentValue = metric.CurrentValue,
                AverageValue = average?.Average ?? 0,
                MinValue = metric.MinValue,
                MaxValue = metric.MaxValue,
                TotalCount = metric.TotalCount,
                LastUpdated = metric.LastUpdated
            };
        }
        
        // Add system metrics
        UpdateSystemMetrics(snapshot);
        
        return snapshot;
    }
    
    private void UpdateSystemMetrics(PerformanceSnapshot snapshot)
    {
        var process = Process.GetCurrentProcess();
        
        // Memory usage
        var memoryMB = process.WorkingSet64 / 1024.0 / 1024.0;
        RecordValue("system.memory_mb", memoryMB);
        
        // GC collections
        var gcCollections = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
        RecordValue("system.gc_collections", gcCollections);
        
        // CPU usage (approximation)
        try
        {
            var cpuPercent = process.TotalProcessorTime.TotalMilliseconds / Environment.TickCount * 100.0;
            RecordValue("system.cpu_percent", Math.Min(cpuPercent, 100.0));
        }
        catch
        {
            // CPU calculation can fail, ignore
        }
    }
    
    private void ReportMetrics(object? state)
    {
        try
        {
            var snapshot = GetSnapshot();
            
            Console.WriteLine("\n🔥 PERFORMANCE METRICS REPORT 🔥");
            Console.WriteLine($"========================================");
            Console.WriteLine($"⏱️  Uptime: {snapshot.UptimeMinutes:F1} minutes");
            Console.WriteLine($"📊 Timestamp: {snapshot.Timestamp:HH:mm:ss}");
            Console.WriteLine();
            
            // Group metrics by category
            var categories = new Dictionary<string, List<MetricSnapshot>>();
            foreach (var metric in snapshot.Metrics.Values)
            {
                var category = metric.Key.Split('.')[0];
                if (!categories.ContainsKey(category))
                    categories[category] = new List<MetricSnapshot>();
                categories[category].Add(metric);
            }
            
            foreach (var category in categories.OrderBy(c => c.Key))
            {
                Console.WriteLine($"📈 {category.Key.ToUpper()} METRICS:");
                foreach (var metric in category.Value.OrderBy(m => m.Key))
                {
                    if (metric.TotalCount > 0)
                    {
                        var icon = GetMetricIcon(metric.Key);
                        Console.WriteLine($"   {icon} {metric.Description}:");
                        Console.WriteLine($"     Current: {metric.CurrentValue:F2} | Avg: {metric.AverageValue:F2} | Min: {metric.MinValue:F2} | Max: {metric.MaxValue:F2}");
                    }
                }
                Console.WriteLine();
            }
            
            // Show optimization effectiveness
            ShowOptimizationEffectiveness(snapshot);
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PerformanceMonitor] Error reporting metrics: {ex.Message}");
        }
    }
    
    private void ShowOptimizationEffectiveness(PerformanceSnapshot snapshot)
    {
        Console.WriteLine("🚀 OPTIMIZATION EFFECTIVENESS:");
        
        // Timer consolidation effectiveness
        if (snapshot.Metrics.TryGetValue("timer.reduction_ratio", out var timerReduction))
        {
            Console.WriteLine($"   ⚡ Timer Consolidation: {timerReduction.CurrentValue:F1}x reduction in timers");
        }
        
        // Object pooling effectiveness
        if (snapshot.Metrics.TryGetValue("pool.allocation_reduction", out var allocReduction))
        {
            Console.WriteLine($"   🎯 Object Pooling: {allocReduction.AverageValue:F1}% allocation reduction");
        }
        
        // Cache effectiveness
        if (snapshot.Metrics.TryGetValue("cache.color_hit_rate", out var hitRate))
        {
            Console.WriteLine($"   💾 Color Sampling Cache: {hitRate.AverageValue:F1}% hit rate");
        }
        
        // Image processing speedup
        if (snapshot.Metrics.TryGetValue("image.parallel_speedup", out var speedup))
        {
            Console.WriteLine($"   🏎️  Parallel Processing: {speedup.AverageValue:F1}x speedup");
        }
        
        Console.WriteLine();
    }
    
    private string GetMetricIcon(string key)
    {
        return key.Split('.')[0] switch
        {
            "system" => "🖥️",
            "timer" => "⏰",
            "pool" => "🎯",
            "cache" => "💾",
            "image" => "🖼️",
            "automation" => "🤖",
            _ => "📊"
        };
    }
    
    public void Dispose()
    {
        _reportTimer?.Dispose();
    }
}

public class PerformanceMetric
{
    public string Key { get; }
    public string Description { get; }
    public double CurrentValue { get; private set; }
    public double MinValue { get; private set; } = double.MaxValue;
    public double MaxValue { get; private set; } = double.MinValue;
    public long TotalCount { get; private set; }
    public DateTime LastUpdated { get; private set; }
    
    private readonly Stopwatch _timer = new();
    
    public PerformanceMetric(string key, string description)
    {
        Key = key;
        Description = description;
    }
    
    public void RecordValue(double value)
    {
        CurrentValue = value;
        MinValue = Math.Min(MinValue, value);
        MaxValue = Math.Max(MaxValue, value);
        TotalCount++;
        LastUpdated = DateTime.Now;
    }
    
    public void IncrementCounter(long increment = 1)
    {
        CurrentValue += increment;
        TotalCount++;
        LastUpdated = DateTime.Now;
    }
    
    public void StartTimer()
    {
        _timer.Restart();
    }
    
    public void StopTimer()
    {
        if (_timer.IsRunning)
        {
            _timer.Stop();
            RecordValue(_timer.ElapsedMilliseconds);
        }
    }
}

public class MovingAverage
{
    private readonly Queue<double> _values;
    private readonly int _windowSize;
    private double _sum;
    
    public MovingAverage(int windowSize)
    {
        _windowSize = windowSize;
        _values = new Queue<double>(windowSize);
    }
    
    public double Average => _values.Count > 0 ? _sum / _values.Count : 0;
    
    public void AddValue(double value)
    {
        _values.Enqueue(value);
        _sum += value;
        
        while (_values.Count > _windowSize)
        {
            _sum -= _values.Dequeue();
        }
    }
}

public class PerformanceSnapshot
{
    public DateTime Timestamp { get; set; }
    public double UptimeMinutes { get; set; }
    public Dictionary<string, MetricSnapshot> Metrics { get; set; } = new();
}

public class MetricSnapshot
{
    public string Key { get; set; } = "";
    public string Description { get; set; } = "";
    public double CurrentValue { get; set; }
    public double AverageValue { get; set; }
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public long TotalCount { get; set; }
    public DateTime LastUpdated { get; set; }
}