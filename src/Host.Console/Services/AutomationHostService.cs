using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PixelAutomation.Host.Console.Services;

public class AutomationHostService : BackgroundService
{
    private readonly ILogger<AutomationHostService> _logger;
    private readonly WorkerManager _workerManager;

    public AutomationHostService(
        ILogger<AutomationHostService> logger,
        WorkerManager workerManager)
    {
        _logger = logger;
        _workerManager = workerManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Automation Host Service starting");

        try
        {
            await _workerManager.StartAsync();

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Automation Host Service stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Automation Host Service");
            throw;
        }
        finally
        {
            _workerManager.StopAll();
        }
    }
}