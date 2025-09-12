using System.Drawing;
using System.Text.Json.Serialization;

namespace PixelAutomation.Core.Models;

public class PartyHealConfig
{
    public PartyHealGlobalConfig Global { get; set; } = new();
    public List<PartyMemberConfig> Members { get; set; } = new();

    public PartyHealConfig()
    {
        // Initialize 8 party members with defaults
        Members = Enumerable.Range(0, 8).Select(i => new PartyMemberConfig
        {
            Index = i,
            SelectKey = GetDefaultSelectKey(i),
            ThresholdPercent = 50,
            RearmMs = 500
        }).ToList();
    }

    private static string GetDefaultSelectKey(int index) => index switch
    {
        0 => "Q",
        1 => "W", 
        2 => "E",
        3 => "R",
        4 => "T",
        5 => "Y",
        6 => "U",
        7 => "I",
        _ => "Q"
    };
}

public class PartyHealGlobalConfig
{
    public string SkillKey { get; set; } = "F1";
    public int PollIntervalMs { get; set; } = 10;
    public int AnimationDelayMs { get; set; } = 1500;
    public int ColorTolerance { get; set; } = 25;
    public int HumanizeDelayMsMin { get; set; } = 20;
    public int HumanizeDelayMsMax { get; set; } = 60;
    public int MinActionSpacingMs { get; set; } = 90;
    public Color BaselineColor { get; set; } = Color.FromArgb(255, 0, 0); // Default red HP
    public bool PreemptEnabled { get; set; } = true; // New lower HP preempts current heal
}

public class PartyMemberConfig
{
    public int Index { get; set; }
    public bool Enabled { get; set; } = false;
    public string SelectKey { get; set; } = "Q";
    public int ThresholdPercent { get; set; } = 50;
    public int XStart { get; set; } = 0;
    public int XStop { get; set; } = 100;
    public int Y { get; set; } = 0;
    public int RearmMs { get; set; } = 500;
    
    [JsonIgnore]
    public Point ThresholdPixel => new(XStart + (int)Math.Floor((XStop - XStart) * (ThresholdPercent / 100.0)), Y);
    
    [JsonIgnore]
    public bool IsConfigured => XStart != XStop && Y > 0;
}