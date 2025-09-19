using Xunit;
using Xunit.Abstractions;

namespace PerformanceTests;

public class MemoryLeakTests : BasePerformanceTest
{
    public MemoryLeakTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task ShortTerm_MemoryUsage_ShouldBeStable()
    {
        // Skip if BabeMakro is not running
        if (BabeMakroProcess == null)
        {
            Output.WriteLine("❌ Skipping test - BabeMakro process not found");
            return;
        }

        Output.WriteLine("🧪 Testing short-term memory stability (5 minutes)...");

        // Monitor for 5 minutes with 10-second intervals
        await MonitorForDuration(
            duration: TimeSpan.FromMinutes(5),
            sampleInterval: TimeSpan.FromSeconds(10),
            testName: "ShortTerm_Memory"
        );

        // Assert memory usage is under 500MB (requirement from docs)
        var currentMemory = GetMemoryUsageMB();
        Assert.True(currentMemory < 500, $"Memory usage too high: {currentMemory}MB (should be < 500MB)");

        Output.WriteLine($"✅ Memory usage OK: {currentMemory}MB");
    }

    [Fact]
    public async Task MediumTerm_MemoryUsage_ShouldNotLeak()
    {
        if (BabeMakroProcess == null)
        {
            Output.WriteLine("❌ Skipping test - BabeMakro process not found");
            return;
        }

        Output.WriteLine("🧪 Testing medium-term memory leak (30 minutes)...");

        var initialMemory = GetMemoryUsageMB();
        Output.WriteLine($"📊 Initial memory: {initialMemory}MB");

        // Monitor for 30 minutes with 30-second intervals
        await MonitorForDuration(
            duration: TimeSpan.FromMinutes(30),
            sampleInterval: TimeSpan.FromSeconds(30),
            testName: "MediumTerm_Memory"
        );

        var finalMemory = GetMemoryUsageMB();
        var memoryGrowth = ((double)(finalMemory - initialMemory) / initialMemory) * 100;

        Output.WriteLine($"📊 Final memory: {finalMemory}MB");
        Output.WriteLine($"📈 Memory growth: {memoryGrowth:F1}%");

        // Assert memory growth is less than 20% (reasonable threshold)
        Assert.True(memoryGrowth < 20, $"Potential memory leak detected: {memoryGrowth:F1}% growth");

        Output.WriteLine($"✅ Memory leak test passed: {memoryGrowth:F1}% growth");
    }

    [Fact(Skip = "Long running test - enable manually for 24h testing")]
    public async Task LongTerm_24Hour_MemoryLeak_Test()
    {
        if (BabeMakroProcess == null)
        {
            Output.WriteLine("❌ Skipping test - BabeMakro process not found");
            return;
        }

        Output.WriteLine("🧪 Starting 24-hour memory leak test...");
        Output.WriteLine("⚠️ This test will run for 24 hours. Make sure system is stable.");

        var initialMemory = GetMemoryUsageMB();
        var initialHandles = GetHandleCount();

        Output.WriteLine($"📊 Initial state: Memory={initialMemory}MB, Handles={initialHandles}");

        // Monitor for 24 hours with 5-minute intervals
        await MonitorForDuration(
            duration: TimeSpan.FromHours(24),
            sampleInterval: TimeSpan.FromMinutes(5),
            testName: "24Hour_Endurance"
        );

        var finalMemory = GetMemoryUsageMB();
        var finalHandles = GetHandleCount();

        var memoryGrowth = ((double)(finalMemory - initialMemory) / initialMemory) * 100;
        var handleGrowth = ((double)(finalHandles - initialHandles) / initialHandles) * 100;

        Output.WriteLine($"📊 Final state: Memory={finalMemory}MB, Handles={finalHandles}");
        Output.WriteLine($"📈 Growth: Memory={memoryGrowth:F1}%, Handles={handleGrowth:F1}%");

        // 24-hour stability requirements
        Assert.True(memoryGrowth < 15, $"Memory leak detected: {memoryGrowth:F1}% growth over 24h");
        Assert.True(handleGrowth < 10, $"Handle leak detected: {handleGrowth:F1}% growth over 24h");
        Assert.True(finalMemory < 600, $"Memory usage too high after 24h: {finalMemory}MB");

        Output.WriteLine("✅ 24-hour stability test passed!");
    }

    [Fact]
    public async Task HandleLeak_Detection_Test()
    {
        if (BabeMakroProcess == null)
        {
            Output.WriteLine("❌ Skipping test - BabeMakro process not found");
            return;
        }

        Output.WriteLine("🧪 Testing handle leak detection (10 minutes)...");

        var initialHandles = GetHandleCount();
        Output.WriteLine($"📊 Initial handles: {initialHandles}");

        // Monitor handles for 10 minutes
        var samples = new List<int>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < TimeSpan.FromMinutes(10))
        {
            var currentHandles = GetHandleCount();
            samples.Add(currentHandles);

            if (samples.Count % 6 == 0) // Log every minute
            {
                Output.WriteLine($"📊 Handles at {stopwatch.Elapsed.TotalMinutes:F1}min: {currentHandles}");
            }

            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        var finalHandles = GetHandleCount();
        var maxHandles = samples.Max();
        var avgHandles = samples.Average();

        Output.WriteLine($"📊 Handle statistics:");
        Output.WriteLine($"   Initial: {initialHandles}");
        Output.WriteLine($"   Final: {finalHandles}");
        Output.WriteLine($"   Max: {maxHandles}");
        Output.WriteLine($"   Average: {avgHandles:F0}");

        var handleGrowth = ((double)(finalHandles - initialHandles) / initialHandles) * 100;

        // Assert handle count is stable (less than 5% growth)
        Assert.True(handleGrowth < 5, $"Handle leak detected: {handleGrowth:F1}% growth");
        Assert.True(maxHandles < initialHandles * 1.1, $"Handle spike detected: Max={maxHandles}, Initial={initialHandles}");

        Output.WriteLine($"✅ Handle leak test passed: {handleGrowth:F1}% growth");
    }

    [Fact]
    public async Task GarbageCollection_Pressure_Test()
    {
        Output.WriteLine("🧪 Testing GC pressure and memory allocation patterns...");

        var initialGCInfo = GetGCInfo();
        Output.WriteLine($"📊 Initial GC state:");
        Output.WriteLine($"   Gen0: {initialGCInfo.GenerationInfo[0].SizeAfterBytes / 1024}KB");
        Output.WriteLine($"   Gen1: {initialGCInfo.GenerationInfo[1].SizeAfterBytes / 1024}KB");
        Output.WriteLine($"   Gen2: {initialGCInfo.GenerationInfo[2].SizeAfterBytes / 1024}KB");

        // Monitor GC activity for 5 minutes
        var gcSamples = new List<GCMemoryInfo>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < TimeSpan.FromMinutes(5))
        {
            var gcInfo = GetGCInfo();
            gcSamples.Add(gcInfo);

            await Task.Delay(TimeSpan.FromSeconds(15));
        }

        var finalGCInfo = GetGCInfo();

        // Analyze GC pressure
        var gen0Collections = gcSamples.Count(gc => gc.GenerationInfo[0].SizeAfterBytes > initialGCInfo.GenerationInfo[0].SizeAfterBytes);
        var avgGen0Size = gcSamples.Average(gc => gc.GenerationInfo[0].SizeAfterBytes) / 1024;
        var avgGen2Size = gcSamples.Average(gc => gc.GenerationInfo[2].SizeAfterBytes) / 1024;

        Output.WriteLine($"📊 GC Analysis:");
        Output.WriteLine($"   Gen0 average size: {avgGen0Size:F0}KB");
        Output.WriteLine($"   Gen2 average size: {avgGen2Size:F0}KB");
        Output.WriteLine($"   Gen0 pressure events: {gen0Collections}");

        // Assert reasonable GC behavior
        Assert.True(avgGen0Size < 1024, $"Gen0 heap too large: {avgGen0Size:F0}KB"); // < 1MB
        Assert.True(avgGen2Size < 10240, $"Gen2 heap too large: {avgGen2Size:F0}KB"); // < 10MB

        Output.WriteLine("✅ GC pressure test passed");
    }
}