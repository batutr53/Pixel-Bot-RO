using System.Text.Json;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace PerformanceTests;

public class PerformanceReportGenerator : BasePerformanceTest
{
    public PerformanceReportGenerator(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task Generate_Comprehensive_Performance_Report()
    {
        Output.WriteLine("📊 Generating comprehensive performance report...");

        var report = new PerformanceReport
        {
            TestDate = DateTime.UtcNow,
            SystemInfo = await CollectSystemInfo(),
            BabeMakroInfo = CollectBabeMakroInfo(),
            TestResults = new List<TestResult>()
        };

        // Run quick performance assessment
        Output.WriteLine("🔍 Running performance assessment...");

        report.TestResults.Add(await RunMemoryAssessment());
        report.TestResults.Add(await RunCpuAssessment());
        report.TestResults.Add(await RunTimerAssessment());
        report.TestResults.Add(await RunStabilityAssessment());

        // Generate overall score
        report.OverallScore = CalculateOverallScore(report.TestResults);
        report.Recommendations = GenerateRecommendations(report.TestResults);

        // Save reports in multiple formats
        await SaveReportAsJson(report);
        await SaveReportAsHtml(report);
        await SaveReportAsMarkdown(report);

        Output.WriteLine($"📋 Performance report generated with overall score: {report.OverallScore}/100");
        Output.WriteLine($"📁 Reports saved in: E:\\ro\\performance-reports\\");

        // Assert overall performance is acceptable
        Assert.True(report.OverallScore >= 70, $"Overall performance score too low: {report.OverallScore}/100");

        Output.WriteLine("✅ Performance report generation completed");
    }

    private async Task<SystemInfo> CollectSystemInfo()
    {
        var os = Environment.OSVersion;
        var totalMemory = GC.GetTotalMemory(false);

        return new SystemInfo
        {
            OperatingSystem = $"{os.Platform} {os.Version}",
            ProcessorCount = Environment.ProcessorCount,
            TotalMemoryGB = (double)totalMemory / (1024 * 1024 * 1024),
            DotNetVersion = Environment.Version.ToString(),
            MachineName = Environment.MachineName,
            Is64BitProcess = Environment.Is64BitProcess,
            WorkingSet = Environment.WorkingSet / (1024 * 1024), // MB
            SystemPageSize = Environment.SystemPageSize
        };
    }

    private BabeMakroInfo? CollectBabeMakroInfo()
    {
        if (BabeMakroProcess == null)
            return null;

        try
        {
            BabeMakroProcess.Refresh();
            return new BabeMakroInfo
            {
                ProcessId = BabeMakroProcess.Id,
                StartTime = BabeMakroProcess.StartTime,
                WorkingSetMB = BabeMakroProcess.WorkingSet64 / (1024 * 1024),
                HandleCount = BabeMakroProcess.HandleCount,
                ThreadCount = BabeMakroProcess.Threads.Count,
                ProcessorTime = BabeMakroProcess.TotalProcessorTime,
                IsResponding = BabeMakroProcess.Responding
            };
        }
        catch
        {
            return null;
        }
    }

    private async Task<TestResult> RunMemoryAssessment()
    {
        Output.WriteLine("🧠 Assessing memory performance...");

        var result = new TestResult { TestName = "Memory Assessment", StartTime = DateTime.UtcNow };

        if (BabeMakroProcess == null)
        {
            result.Status = "Skipped";
            result.Score = 0;
            result.Notes = "BabeMakro process not found";
            return result;
        }

        var memorySamples = new List<long>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Monitor for 2 minutes
        while (stopwatch.Elapsed < TimeSpan.FromMinutes(2))
        {
            memorySamples.Add(GetMemoryUsageMB());
            await Task.Delay(5000);
        }

        result.EndTime = DateTime.UtcNow;

        var avgMemory = memorySamples.Average();
        var maxMemory = memorySamples.Max();
        var memoryStability = 100 - (memorySamples.Max() - memorySamples.Min()) / memorySamples.Average() * 100;

        result.Metrics = new Dictionary<string, object>
        {
            ["AverageMemoryMB"] = avgMemory,
            ["MaxMemoryMB"] = maxMemory,
            ["MemoryStabilityPercent"] = memoryStability,
            ["SampleCount"] = memorySamples.Count
        };

        // Score based on memory usage and stability
        var memoryScore = Math.Max(0, 100 - (avgMemory / 5)); // Penalty for high memory usage
        var stabilityScore = Math.Max(0, memoryStability);
        result.Score = (int)((memoryScore + stabilityScore) / 2);

        result.Status = result.Score >= 70 ? "Pass" : result.Score >= 50 ? "Warning" : "Fail";
        result.Notes = $"Memory usage avg: {avgMemory:F1}MB, stability: {memoryStability:F1}%";

        return result;
    }

    private async Task<TestResult> RunCpuAssessment()
    {
        Output.WriteLine("🔥 Assessing CPU performance...");

        var result = new TestResult { TestName = "CPU Assessment", StartTime = DateTime.UtcNow };

        var cpuSamples = new List<double>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Warm up CPU counter
        GetCpuUsage();
        await Task.Delay(1000);

        // Monitor for 2 minutes
        while (stopwatch.Elapsed < TimeSpan.FromMinutes(2))
        {
            cpuSamples.Add(GetCpuUsage());
            await Task.Delay(3000);
        }

        result.EndTime = DateTime.UtcNow;

        var avgCpu = cpuSamples.Average();
        var maxCpu = cpuSamples.Max();
        var cpuSpikes = cpuSamples.Count(cpu => cpu > 15);

        result.Metrics = new Dictionary<string, object>
        {
            ["AverageCpuPercent"] = avgCpu,
            ["MaxCpuPercent"] = maxCpu,
            ["CpuSpikes"] = cpuSpikes,
            ["SampleCount"] = cpuSamples.Count
        };

        // Score based on CPU efficiency
        var avgScore = Math.Max(0, 100 - avgCpu * 5); // Penalty for high CPU
        var spikeScore = Math.Max(0, 100 - cpuSpikes * 10); // Penalty for spikes
        result.Score = (int)((avgScore + spikeScore) / 2);

        result.Status = result.Score >= 70 ? "Pass" : result.Score >= 50 ? "Warning" : "Fail";
        result.Notes = $"CPU usage avg: {avgCpu:F1}%, max: {maxCpu:F1}%, spikes: {cpuSpikes}";

        return result;
    }

    private async Task<TestResult> RunTimerAssessment()
    {
        Output.WriteLine("⏱️ Assessing timer precision...");

        var result = new TestResult { TestName = "Timer Precision Assessment", StartTime = DateTime.UtcNow };

        var targetInterval = 16.67; // 60Hz in milliseconds
        var intervals = new List<double>();
        DateTime lastTick = DateTime.UtcNow;

        // Test timer precision for 1 minute
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromMinutes(1))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(targetInterval));

            var now = DateTime.UtcNow;
            var actualInterval = (now - lastTick).TotalMilliseconds;
            intervals.Add(actualInterval);
            lastTick = now;
        }

        result.EndTime = DateTime.UtcNow;

        var avgInterval = intervals.Average();
        var stdDev = Math.Sqrt(intervals.Select(x => Math.Pow(x - avgInterval, 2)).Average());
        var accuracy = 100 - Math.Abs(avgInterval - targetInterval) / targetInterval * 100;
        var precision = Math.Max(0, 100 - stdDev * 10);

        result.Metrics = new Dictionary<string, object>
        {
            ["TargetIntervalMs"] = targetInterval,
            ["AverageIntervalMs"] = avgInterval,
            ["StandardDeviationMs"] = stdDev,
            ["AccuracyPercent"] = accuracy,
            ["PrecisionPercent"] = precision,
            ["SampleCount"] = intervals.Count
        };

        result.Score = (int)((accuracy + precision) / 2);
        result.Status = result.Score >= 70 ? "Pass" : result.Score >= 50 ? "Warning" : "Fail";
        result.Notes = $"Timer accuracy: {accuracy:F1}%, precision: {precision:F1}%";

        return result;
    }

    private async Task<TestResult> RunStabilityAssessment()
    {
        Output.WriteLine("🛡️ Assessing overall stability...");

        var result = new TestResult { TestName = "Stability Assessment", StartTime = DateTime.UtcNow };

        if (BabeMakroProcess == null)
        {
            result.Status = "Skipped";
            result.Score = 0;
            result.Notes = "BabeMakro process not found";
            return result;
        }

        var initialHandles = GetHandleCount();
        var initialThreads = GetThreadCount();
        var initialMemory = GetMemoryUsageMB();

        var handleSamples = new List<int>();
        var threadSamples = new List<int>();

        // Monitor for 5 minutes
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromMinutes(5))
        {
            handleSamples.Add(GetHandleCount());
            threadSamples.Add(GetThreadCount());
            await Task.Delay(10000); // 10-second intervals
        }

        result.EndTime = DateTime.UtcNow;

        var finalHandles = GetHandleCount();
        var finalThreads = GetThreadCount();
        var finalMemory = GetMemoryUsageMB();

        var handleStability = 100 - Math.Abs((double)(finalHandles - initialHandles) / initialHandles * 100);
        var threadStability = 100 - Math.Abs((double)(finalThreads - initialThreads) / initialThreads * 100);
        var memoryGrowth = (double)(finalMemory - initialMemory) / initialMemory * 100;

        result.Metrics = new Dictionary<string, object>
        {
            ["InitialHandles"] = initialHandles,
            ["FinalHandles"] = finalHandles,
            ["HandleStabilityPercent"] = handleStability,
            ["InitialThreads"] = initialThreads,
            ["FinalThreads"] = finalThreads,
            ["ThreadStabilityPercent"] = threadStability,
            ["MemoryGrowthPercent"] = memoryGrowth
        };

        var stabilityScore = (handleStability + threadStability + Math.Max(0, 100 - memoryGrowth * 5)) / 3;
        result.Score = (int)stabilityScore;

        result.Status = result.Score >= 80 ? "Pass" : result.Score >= 60 ? "Warning" : "Fail";
        result.Notes = $"Handle stability: {handleStability:F1}%, thread stability: {threadStability:F1}%, memory growth: {memoryGrowth:F1}%";

        return result;
    }

    private int CalculateOverallScore(List<TestResult> results)
    {
        var validResults = results.Where(r => r.Score > 0).ToList();
        if (!validResults.Any()) return 0;

        // Weighted scoring
        var weights = new Dictionary<string, double>
        {
            ["Memory Assessment"] = 0.3,
            ["CPU Assessment"] = 0.25,
            ["Timer Precision Assessment"] = 0.2,
            ["Stability Assessment"] = 0.25
        };

        double totalScore = 0;
        double totalWeight = 0;

        foreach (var result in validResults)
        {
            var weight = weights.GetValueOrDefault(result.TestName, 1.0);
            totalScore += result.Score * weight;
            totalWeight += weight;
        }

        return totalWeight > 0 ? (int)(totalScore / totalWeight) : 0;
    }

    private List<string> GenerateRecommendations(List<TestResult> results)
    {
        var recommendations = new List<string>();

        foreach (var result in results)
        {
            if (result.Status == "Fail" || result.Status == "Warning")
            {
                switch (result.TestName)
                {
                    case "Memory Assessment":
                        recommendations.Add($"🧠 Memory: {result.Notes}. Consider optimizing memory usage or increasing available RAM.");
                        break;
                    case "CPU Assessment":
                        recommendations.Add($"🔥 CPU: {result.Notes}. Consider optimizing algorithms or reducing update frequency.");
                        break;
                    case "Timer Precision Assessment":
                        recommendations.Add($"⏱️ Timer: {result.Notes}. Consider using higher precision timers or adjusting target frequencies.");
                        break;
                    case "Stability Assessment":
                        recommendations.Add($"🛡️ Stability: {result.Notes}. Check for resource leaks and improve cleanup procedures.");
                        break;
                }
            }
        }

        if (!recommendations.Any())
        {
            recommendations.Add("✅ All performance metrics are within acceptable ranges. System is performing well!");
        }

        return recommendations;
    }

    private async Task SaveReportAsJson(PerformanceReport report)
    {
        var directory = "E:\\ro\\performance-reports";
        Directory.CreateDirectory(directory);

        var fileName = $"performance-report-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}.json";
        var filePath = Path.Combine(directory, fileName);

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);

        Output.WriteLine($"📄 JSON report saved: {fileName}");
    }

    private async Task SaveReportAsHtml(PerformanceReport report)
    {
        var directory = "E:\\ro\\performance-reports";
        var fileName = $"performance-report-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}.html";
        var filePath = Path.Combine(directory, fileName);

        var html = GenerateHtmlReport(report);
        await File.WriteAllTextAsync(filePath, html);

        Output.WriteLine($"🌐 HTML report saved: {fileName}");
    }

    private async Task SaveReportAsMarkdown(PerformanceReport report)
    {
        var directory = "E:\\ro\\performance-reports";
        var fileName = $"performance-report-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}.md";
        var filePath = Path.Combine(directory, fileName);

        var markdown = GenerateMarkdownReport(report);
        await File.WriteAllTextAsync(filePath, markdown);

        Output.WriteLine($"📝 Markdown report saved: {fileName}");
    }

    private string GenerateHtmlReport(PerformanceReport report)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html><head><title>BabeMakro Performance Report</title>");
        html.AppendLine("<style>");
        html.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
        html.AppendLine(".header { background: #f0f0f0; padding: 20px; border-radius: 5px; }");
        html.AppendLine(".score { font-size: 24px; font-weight: bold; }");
        html.AppendLine(".pass { color: green; } .warning { color: orange; } .fail { color: red; }");
        html.AppendLine("table { border-collapse: collapse; width: 100%; }");
        html.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
        html.AppendLine("th { background-color: #f2f2f2; }");
        html.AppendLine("</style></head><body>");

        html.AppendLine($"<div class='header'>");
        html.AppendLine($"<h1>BabeMakro Performance Report</h1>");
        html.AppendLine($"<p>Generated: {report.TestDate:yyyy-MM-dd HH:mm:ss} UTC</p>");
        html.AppendLine($"<div class='score'>Overall Score: {report.OverallScore}/100</div>");
        html.AppendLine($"</div>");

        // System Info
        html.AppendLine("<h2>System Information</h2>");
        html.AppendLine("<table>");
        html.AppendLine($"<tr><td>OS</td><td>{report.SystemInfo.OperatingSystem}</td></tr>");
        html.AppendLine($"<tr><td>Processors</td><td>{report.SystemInfo.ProcessorCount}</td></tr>");
        html.AppendLine($"<tr><td>Memory</td><td>{report.SystemInfo.TotalMemoryGB:F1} GB</td></tr>");
        html.AppendLine($"<tr><td>.NET Version</td><td>{report.SystemInfo.DotNetVersion}</td></tr>");
        html.AppendLine("</table>");

        // Test Results
        html.AppendLine("<h2>Test Results</h2>");
        html.AppendLine("<table>");
        html.AppendLine("<tr><th>Test</th><th>Status</th><th>Score</th><th>Notes</th></tr>");

        foreach (var result in report.TestResults)
        {
            var statusClass = result.Status.ToLower();
            html.AppendLine($"<tr><td>{result.TestName}</td><td class='{statusClass}'>{result.Status}</td><td>{result.Score}</td><td>{result.Notes}</td></tr>");
        }

        html.AppendLine("</table>");

        // Recommendations
        html.AppendLine("<h2>Recommendations</h2>");
        html.AppendLine("<ul>");
        foreach (var rec in report.Recommendations)
        {
            html.AppendLine($"<li>{rec}</li>");
        }
        html.AppendLine("</ul>");

        html.AppendLine("</body></html>");
        return html.ToString();
    }

    private string GenerateMarkdownReport(PerformanceReport report)
    {
        var md = new StringBuilder();
        md.AppendLine("# BabeMakro Performance Report");
        md.AppendLine();
        md.AppendLine($"**Generated:** {report.TestDate:yyyy-MM-dd HH:mm:ss} UTC");
        md.AppendLine($"**Overall Score:** {report.OverallScore}/100");
        md.AppendLine();

        md.AppendLine("## System Information");
        md.AppendLine();
        md.AppendLine("| Property | Value |");
        md.AppendLine("|----------|-------|");
        md.AppendLine($"| Operating System | {report.SystemInfo.OperatingSystem} |");
        md.AppendLine($"| Processor Count | {report.SystemInfo.ProcessorCount} |");
        md.AppendLine($"| Total Memory | {report.SystemInfo.TotalMemoryGB:F1} GB |");
        md.AppendLine($"| .NET Version | {report.SystemInfo.DotNetVersion} |");
        md.AppendLine();

        if (report.BabeMakroInfo != null)
        {
            md.AppendLine("## BabeMakro Process Information");
            md.AppendLine();
            md.AppendLine("| Property | Value |");
            md.AppendLine("|----------|-------|");
            md.AppendLine($"| Process ID | {report.BabeMakroInfo.ProcessId} |");
            md.AppendLine($"| Working Set | {report.BabeMakroInfo.WorkingSetMB} MB |");
            md.AppendLine($"| Handle Count | {report.BabeMakroInfo.HandleCount} |");
            md.AppendLine($"| Thread Count | {report.BabeMakroInfo.ThreadCount} |");
            md.AppendLine($"| Responding | {report.BabeMakroInfo.IsResponding} |");
            md.AppendLine();
        }

        md.AppendLine("## Test Results");
        md.AppendLine();
        md.AppendLine("| Test | Status | Score | Notes |");
        md.AppendLine("|------|--------|-------|-------|");

        foreach (var result in report.TestResults)
        {
            var statusEmoji = result.Status switch
            {
                "Pass" => "✅",
                "Warning" => "⚠️",
                "Fail" => "❌",
                _ => "❓"
            };

            md.AppendLine($"| {result.TestName} | {statusEmoji} {result.Status} | {result.Score} | {result.Notes} |");
        }

        md.AppendLine();
        md.AppendLine("## Recommendations");
        md.AppendLine();

        foreach (var rec in report.Recommendations)
        {
            md.AppendLine($"- {rec}");
        }

        return md.ToString();
    }
}

// Data models for the performance report
public class PerformanceReport
{
    public DateTime TestDate { get; set; }
    public SystemInfo SystemInfo { get; set; } = new();
    public BabeMakroInfo? BabeMakroInfo { get; set; }
    public List<TestResult> TestResults { get; set; } = new();
    public int OverallScore { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

public class SystemInfo
{
    public string OperatingSystem { get; set; } = "";
    public int ProcessorCount { get; set; }
    public double TotalMemoryGB { get; set; }
    public string DotNetVersion { get; set; } = "";
    public string MachineName { get; set; } = "";
    public bool Is64BitProcess { get; set; }
    public long WorkingSet { get; set; }
    public int SystemPageSize { get; set; }
}

public class BabeMakroInfo
{
    public int ProcessId { get; set; }
    public DateTime StartTime { get; set; }
    public long WorkingSetMB { get; set; }
    public int HandleCount { get; set; }
    public int ThreadCount { get; set; }
    public TimeSpan ProcessorTime { get; set; }
    public bool IsResponding { get; set; }
}

public class TestResult
{
    public string TestName { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = ""; // Pass, Warning, Fail, Skipped
    public int Score { get; set; } // 0-100
    public string Notes { get; set; } = "";
    public Dictionary<string, object> Metrics { get; set; } = new();
}