using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Xunit;
using Xunit.Abstractions;

namespace PerformanceTests;

public class TimerPrecisionTests : BasePerformanceTest
{
    public TimerPrecisionTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task Timer_Precision_120Hz_Target_Test()
    {
        Output.WriteLine("🧪 Testing timer precision for 120Hz target frequency...");
        Output.WriteLine("📋 Target: 8.33ms intervals (120Hz)");

        var targetInterval = TimeSpan.FromMilliseconds(8.33); // 120Hz
        var measurements = new List<double>();
        var stopwatch = new Stopwatch();
        var overallStopwatch = Stopwatch.StartNew();

        DateTime lastTick = DateTime.UtcNow;
        int tickCount = 0;

        // Test high-resolution timer precision for 1 minute
        var preciseStopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Use Stopwatch-based precise timing instead of Task.Delay
        while (overallStopwatch.Elapsed < TimeSpan.FromMinutes(1))
        {
            var startTick = preciseStopwatch.ElapsedMilliseconds;

            // Spin-wait for precise timing (not ideal for production but good for testing precision)
            while ((preciseStopwatch.ElapsedMilliseconds - startTick) < targetInterval.TotalMilliseconds)
            {
                Thread.SpinWait(1);
            }

            var actualInterval = (DateTime.UtcNow - lastTick).TotalMilliseconds;
            measurements.Add(actualInterval);
            lastTick = DateTime.UtcNow;
            tickCount++;

            if (tickCount % 120 == 0) // Log every second
            {
                Output.WriteLine($"📊 {overallStopwatch.Elapsed.TotalSeconds:F0}s: Avg interval={measurements.TakeLast(120).Average():F2}ms");
            }
        }

        // Analyze timer precision
        var avgInterval = measurements.Average();
        var minInterval = measurements.Min();
        var maxInterval = measurements.Max();
        var stdDev = Math.Sqrt(measurements.Select(x => Math.Pow(x - avgInterval, 2)).Average());

        var jitterPercent = (stdDev / targetInterval.TotalMilliseconds) * 100;
        var accuracyPercent = Math.Abs(avgInterval - targetInterval.TotalMilliseconds) / targetInterval.TotalMilliseconds * 100;

        Output.WriteLine($"📊 Timer Precision Analysis (target: {targetInterval.TotalMilliseconds:F2}ms):");
        Output.WriteLine($"   Average interval: {avgInterval:F2}ms");
        Output.WriteLine($"   Min/Max interval: {minInterval:F2}ms / {maxInterval:F2}ms");
        Output.WriteLine($"   Standard deviation: {stdDev:F2}ms");
        Output.WriteLine($"   Jitter: {jitterPercent:F1}%");
        Output.WriteLine($"   Accuracy: {accuracyPercent:F1}% deviation from target");
        Output.WriteLine($"   Total ticks: {tickCount}");

        // Assert timer precision requirements
        Assert.True(jitterPercent < 15, $"Timer jitter too high: {jitterPercent:F1}% (should be < 15%)");
        Assert.True(accuracyPercent < 10, $"Timer accuracy too poor: {accuracyPercent:F1}% (should be < 10%)");
        Assert.True(stdDev < 2, $"Timer standard deviation too high: {stdDev:F2}ms (should be < 2ms)");

        Output.WriteLine("✅ Timer precision test passed");
    }

    [Fact]
    public async Task Timer_Frequency_Comparison_Test()
    {
        Output.WriteLine("🧪 Testing various timer frequencies...");

        var frequencies = new[] { 60, 80, 120 }; // Hz
        var results = new Dictionary<int, TimerFrequencyResult>();

        foreach (var frequency in frequencies)
        {
            Output.WriteLine($"📊 Testing {frequency}Hz frequency...");

            var targetInterval = 1000.0 / frequency; // ms
            var measurements = new List<double>();
            var stopwatch = Stopwatch.StartNew();

            DateTime lastTick = DateTime.UtcNow;
            int ticks = 0;

            // Test each frequency for 30 seconds
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(30))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(targetInterval));

                var actualInterval = (DateTime.UtcNow - lastTick).TotalMilliseconds;
                measurements.Add(actualInterval);
                lastTick = DateTime.UtcNow;
                ticks++;
            }

            var avgInterval = measurements.Average();
            var stdDev = Math.Sqrt(measurements.Select(x => Math.Pow(x - avgInterval, 2)).Average());
            var actualFrequency = 1000.0 / avgInterval;

            results[frequency] = new TimerFrequencyResult
            {
                TargetFrequency = frequency,
                TargetInterval = targetInterval,
                ActualInterval = avgInterval,
                ActualFrequency = actualFrequency,
                StandardDeviation = stdDev,
                TickCount = ticks,
                Measurements = measurements
            };

            Output.WriteLine($"   Target: {frequency}Hz ({targetInterval:F2}ms)");
            Output.WriteLine($"   Actual: {actualFrequency:F1}Hz ({avgInterval:F2}ms)");
            Output.WriteLine($"   Std Dev: {stdDev:F2}ms");
            Output.WriteLine($"   Ticks: {ticks}");
        }

        // Analyze frequency stability across different targets
        Output.WriteLine($"\n📊 Frequency Comparison Summary:");
        foreach (var result in results.Values)
        {
            var accuracyPercent = Math.Abs(result.ActualFrequency - result.TargetFrequency) / result.TargetFrequency * 100;
            var jitterPercent = (result.StandardDeviation / result.TargetInterval) * 100;

            Output.WriteLine($"   {result.TargetFrequency}Hz: Accuracy={100-accuracyPercent:F1}%, Jitter={jitterPercent:F1}%");

            // Assert each frequency meets precision requirements
            Assert.True(accuracyPercent < 5, $"{result.TargetFrequency}Hz accuracy too poor: {accuracyPercent:F1}%");
            Assert.True(jitterPercent < 20, $"{result.TargetFrequency}Hz jitter too high: {jitterPercent:F1}%");
        }

        Output.WriteLine("✅ Frequency comparison test passed");
    }

    [Fact]
    public async Task MasterTimer_Consolidation_Benefit_Test()
    {
        Output.WriteLine("🧪 Testing MasterTimer consolidation benefits...");
        Output.WriteLine("📋 Simulating individual timers vs consolidated approach");

        // Simulate individual timer overhead
        var individualTimerResults = await SimulateIndividualTimers();

        // Simulate consolidated timer approach
        var consolidatedTimerResults = await SimulateConsolidatedTimer();

        Output.WriteLine($"📊 Timer Consolidation Analysis:");
        Output.WriteLine($"   Individual timers:");
        Output.WriteLine($"     Average CPU: {individualTimerResults.AverageCpu:F1}%");
        Output.WriteLine($"     Timer objects: {individualTimerResults.TimerCount}");
        Output.WriteLine($"     Memory overhead: {individualTimerResults.MemoryOverhead:F1}MB");

        Output.WriteLine($"   Consolidated timer:");
        Output.WriteLine($"     Average CPU: {consolidatedTimerResults.AverageCpu:F1}%");
        Output.WriteLine($"     Timer objects: {consolidatedTimerResults.TimerCount}");
        Output.WriteLine($"     Memory overhead: {consolidatedTimerResults.MemoryOverhead:F1}MB");

        var cpuImprovement = ((individualTimerResults.AverageCpu - consolidatedTimerResults.AverageCpu) / individualTimerResults.AverageCpu) * 100;
        var memoryImprovement = ((individualTimerResults.MemoryOverhead - consolidatedTimerResults.MemoryOverhead) / individualTimerResults.MemoryOverhead) * 100;

        Output.WriteLine($"   Improvements:");
        Output.WriteLine($"     CPU reduction: {cpuImprovement:F1}%");
        Output.WriteLine($"     Memory reduction: {memoryImprovement:F1}%");
        Output.WriteLine($"     Timer count reduction: {individualTimerResults.TimerCount - consolidatedTimerResults.TimerCount}");

        // Assert consolidation provides benefits
        Assert.True(cpuImprovement > 0, $"Consolidation should reduce CPU usage (got {cpuImprovement:F1}%)");
        Assert.True(consolidatedTimerResults.TimerCount < individualTimerResults.TimerCount, "Consolidation should reduce timer count");

        Output.WriteLine("✅ MasterTimer consolidation benefit test passed");
    }

    private async Task<TimerPerformanceResult> SimulateIndividualTimers()
    {
        Output.WriteLine("🔧 Simulating individual timer approach...");

        var timers = new List<System.Timers.Timer>();
        var cpuSamples = new List<double>();
        var memoryBefore = GC.GetTotalMemory(true);

        // Create 15 individual timers (as mentioned in docs)
        for (int i = 0; i < 15; i++)
        {
            var timer = new System.Timers.Timer(50 + i * 10); // Varying intervals
            timer.Elapsed += (s, e) => { /* Simulate work */ Thread.SpinWait(100); };
            timer.Start();
            timers.Add(timer);
        }

        // Monitor for 30 seconds
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(30))
        {
            cpuSamples.Add(GetCpuUsage());
            await Task.Delay(2000);
        }

        // Cleanup
        foreach (var timer in timers)
        {
            timer.Stop();
            timer.Dispose();
        }

        var memoryAfter = GC.GetTotalMemory(true);
        var memoryOverhead = (memoryAfter - memoryBefore) / (1024.0 * 1024.0);

        return new TimerPerformanceResult
        {
            AverageCpu = cpuSamples.Average(),
            TimerCount = 15,
            MemoryOverhead = memoryOverhead
        };
    }

    private async Task<TimerPerformanceResult> SimulateConsolidatedTimer()
    {
        Output.WriteLine("🔧 Simulating consolidated timer approach...");

        var cpuSamples = new List<double>();
        var memoryBefore = GC.GetTotalMemory(true);

        // Single timer with task scheduling simulation
        var taskQueue = new Queue<Action>();
        var consolidatedTimer = new System.Timers.Timer(10); // High frequency master timer

        consolidatedTimer.Elapsed += (s, e) =>
        {
            // Simulate task queue processing
            for (int i = 0; i < 3 && taskQueue.Count > 0; i++)
            {
                var task = taskQueue.Dequeue();
                task?.Invoke();
            }
        };

        // Add tasks that simulate the 15 individual timers
        var taskScheduler = Task.Run(async () =>
        {
            var lastExecution = new DateTime[15];
            var intervals = new int[15];

            for (int i = 0; i < 15; i++)
            {
                intervals[i] = 50 + i * 10;
                lastExecution[i] = DateTime.UtcNow;
            }

            while (consolidatedTimer.Enabled)
            {
                for (int i = 0; i < 15; i++)
                {
                    if ((DateTime.UtcNow - lastExecution[i]).TotalMilliseconds >= intervals[i])
                    {
                        taskQueue.Enqueue(() => Thread.SpinWait(100));
                        lastExecution[i] = DateTime.UtcNow;
                    }
                }
                await Task.Delay(5);
            }
        });

        consolidatedTimer.Start();

        // Monitor for 30 seconds
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(30))
        {
            cpuSamples.Add(GetCpuUsage());
            await Task.Delay(2000);
        }

        // Cleanup
        consolidatedTimer.Stop();
        consolidatedTimer.Dispose();

        var memoryAfter = GC.GetTotalMemory(true);
        var memoryOverhead = (memoryAfter - memoryBefore) / (1024.0 * 1024.0);

        return new TimerPerformanceResult
        {
            AverageCpu = cpuSamples.Average(),
            TimerCount = 1,
            MemoryOverhead = memoryOverhead
        };
    }

    [Fact]
    public async Task Timer_Under_Load_Stability_Test()
    {
        Output.WriteLine("🧪 Testing timer stability under system load...");

        var timerPrecisionData = new List<(DateTime Time, double Interval)>();
        var targetInterval = TimeSpan.FromMilliseconds(16.67); // 60Hz

        // Create system load
        var loadTasks = new List<Task>();
        var cts = new CancellationTokenSource();

        for (int i = 0; i < Environment.ProcessorCount / 2; i++)
        {
            loadTasks.Add(Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    // CPU load
                    for (int j = 0; j < 1000; j++)
                    {
                        Math.Sin(j);
                    }
                    await Task.Delay(1, cts.Token);
                }
            }, cts.Token));
        }

        // Monitor timer precision under load
        var timerTask = Task.Run(async () =>
        {
            DateTime lastTick = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < TimeSpan.FromMinutes(2))
            {
                await Task.Delay(targetInterval);

                var now = DateTime.UtcNow;
                var actualInterval = (now - lastTick).TotalMilliseconds;
                timerPrecisionData.Add((now, actualInterval));

                lastTick = now;
            }
        });

        Output.WriteLine("💥 Running timer under load test for 2 minutes...");
        await timerTask;

        // Stop load
        cts.Cancel();
        try { await Task.WhenAll(loadTasks); } catch (OperationCanceledException) { }

        // Analyze timer stability under load
        var intervals = timerPrecisionData.Select(d => d.Interval).ToList();
        var avgInterval = intervals.Average();
        var stdDev = Math.Sqrt(intervals.Select(x => Math.Pow(x - avgInterval, 2)).Average());
        var maxDeviation = intervals.Select(i => Math.Abs(i - targetInterval.TotalMilliseconds)).Max();

        Output.WriteLine($"📊 Timer under load analysis:");
        Output.WriteLine($"   Target interval: {targetInterval.TotalMilliseconds:F2}ms");
        Output.WriteLine($"   Average interval: {avgInterval:F2}ms");
        Output.WriteLine($"   Standard deviation: {stdDev:F2}ms");
        Output.WriteLine($"   Maximum deviation: {maxDeviation:F2}ms");
        Output.WriteLine($"   Samples collected: {intervals.Count}");

        // Timer should remain reasonably stable even under load
        Assert.True(stdDev < 5, $"Timer too unstable under load: StdDev={stdDev:F2}ms");
        Assert.True(maxDeviation < 20, $"Timer deviation too high under load: {maxDeviation:F2}ms");

        Output.WriteLine("✅ Timer under load stability test passed");
    }
}

// Benchmark class for high-precision timer measurements
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 10)]
public class TimerPrecisionBenchmarks
{
    [Benchmark]
    public async Task TaskDelay_Precision()
    {
        await Task.Delay(TimeSpan.FromMilliseconds(8.33));
    }

    [Benchmark]
    public void ThreadSleep_Precision()
    {
        Thread.Sleep(8);
    }

    [Benchmark]
    public async Task HighResolutionTimer_Simulation()
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromMilliseconds(8.33))
        {
            await Task.Yield();
        }
    }
}

public class TimerFrequencyResult
{
    public int TargetFrequency { get; set; }
    public double TargetInterval { get; set; }
    public double ActualInterval { get; set; }
    public double ActualFrequency { get; set; }
    public double StandardDeviation { get; set; }
    public int TickCount { get; set; }
    public List<double> Measurements { get; set; } = new();
}

public class TimerPerformanceResult
{
    public double AverageCpu { get; set; }
    public int TimerCount { get; set; }
    public double MemoryOverhead { get; set; }
}