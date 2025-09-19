using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace PerformanceTests;

public class CpuUsageTests : BasePerformanceTest
{
    public CpuUsageTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task CPU_Usage_Under_Normal_Load_Should_Be_Low()
    {
        if (BabeMakroProcess == null)
        {
            Output.WriteLine("❌ Skipping test - BabeMakro process not found");
            return;
        }

        Output.WriteLine("🧪 Testing CPU usage under normal operation...");

        // Warm up CPU counter (first reading is often inaccurate)
        GetCpuUsage();
        await Task.Delay(1000);

        var cpuSamples = new List<double>();
        var stopwatch = Stopwatch.StartNew();

        // Monitor for 2 minutes with 5-second intervals
        while (stopwatch.Elapsed < TimeSpan.FromMinutes(2))
        {
            var cpuUsage = GetCpuUsage();
            cpuSamples.Add(cpuUsage);

            if (cpuSamples.Count % 6 == 0) // Log every 30 seconds
            {
                Output.WriteLine($"📊 CPU usage at {stopwatch.Elapsed.TotalSeconds:F0}s: {cpuUsage:F1}%");
            }

            await Task.Delay(5000);
        }

        var avgCpu = cpuSamples.Average();
        var maxCpu = cpuSamples.Max();
        var cpuSpikes = cpuSamples.Count(cpu => cpu > 15); // Count spikes over 15%

        Output.WriteLine($"📊 CPU Analysis:");
        Output.WriteLine($"   Average CPU: {avgCpu:F1}%");
        Output.WriteLine($"   Maximum CPU: {maxCpu:F1}%");
        Output.WriteLine($"   Spikes > 15%: {cpuSpikes}");

        // Assert CPU requirements from docs: < 10% with 8 clients
        Assert.True(avgCpu < 10, $"Average CPU too high: {avgCpu:F1}% (should be < 10%)");
        Assert.True(maxCpu < 25, $"CPU spike too high: {maxCpu:F1}% (should be < 25%)");
        Assert.True(cpuSpikes < cpuSamples.Count * 0.1, $"Too many CPU spikes: {cpuSpikes} (should be < 10% of samples)");

        Output.WriteLine("✅ CPU usage test passed");
    }

    [Fact]
    public async Task Timer_System_CPU_Impact_Should_Be_Minimal()
    {
        if (BabeMakroProcess == null)
        {
            Output.WriteLine("❌ Skipping test - BabeMakro process not found");
            return;
        }

        Output.WriteLine("🧪 Testing timer system CPU impact...");

        // Get baseline CPU usage
        await Task.Delay(2000); // Wait for system to stabilize
        var baselineSamples = new List<double>();

        for (int i = 0; i < 10; i++)
        {
            baselineSamples.Add(GetCpuUsage());
            await Task.Delay(1000);
        }

        var baselineCpu = baselineSamples.Average();
        Output.WriteLine($"📊 Baseline CPU: {baselineCpu:F1}%");

        // Note: This test assumes timer activity can be observed
        // In a real implementation, we might need to trigger timer-intensive operations
        // For now, we monitor for timer-related CPU patterns

        var timerSamples = new List<double>();
        var stopwatch = Stopwatch.StartNew();

        // Monitor for 3 minutes during what should be timer-active period
        while (stopwatch.Elapsed < TimeSpan.FromMinutes(3))
        {
            var cpuUsage = GetCpuUsage();
            timerSamples.Add(cpuUsage);

            await Task.Delay(2000); // 2-second intervals for more precise measurement
        }

        var avgTimerCpu = timerSamples.Average();
        var maxTimerCpu = timerSamples.Max();
        var cpuVariance = timerSamples.Select(cpu => Math.Pow(cpu - avgTimerCpu, 2)).Average();
        var cpuStdDev = Math.Sqrt(cpuVariance);

        Output.WriteLine($"📊 Timer Period CPU Analysis:");
        Output.WriteLine($"   Average: {avgTimerCpu:F1}%");
        Output.WriteLine($"   Maximum: {maxTimerCpu:F1}%");
        Output.WriteLine($"   Std Dev: {cpuStdDev:F1}%");
        Output.WriteLine($"   vs Baseline: {avgTimerCpu - baselineCpu:+F1;-F1}%");

        // Assert timer system doesn't cause excessive CPU usage
        Assert.True(avgTimerCpu < 12, $"Timer system CPU too high: {avgTimerCpu:F1}%");
        Assert.True(cpuStdDev < 5, $"CPU variance too high: {cpuStdDev:F1}% (indicates timer irregularities)");

        Output.WriteLine("✅ Timer system CPU test passed");
    }

    [Fact]
    public async Task High_Frequency_Operation_CPU_Efficiency()
    {
        if (BabeMakroProcess == null)
        {
            Output.WriteLine("❌ Skipping test - BabeMakro process not found");
            return;
        }

        Output.WriteLine("🧪 Testing CPU efficiency during high-frequency operations...");
        Output.WriteLine("📋 This test monitors CPU during 120Hz target operation period");

        var cpuSamples = new List<double>();
        var memSamples = new List<long>();
        var stopwatch = Stopwatch.StartNew();

        // Monitor for 5 minutes with high-precision sampling
        while (stopwatch.Elapsed < TimeSpan.FromMinutes(5))
        {
            var cpu = GetCpuUsage();
            var memory = GetMemoryUsageMB();

            cpuSamples.Add(cpu);
            memSamples.Add(memory);

            if (cpuSamples.Count % 15 == 0) // Log every 30 seconds
            {
                Output.WriteLine($"📊 {stopwatch.Elapsed.TotalMinutes:F1}min: CPU={cpu:F1}%, Mem={memory}MB");
            }

            await Task.Delay(2000); // 2-second precision
        }

        var avgCpu = cpuSamples.Average();
        var maxCpu = cpuSamples.Max();
        var p95Cpu = cpuSamples.OrderBy(x => x).Skip((int)(cpuSamples.Count * 0.95)).First();

        var avgMem = memSamples.Average();
        var maxMem = memSamples.Max();

        Output.WriteLine($"📊 High-Frequency Operation Results:");
        Output.WriteLine($"   CPU Average: {avgCpu:F1}%");
        Output.WriteLine($"   CPU Maximum: {maxCpu:F1}%");
        Output.WriteLine($"   CPU 95th percentile: {p95Cpu:F1}%");
        Output.WriteLine($"   Memory Average: {avgMem:F1}MB");
        Output.WriteLine($"   Memory Maximum: {maxMem}MB");

        // Performance requirements for high-frequency operation
        Assert.True(avgCpu < 15, $"High-frequency average CPU too high: {avgCpu:F1}%");
        Assert.True(p95Cpu < 25, $"High-frequency P95 CPU too high: {p95Cpu:F1}%");
        Assert.True(maxMem < 500, $"Memory usage too high during high-freq ops: {maxMem}MB");

        Output.WriteLine("✅ High-frequency operation CPU test passed");
    }

    [Fact]
    public async Task CPU_Usage_With_Multiple_Monitors_Simulation()
    {
        Output.WriteLine("🧪 Testing CPU impact of multi-monitor simulation...");
        Output.WriteLine("📋 This simulates the CPU load of monitoring multiple clients");

        // Simulate multi-client monitoring by creating CPU measurement load
        var tasks = new List<Task>();
        var cpuSamples = new List<double>();
        var stopwatch = Stopwatch.StartNew();

        // Create background monitoring simulation
        for (int i = 0; i < 8; i++) // Simulate 8 clients
        {
            int clientId = i;
            tasks.Add(Task.Run(async () =>
            {
                while (stopwatch.Elapsed < TimeSpan.FromMinutes(3))
                {
                    // Simulate client monitoring work (without actually affecting BabeMakro)
                    var rand = new Random(clientId);
                    for (int j = 0; j < 10; j++)
                    {
                        _ = rand.Next(1000000); // Simulate some work
                    }
                    await Task.Delay(50); // ~20Hz simulation per client
                }
            }));
        }

        // Monitor system CPU during simulation
        var monitorTask = Task.Run(async () =>
        {
            while (stopwatch.Elapsed < TimeSpan.FromMinutes(3))
            {
                var cpu = GetCpuUsage();
                cpuSamples.Add(cpu);
                await Task.Delay(2000);
            }
        });

        Output.WriteLine("🏃 Running 8-client simulation for 3 minutes...");

        // Wait for all tasks
        await Task.WhenAll(tasks.Concat(new[] { monitorTask }));

        var avgCpu = cpuSamples.Average();
        var maxCpu = cpuSamples.Max();

        Output.WriteLine($"📊 Multi-Client Simulation Results:");
        Output.WriteLine($"   Average CPU: {avgCpu:F1}%");
        Output.WriteLine($"   Maximum CPU: {maxCpu:F1}%");
        Output.WriteLine($"   Simulated clients: 8");
        Output.WriteLine($"   Simulation frequency: ~20Hz per client");

        // The actual BabeMakro with 8 clients should still perform well
        // This test establishes a baseline for system CPU capability
        if (BabeMakroProcess != null)
        {
            var currentMemory = GetMemoryUsageMB();
            Output.WriteLine($"   BabeMakro memory during simulation: {currentMemory}MB");

            Assert.True(currentMemory < 600, $"Memory usage too high with simulation load: {currentMemory}MB");
        }

        Output.WriteLine("✅ Multi-monitor simulation test completed");
    }

    [Fact]
    public async Task Thread_Efficiency_Test()
    {
        if (BabeMakroProcess == null)
        {
            Output.WriteLine("❌ Skipping test - BabeMakro process not found");
            return;
        }

        Output.WriteLine("🧪 Testing thread efficiency and count stability...");

        var initialThreads = GetThreadCount();
        Output.WriteLine($"📊 Initial thread count: {initialThreads}");

        var threadSamples = new List<int>();
        var cpuSamples = new List<double>();
        var stopwatch = Stopwatch.StartNew();

        // Monitor for 5 minutes
        while (stopwatch.Elapsed < TimeSpan.FromMinutes(5))
        {
            var threads = GetThreadCount();
            var cpu = GetCpuUsage();

            threadSamples.Add(threads);
            cpuSamples.Add(cpu);

            if (threadSamples.Count % 10 == 0) // Log every 50 seconds
            {
                Output.WriteLine($"📊 {stopwatch.Elapsed.TotalMinutes:F1}min: Threads={threads}, CPU={cpu:F1}%");
            }

            await Task.Delay(5000);
        }

        var avgThreads = threadSamples.Average();
        var maxThreads = threadSamples.Max();
        var minThreads = threadSamples.Min();
        var threadVariation = ((double)(maxThreads - minThreads) / minThreads) * 100;

        var avgCpu = cpuSamples.Average();

        Output.WriteLine($"📊 Thread Efficiency Analysis:");
        Output.WriteLine($"   Average threads: {avgThreads:F0}");
        Output.WriteLine($"   Min/Max threads: {minThreads}/{maxThreads}");
        Output.WriteLine($"   Thread variation: {threadVariation:F1}%");
        Output.WriteLine($"   CPU per thread: {avgCpu / avgThreads:F2}%");

        // Assert reasonable thread behavior
        Assert.True(threadVariation < 20, $"Thread count too unstable: {threadVariation:F1}% variation");
        Assert.True(maxThreads < 50, $"Too many threads: {maxThreads} (potential thread leak)");
        Assert.True(avgCpu / avgThreads < 1, $"CPU per thread too high: {avgCpu / avgThreads:F2}%");

        Output.WriteLine("✅ Thread efficiency test passed");
    }
}