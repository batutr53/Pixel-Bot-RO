namespace PixelAutomation.Core.Models;

public class WindowTarget
{
    public string? TitleRegex { get; set; }
    
    // IntPtr cannot be serialized, store as string
    private string? _hwndString;
    public string? HwndString 
    { 
        get => _hwndString;
        set => _hwndString = value;
    }
    
    // Non-serialized property for runtime use
    [System.Text.Json.Serialization.JsonIgnore]
    public IntPtr? Hwnd 
    { 
        get => string.IsNullOrEmpty(_hwndString) ? null : new IntPtr(long.Parse(_hwndString));
        set => _hwndString = value?.ToInt64().ToString();
    }
    
    public string? ProcessName { get; set; }
    public bool DpiAware { get; set; } = true;
    public List<ProbeConfig> Probes { get; set; } = new();
    public List<PercentageProbeConfig> PercentageProbes { get; set; } = new();
    public List<EventConfig> Events { get; set; } = new();
    public List<PeriodicClickConfig> PeriodicClicks { get; set; } = new();
    public RateLimitConfig? Limits { get; set; }
}

public class ProbeConfig
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "point";
    public int X { get; set; }
    public int Y { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int Box { get; set; } = 3;
    public string Mode { get; set; } = "level";
    public string Metric { get; set; } = "rgb";
    public int[] RefColor { get; set; } = new[] { 0, 0, 0 };
    public int[]? ToColor { get; set; }
    public int Tolerance { get; set; } = 20;
    public int? DebounceMs { get; set; }
}

public class EventConfig
{
    public string When { get; set; } = "";
    public ClickTarget Click { get; set; } = new();
    public int? CooldownMs { get; set; }
    public int Priority { get; set; } = 0;
}

public class ClickTarget
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class PeriodicClickConfig
{
    public string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int? PeriodMs { get; set; }
    public double? PeriodSec { get; set; }
    public bool Enabled { get; set; }
}

public class PercentageProbeConfig
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "HP"; // HP, MP, Custom
    public int StartX { get; set; }
    public int EndX { get; set; }
    public int Y { get; set; }
    public double MonitorPercentage { get; set; } = 50.0;
    public int[] ExpectedColor { get; set; } = new[] { 255, 0, 0 }; // Default red for HP
    public int[]? EmptyColor { get; set; }
    public int Tolerance { get; set; } = 30;
    public int Box { get; set; } = 1;
    public string Mode { get; set; } = "edge";
    public string Metric { get; set; } = "rgb";
    public int? DebounceMs { get; set; } = 100;
}

public class RateLimitConfig
{
    public int MaxBurstPerSec { get; set; } = 30;
    public int GlobalCooldownMs { get; set; } = 20;
}