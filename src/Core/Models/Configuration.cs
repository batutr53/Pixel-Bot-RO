namespace PixelAutomation.Core.Models;

public class Configuration
{
    public Dictionary<string, ProfileConfig> Profiles { get; set; } = new();
    public Dictionary<string, HotkeySet> HotkeySets { get; set; } = new();
    public string ActiveHotkeys { get; set; } = "Default";
}

public class ProfileConfig
{
    public GlobalConfig Global { get; set; } = new();
    public List<WindowTarget> Windows { get; set; } = new();
    public string? Hotkeys { get; set; }
}

public class GlobalConfig
{
    public string CaptureMode { get; set; } = "WGC";
    public string ClickMode { get; set; } = "message";
    public int DefaultHz { get; set; } = 80;
    public string LogLevel { get; set; } = "Info";
    public bool EnableTelemetry { get; set; } = true;
    public bool DryRun { get; set; } = false;
}

public class HotkeySet
{
    public string ToggleOverlay { get; set; } = "Ctrl+~";
    public string LockWindow { get; set; } = "F2";
    public string ToggleSnap { get; set; } = "F3";
    public string MagnifierToggle { get; set; } = "F4";
    public string CaptureModeCycle { get; set; } = "F6";
    public string ClickModeCycle { get; set; } = "F7";
    public string ShapePoint { get; set; } = "P";
    public string ShapeRect { get; set; } = "R";
    public string DeleteShape { get; set; } = "Del";
    public string CopyColorAtCursor { get; set; } = "Ctrl+Shift+C";
    public string SaveConfig { get; set; } = "Ctrl+S";
    public string OpenConfig { get; set; } = "Ctrl+O";
    public string ProfileNext { get; set; } = "Ctrl+Alt+Right";
    public string ProfilePrev { get; set; } = "Ctrl+Alt+Left";
    public string PanicStopAll { get; set; } = "Pause";
}