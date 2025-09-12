using System.Drawing;
using PixelAutomation.Core.Models;

namespace PixelAutomation.Core.Interfaces;

public interface IPartyHealService : IDisposable
{
    event EventHandler<PartyMemberHealedEventArgs>? MemberHealed;
    event EventHandler<PartyHealStatusChangedEventArgs>? StatusChanged;
    
    bool IsRunning { get; }
    PartyHealConfig Configuration { get; set; }
    
    Task StartAsync();
    Task StopAsync();
    void SetTargetWindow(IntPtr hwnd);
    void SetKeyPressCallback(Action<string> keyPressCallback);
    Task<Color> CalibrateBaselineColorAsync(int memberIndex, IntPtr targetWindow);
    PartyMemberStatus GetMemberStatus(int memberIndex);
}

public class PartyMemberHealedEventArgs : EventArgs
{
    public int MemberIndex { get; set; }
    public DateTime Timestamp { get; set; }
    public Color DetectedColor { get; set; }
    public double ColorDistance { get; set; }
}

public class PartyHealStatusChangedEventArgs : EventArgs
{
    public bool IsRunning { get; set; }
    public string? StatusMessage { get; set; }
}

public class PartyMemberStatus
{
    public int Index { get; set; }
    public bool IsEnabled { get; set; }
    public Color LastDetectedColor { get; set; }
    public double LastColorDistance { get; set; }
    public DateTime LastCheck { get; set; }
    public DateTime? LastHealed { get; set; }
    public bool IsOnCooldown { get; set; }
    public int TotalHeals { get; set; }
}