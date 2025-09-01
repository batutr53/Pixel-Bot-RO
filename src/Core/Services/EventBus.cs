using System.Collections.Concurrent;
using PixelAutomation.Core.Interfaces;

namespace PixelAutomation.Core.Services;

public interface IEventBus
{
    void Publish<T>(T @event) where T : class;
    void Subscribe<T>(Action<T> handler) where T : class;
    void Unsubscribe<T>(Action<T> handler) where T : class;
}

public class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _lock = new();

    public void Publish<T>(T @event) where T : class
    {
        if (_handlers.TryGetValue(typeof(T), out var handlers))
        {
            List<Delegate> handlersCopy;
            lock (_lock)
            {
                handlersCopy = new List<Delegate>(handlers);
            }

            foreach (var handler in handlersCopy)
            {
                try
                {
                    ((Action<T>)handler)(@event);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"EventBus handler error: {ex.Message}");
                }
            }
        }
    }

    public void Subscribe<T>(Action<T> handler) where T : class
    {
        lock (_lock)
        {
            var handlers = _handlers.GetOrAdd(typeof(T), _ => new List<Delegate>());
            handlers.Add(handler);
        }
    }

    public void Unsubscribe<T>(Action<T> handler) where T : class
    {
        lock (_lock)
        {
            if (_handlers.TryGetValue(typeof(T), out var handlers))
            {
                handlers.Remove(handler);
            }
        }
    }
}

public class ProbeTriggeredEvent
{
    public string WindowId { get; init; } = "";
    public string ProbeName { get; init; } = "";
    public bool IsEdge { get; init; }
    public DateTime Timestamp { get; init; }
}

public class ClickExecutedEvent
{
    public string WindowId { get; init; } = "";
    public Point Location { get; init; }
    public ClickMode Mode { get; init; }
    public DateTime Timestamp { get; init; }
}

public class BackendFallbackEvent
{
    public string WindowId { get; init; } = "";
    public CaptureBackendType From { get; init; }
    public CaptureBackendType To { get; init; }
    public string Reason { get; init; } = "";
}