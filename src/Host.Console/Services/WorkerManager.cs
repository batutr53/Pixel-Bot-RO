using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PixelAutomation.Core.Interfaces;
using PixelAutomation.Core.Models;
using PixelAutomation.Core.Services;
using PixelAutomation.Capture.Win;
using PixelAutomation.Capture.Win.Providers;
using Vanara.PInvoke;

namespace PixelAutomation.Host.Console.Services;

public class WorkerManager : IDisposable
{
    private readonly ILogger<WorkerManager> _logger;
    private readonly ProfileConfig _profile;
    private readonly IEventBus _eventBus;
    private readonly ConcurrentDictionary<string, WindowWorker> _workers = new();
    private readonly CaptureFactory _captureFactory = new();
    private readonly CancellationTokenSource _shutdownTokenSource = new();

    public WorkerManager(ILogger<WorkerManager> logger, ProfileConfig profile, IEventBus eventBus)
    {
        _logger = logger;
        _profile = profile;
        _eventBus = eventBus;
    }

    public async Task StartAsync()
    {
        _logger.LogInformation($"Starting workers for {_profile.Windows.Count} window configurations");

        foreach (var windowConfig in _profile.Windows)
        {
            var hwnd = await FindWindowAsync(windowConfig);
            if (hwnd != IntPtr.Zero)
            {
                await StartWorkerAsync(hwnd, windowConfig);
            }
            else
            {
                _logger.LogWarning($"Window not found: {windowConfig.TitleRegex ?? windowConfig.ProcessName}");
            }
        }

        _logger.LogInformation($"Started {_workers.Count} workers");
    }

    private async Task<IntPtr> FindWindowAsync(WindowTarget config)
    {
        if (config.Hwnd.HasValue && config.Hwnd.Value != IntPtr.Zero)
            return config.Hwnd.Value;

        return await Task.Run(() =>
        {
            IntPtr foundHwnd = IntPtr.Zero;
            
            User32.EnumWindows((hwnd, lParam) =>
            {
                if (!User32.IsWindowVisible(hwnd))
                    return true;

                var titleLength = User32.GetWindowTextLength(hwnd);
                if (titleLength == 0)
                    return true;

                var sb = new System.Text.StringBuilder(256);
                User32.GetWindowText(hwnd, sb, sb.Capacity);
                var title = sb.ToString();
                
                if (!string.IsNullOrEmpty(config.TitleRegex))
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(title, config.TitleRegex))
                    {
                        foundHwnd = (IntPtr)hwnd;
                        return false;
                    }
                }
                
                if (!string.IsNullOrEmpty(config.ProcessName))
                {
                    User32.GetWindowThreadProcessId(hwnd, out var processId);
                    try
                    {
                        var process = System.Diagnostics.Process.GetProcessById((int)processId);
                        if (process.ProcessName.Equals(config.ProcessName, StringComparison.OrdinalIgnoreCase))
                        {
                            foundHwnd = (IntPtr)hwnd;
                            return false;
                        }
                    }
                    catch { }
                }
                
                return true;
            }, IntPtr.Zero);
            
            return foundHwnd;
        });
    }

    private async Task StartWorkerAsync(IntPtr hwnd, WindowTarget config)
    {
        var workerId = $"Worker_{hwnd:X}";
        
        if (_workers.ContainsKey(workerId))
        {
            _logger.LogWarning($"Worker already exists for HWND {hwnd:X}");
            return;
        }

        var captureBackend = CreateCaptureBackend();
        var clickProvider = CreateClickProvider();
        
        if (!await captureBackend.InitializeAsync(hwnd))
        {
            _logger.LogError($"Failed to initialize capture for HWND {hwnd:X}");
            captureBackend.Dispose();
            return;
        }

        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => { });
        var scheduler = new ActionScheduler(loggerFactory.CreateLogger<ActionScheduler>());
        
        var worker = new WindowWorker(
            workerId,
            hwnd,
            config,
            captureBackend,
            clickProvider,
            scheduler,
            _eventBus,
            loggerFactory.CreateLogger<WindowWorker>(),
            _profile.Global);

        _workers[workerId] = worker;
        
        _ = Task.Run(() => worker.RunAsync(_shutdownTokenSource.Token));
        
        _logger.LogInformation($"Started worker for HWND {hwnd:X} ({config.TitleRegex ?? config.ProcessName})");
    }

    private ICaptureBackend CreateCaptureBackend()
    {
        var captureType = _profile.Global.CaptureMode.ToUpperInvariant() switch
        {
            "WGC" => CaptureBackendType.WindowsGraphicsCapture,
            "PRINT" => CaptureBackendType.PrintWindow,
            "GPIXEL" => CaptureBackendType.GetPixel,
            _ => CaptureBackendType.WindowsGraphicsCapture
        };

        return _captureFactory.CreateWithFallback(captureType);
    }

    private IClickProvider CreateClickProvider()
    {
        IClickProvider provider = _profile.Global.ClickMode.ToLowerInvariant() switch
        {
            "cursor-jump" => new SendInputClickProvider(false),
            "cursor-return" => new SendInputClickProvider(true),
            _ => new MessageClickProvider()
        };

        if (provider is MessageClickProvider messageProvider)
        {
            messageProvider.SetFallbackProvider(new SendInputClickProvider(true));
        }

        return provider;
    }

    public void StopAll()
    {
        _logger.LogInformation("Stopping all workers...");
        _shutdownTokenSource.Cancel();
        
        foreach (var worker in _workers.Values)
        {
            worker.Dispose();
        }
        
        _workers.Clear();
    }

    public Dictionary<string, object> GetTelemetry()
    {
        var telemetry = new Dictionary<string, object>();
        
        foreach (var (id, worker) in _workers)
        {
            telemetry[id] = worker.GetStats();
        }
        
        return telemetry;
    }

    public void Dispose()
    {
        StopAll();
        _shutdownTokenSource.Dispose();
    }
}