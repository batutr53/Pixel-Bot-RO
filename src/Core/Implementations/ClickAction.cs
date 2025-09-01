using PixelAutomation.Core.Interfaces;

namespace PixelAutomation.Core.Implementations;

public class ClickAction : IAction
{
    public ActionType Type => ActionType.Click;
    public Point? ClickLocation { get; }
    public string? KeySequence => null;
    public object? CustomData { get; }

    private readonly IClickProvider _clickProvider;

    public ClickAction(Point clickLocation, IClickProvider clickProvider, object? customData = null)
    {
        ClickLocation = clickLocation;
        _clickProvider = clickProvider;
        CustomData = customData;
    }

    public async Task ExecuteAsync(IntPtr hwnd, CancellationToken cancellationToken)
    {
        if (ClickLocation.HasValue)
        {
            await _clickProvider.ClickAsync(hwnd, ClickLocation.Value);
        }
    }
}

public class EventRule : IRule
{
    public string Name { get; }
    public string When { get; }
    public int Priority { get; }
    public int? CooldownMs { get; }

    private readonly IAction _action;
    private DateTime _lastTriggered = DateTime.MinValue;

    public EventRule(string name, string when, IAction action, int priority = 0, int? cooldownMs = null)
    {
        Name = name;
        When = when;
        _action = action;
        Priority = priority;
        CooldownMs = cooldownMs;
    }

    public bool Evaluate(Dictionary<string, ProbeResult> probeResults)
    {
        if (CooldownMs.HasValue)
        {
            var timeSinceLastTrigger = DateTime.UtcNow - _lastTriggered;
            if (timeSinceLastTrigger.TotalMilliseconds < CooldownMs.Value)
                return false;
        }

        var parts = When.Split(':');
        if (parts.Length != 2)
            return false;

        var probeName = parts[0];
        var condition = parts[1];

        if (!probeResults.TryGetValue(probeName, out var result))
            return false;

        bool shouldTrigger = condition.ToLowerInvariant() switch
        {
            "level" => result.Triggered,
            "edge-up" or "edge-rising" => result.Triggered && result.EdgeDirection == "rising",
            "edge-down" or "edge-falling" => result.Triggered && result.EdgeDirection == "falling",
            "edge" or "edge-any" => result.Triggered && !string.IsNullOrEmpty(result.EdgeDirection),
            "transition" => result.Triggered && result.EdgeDirection == "transition",
            _ => false
        };

        if (shouldTrigger)
        {
            _lastTriggered = DateTime.UtcNow;
        }

        return shouldTrigger;
    }

    public IAction? GetAction()
    {
        return _action;
    }
}