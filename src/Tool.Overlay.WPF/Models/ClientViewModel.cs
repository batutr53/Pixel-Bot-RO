using CommunityToolkit.Mvvm.ComponentModel;

namespace PixelAutomation.Tool.Overlay.WPF.Models;

public partial class ClientViewModel : ObservableObject
{
    [ObservableProperty]
    private string clientName = "";
    
    [ObservableProperty]
    private string windowTitle = "No window selected";
    
    [ObservableProperty]
    private IntPtr targetHwnd = IntPtr.Zero;
    
    [ObservableProperty]
    private bool isRunning = false;
    
    [ObservableProperty]
    private double fps = 0.0;
    
    [ObservableProperty]
    private long clickCount = 0;
    
    [ObservableProperty]
    private long triggerCount = 0;

    public ProbeViewModel HpProbe { get; set; } = new()
    {
        Name = "HP",
        ExpectedColor = System.Drawing.Color.Red,
        TriggerColor = System.Drawing.Color.Black,
        Tolerance = 70  // 70% threshold
    };

    public ProbeViewModel MpProbe { get; set; } = new()
    {
        Name = "MP", 
        ExpectedColor = System.Drawing.Color.Blue,
        TriggerColor = System.Drawing.Color.Black,
        Tolerance = 50  // 50% threshold
    };

    public TriggerClickViewModel HpTrigger { get; set; } = new() { Name = "HP_Trigger", CooldownMs = 120, Enabled = true };
    public TriggerClickViewModel MpTrigger { get; set; } = new() { Name = "MP_Trigger", CooldownMs = 120, Enabled = true };
    public PeriodicClickViewModel YClick { get; set; } = new() 
    { 
        Name = "Y_Periodic", 
        PeriodMs = 120, 
        Enabled = true 
    };
    public PeriodicClickViewModel Extra1Click { get; set; } = new() 
    { 
        Name = "Extra1", 
        PeriodMs = 250, 
        Enabled = false 
    };
    public PeriodicClickViewModel Extra2Click { get; set; } = new() 
    { 
        Name = "Extra2", 
        PeriodMs = 500, 
        Enabled = false 
    };
    public PeriodicClickViewModel Extra3Click { get; set; } = new() 
    { 
        Name = "Extra3", 
        PeriodMs = 1000, 
        Enabled = false 
    };
}

public partial class ProbeViewModel : ObservableObject
{
    [ObservableProperty]
    private string name = "";
    
    [ObservableProperty]
    private int x = 0;
    
    [ObservableProperty]
    private int y = 0;
    
    [ObservableProperty]
    private int width = 100;
    
    [ObservableProperty]
    private int height = 20;
    
    [ObservableProperty]
    private System.Drawing.Color? currentColor;
    
    [ObservableProperty]
    private System.Drawing.Color? referenceColor;
    
    [ObservableProperty]
    private System.Drawing.Color expectedColor = System.Drawing.Color.Red;
    
    [ObservableProperty]
    private System.Drawing.Color triggerColor = System.Drawing.Color.Black;
    
    [ObservableProperty]
    private int tolerance = 30;
    
    [ObservableProperty]
    private System.Drawing.Color lastCheckedColor = System.Drawing.Color.Black;
    
    [ObservableProperty]
    private bool isTriggered = false;
}

public partial class ClickViewModel : ObservableObject
{
    [ObservableProperty]
    private string name = "";
    
    [ObservableProperty]
    private int x = 0;
    
    [ObservableProperty]
    private int y = 0;
    
    [ObservableProperty]
    private long executionCount = 0;
}

public partial class PeriodicClickViewModel : ClickViewModel
{
    [ObservableProperty]
    private int periodMs = 1000;
    
    [ObservableProperty]
    private bool enabled = false;
    
    [ObservableProperty]
    private DateTime lastExecution = DateTime.MinValue;
}

public partial class TriggerClickViewModel : ClickViewModel
{
    [ObservableProperty]
    private int cooldownMs = 120;
    
    [ObservableProperty]
    private bool enabled = true;
    
    [ObservableProperty]
    private DateTime lastExecution = DateTime.MinValue;
    
    [ObservableProperty]
    private bool isTriggered = false;
    
    [ObservableProperty]
    private bool keepClicking = false;
}