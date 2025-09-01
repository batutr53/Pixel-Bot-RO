using System.Drawing;

namespace PixelAutomation.Core.Interfaces;

public interface IRule
{
    string Name { get; }
    string When { get; }
    int Priority { get; }
    int? CooldownMs { get; }
    
    bool Evaluate(Dictionary<string, ProbeResult> probeResults);
    IAction? GetAction();
}

public interface IAction
{
    ActionType Type { get; }
    Point? ClickLocation { get; }
    string? KeySequence { get; }
    object? CustomData { get; }
    
    Task ExecuteAsync(IntPtr hwnd, CancellationToken cancellationToken);
}

public enum ActionType
{
    Click,
    KeyPress,
    Custom
}