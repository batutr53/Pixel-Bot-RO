using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows.Threading;

namespace PixelAutomation.Tool.Overlay.WPF.Services;

/// <summary>
/// Consolidated timer manager that replaces multiple individual DispatcherTimers with a single high-frequency timer
/// and task-based scheduling. This significantly reduces CPU overhead from context switching between multiple timers.
/// </summary>
public class MasterTimerManager : IDisposable
{
    private readonly DispatcherTimer _masterTimer;
    private readonly ConcurrentDictionary<string, TimerTask> _tasks = new();
    private readonly object _lockObject = new();
    private DateTime _lastTick = DateTime.Now;
    private bool _isRunning = false;
    private bool _disposed = false;

    /// <summary>
    /// Statistics for performance monitoring
    /// </summary>
    public class PerformanceStats
    {
        public int TotalTasks { get; set; }
        public int EnabledTasks { get; set; }
        public int ExecutedTasksLastCycle { get; set; }
        public double AverageExecutionTimeMs { get; set; }
        public double MaxExecutionTimeMs { get; set; }
        public long TotalExecutions { get; set; }
    }

    /// <summary>
    /// Represents a scheduled task within the master timer
    /// </summary>
    public class TimerTask
    {
        public string Name { get; set; } = "";
        public TimeSpan Interval { get; set; }
        public DateTime NextExecution { get; set; }
        public Action ExecuteAction { get; set; } = () => { };
        public bool IsEnabled { get; set; } = true;
        public int Priority { get; set; } = 0; // Higher numbers = higher priority
        public bool IsOneShot { get; set; } = false; // If true, task runs once and is removed
        public long ExecutionCount { get; set; } = 0;
        public double LastExecutionTimeMs { get; set; } = 0;
        public double AverageExecutionTimeMs { get; set; } = 0;
        public double MaxExecutionTimeMs { get; set; } = 0;
    }

    /// <summary>
    /// Initializes a new instance of MasterTimerManager
    /// </summary>
    /// <param name="masterInterval">The master timer interval (default: 25ms for 40Hz)</param>
    public MasterTimerManager(TimeSpan? masterInterval = null)
    {
        _masterTimer = new DispatcherTimer
        {
            Interval = masterInterval ?? TimeSpan.FromMilliseconds(25) // 40Hz by default
        };
        _masterTimer.Tick += OnMasterTimerTick;
    }

    /// <summary>
    /// Starts the master timer
    /// </summary>
    public void Start()
    {
        lock (_lockObject)
        {
            if (!_isRunning && !_disposed)
            {
                _isRunning = true;
                _lastTick = DateTime.Now;
                _masterTimer.Start();
                Debug.WriteLine($"[MasterTimer] Started with {_masterTimer.Interval.TotalMilliseconds}ms interval");
            }
        }
    }

    /// <summary>
    /// Stops the master timer
    /// </summary>
    public void Stop()
    {
        lock (_lockObject)
        {
            if (_isRunning)
            {
                _isRunning = false;
                _masterTimer.Stop();
                Debug.WriteLine("[MasterTimer] Stopped");
            }
        }
    }

    /// <summary>
    /// Adds or updates a timer task
    /// </summary>
    /// <param name="name">Unique task name</param>
    /// <param name="interval">Execution interval</param>
    /// <param name="action">Action to execute</param>
    /// <param name="enabled">Whether task is enabled</param>
    /// <param name="priority">Task priority (higher = more important)</param>
    /// <param name="isOneShot">If true, task runs once and is removed</param>
    public void AddOrUpdateTask(string name, TimeSpan interval, Action action, bool enabled = true, int priority = 0, bool isOneShot = false)
    {
        lock (_lockObject)
        {
            if (_disposed) return;

            var now = DateTime.Now;
            var task = new TimerTask
            {
                Name = name,
                Interval = interval,
                NextExecution = now.Add(interval),
                ExecuteAction = action,
                IsEnabled = enabled,
                Priority = priority,
                IsOneShot = isOneShot
            };

            _tasks.AddOrUpdate(name, task, (key, existingTask) =>
            {
                // Preserve execution statistics when updating
                task.ExecutionCount = existingTask.ExecutionCount;
                task.AverageExecutionTimeMs = existingTask.AverageExecutionTimeMs;
                task.MaxExecutionTimeMs = existingTask.MaxExecutionTimeMs;
                return task;
            });

            Debug.WriteLine($"[MasterTimer] Task '{name}' {(enabled ? "ADDED" : "DISABLED")} - Interval: {interval.TotalMilliseconds}ms, Priority: {priority}");
        }
    }

    /// <summary>
    /// Removes a timer task
    /// </summary>
    /// <param name="name">Task name to remove</param>
    public void RemoveTask(string name)
    {
        lock (_lockObject)
        {
            if (_tasks.TryRemove(name, out var removedTask))
            {
                Debug.WriteLine($"[MasterTimer] Task '{name}' REMOVED");
            }
        }
    }

    /// <summary>
    /// Enables or disables a timer task
    /// </summary>
    /// <param name="name">Task name</param>
    /// <param name="enabled">Enabled state</param>
    public void SetTaskEnabled(string name, bool enabled)
    {
        lock (_lockObject)
        {
            if (_tasks.TryGetValue(name, out var task))
            {
                task.IsEnabled = enabled;
                if (enabled)
                {
                    // Reset next execution time when re-enabling
                    task.NextExecution = DateTime.Now.Add(task.Interval);
                }
                Debug.WriteLine($"[MasterTimer] Task '{name}' {(enabled ? "ENABLED" : "DISABLED")}");
            }
        }
    }

    /// <summary>
    /// Updates the interval for an existing task
    /// </summary>
    /// <param name="name">Task name</param>
    /// <param name="interval">New interval</param>
    public void UpdateTaskInterval(string name, TimeSpan interval)
    {
        lock (_lockObject)
        {
            if (_tasks.TryGetValue(name, out var task))
            {
                task.Interval = interval;
                task.NextExecution = DateTime.Now.Add(interval);
                Debug.WriteLine($"[MasterTimer] Task '{name}' interval updated to {interval.TotalMilliseconds}ms");
            }
        }
    }

    /// <summary>
    /// Gets current performance statistics
    /// </summary>
    public PerformanceStats GetPerformanceStats()
    {
        lock (_lockObject)
        {
            var enabledTasks = 0;
            var totalExecutions = 0L;
            var totalAvgTime = 0.0;
            var maxTime = 0.0;

            foreach (var task in _tasks.Values)
            {
                if (task.IsEnabled) enabledTasks++;
                totalExecutions += task.ExecutionCount;
                totalAvgTime += task.AverageExecutionTimeMs;
                if (task.MaxExecutionTimeMs > maxTime) maxTime = task.MaxExecutionTimeMs;
            }

            return new PerformanceStats
            {
                TotalTasks = _tasks.Count,
                EnabledTasks = enabledTasks,
                TotalExecutions = totalExecutions,
                AverageExecutionTimeMs = _tasks.Count > 0 ? totalAvgTime / _tasks.Count : 0,
                MaxExecutionTimeMs = maxTime
            };
        }
    }

    /// <summary>
    /// Main timer tick handler - executes scheduled tasks
    /// </summary>
    private void OnMasterTimerTick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        var executedTasks = 0;
        var tasksToExecute = new List<TimerTask>();
        var tasksToRemove = new List<string>();

        // Collect tasks that need execution (minimize lock time)
        lock (_lockObject)
        {
            if (_disposed) return;

            foreach (var kvp in _tasks)
            {
                var task = kvp.Value;
                if (task.IsEnabled && now >= task.NextExecution)
                {
                    tasksToExecute.Add(task);
                }
            }

            // Sort by priority (higher priority first)
            tasksToExecute.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        // Execute tasks outside of lock to prevent blocking
        foreach (var task in tasksToExecute)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                task.ExecuteAction();
                stopwatch.Stop();

                // Update execution statistics
                lock (_lockObject)
                {
                    task.ExecutionCount++;
                    task.LastExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                    
                    // Update average execution time using running average
                    if (task.ExecutionCount == 1)
                    {
                        task.AverageExecutionTimeMs = task.LastExecutionTimeMs;
                    }
                    else
                    {
                        task.AverageExecutionTimeMs = (task.AverageExecutionTimeMs * (task.ExecutionCount - 1) + task.LastExecutionTimeMs) / task.ExecutionCount;
                    }

                    if (task.LastExecutionTimeMs > task.MaxExecutionTimeMs)
                    {
                        task.MaxExecutionTimeMs = task.LastExecutionTimeMs;
                    }

                    // Schedule next execution
                    if (task.IsOneShot)
                    {
                        tasksToRemove.Add(task.Name);
                    }
                    else
                    {
                        task.NextExecution = now.Add(task.Interval);
                    }
                }

                executedTasks++;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MasterTimer] ERROR in task '{task.Name}': {ex.Message}");
            }
        }

        // Remove one-shot tasks
        lock (_lockObject)
        {
            foreach (var taskName in tasksToRemove)
            {
                _tasks.TryRemove(taskName, out _);
                Debug.WriteLine($"[MasterTimer] One-shot task '{taskName}' completed and removed");
            }
        }

        _lastTick = now;

        // Log performance every 1000 ticks (about every 25 seconds at 40Hz)
        if ((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) % 25000 < _masterTimer.Interval.TotalMilliseconds)
        {
            var stats = GetPerformanceStats();
            Debug.WriteLine($"[MasterTimer] Stats - Tasks: {stats.EnabledTasks}/{stats.TotalTasks}, Avg: {stats.AverageExecutionTimeMs:F2}ms, Max: {stats.MaxExecutionTimeMs:F2}ms");
        }
    }

    /// <summary>
    /// Disposes the master timer manager
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lockObject)
        {
            _disposed = true;
            Stop();
            _tasks.Clear();
            _masterTimer?.Stop();
            Debug.WriteLine("[MasterTimer] Disposed");
        }
    }
}