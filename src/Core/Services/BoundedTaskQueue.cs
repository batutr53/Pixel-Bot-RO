using System.Collections.Concurrent;
using System.Diagnostics;

namespace Core.Services;

/// <summary>
/// Priority levels for task execution
/// </summary>
public enum TaskPriority
{
    Critical = 10,    // HP/MP triggers, critical healing - highest priority
    High = 7,         // Regular HP/MP monitoring
    Normal = 5,       // Attack skills, buff/AC
    Low = 3,          // Background processing
    Maintenance = 1   // Statistics, logging - lowest priority
}

/// <summary>
/// Represents a queued task with priority and metadata
/// </summary>
public class QueuedTask
{
    public Func<Task> TaskFactory { get; }
    public TaskPriority Priority { get; }
    public string Name { get; }
    public DateTime EnqueuedAt { get; }
    public string Source { get; }

    public QueuedTask(Func<Task> taskFactory, TaskPriority priority, string name, string source = "Unknown")
    {
        TaskFactory = taskFactory;
        Priority = priority;
        Name = name;
        Source = source;
        EnqueuedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// A high-performance bounded task queue with priority support and intelligent task management
/// Prevents memory exhaustion by limiting queue sizes and implementing task dropping strategies
/// </summary>
public class BoundedTaskQueue : IDisposable
{
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly ConcurrentDictionary<TaskPriority, ConcurrentQueue<QueuedTask>> _priorityQueues;
    private readonly int _maxQueueSize;
    private readonly int _maxConcurrency;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Timer _processingTimer;
    private readonly Timer _statsTimer;
    
    // Performance tracking (using interlocked operations instead of volatile for long)
    private long _totalEnqueued = 0;
    private long _totalProcessed = 0;
    private long _totalDropped = 0;
    private long _totalFailed = 0;
    private readonly ConcurrentDictionary<TaskPriority, long> _queueCounts;
    private readonly ConcurrentDictionary<string, long> _taskTypeStats;
    
    // Task coalescing to prevent duplicate operations
    private readonly ConcurrentDictionary<string, QueuedTask> _coalescingMap;
    
    public BoundedTaskQueue(int maxQueueSize = 500, int maxConcurrency = 4)
    {
        _maxQueueSize = maxQueueSize;
        _maxConcurrency = maxConcurrency;
        _concurrencyLimiter = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Initialize priority queues for each priority level
        _priorityQueues = new ConcurrentDictionary<TaskPriority, ConcurrentQueue<QueuedTask>>();
        foreach (TaskPriority priority in Enum.GetValues<TaskPriority>())
        {
            _priorityQueues[priority] = new ConcurrentQueue<QueuedTask>();
        }
        
        _queueCounts = new ConcurrentDictionary<TaskPriority, long>();
        _taskTypeStats = new ConcurrentDictionary<string, long>();
        _coalescingMap = new ConcurrentDictionary<string, QueuedTask>();
        
        // Start continuous processing
        _processingTimer = new Timer(ProcessTasks, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(10));
        _statsTimer = new Timer(LogStatistics, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Attempts to enqueue a task with the specified priority
    /// </summary>
    /// <param name="taskFactory">Factory function to create the task</param>
    /// <param name="priority">Priority level for execution</param>
    /// <param name="taskName">Descriptive name for the task</param>
    /// <param name="source">Source component that created the task</param>
    /// <param name="coalescingKey">Optional key for task coalescing (prevents duplicate tasks)</param>
    /// <returns>True if successfully enqueued, false if queue is full or task was coalesced</returns>
    public bool TryEnqueue(Func<Task> taskFactory, TaskPriority priority, string taskName, string source = "Unknown", string? coalescingKey = null)
    {
        if (_cancellationTokenSource.Token.IsCancellationRequested)
            return false;

        // Handle task coalescing for duplicate operations
        if (coalescingKey != null)
        {
            var queuedTask = new QueuedTask(taskFactory, priority, taskName, source);
            var existingTask = _coalescingMap.AddOrUpdate(coalescingKey, queuedTask, (key, existing) =>
            {
                // If there's an existing task with same coalescing key, replace it with newer one
                return queuedTask;
            });
            
            // If we replaced an existing task, we don't need to enqueue again
            if (existingTask != queuedTask)
            {
                return true; // Coalesced with existing task
            }
        }

        // Check total queue size across all priorities
        var totalQueueSize = _priorityQueues.Values.Sum(q => q.Count);
        if (totalQueueSize >= _maxQueueSize)
        {
            // Implement priority-based dropping strategy
            if (!TryDropLowerPriorityTask(priority))
            {
                Interlocked.Increment(ref _totalDropped);
                return false; // Queue is full and couldn't drop any lower priority tasks
            }
        }

        var task = new QueuedTask(taskFactory, priority, taskName, source);
        _priorityQueues[priority].Enqueue(task);
        
        Interlocked.Increment(ref _totalEnqueued);
        _queueCounts.AddOrUpdate(priority, 1, (key, count) => count + 1);
        _taskTypeStats.AddOrUpdate(taskName, 1, (key, count) => count + 1);
        
        return true;
    }

    /// <summary>
    /// Tries to drop a lower priority task to make room for a higher priority task
    /// </summary>
    private bool TryDropLowerPriorityTask(TaskPriority incomingPriority)
    {
        // Try to drop tasks in ascending priority order (drop lowest priority first)
        var prioritiesToTry = Enum.GetValues<TaskPriority>()
            .Where(p => (int)p < (int)incomingPriority)
            .OrderBy(p => (int)p);

        foreach (var priority in prioritiesToTry)
        {
            if (_priorityQueues[priority].TryDequeue(out var droppedTask))
            {
                Interlocked.Increment(ref _totalDropped);
                
                // Remove from coalescing map if applicable
                var coalescingKeys = _coalescingMap.Where(kvp => kvp.Value == droppedTask).Select(kvp => kvp.Key).ToList();
                foreach (var key in coalescingKeys)
                {
                    _coalescingMap.TryRemove(key, out _);
                }
                
                return true;
            }
        }
        
        return false; // Couldn't drop any lower priority tasks
    }

    /// <summary>
    /// Continuously processes tasks from the priority queues
    /// </summary>
    private async void ProcessTasks(object? state)
    {
        if (_cancellationTokenSource.Token.IsCancellationRequested)
            return;

        // Try to acquire concurrency slot
        if (!await _concurrencyLimiter.WaitAsync(1, _cancellationTokenSource.Token))
            return;

        try
        {
            var task = GetNextTask();
            if (task != null)
            {
                await ExecuteTask(task);
            }
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    /// <summary>
    /// Gets the next highest priority task from the queues
    /// </summary>
    private QueuedTask? GetNextTask()
    {
        // Process priorities in descending order (highest priority first)
        var priorities = Enum.GetValues<TaskPriority>().OrderByDescending(p => (int)p);
        
        foreach (var priority in priorities)
        {
            if (_priorityQueues[priority].TryDequeue(out var task))
            {
                // Remove from coalescing map if it was there
                var coalescingKeys = _coalescingMap.Where(kvp => kvp.Value == task).Select(kvp => kvp.Key).ToList();
                foreach (var key in coalescingKeys)
                {
                    _coalescingMap.TryRemove(key, out _);
                }
                
                return task;
            }
        }
        
        return null;
    }

    /// <summary>
    /// Executes a task with timeout and error handling
    /// </summary>
    private async Task ExecuteTask(QueuedTask queuedTask)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 30 second timeout
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _cancellationTokenSource.Token, timeoutCts.Token);

            var task = queuedTask.TaskFactory();
            await task.WaitAsync(linkedCts.Token);
            
            Interlocked.Increment(ref _totalProcessed);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.Token.IsCancellationRequested)
        {
            // Expected during shutdown
        }
        catch (TimeoutException)
        {
            Interlocked.Increment(ref _totalFailed);
            Console.WriteLine($"[BoundedTaskQueue] Task timeout: {queuedTask.Name} from {queuedTask.Source}");
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _totalFailed);
            Console.WriteLine($"[BoundedTaskQueue] Task failed: {queuedTask.Name} from {queuedTask.Source}: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            
            // Log slow tasks
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                Console.WriteLine($"[BoundedTaskQueue] Slow task: {queuedTask.Name} took {stopwatch.ElapsedMilliseconds}ms");
            }
        }
    }

    /// <summary>
    /// Logs queue statistics for monitoring
    /// </summary>
    private void LogStatistics(object? state)
    {
        var totalQueued = _priorityQueues.Values.Sum(q => q.Count);
        var coalescingMapSize = _coalescingMap.Count;
        
        Console.WriteLine($"[BoundedTaskQueue] Stats: Queued={totalQueued}, Enqueued={_totalEnqueued}, " +
                         $"Processed={_totalProcessed}, Dropped={_totalDropped}, Failed={_totalFailed}, " +
                         $"CoalescingMap={coalescingMapSize}");
        
        // Log queue sizes by priority
        foreach (var priority in Enum.GetValues<TaskPriority>())
        {
            var count = _priorityQueues[priority].Count;
            if (count > 0)
            {
                Console.WriteLine($"[BoundedTaskQueue] {priority} queue: {count} tasks");
            }
        }

        // Warn about high queue usage
        if (totalQueued > _maxQueueSize * 0.8)
        {
            Console.WriteLine($"[BoundedTaskQueue] WARNING: Queue usage is high ({totalQueued}/{_maxQueueSize})");
        }
    }

    /// <summary>
    /// Gets current queue statistics
    /// </summary>
    public QueueStatistics GetStatistics()
    {
        return new QueueStatistics
        {
            TotalQueued = _priorityQueues.Values.Sum(q => q.Count),
            MaxQueueSize = _maxQueueSize,
            TotalEnqueued = _totalEnqueued,
            TotalProcessed = _totalProcessed,
            TotalDropped = _totalDropped,
            TotalFailed = _totalFailed,
            QueueSizesByPriority = Enum.GetValues<TaskPriority>()
                .ToDictionary(p => p, p => _priorityQueues[p].Count),
            TaskTypeStats = _taskTypeStats.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            CoalescingMapSize = _coalescingMap.Count
        };
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _processingTimer?.Dispose();
        _statsTimer?.Dispose();
        _concurrencyLimiter?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}

/// <summary>
/// Statistics about the bounded task queue performance
/// </summary>
public class QueueStatistics
{
    public int TotalQueued { get; init; }
    public int MaxQueueSize { get; init; }
    public long TotalEnqueued { get; init; }
    public long TotalProcessed { get; init; }
    public long TotalDropped { get; init; }
    public long TotalFailed { get; init; }
    public Dictionary<TaskPriority, int> QueueSizesByPriority { get; init; } = new();
    public Dictionary<string, long> TaskTypeStats { get; init; } = new();
    public int CoalescingMapSize { get; init; }
}