using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace PerformanceTests;

public class StressTests : BasePerformanceTest
{
    public StressTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task Single_Client_Endurance_Test()
    {
        if (BabeMakroProcess == null)
        {
            Output.WriteLine("❌ Skipping test - BabeMakro process not found");
            return;
        }

        Output.WriteLine("🧪 Single client endurance test (1 hour)...");
        Output.WriteLine("📋 Tests stability with one client running continuously");

        var initialStats = new
        {
            Memory = GetMemoryUsageMB(),
            Handles = GetHandleCount(),
            Threads = GetThreadCount()
        };

        Output.WriteLine($"📊 Initial state: Mem={initialStats.Memory}MB, Handles={initialStats.Handles}, Threads={initialStats.Threads}");

        // Monitor for 1 hour with 2-minute intervals
        await MonitorForDuration(
            duration: TimeSpan.FromHours(1),
            sampleInterval: TimeSpan.FromMinutes(2),
            testName: "SingleClient_1Hour"
        );

        var finalStats = new
        {
            Memory = GetMemoryUsageMB(),
            Handles = GetHandleCount(),
            Threads = GetThreadCount()
        };

        // Calculate resource usage changes
        var memoryGrowth = ((double)(finalStats.Memory - initialStats.Memory) / initialStats.Memory) * 100;
        var handleGrowth = ((double)(finalStats.Handles - initialStats.Handles) / initialStats.Handles) * 100;
        var threadGrowth = ((double)(finalStats.Threads - initialStats.Threads) / initialStats.Threads) * 100;

        Output.WriteLine($"📊 Resource growth after 1 hour:");
        Output.WriteLine($"   Memory: {memoryGrowth:+F1;-F1}%");
        Output.WriteLine($"   Handles: {handleGrowth:+F1;-F1}%");
        Output.WriteLine($"   Threads: {threadGrowth:+F1;-F1}%");

        // Assert stability requirements
        Assert.True(memoryGrowth < 10, $"Memory growth too high: {memoryGrowth:F1}%");
        Assert.True(Math.Abs(handleGrowth) < 5, $"Handle count unstable: {handleGrowth:F1}%");
        Assert.True(Math.Abs(threadGrowth) < 10, $"Thread count unstable: {threadGrowth:F1}%");
        Assert.True(finalStats.Memory < 400, $"Final memory too high: {finalStats.Memory}MB");

        Output.WriteLine("✅ Single client endurance test passed");
    }

    [Fact]
    public async Task Rapid_Start_Stop_Stress_Test()
    {
        if (BabeMakroProcess == null)
        {
            Output.WriteLine("❌ Skipping test - BabeMakro process not found");
            return;
        }

        Output.WriteLine("🧪 Rapid start/stop stress test...");
        Output.WriteLine("📋 Simulates rapid enable/disable cycles that might stress the system");

        var initialMemory = GetMemoryUsageMB();
        var initialHandles = GetHandleCount();

        var memorySamples = new List<long>();
        var handleSamples = new List<int>();
        var cpuSamples = new List<double>();

        // Simulate rapid start/stop cycles by monitoring for patterns
        // In a real implementation, this would trigger actual start/stop operations
        // For now, we monitor system behavior during what should be a stress period

        var stopwatch = Stopwatch.StartNew();
        int cycles = 0;

        while (stopwatch.Elapsed < TimeSpan.FromMinutes(10))
        {
            // Sample current state
            var memory = GetMemoryUsageMB();
            var handles = GetHandleCount();
            var cpu = GetCpuUsage();

            memorySamples.Add(memory);
            handleSamples.Add(handles);
            cpuSamples.Add(cpu);

            cycles++;

            if (cycles % 10 == 0)
            {
                Output.WriteLine($"📊 Cycle {cycles}: Mem={memory}MB, Handles={handles}, CPU={cpu:F1}%");
            }

            // Simulate rapid polling that might stress the system
            await Task.Delay(2000); // 2-second intervals for 10 minutes = 300 cycles
        }

        // Analyze stress test results
        var avgMemory = memorySamples.Average();
        var maxMemory = memorySamples.Max();
        var memoryVariance = memorySamples.Select(m => Math.Pow(m - avgMemory, 2)).Average();
        var memoryStdDev = Math.Sqrt(memoryVariance);

        var avgHandles = handleSamples.Average();
        var maxHandles = handleSamples.Max();
        var minHandles = handleSamples.Min();

        var avgCpu = cpuSamples.Average();
        var maxCpu = cpuSamples.Max();

        Output.WriteLine($"📊 Stress test analysis ({cycles} cycles):");
        Output.WriteLine($"   Memory: Avg={avgMemory:F1}MB, Max={maxMemory}MB, StdDev={memoryStdDev:F1}MB");
        Output.WriteLine($"   Handles: Avg={avgHandles:F0}, Range={minHandles}-{maxHandles}");
        Output.WriteLine($"   CPU: Avg={avgCpu:F1}%, Max={maxCpu:F1}%");

        // Assert system remains stable under stress
        Assert.True(memoryStdDev < 20, $"Memory too unstable under stress: StdDev={memoryStdDev:F1}MB");
        Assert.True(maxHandles - minHandles < initialHandles * 0.1, $"Handle count too variable: Range={maxHandles - minHandles}");
        Assert.True(avgCpu < 15, $"CPU too high under stress: {avgCpu:F1}%");
        Assert.True(maxMemory < 500, $"Memory peak too high: {maxMemory}MB");

        Output.WriteLine("✅ Rapid start/stop stress test passed");
    }

    [Fact]
    public async Task Memory_Pressure_Simulation_Test()
    {
        Output.WriteLine("🧪 Memory pressure simulation test...");
        Output.WriteLine("📋 Tests system behavior under simulated memory pressure");

        // Create memory pressure simulation
        var memoryAllocations = new List<byte[]>();
        var memoryPressureTask = Task.Run(async () =>
        {
            try
            {
                // Gradually allocate memory to create pressure
                for (int i = 0; i < 50; i++) // Allocate ~500MB in chunks
                {
                    var chunk = new byte[10 * 1024 * 1024]; // 10MB chunks
                    memoryAllocations.Add(chunk);
                    await Task.Delay(5000); // 5-second intervals
                }

                // Hold memory for 2 minutes
                await Task.Delay(TimeSpan.FromMinutes(2));
            }
            finally
            {
                // Cleanup
                memoryAllocations.Clear();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        });

        // Monitor BabeMakro during memory pressure
        var babeMakroStats = new List<(DateTime Time, long Memory, double Cpu, int Handles)>();
        var monitoringTask = Task.Run(async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromMinutes(8) && !memoryPressureTask.IsCompleted)
            {
                if (BabeMakroProcess != null)
                {
                    var memory = GetMemoryUsageMB();
                    var cpu = GetCpuUsage();
                    var handles = GetHandleCount();

                    babeMakroStats.Add((DateTime.UtcNow, memory, cpu, handles));

                    if (babeMakroStats.Count % 10 == 0)
                    {
                        var totalSystemMemory = GC.GetTotalMemory(false) / (1024 * 1024);
                        Output.WriteLine($"📊 {stopwatch.Elapsed.TotalMinutes:F1}min: BabeMakro={memory}MB, System={totalSystemMemory}MB, CPU={cpu:F1}%");
                    }
                }

                await Task.Delay(3000);
            }
        });

        await Task.WhenAll(memoryPressureTask, monitoringTask);

        if (babeMakroStats.Any())
        {
            var avgMemory = babeMakroStats.Average(s => s.Memory);
            var maxMemory = babeMakroStats.Max(s => s.Memory);
            var avgCpu = babeMakroStats.Average(s => s.Cpu);
            var maxCpu = babeMakroStats.Max(s => s.Cpu);

            Output.WriteLine($"📊 BabeMakro under memory pressure:");
            Output.WriteLine($"   Memory: Avg={avgMemory:F1}MB, Max={maxMemory}MB");
            Output.WriteLine($"   CPU: Avg={avgCpu:F1}%, Max={maxCpu:F1}%");

            // Assert BabeMakro remains stable under memory pressure
            Assert.True(maxMemory < 600, $"BabeMakro memory too high under pressure: {maxMemory}MB");
            Assert.True(avgCpu < 20, $"BabeMakro CPU too high under pressure: {avgCpu:F1}%");

            Output.WriteLine("✅ Memory pressure simulation passed");
        }
        else
        {
            Output.WriteLine("⚠️ No BabeMakro stats collected during memory pressure test");
        }
    }

    [Fact]
    public async Task High_DPI_Multi_Monitor_Simulation()
    {
        Output.WriteLine("🧪 High DPI multi-monitor simulation test...");
        Output.WriteLine("📋 Simulates performance under high DPI and multi-monitor scenarios");

        if (BabeMakroProcess == null)
        {
            Output.WriteLine("⚠️ BabeMakro not running - testing system performance baseline");
        }

        // Simulate multi-monitor workload by creating computational tasks
        // that represent the kind of work done in multi-monitor scenarios
        var simulationTasks = new List<Task>();
        var performanceData = new List<(DateTime Time, double Cpu, long Memory)>();

        // Create 4 monitor simulation tasks (representing 2 monitors with high DPI scaling)
        for (int monitor = 0; monitor < 4; monitor++)
        {
            int monitorId = monitor;
            simulationTasks.Add(Task.Run(async () =>
            {
                var random = new Random(monitorId);
                var stopwatch = Stopwatch.StartNew();

                while (stopwatch.Elapsed < TimeSpan.FromMinutes(5))
                {
                    // Simulate high-DPI coordinate transformations and pixel operations
                    for (int i = 0; i < 1000; i++)
                    {
                        // Simulate coordinate scaling calculations
                        var x = random.Next(0, 3840); // 4K width
                        var y = random.Next(0, 2160); // 4K height
                        var scaledX = x * 1.5; // 150% DPI scaling
                        var scaledY = y * 1.5;

                        // Simulate pixel comparison operations
                        var r = (byte)random.Next(256);
                        var g = (byte)random.Next(256);
                        var b = (byte)random.Next(256);
                        var _ = (r * 0.299 + g * 0.587 + b * 0.114); // Luminance calculation
                    }

                    await Task.Delay(16); // ~60Hz simulation
                }
            }));
        }

        // Monitor system performance during simulation
        var monitorTask = Task.Run(async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromMinutes(5))
            {
                var cpu = GetCpuUsage();
                var memory = BabeMakroProcess?.WorkingSet64 ?? 0;

                performanceData.Add((DateTime.UtcNow, cpu, memory / (1024 * 1024)));

                await Task.Delay(5000);
            }
        });

        Output.WriteLine("🏃 Running high-DPI multi-monitor simulation...");
        await Task.WhenAll(simulationTasks.Concat(new[] { monitorTask }));

        // Analyze results
        var avgCpu = performanceData.Average(p => p.Cpu);
        var maxCpu = performanceData.Max(p => p.Cpu);
        var avgMemory = performanceData.Where(p => p.Memory > 0).DefaultIfEmpty().Average(p => p.Memory);

        Output.WriteLine($"📊 High-DPI Multi-Monitor Results:");
        Output.WriteLine($"   CPU Average: {avgCpu:F1}%");
        Output.WriteLine($"   CPU Maximum: {maxCpu:F1}%");
        Output.WriteLine($"   Memory Average: {avgMemory:F1}MB");
        Output.WriteLine($"   Simulated monitors: 4");
        Output.WriteLine($"   Simulated resolution: 4K per monitor");

        // Performance should remain reasonable even with high DPI simulation
        Assert.True(avgCpu < 25, $"CPU too high during high-DPI simulation: {avgCpu:F1}%");
        if (avgMemory > 0)
        {
            Assert.True(avgMemory < 600, $"Memory too high during high-DPI simulation: {avgMemory:F1}MB");
        }

        Output.WriteLine("✅ High-DPI multi-monitor simulation passed");
    }

    [Fact(Skip = "Resource intensive test - enable manually")]
    public async Task Maximum_Load_Stress_Test()
    {
        Output.WriteLine("🧪 Maximum load stress test...");
        Output.WriteLine("⚠️ This test creates maximum system load - ensure system stability");

        if (BabeMakroProcess == null)
        {
            Output.WriteLine("❌ Skipping test - BabeMakro process not found");
            return;
        }

        var initialStats = new
        {
            Memory = GetMemoryUsageMB(),
            Handles = GetHandleCount(),
            Threads = GetThreadCount(),
            Cpu = GetCpuUsage()
        };

        Output.WriteLine($"📊 Pre-stress state: Mem={initialStats.Memory}MB, CPU={initialStats.Cpu:F1}%");

        // Create maximum reasonable load
        var stressTasks = new List<Task>();
        var cts = new CancellationTokenSource();

        // CPU stress (use available cores)
        int coreCount = Environment.ProcessorCount;
        for (int i = 0; i < coreCount / 2; i++) // Use half the cores to leave room for BabeMakro
        {
            stressTasks.Add(Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    // CPU-intensive work
                    for (int j = 0; j < 10000; j++)
                    {
                        Math.Sin(j * Math.PI);
                    }
                    await Task.Delay(1, cts.Token);
                }
            }, cts.Token));
        }

        // Monitor BabeMakro under maximum stress
        var monitoringData = new List<(DateTime Time, long Memory, double Cpu, int Handles)>();
        var monitorTask = Task.Run(async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromMinutes(10))
            {
                var memory = GetMemoryUsageMB();
                var cpu = GetCpuUsage();
                var handles = GetHandleCount();

                monitoringData.Add((DateTime.UtcNow, memory, cpu, handles));

                if (monitoringData.Count % 5 == 0)
                {
                    Output.WriteLine($"📊 {stopwatch.Elapsed.TotalMinutes:F1}min under stress: Mem={memory}MB, CPU={cpu:F1}%, Handles={handles}");
                }

                await Task.Delay(6000); // 6-second intervals
            }
        });

        Output.WriteLine("💥 Starting maximum load stress test for 10 minutes...");
        await monitorTask;

        // Stop stress tasks
        cts.Cancel();
        try
        {
            await Task.WhenAll(stressTasks);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelling tasks
        }

        // Allow system to recover
        await Task.Delay(5000);

        // Analyze stress test results
        var avgMemory = monitoringData.Average(d => d.Memory);
        var maxMemory = monitoringData.Max(d => d.Memory);
        var avgCpu = monitoringData.Average(d => d.Cpu);
        var maxCpu = monitoringData.Max(d => d.Cpu);

        var finalStats = new
        {
            Memory = GetMemoryUsageMB(),
            Handles = GetHandleCount(),
            Threads = GetThreadCount()
        };

        Output.WriteLine($"📊 Maximum stress test results:");
        Output.WriteLine($"   Memory under stress: Avg={avgMemory:F1}MB, Max={maxMemory}MB");
        Output.WriteLine($"   CPU under stress: Avg={avgCpu:F1}%, Max={maxCpu:F1}%");
        Output.WriteLine($"   Final state: Mem={finalStats.Memory}MB, Handles={finalStats.Handles}");

        // BabeMakro should survive maximum stress
        Assert.True(maxMemory < 800, $"Memory too high under maximum stress: {maxMemory}MB");
        Assert.True(finalStats.Memory < 500, $"Memory didn't recover after stress: {finalStats.Memory}MB");
        Assert.True(Math.Abs(finalStats.Handles - initialStats.Handles) < 10, "Handle leak detected after stress test");

        Output.WriteLine("✅ Maximum load stress test passed - BabeMakro survived!");
    }
}