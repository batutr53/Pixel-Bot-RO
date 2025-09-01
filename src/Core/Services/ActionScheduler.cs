using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace PixelAutomation.Core.Services;

public interface IActionScheduler : IDisposable
{
    void SchedulePeriodic(string id, Func<Task> action, TimeSpan period, int priority = 0);
    void ScheduleEvent(string id, Func<Task> action, int priority = 1);
    void CancelPeriodic(string id);
    void CancelAll();
    ActionSchedulerStats GetStats();
}

public class ActionScheduler : IActionScheduler
{
    private readonly ILogger<ActionScheduler> _logger;
    private readonly ConcurrentDictionary<string, PeriodicTask> _periodicTasks = new();
    private readonly PriorityQueue<EventAction, int> _eventQueue = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastExecutions = new();
    private readonly ConcurrentDictionary<string, int> _executionCounts = new();
    private readonly SemaphoreSlim _eventSemaphore = new(0);
    private readonly CancellationTokenSource _shutdownTokenSource = new();
    private readonly Task _eventProcessor;
    private readonly object _queueLock = new();
    private long _totalExecutions;
    private long _droppedActions;

    public ActionScheduler(ILogger<ActionScheduler> logger)
    {
        _logger = logger;
        _eventProcessor = Task.Run(ProcessEventQueueAsync);
    }

    public void SchedulePeriodic(string id, Func<Task> action, TimeSpan period, int priority = 0)
    {
        if (_periodicTasks.ContainsKey(id))
        {
            CancelPeriodic(id);
        }

        var task = new PeriodicTask
        {
            Id = id,
            Action = action,
            Period = period,
            Priority = priority,
            CancellationTokenSource = new CancellationTokenSource()
        };

        _periodicTasks[id] = task;
        task.RunTask = Task.Run(() => RunPeriodicAsync(task));
    }

    private async Task RunPeriodicAsync(PeriodicTask task)
    {
        var stopwatch = new Stopwatch();
        var nextRun = DateTime.UtcNow;

        while (!task.CancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                stopwatch.Restart();
                nextRun = nextRun.Add(task.Period);

                await task.Action();
                
                Interlocked.Increment(ref _totalExecutions);
                _executionCounts.AddOrUpdate(task.Id, 1, (_, count) => count + 1);
                _lastExecutions[task.Id] = DateTime.UtcNow;

                stopwatch.Stop();
                
                var delay = nextRun - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, task.CancellationTokenSource.Token);
                }
                else if (delay < TimeSpan.FromMilliseconds(-100))
                {
                    nextRun = DateTime.UtcNow;
                    _logger.LogWarning($"Periodic task {task.Id} is running behind schedule");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in periodic task {task.Id}");
            }
        }
    }

    public void ScheduleEvent(string id, Func<Task> action, int priority = 1)
    {
        lock (_queueLock)
        {
            if (ShouldCoalesce(id))
            {
                Interlocked.Increment(ref _droppedActions);
                return;
            }

            var eventAction = new EventAction
            {
                Id = id,
                Action = action,
                Priority = priority,
                Timestamp = DateTime.UtcNow
            };

            _eventQueue.Enqueue(eventAction, -priority);
            _lastExecutions[id] = DateTime.UtcNow;
        }
        
        _eventSemaphore.Release();
    }

    private bool ShouldCoalesce(string id)
    {
        if (_lastExecutions.TryGetValue(id, out var lastExecution))
        {
            var timeSinceLastExecution = DateTime.UtcNow - lastExecution;
            if (timeSinceLastExecution < TimeSpan.FromMilliseconds(20))
            {
                return true;
            }
        }
        return false;
    }

    private async Task ProcessEventQueueAsync()
    {
        while (!_shutdownTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                await _eventSemaphore.WaitAsync(_shutdownTokenSource.Token);

                EventAction? eventAction = null;
                lock (_queueLock)
                {
                    if (_eventQueue.TryDequeue(out var item, out _))
                    {
                        eventAction = item;
                    }
                }

                if (eventAction != null)
                {
                    try
                    {
                        await eventAction.Action();
                        Interlocked.Increment(ref _totalExecutions);
                        _executionCounts.AddOrUpdate(eventAction.Id, 1, (_, count) => count + 1);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error executing event action {eventAction.Id}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void CancelPeriodic(string id)
    {
        if (_periodicTasks.TryRemove(id, out var task))
        {
            task.CancellationTokenSource.Cancel();
            task.CancellationTokenSource.Dispose();
        }
    }

    public void CancelAll()
    {
        foreach (var task in _periodicTasks.Values)
        {
            task.CancellationTokenSource.Cancel();
            task.CancellationTokenSource.Dispose();
        }
        _periodicTasks.Clear();
        
        lock (_queueLock)
        {
            _eventQueue.Clear();
        }
    }

    public ActionSchedulerStats GetStats()
    {
        return new ActionSchedulerStats
        {
            TotalExecutions = _totalExecutions,
            DroppedActions = _droppedActions,
            ActivePeriodicTasks = _periodicTasks.Count,
            QueuedEvents = _eventQueue.Count,
            ExecutionCounts = new Dictionary<string, int>(_executionCounts)
        };
    }

    public void Dispose()
    {
        _shutdownTokenSource.Cancel();
        CancelAll();
        _eventProcessor.Wait(TimeSpan.FromSeconds(2));
        _shutdownTokenSource.Dispose();
        _eventSemaphore.Dispose();
    }

    private class PeriodicTask
    {
        public string Id { get; init; } = "";
        public Func<Task> Action { get; init; } = () => Task.CompletedTask;
        public TimeSpan Period { get; init; }
        public int Priority { get; init; }
        public CancellationTokenSource CancellationTokenSource { get; init; } = new();
        public Task? RunTask { get; set; }
    }

    private class EventAction
    {
        public string Id { get; init; } = "";
        public Func<Task> Action { get; init; } = () => Task.CompletedTask;
        public int Priority { get; init; }
        public DateTime Timestamp { get; init; }
    }
}

public class ActionSchedulerStats
{
    public long TotalExecutions { get; init; }
    public long DroppedActions { get; init; }
    public int ActivePeriodicTasks { get; init; }
    public int QueuedEvents { get; init; }
    public Dictionary<string, int> ExecutionCounts { get; init; } = new();
}