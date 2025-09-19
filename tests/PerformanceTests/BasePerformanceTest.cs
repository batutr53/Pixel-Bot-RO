using System.Diagnostics;
using System.Management;
using Xunit.Abstractions;

namespace PerformanceTests;

public abstract class BasePerformanceTest : IDisposable
{
    protected readonly ITestOutputHelper Output;
    protected readonly PerformanceCounter CpuCounter;
    protected readonly Process? BabeMakroProcess;

    protected BasePerformanceTest(ITestOutputHelper output)
    {
        Output = output;
        CpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");

        // Try to find running BabeMakro process
        var processes = Process.GetProcessesByName("BabeMakro");
        BabeMakroProcess = processes.FirstOrDefault();

        if (BabeMakroProcess == null)
        {
            Output.WriteLine("⚠️ BabeMakro process not found. Some tests may be skipped.");
        }
        else
        {
            Output.WriteLine($"✅ Found BabeMakro process: PID {BabeMakroProcess.Id}");
        }
    }

    /// <summary>
    /// Gets current memory usage of BabeMakro process in MB
    /// </summary>
    protected long GetMemoryUsageMB()
    {
        if (BabeMakroProcess == null) return 0;

        BabeMakroProcess.Refresh();
        return BabeMakroProcess.WorkingSet64 / (1024 * 1024);
    }

    /// <summary>
    /// Gets current CPU usage percentage
    /// </summary>
    protected double GetCpuUsage()
    {
        return CpuCounter.NextValue();
    }

    /// <summary>
    /// Gets handle count for BabeMakro process
    /// </summary>
    protected int GetHandleCount()
    {
        if (BabeMakroProcess == null) return 0;

        BabeMakroProcess.Refresh();
        return BabeMakroProcess.HandleCount;
    }

    /// <summary>
    /// Gets thread count for BabeMakro process
    /// </summary>
    protected int GetThreadCount()
    {
        if (BabeMakroProcess == null) return 0;

        BabeMakroProcess.Refresh();
        return BabeMakroProcess.Threads.Count;
    }

    /// <summary>
    /// Gets GC memory info
    /// </summary>
    protected GCMemoryInfo GetGCInfo()
    {
        return GC.GetGCMemoryInfo();
    }

    /// <summary>
    /// Logs current system stats
    /// </summary>
    protected void LogCurrentStats(string context = "")
    {
        var memoryMB = GetMemoryUsageMB();
        var cpu = GetCpuUsage();
        var handles = GetHandleCount();
        var threads = GetThreadCount();
        var gcInfo = GetGCInfo();

        Output.WriteLine($"📊 [{context}] Memory: {memoryMB}MB | CPU: {cpu:F1}% | Handles: {handles} | Threads: {threads} | GC: Gen0={gcInfo.GenerationInfo[0].SizeAfterBytes/1024}KB");
    }

    /// <summary>
    /// Waits and monitors for specified duration
    /// </summary>
    protected async Task MonitorForDuration(TimeSpan duration, TimeSpan sampleInterval, string testName = "")
    {
        var stopwatch = Stopwatch.StartNew();
        var samples = new List<PerformanceSample>();

        Output.WriteLine($"🔍 Starting {testName} monitoring for {duration.TotalMinutes:F1} minutes...");

        while (stopwatch.Elapsed < duration)
        {
            var sample = new PerformanceSample
            {
                Timestamp = DateTime.UtcNow,
                MemoryMB = GetMemoryUsageMB(),
                CpuPercent = GetCpuUsage(),
                HandleCount = GetHandleCount(),
                ThreadCount = GetThreadCount()
            };

            samples.Add(sample);

            if (samples.Count % 10 == 0) // Log every 10th sample
            {
                LogCurrentStats($"{testName} - {stopwatch.Elapsed.TotalMinutes:F1}min");
            }

            await Task.Delay(sampleInterval);
        }

        // Generate report
        GenerateReport(samples, testName);
    }

    private void GenerateReport(List<PerformanceSample> samples, string testName)
    {
        if (!samples.Any()) return;

        var avgMemory = samples.Average(s => s.MemoryMB);
        var maxMemory = samples.Max(s => s.MemoryMB);
        var minMemory = samples.Min(s => s.MemoryMB);

        var avgCpu = samples.Average(s => s.CpuPercent);
        var maxCpu = samples.Max(s => s.CpuPercent);

        var avgHandles = samples.Average(s => s.HandleCount);
        var maxHandles = samples.Max(s => s.HandleCount);

        Output.WriteLine($"\n📈 {testName} Performance Report:");
        Output.WriteLine($"   Memory: Avg={avgMemory:F1}MB, Max={maxMemory}MB, Min={minMemory}MB");
        Output.WriteLine($"   CPU: Avg={avgCpu:F1}%, Max={maxCpu:F1}%");
        Output.WriteLine($"   Handles: Avg={avgHandles:F0}, Max={maxHandles}");
        Output.WriteLine($"   Samples: {samples.Count} over {(samples.Last().Timestamp - samples.First().Timestamp).TotalMinutes:F1} minutes");

        // Check for memory leaks (10% growth threshold)
        var memoryGrowth = (maxMemory - minMemory) / minMemory * 100;
        if (memoryGrowth > 10)
        {
            Output.WriteLine($"⚠️ Potential memory leak detected: {memoryGrowth:F1}% growth");
        }
        else
        {
            Output.WriteLine($"✅ Memory usage stable: {memoryGrowth:F1}% variation");
        }
    }

    public virtual void Dispose()
    {
        CpuCounter?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class PerformanceSample
{
    public DateTime Timestamp { get; set; }
    public long MemoryMB { get; set; }
    public double CpuPercent { get; set; }
    public int HandleCount { get; set; }
    public int ThreadCount { get; set; }
}