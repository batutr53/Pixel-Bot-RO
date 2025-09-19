using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PixelAutomation.Tool.Overlay.WPF.Services;

/// <summary>
/// High-resolution timer that provides much better precision than DispatcherTimer or Task.Delay
/// Uses multimedia timer APIs for 1ms resolution
/// </summary>
public class HighResolutionTimer : IDisposable
{
    #region Native API Imports

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern uint timeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern uint timeEndPeriod(uint uPeriod);

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern uint timeSetEvent(uint uDelay, uint uResolution,
        TimerCallback lpTimeProc, IntPtr dwUser, uint fuEvent);

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern uint timeKillEvent(uint uTimerID);

    [DllImport("kernel32.dll")]
    private static extern long GetTickCount64();

    private delegate void TimerCallback(uint uTimerID, uint uMsg, IntPtr dwUser, IntPtr dw1, IntPtr dw2);

    private const uint TIME_PERIODIC = 1;
    private const uint TIME_ONESHOT = 0;

    #endregion

    private uint _timerId;
    private readonly uint _interval;
    private readonly TimerCallback _timerCallback;
    private volatile bool _disposed;
    private volatile bool _running;
    private readonly object _lockObject = new();

    public event Action? Elapsed;

    /// <summary>
    /// Statistics for monitoring timer precision
    /// </summary>
    public class TimerStats
    {
        public double AverageInterval { get; set; }
        public double MinInterval { get; set; }
        public double MaxInterval { get; set; }
        public double StandardDeviation { get; set; }
        public long TickCount { get; set; }
        public double JitterPercent { get; set; }
        public double AccuracyPercent { get; set; }
    }

    private readonly Stopwatch _statsStopwatch = new();
    private readonly Queue<double> _intervalMeasurements = new();
    private long _lastTickTime;
    private const int MAX_MEASUREMENTS = 1000;

    public TimerStats GetStatistics()
    {
        lock (_lockObject)
        {
            if (_intervalMeasurements.Count == 0)
                return new TimerStats();

            var measurements = _intervalMeasurements.ToArray();
            var avg = measurements.Average();
            var min = measurements.Min();
            var max = measurements.Max();
            var stdDev = Math.Sqrt(measurements.Select(x => Math.Pow(x - avg, 2)).Average());

            return new TimerStats
            {
                AverageInterval = avg,
                MinInterval = min,
                MaxInterval = max,
                StandardDeviation = stdDev,
                TickCount = measurements.Length,
                JitterPercent = (stdDev / _interval) * 100,
                AccuracyPercent = Math.Abs(avg - _interval) / _interval * 100
            };
        }
    }

    public HighResolutionTimer(uint intervalMs)
    {
        _interval = intervalMs;
        _timerCallback = TimerProc;

        // Set system timer resolution to 1ms for better precision
        timeBeginPeriod(1);
        _statsStopwatch.Start();
        _lastTickTime = GetTickCount64();
    }

    public bool Start()
    {
        lock (_lockObject)
        {
            if (_running || _disposed)
                return false;

            _timerId = timeSetEvent(_interval, 1, _timerCallback, IntPtr.Zero, TIME_PERIODIC);
            if (_timerId != 0)
            {
                _running = true;
                return true;
            }
            return false;
        }
    }

    public void Stop()
    {
        lock (_lockObject)
        {
            if (!_running || _disposed)
                return;

            if (_timerId != 0)
            {
                timeKillEvent(_timerId);
                _timerId = 0;
            }
            _running = false;
        }
    }

    private void TimerProc(uint uTimerID, uint uMsg, IntPtr dwUser, IntPtr dw1, IntPtr dw2)
    {
        if (_disposed)
            return;

        try
        {
            // Measure interval precision
            var currentTime = GetTickCount64();
            var actualInterval = currentTime - _lastTickTime;
            _lastTickTime = currentTime;

            lock (_lockObject)
            {
                _intervalMeasurements.Enqueue(actualInterval);
                if (_intervalMeasurements.Count > MAX_MEASUREMENTS)
                    _intervalMeasurements.Dequeue();
            }

            // Invoke event handler
            Elapsed?.Invoke();
        }
        catch (Exception ex)
        {
            // Log error but don't let it crash the timer
            Console.WriteLine($"HighResolutionTimer error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
        timeEndPeriod(1);
        GC.SuppressFinalize(this);
    }

    ~HighResolutionTimer()
    {
        Dispose();
    }
}

/// <summary>
/// High-resolution replacement for MasterTimerManager using multimedia timers
/// Provides 1ms precision instead of ~15ms from DispatcherTimer
/// </summary>
public class HighResolutionMasterTimer : IDisposable
{
    private readonly HighResolutionTimer _highResTimer;
    private readonly ConcurrentDictionary<string, TimerTask> _tasks = new();
    private readonly object _lockObject = new();
    private volatile bool _disposed;
    private long _tickCount;

    public class TimerTask
    {
        public string Name { get; set; } = "";
        public TimeSpan Interval { get; set; }
        public DateTime NextExecution { get; set; }
        public Action ExecuteAction { get; set; } = () => { };
        public bool IsEnabled { get; set; } = true;
        public int Priority { get; set; } = 0;
        public long ExecutionCount { get; set; } = 0;
        public double LastExecutionTimeMs { get; set; } = 0;
    }

    public HighResolutionMasterTimer(uint baseIntervalMs = 8) // 8ms = ~120Hz
    {
        _highResTimer = new HighResolutionTimer(baseIntervalMs);
        _highResTimer.Elapsed += OnTimerElapsed;
    }

    public bool Start()
    {
        return _highResTimer.Start();
    }

    public void Stop()
    {
        _highResTimer.Stop();
    }

    public void AddOrUpdateTask(string name, TimeSpan interval, Action action, int priority = 0)
    {
        var task = new TimerTask
        {
            Name = name,
            Interval = interval,
            ExecuteAction = action,
            Priority = priority,
            NextExecution = DateTime.Now.Add(interval),
            IsEnabled = true
        };

        _tasks.AddOrUpdate(name, task, (key, existing) => task);
    }

    public void SetTaskEnabled(string name, bool enabled)
    {
        if (_tasks.TryGetValue(name, out var task))
        {
            task.IsEnabled = enabled;
        }
    }

    public void RemoveTask(string name)
    {
        _tasks.TryRemove(name, out _);
    }

    private void OnTimerElapsed()
    {
        if (_disposed)
            return;

        _tickCount++;
        var now = DateTime.Now;

        // Execute tasks that are due
        var tasksToExecute = _tasks.Values
            .Where(t => t.IsEnabled && now >= t.NextExecution)
            .OrderByDescending(t => t.Priority)
            .ToList();

        foreach (var task in tasksToExecute)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                task.ExecuteAction();
                sw.Stop();

                task.LastExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
                task.ExecutionCount++;
                task.NextExecution = now.Add(task.Interval);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing task {task.Name}: {ex.Message}");
            }
        }
    }

    public HighResolutionTimer.TimerStats GetTimerStats()
    {
        return _highResTimer.GetStatistics();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _highResTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}