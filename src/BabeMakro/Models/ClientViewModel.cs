using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Threading;

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

    public TriggerClickViewModel HpTrigger { get; set; } = new() { Name = "HP_Trigger", CooldownMs = 120, Enabled = true, UseKeyPress = true, UseCoordinate = false, KeyToPress = "Q" };
    public TriggerClickViewModel MpTrigger { get; set; } = new() { Name = "MP_Trigger", CooldownMs = 120, Enabled = true, UseKeyPress = true, UseCoordinate = false, KeyToPress = "W" };
    
    // Percentage-based HP/MP monitoring (Python-style)
    public PercentageProbeViewModel HpPercentageProbe { get; set; } = new()
    {
        Name = "HP_Percentage",
        Type = "HP",
        StartX = 100,
        EndX = 250,
        Y = 67,
        MonitorPercentage = 50.0,
        ExpectedColor = System.Drawing.Color.FromArgb(16, 12, 255), // 0x100CFF from Python
        EmptyColor = System.Drawing.Color.Black,
        Tolerance = 30,
        Enabled = false // Start disabled, user can enable
    };
    
    public PercentageProbeViewModel MpPercentageProbe { get; set; } = new()
    {
        Name = "MP_Percentage",
        Type = "MP",
        StartX = 100,
        EndX = 250,
        Y = 84,
        MonitorPercentage = 50.0,
        ExpectedColor = System.Drawing.Color.FromArgb(255, 77, 23), // 0xFF4D17 from Python
        EmptyColor = System.Drawing.Color.Black,
        Tolerance = 30,
        Enabled = false // Start disabled, user can enable
    };
    
    // Python-style potion click coordinates (separate from regular HP/MP triggers)
    public PythonStyleClickViewModel PythonHpPotionClick { get; set; } = new()
    {
        Name = "Python_HP_Potion",
        X = 400,
        Y = 300,
        CooldownMs = 500,
        Enabled = true,
        UseKeyPress = true,
        UseCoordinate = false,
        KeyToPress = "Q"
    };
    
    public PythonStyleClickViewModel PythonMpPotionClick { get; set; } = new()
    {
        Name = "Python_MP_Potion", 
        X = 450,
        Y = 350,
        CooldownMs = 500,
        Enabled = true,
        UseKeyPress = true,
        UseCoordinate = false,
        KeyToPress = "W"
    };
    public PeriodicClickViewModel YClick { get; set; } = new() 
    { 
        Name = "Y_Periodic", 
        PeriodMs = 120, 
        Enabled = true,
        UseKeyPress = true,
        UseCoordinate = false,
        KeyToPress = "Y"
    };
    public PeriodicClickViewModel Extra1Click { get; set; } = new() 
    { 
        Name = "Extra1", 
        PeriodMs = 250, 
        Enabled = false,
        UseKeyPress = true,
        UseCoordinate = false
    };
    public PeriodicClickViewModel Extra2Click { get; set; } = new() 
    { 
        Name = "Extra2", 
        PeriodMs = 500, 
        Enabled = false,
        UseKeyPress = true,
        UseCoordinate = false
    };
    public PeriodicClickViewModel Extra3Click { get; set; } = new() 
    { 
        Name = "Extra3", 
        PeriodMs = 1000, 
        Enabled = false,
        UseKeyPress = true,
        UseCoordinate = false
    };
    
    // BabeBot Style HP/MP monitoring
    public BabeBotHpViewModel BabeBotHp { get; set; } = new()
    {
        StartX = 107,
        EndX = 325,
        Y = 81,
        ThresholdPercentage = 90,
        PotionX = 376,
        PotionY = 236,
        Enabled = false
    };
    
    public BabeBotMpViewModel BabeBotMp { get; set; } = new()
    {
        StartX = 103,
        EndX = 306,
        Y = 104,
        ThresholdPercentage = 90,
        PotionX = 376,
        PotionY = 200,
        Enabled = false
    };
    
    // Multi HP System - 8 clients
    public List<MultiHpClientViewModel> MultiHpClients { get; set; } = new();
    
    [ObservableProperty]
    private bool multiHpEnabled = false;
    
    [ObservableProperty]
    private int animationDelay = 1500; // Skill animation delay in ms
    
    [ObservableProperty]
    private int multiHpCheckInterval = 100; // Check interval in ms
    
    // Attack/Skills System
    public List<AttackSkillViewModel> AttackSkills { get; set; } = new();
    
    [ObservableProperty]
    private bool attackSystemEnabled = false;
    
    [ObservableProperty]
    private bool attackRunning = false;
    
    // Buff/AC System
    public BuffAcSettingsViewModel BuffAcSettings { get; set; } = new();
    
    [ObservableProperty]
    private bool buffAcSystemEnabled = false;
    
    [ObservableProperty]
    private bool buffAcRunning = false;
    
    [ObservableProperty]
    private bool buffAcCycleActive = false;
    
    // Captcha Settings
    public Dictionary<string, object> CaptchaSettings { get; set; } = new();
    
    public ClientViewModel()
    {
        // Initialize 8 Multi HP clients
        for (int i = 1; i <= 8; i++)
        {
            MultiHpClients.Add(new MultiHpClientViewModel
            {
                ClientIndex = i,
                StartX = 1,
                EndX = 90,
                Y = 5,
                ThresholdPercentage = 30,
                ClickX = 376,
                ClickY = 236,
                UserSelectionKey = i.ToString(),
                SkillKey = "1",
                Enabled = false,
                Status = "Waiting...",
                Percentage = 100
            });
        }
    }
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

public partial class PercentageProbeViewModel : ObservableObject
{
    [ObservableProperty]
    private string name = "";
    
    [ObservableProperty]
    private string type = "HP"; // HP, MP, Custom
    
    [ObservableProperty]
    private int startX = 100;
    
    [ObservableProperty]
    private int endX = 250;
    
    [ObservableProperty]
    private int y = 67;
    
    [ObservableProperty]
    private double monitorPercentage = 50.0;
    
    [ObservableProperty]
    private System.Drawing.Color expectedColor = System.Drawing.Color.FromArgb(16, 12, 255); // Python HP color
    
    [ObservableProperty]
    private System.Drawing.Color? emptyColor = System.Drawing.Color.Black;
    
    [ObservableProperty]
    private int tolerance = 30;
    
    [ObservableProperty]
    private int box = 1;
    
    [ObservableProperty]
    private string mode = "edge";
    
    [ObservableProperty]
    private string metric = "rgb";
    
    [ObservableProperty]
    private int? debounceMs = 100;
    
    [ObservableProperty]
    private System.Drawing.Color? currentColor;
    
    [ObservableProperty]
    private double currentPercentage = 100.0;
    
    [ObservableProperty]
    private bool isTriggered = false;
    
    [ObservableProperty]
    private bool enabled = true;
    
    // Calculated X coordinate based on percentage
    public int CalculatedX => StartX + (int)((MonitorPercentage / 100.0) * (EndX - StartX));
}

public partial class PythonStyleClickViewModel : ClickViewModel
{
    [ObservableProperty]
    private bool enabled = true;
    
    [ObservableProperty]
    private int cooldownMs = 500;
    
    [ObservableProperty]
    private DateTime lastExecution = DateTime.MinValue;
    
    [ObservableProperty]
    private bool useCoordinate = false;
    
    [ObservableProperty]
    private bool useKeyPress = true;
    
    [ObservableProperty]
    private string? keyToPress;
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
    
    [ObservableProperty]
    private bool useCoordinate = false;
    
    [ObservableProperty]
    private bool useKeyPress = true;
    
    [ObservableProperty]
    private string? keyToPress;
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
    
    [ObservableProperty]
    private bool useCoordinate = false;
    
    [ObservableProperty]
    private bool useKeyPress = true;
    
    [ObservableProperty]
    private string? keyToPress;
}

// BabeBot Style HP/MP ViewModels
public partial class BabeBotHpViewModel : ObservableObject
{
    [ObservableProperty]
    private int startX = 107;
    
    [ObservableProperty]
    private int endX = 325;
    
    [ObservableProperty]
    private int y = 81;
    
    [ObservableProperty]
    private int thresholdPercentage = 90;
    
    [ObservableProperty]
    private int potionX = 376;
    
    [ObservableProperty]
    private int potionY = 236;
    
    [ObservableProperty]
    private bool enabled = false;
    
    [ObservableProperty]
    private bool useCoordinate = false;
    
    [ObservableProperty]
    private bool useKeyPress = true;
    
    [ObservableProperty]
    private string? keyToPress = "Q";
    
    [ObservableProperty]
    private System.Drawing.Color currentColor = System.Drawing.Color.Black;
    
    [ObservableProperty]
    private System.Drawing.Color referenceColor = System.Drawing.Color.Red;
    
    [ObservableProperty]
    private bool isTriggered = false;
    
    [ObservableProperty]
    private string status = "--";
    
    // BabeBot calibration storage - 19 reference colors for %5-%95
    public Dictionary<int, System.Drawing.Color> ReferenceColors { get; set; } = new();
    
    // Calculate the X coordinate based on percentage (BabeBot formula)
    public int CalculateXForPercentage(double percentage)
    {
        // BabeBot formula: Math.Round(((bitis - baslangic) * percentX + baslangic), 0)
        double percentX = percentage / 100.0;
        return (int)Math.Round(((EndX - StartX) * percentX + StartX), 0);
    }
    
    // Get the monitoring X coordinate based on threshold
    public int MonitorX => CalculateXForPercentage(ThresholdPercentage);
    
    public DateTime LastExecution { get; set; } = DateTime.MinValue;
    
    [ObservableProperty]
    private long executionCount = 0;
}

public partial class BabeBotMpViewModel : ObservableObject
{
    [ObservableProperty]
    private int startX = 103;
    
    [ObservableProperty]
    private int endX = 306;
    
    [ObservableProperty]
    private int y = 104;
    
    [ObservableProperty]
    private int thresholdPercentage = 90;
    
    [ObservableProperty]
    private int potionX = 376;
    
    [ObservableProperty]
    private int potionY = 200;
    
    [ObservableProperty]
    private bool enabled = false;
    
    [ObservableProperty]
    private bool useCoordinate = false;
    
    [ObservableProperty]
    private bool useKeyPress = true;
    
    [ObservableProperty]
    private string? keyToPress = "W";
    
    [ObservableProperty]
    private System.Drawing.Color currentColor = System.Drawing.Color.Black;
    
    [ObservableProperty]
    private System.Drawing.Color referenceColor = System.Drawing.Color.Blue;
    
    [ObservableProperty]
    private bool isTriggered = false;
    
    [ObservableProperty]
    private string status = "--";
    
    // BabeBot calibration storage - 19 reference colors for %5-%95
    public Dictionary<int, System.Drawing.Color> ReferenceColors { get; set; } = new();
    
    // Calculate the X coordinate based on percentage (BabeBot formula)
    public int CalculateXForPercentage(double percentage)
    {
        // BabeBot formula: Math.Round(((bitis - baslangic) * percentX + baslangic), 0)
        double percentX = percentage / 100.0;
        return (int)Math.Round(((EndX - StartX) * percentX + StartX), 0);
    }
    
    // Get the monitoring X coordinate based on threshold
    public int MonitorX => CalculateXForPercentage(ThresholdPercentage);
    
    public DateTime LastExecution { get; set; } = DateTime.MinValue;
    
    [ObservableProperty]
    private long executionCount = 0;
}

// Multi HP Client ViewModel for 8-client system
public partial class MultiHpClientViewModel : ObservableObject
{
    [ObservableProperty]
    private int clientIndex = 1;
    
    [ObservableProperty]
    private int startX = 1;
    
    [ObservableProperty]
    private int endX = 90;
    
    [ObservableProperty]
    private int y = 5;
    
    [ObservableProperty]
    private int thresholdPercentage = 30;
    
    [ObservableProperty]
    private int clickX = 376;
    
    [ObservableProperty]
    private int clickY = 236;
    
    [ObservableProperty]
    private string userSelectionKey = "1";
    
    [ObservableProperty]
    private string skillKey = "1";
    
    [ObservableProperty]
    private bool enabled = false;
    
    [ObservableProperty]
    private string status = "Waiting...";
    
    [ObservableProperty]
    private double percentage = 100.0;
    
    [ObservableProperty]
    private System.Drawing.Color currentColor = System.Drawing.Color.Black;
    
    [ObservableProperty]
    private System.Drawing.Color referenceColor = System.Drawing.Color.Red;
    
    [ObservableProperty]
    private bool isTriggered = false;
    
    [ObservableProperty]
    private DateTime lastExecution = DateTime.MinValue;
    
    [ObservableProperty]
    private long executionCount = 0;
    
    [ObservableProperty]
    private bool isWaitingForAnimation = false;
    
    // BabeBot calibration storage for this client
    public Dictionary<int, System.Drawing.Color> ReferenceColors { get; set; } = new();
    
    // Calculate the X coordinate based on percentage (same as BabeBot formula)
    public int CalculateXForPercentage(double percentage)
    {
        double percentX = percentage / 100.0;
        return (int)Math.Round(((EndX - StartX) * percentX + StartX), 0);
    }
    
    // Get the monitoring X coordinate based on threshold
    public int MonitorX => CalculateXForPercentage(ThresholdPercentage);
    
    [ObservableProperty]
    private int monitorY = 5;
}

// Attack Skill ViewModel
public partial class AttackSkillViewModel : ObservableObject
{
    [ObservableProperty]
    private string name = "";
    
    [ObservableProperty]
    private string key = "";
    
    [ObservableProperty]
    private int intervalMs = 1500;
    
    [ObservableProperty]
    private bool enabled = false;
    
    [ObservableProperty]
    private bool isRunning = false;
    
    [ObservableProperty]
    private string status = "Stopped";
    
    [ObservableProperty]
    private long executionCount = 0;
    
    // Timer reference (not observable)
    public DispatcherTimer? Timer { get; set; }
}

// Buff/AC Settings ViewModel
public partial class BuffAcSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string buffKey = "6";
    
    [ObservableProperty]
    private string acKey = "1";
    
    [ObservableProperty]
    private double buffAnimationTime = 2.5;
    
    [ObservableProperty]
    private double acAnimationTime = 2.5;
    
    [ObservableProperty]
    private int cycleIntervalSeconds = 30;
    
    [ObservableProperty]
    private bool enabled = false;
    
    [ObservableProperty]
    private int currentMemberIndex = 0;
    
    [ObservableProperty]
    private string status = "Stopped";
    
    [ObservableProperty]
    private long cycleCount = 0;
    
    // Member selection keys for Party Buff/AC (independent from Party Heal)
    [ObservableProperty]
    private string member1Key = "Q";
    
    [ObservableProperty]
    private string member2Key = "W";
    
    [ObservableProperty]
    private string member3Key = "E";
    
    [ObservableProperty]
    private string member4Key = "R";
    
    [ObservableProperty]
    private string member5Key = "T";
    
    [ObservableProperty]
    private string member6Key = "Y";
    
    [ObservableProperty]
    private string member7Key = "U";
    
    [ObservableProperty]
    private string member8Key = "I";
    
    // Member enable/disable for Party Buff/AC
    [ObservableProperty]
    private bool member1Enabled = true;
    
    [ObservableProperty]
    private bool member2Enabled = true;
    
    [ObservableProperty]
    private bool member3Enabled = true;
    
    [ObservableProperty]
    private bool member4Enabled = true;
    
    [ObservableProperty]
    private bool member5Enabled = true;
    
    [ObservableProperty]
    private bool member6Enabled = true;
    
    [ObservableProperty]
    private bool member7Enabled = true;
    
    [ObservableProperty]
    private bool member8Enabled = true;
}