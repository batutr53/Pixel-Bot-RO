using System.Diagnostics;
using System.Drawing;
using Microsoft.Extensions.Logging;
using PixelAutomation.Core.Interfaces;
using PixelAutomation.Core.Models;
using PixelAutomation.Core.Services;

namespace PixelAutomation.Host.Console.Services;

public class WindowWorker : IDisposable
{
    private readonly string _workerId;
    private readonly IntPtr _hwnd;
    private readonly WindowTarget _config;
    private readonly ICaptureBackend _captureBackend;
    private readonly IClickProvider _clickProvider;
    private readonly IActionScheduler _scheduler;
    private readonly IEventBus _eventBus;
    private readonly ILogger<WindowWorker> _logger;
    private readonly GlobalConfig _globalConfig;
    
    private readonly Dictionary<string, ProbeState> _probeStates = new();
    private readonly Stopwatch _fpsStopwatch = new();
    private long _frameCount;
    private long _clickCount;
    private long _probeTriggeredCount;
    private double _currentFps;

    public WindowWorker(
        string workerId,
        IntPtr hwnd,
        WindowTarget config,
        ICaptureBackend captureBackend,
        IClickProvider clickProvider,
        IActionScheduler scheduler,
        IEventBus eventBus,
        ILogger<WindowWorker> logger,
        GlobalConfig globalConfig)
    {
        _workerId = workerId;
        _hwnd = hwnd;
        _config = config;
        _captureBackend = captureBackend;
        _clickProvider = clickProvider;
        _scheduler = scheduler;
        _eventBus = eventBus;
        _logger = logger;
        _globalConfig = globalConfig;

        InitializeProbes();
    }

    private void InitializeProbes()
    {
        foreach (var probeConfig in _config.Probes)
        {
            _probeStates[probeConfig.Name] = new ProbeState
            {
                Config = probeConfig,
                LastTriggered = DateTime.MinValue
            };
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Worker {_workerId} starting");
        
        SetupPeriodicClicks();
        
        var captureDelay = TimeSpan.FromMilliseconds(1000.0 / _globalConfig.DefaultHz);
        _fpsStopwatch.Start();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var captureStart = Stopwatch.GetTimestamp();
                
                var bitmap = await _captureBackend.CaptureAsync();
                if (bitmap != null)
                {
                    ProcessFrame(bitmap);
                    bitmap.Dispose();
                    _frameCount++;
                }

                UpdateFps();

                var elapsed = Stopwatch.GetElapsedTime(captureStart);
                var delay = captureDelay - elapsed;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Worker {_workerId} frame processing error");
                await Task.Delay(100, cancellationToken);
            }
        }

        _logger.LogInformation($"Worker {_workerId} stopped");
    }

    private void SetupPeriodicClicks()
    {
        foreach (var periodic in _config.PeriodicClicks.Where(p => p.Enabled))
        {
            var period = periodic.PeriodMs.HasValue 
                ? TimeSpan.FromMilliseconds(periodic.PeriodMs.Value)
                : TimeSpan.FromSeconds(periodic.PeriodSec ?? 1.0);

            _scheduler.SchedulePeriodic(
                $"{_workerId}_{periodic.Name}",
                async () => await ExecuteClickAsync(new Point(periodic.X, periodic.Y), periodic.Name),
                period);

            _logger.LogInformation($"Scheduled periodic click '{periodic.Name}' every {period.TotalMilliseconds}ms");
        }
    }

    private void ProcessFrame(Bitmap bitmap)
    {
        var probeResults = new Dictionary<string, ProbeResult>();

        foreach (var (probeName, probeState) in _probeStates)
        {
            var result = EvaluateProbe(probeState.Config, bitmap, probeState.LastResult);
            probeResults[probeName] = result;

            if (result.Triggered && ShouldTrigger(probeState, result))
            {
                probeState.LastTriggered = DateTime.UtcNow;
                probeState.LastResult = result;
                _probeTriggeredCount++;

                _eventBus.Publish(new ProbeTriggeredEvent
                {
                    WindowId = _workerId,
                    ProbeName = probeName,
                    IsEdge = probeState.Config.Mode == "edge",
                    Timestamp = DateTime.UtcNow
                });

                ProcessEvents(probeName);
            }
            
            probeState.LastResult = result;
        }
    }

    private ProbeResult EvaluateProbe(ProbeConfig config, Bitmap bitmap, ProbeResult? previousResult)
    {
        var location = new Point(config.X, config.Y);
        var currentColor = GetAverageColor(bitmap, location, config.Box);
        
        var refColor = Color.FromArgb(config.RefColor[0], config.RefColor[1], config.RefColor[2]);
        var distance = CalculateColorDistance(currentColor, refColor);

        bool triggered = false;
        string? edgeDirection = null;

        if (config.Mode == "level")
        {
            triggered = distance <= config.Tolerance;
        }
        else if (config.Mode == "edge" && previousResult != null)
        {
            var wasClose = previousResult.Distance <= config.Tolerance;
            var isClose = distance <= config.Tolerance;
            
            if (config.ToColor != null)
            {
                var toColor = Color.FromArgb(config.ToColor[0], config.ToColor[1], config.ToColor[2]);
                var toDistance = CalculateColorDistance(currentColor, toColor);
                
                if (wasClose && toDistance <= config.Tolerance)
                {
                    triggered = true;
                    edgeDirection = "transition";
                }
            }
            else
            {
                if (wasClose && !isClose)
                {
                    triggered = true;
                    edgeDirection = "falling";
                }
                else if (!wasClose && isClose)
                {
                    triggered = true;
                    edgeDirection = "rising";
                }
            }
        }

        return new ProbeResult
        {
            Triggered = triggered,
            CurrentColor = currentColor,
            Distance = distance,
            Timestamp = DateTime.UtcNow,
            EdgeDirection = edgeDirection
        };
    }

    private Color GetAverageColor(Bitmap bitmap, Point center, int boxSize)
    {
        int totalR = 0, totalG = 0, totalB = 0;
        int count = 0;

        int halfBox = boxSize / 2;
        int startX = Math.Max(0, center.X - halfBox);
        int startY = Math.Max(0, center.Y - halfBox);
        int endX = Math.Min(bitmap.Width - 1, center.X + halfBox);
        int endY = Math.Min(bitmap.Height - 1, center.Y + halfBox);

        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                totalR += pixel.R;
                totalG += pixel.G;
                totalB += pixel.B;
                count++;
            }
        }

        if (count == 0)
            return Color.Black;

        return Color.FromArgb(totalR / count, totalG / count, totalB / count);
    }

    private double CalculateColorDistance(Color c1, Color c2)
    {
        var dr = c1.R - c2.R;
        var dg = c1.G - c2.G;
        var db = c1.B - c2.B;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    private bool ShouldTrigger(ProbeState state, ProbeResult result)
    {
        if (state.Config.DebounceMs.HasValue)
        {
            var timeSinceLastTrigger = DateTime.UtcNow - state.LastTriggered;
            if (timeSinceLastTrigger.TotalMilliseconds < state.Config.DebounceMs.Value)
                return false;
        }

        return true;
    }

    private void ProcessEvents(string probeName)
    {
        foreach (var eventConfig in _config.Events.Where(e => e.When.Contains(probeName)))
        {
            _scheduler.ScheduleEvent(
                $"{_workerId}_{probeName}_{eventConfig.When}",
                async () => await ExecuteClickAsync(
                    new Point(eventConfig.Click.X, eventConfig.Click.Y),
                    $"Event:{eventConfig.When}"),
                eventConfig.Priority);
        }
    }

    private async Task ExecuteClickAsync(Point location, string source)
    {
        if (_globalConfig.DryRun)
        {
            _logger.LogInformation($"[DRY RUN] Click at ({location.X}, {location.Y}) from {source}");
        }
        else
        {
            var success = await _clickProvider.ClickAsync(_hwnd, location);
            if (success)
            {
                _clickCount++;
                _eventBus.Publish(new ClickExecutedEvent
                {
                    WindowId = _workerId,
                    Location = location,
                    Mode = _clickProvider.Mode,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
    }

    private void UpdateFps()
    {
        if (_fpsStopwatch.ElapsedMilliseconds >= 1000)
        {
            _currentFps = _frameCount / (_fpsStopwatch.ElapsedMilliseconds / 1000.0);
            _frameCount = 0;
            _fpsStopwatch.Restart();
        }
    }

    public WorkerStats GetStats()
    {
        return new WorkerStats
        {
            WorkerId = _workerId,
            Fps = _currentFps,
            ClickCount = _clickCount,
            ProbeTriggeredCount = _probeTriggeredCount,
            CaptureBackend = _captureBackend.Name,
            SchedulerStats = _scheduler.GetStats()
        };
    }

    public void Dispose()
    {
        _scheduler?.Dispose();
        _captureBackend?.Dispose();
    }

    private class ProbeState
    {
        public ProbeConfig Config { get; init; } = new();
        public ProbeResult? LastResult { get; set; }
        public DateTime LastTriggered { get; set; }
    }
}

public class WorkerStats
{
    public string WorkerId { get; init; } = "";
    public double Fps { get; init; }
    public long ClickCount { get; init; }
    public long ProbeTriggeredCount { get; init; }
    public string CaptureBackend { get; init; } = "";
    public ActionSchedulerStats? SchedulerStats { get; init; }
}