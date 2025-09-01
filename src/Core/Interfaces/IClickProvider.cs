namespace PixelAutomation.Core.Interfaces;

public interface IClickProvider
{
    string Name { get; }
    ClickMode Mode { get; }
    
    Task<bool> ClickAsync(IntPtr hwnd, Point clientCoordinate, ClickOptions? options = null);
    void SetFallbackProvider(IClickProvider fallback);
}

public enum ClickMode
{
    Message,
    CursorJump,
    CursorReturn
}

public class ClickOptions
{
    public int? DelayMs { get; set; }
    public bool DoubleClick { get; set; }
    public MouseButton Button { get; set; } = MouseButton.Left;
}

public enum MouseButton
{
    Left,
    Right,
    Middle
}