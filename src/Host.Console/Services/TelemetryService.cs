using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PixelAutomation.Host.Console.Services;

public class TelemetryService : BackgroundService
{
    private readonly ILogger<TelemetryService> _logger;
    private readonly WorkerManager _workerManager;
    private readonly TimeSpan _reportInterval = TimeSpan.FromSeconds(5);

    public TelemetryService(
        ILogger<TelemetryService> logger,
        WorkerManager workerManager)
    {
        _logger = logger;
        _workerManager = workerManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telemetry service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_reportInterval, stoppingToken);
                
                var telemetry = _workerManager.GetTelemetry();
                
                if (telemetry.Any())
                {
                    var report = new StringBuilder();
                    report.AppendLine("\n=== TELEMETRY ===");
                    
                    foreach (var (workerId, stats) in telemetry)
                    {
                        if (stats is WorkerStats workerStats)
                        {
                            report.AppendLine($"{workerId}:");
                            report.AppendLine($"  FPS: {workerStats.Fps:F1}");
                            report.AppendLine($"  Clicks: {workerStats.ClickCount}");
                            report.AppendLine($"  Triggers: {workerStats.ProbeTriggeredCount}");
                            report.AppendLine($"  Backend: {workerStats.CaptureBackend}");
                            
                            if (workerStats.SchedulerStats != null)
                            {
                                report.AppendLine($"  Scheduler:");
                                report.AppendLine($"    Total: {workerStats.SchedulerStats.TotalExecutions}");
                                report.AppendLine($"    Dropped: {workerStats.SchedulerStats.DroppedActions}");
                                report.AppendLine($"    Periodic: {workerStats.SchedulerStats.ActivePeriodicTasks}");
                                report.AppendLine($"    Queued: {workerStats.SchedulerStats.QueuedEvents}");
                            }
                        }
                    }
                    
                    report.AppendLine("================");
                    _logger.LogInformation(report.ToString());
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telemetry reporting error");
            }
        }

        _logger.LogInformation("Telemetry service stopped");
    }
}