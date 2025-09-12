using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Drawing;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.Logging;
using PixelAutomation.Core.Interfaces;
using PixelAutomation.Core.Models;

namespace PixelAutomation.Tool.Overlay.WPF.ViewModels;

public partial class PartyHealViewModel : ObservableObject, IDisposable
{
    public readonly IPartyHealService _partyHealService;
    private readonly ILogger<PartyHealViewModel> _logger;
    private bool _disposed = false;

    [ObservableProperty]
    private bool isRunning = false;

    [ObservableProperty]
    private string statusMessage = "Stopped";

    [ObservableProperty]
    private string skillKey = "F1";

    [ObservableProperty]
    private int pollIntervalMs = 10;

    [ObservableProperty]
    private int animationDelayMs = 1500;

    [ObservableProperty]
    private int colorTolerance = 25;

    [ObservableProperty]
    private int humanizeDelayMsMin = 20;

    [ObservableProperty]
    private int humanizeDelayMsMax = 60;

    [ObservableProperty]
    private int minActionSpacingMs = 90;

    [ObservableProperty]
    private string baselineColorHex = "FF0000";

    [ObservableProperty]
    private bool preemptEnabled = true;

    [ObservableProperty]
    private IntPtr targetWindow = IntPtr.Zero;

    public List<PartyMemberViewModel> Members { get; } = new();

    public PartyHealViewModel(IPartyHealService partyHealService, ILogger<PartyHealViewModel> logger)
    {
        _partyHealService = partyHealService;
        _logger = logger;

        // Initialize 8 party members
        var defaultKeys = new[] { "Q", "W", "E", "R", "T", "Y", "U", "I" };
        for (int i = 0; i < 8; i++)
        {
            Members.Add(new PartyMemberViewModel(i, defaultKeys[i], this));
        }

        // Subscribe to service events
        _partyHealService.MemberHealed += OnMemberHealed;
        _partyHealService.StatusChanged += OnStatusChanged;

        // Load configuration
        LoadConfiguration();
    }

    [RelayCommand]
    public async Task StartAsync()
    {
        try
        {
            // Validate configuration
            if (!ValidateConfiguration())
                return;
            
            UpdateConfigurationFromViewModel();
            await _partyHealService.StartAsync();
            
            StatusMessage = "Party healing started successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start PartyHeal");
            StatusMessage = $"Failed to start: {ex.Message}";
            MessageBox.Show($"Failed to start PartyHeal:\n\n{ex.Message}\n\nCheck the console for more details.", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task StopAsync()
    {
        try
        {
            await _partyHealService.StopAsync();
            StatusMessage = "Party healing stopped";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop PartyHeal");
            StatusMessage = $"Failed to stop: {ex.Message}";
            MessageBox.Show($"Failed to stop PartyHeal:\n\n{ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void SaveConfiguration()
    {
        try
        {
            UpdateConfigurationFromViewModel();
            var json = JsonSerializer.Serialize(_partyHealService.Configuration, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            var configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "partyheal-config.json");
            System.IO.File.WriteAllText(configPath, json);
            
            _logger.LogInformation("PartyHeal configuration saved to {Path}", configPath);
            MessageBox.Show("Configuration saved successfully!", "Success", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save PartyHeal configuration");
            MessageBox.Show($"Failed to save configuration: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void LoadConfiguration()
    {
        try
        {
            var configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "partyheal-config.json");
            if (System.IO.File.Exists(configPath))
            {
                var json = System.IO.File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<PartyHealConfig>(json);
                if (config != null)
                {
                    _partyHealService.Configuration = config;
                    UpdateViewModelFromConfiguration();
                    _logger.LogInformation("PartyHeal configuration loaded from {Path}", configPath);
                }
            }
            else
            {
                // Create default configuration
                UpdateConfigurationFromViewModel();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load PartyHeal configuration");
            MessageBox.Show($"Failed to load configuration: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateConfigurationFromViewModel()
    {
        var config = _partyHealService.Configuration;
        
        config.Global.SkillKey = SkillKey;
        config.Global.PollIntervalMs = PollIntervalMs;
        config.Global.AnimationDelayMs = AnimationDelayMs;
        config.Global.ColorTolerance = ColorTolerance;
        config.Global.HumanizeDelayMsMin = HumanizeDelayMsMin;
        config.Global.HumanizeDelayMsMax = HumanizeDelayMsMax;
        config.Global.MinActionSpacingMs = MinActionSpacingMs;
        config.Global.PreemptEnabled = PreemptEnabled;
        
        // Parse baseline color from hex
        try
        {
            var color = ColorTranslator.FromHtml("#" + BaselineColorHex.TrimStart('#'));
            config.Global.BaselineColor = color;
        }
        catch
        {
            // Use default red color if parsing fails
            config.Global.BaselineColor = Color.Red;
        }

        // Update member configurations
        for (int i = 0; i < Math.Min(Members.Count, config.Members.Count); i++)
        {
            var memberVM = Members[i];
            var memberConfig = config.Members[i];
            
            memberConfig.Enabled = memberVM.Enabled;
            memberConfig.SelectKey = memberVM.SelectKey;
            memberConfig.ThresholdPercent = memberVM.ThresholdPercent;
            memberConfig.XStart = memberVM.XStart;
            memberConfig.XStop = memberVM.XStop;
            memberConfig.Y = memberVM.Y;
            memberConfig.RearmMs = memberVM.RearmMs;
        }
    }

    private void UpdateViewModelFromConfiguration()
    {
        var config = _partyHealService.Configuration;
        
        SkillKey = config.Global.SkillKey;
        PollIntervalMs = config.Global.PollIntervalMs;
        AnimationDelayMs = config.Global.AnimationDelayMs;
        ColorTolerance = config.Global.ColorTolerance;
        HumanizeDelayMsMin = config.Global.HumanizeDelayMsMin;
        HumanizeDelayMsMax = config.Global.HumanizeDelayMsMax;
        MinActionSpacingMs = config.Global.MinActionSpacingMs;
        PreemptEnabled = config.Global.PreemptEnabled;
        BaselineColorHex = config.Global.BaselineColor.ToArgb().ToString("X6");

        // Update member view models
        for (int i = 0; i < Math.Min(Members.Count, config.Members.Count); i++)
        {
            var memberVM = Members[i];
            var memberConfig = config.Members[i];
            
            memberVM.Enabled = memberConfig.Enabled;
            memberVM.SelectKey = memberConfig.SelectKey;
            memberVM.ThresholdPercent = memberConfig.ThresholdPercent;
            memberVM.XStart = memberConfig.XStart;
            memberVM.XStop = memberConfig.XStop;
            memberVM.Y = memberConfig.Y;
            memberVM.RearmMs = memberConfig.RearmMs;
        }
    }

    private bool ValidateConfiguration()
    {
        var errors = new List<string>();
        
        // Check if any members are enabled
        var enabledMembers = Members.Where(m => m.Enabled).ToList();
        if (enabledMembers.Count == 0)
        {
            errors.Add("At least one party member must be enabled");
        }
        
        // Check if target window is selected
        if (TargetWindow == IntPtr.Zero)
        {
            errors.Add("No target window selected. Please select a window from a Client Card first");
        }
        
        // Check global settings
        if (string.IsNullOrWhiteSpace(SkillKey))
        {
            errors.Add("Heal skill key is required");
        }
        
        if (PollIntervalMs < 5 || PollIntervalMs > 1000)
        {
            errors.Add("Poll interval must be between 5-1000 ms");
        }
        
        // Check enabled members configuration
        foreach (var member in enabledMembers)
        {
            if (member.XStart == member.XStop || member.Y <= 0)
            {
                errors.Add($"Member {member.DisplayName} is enabled but not configured (missing coordinates)");
            }
            
            if (member.ThresholdPercent < 1 || member.ThresholdPercent > 99)
            {
                errors.Add($"Member {member.DisplayName} threshold must be between 1-99%");
            }
        }
        
        // Show validation errors if any
        if (errors.Count > 0)
        {
            var errorMessage = "Configuration validation failed:\n\n" + string.Join("\n• ", errors);
            MessageBox.Show(errorMessage, "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusMessage = "Configuration validation failed";
            return false;
        }
        
        return true;
    }

    private void OnMemberHealed(object? sender, PartyMemberHealedEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (e.MemberIndex < Members.Count)
            {
                var member = Members[e.MemberIndex];
                member.LastHealed = e.Timestamp;
                member.TotalHeals++;
            }
            
            StatusMessage = $"Healed member {e.MemberIndex} at {e.Timestamp:HH:mm:ss}";
        });
    }

    private void OnStatusChanged(object? sender, PartyHealStatusChangedEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsRunning = e.IsRunning;
            StatusMessage = e.StatusMessage ?? (e.IsRunning ? "Running" : "Stopped");
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        _partyHealService.MemberHealed -= OnMemberHealed;
        _partyHealService.StatusChanged -= OnStatusChanged;
        
        foreach (var member in Members)
        {
            member.Dispose();
        }
    }
}

public partial class PartyMemberViewModel : ObservableObject, IDisposable
{
    private readonly PartyHealViewModel _parent;
    private bool _disposed = false;

    [ObservableProperty]
    private bool enabled = false;

    [ObservableProperty]
    private string selectKey = "Q";

    [ObservableProperty]
    private int thresholdPercent = 50;

    [ObservableProperty]
    private int xStart = 0;

    [ObservableProperty]
    private int xStop = 100;

    [ObservableProperty]
    private int y = 0;

    [ObservableProperty]
    private int rearmMs = 500;

    [ObservableProperty]
    private DateTime? lastHealed;

    [ObservableProperty]
    private int totalHeals = 0;

    [ObservableProperty]
    private string statusText = "Not configured";

    public int Index { get; }
    public string DisplayName => $"Member {Index + 1}";

    public PartyMemberViewModel(int index, string defaultSelectKey, PartyHealViewModel parent)
    {
        Index = index;
        SelectKey = defaultSelectKey;
        _parent = parent;
        
        // Update status when properties change
        PropertyChanged += (_, e) => UpdateStatus();
        UpdateStatus();
    }

    [RelayCommand]
    public async Task CalibrateAsync()
    {
        try
        {
            if (!IsConfigured)
            {
                MessageBox.Show("Please configure X coordinates and Y position first.", "Configuration Required", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_parent.TargetWindow == IntPtr.Zero)
            {
                MessageBox.Show("No target window selected. Please select a window from a Client Card first.", "Window Required", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var color = await _parent._partyHealService.CalibrateBaselineColorAsync(Index, _parent.TargetWindow);
            _parent.BaselineColorHex = color.ToArgb().ToString("X6").TrimStart('F', 'F'); // Remove alpha channel
            
            StatusText = $"Calibrated: {color}";
            MessageBox.Show($"Calibrated baseline color: #{_parent.BaselineColorHex}", "Calibration Complete", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Calibration failed: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateStatus()
    {
        if (XStart == XStop || Y <= 0)
        {
            StatusText = "Not configured";
        }
        else if (!Enabled)
        {
            StatusText = "Disabled";
        }
        else
        {
            var cooldownText = LastHealed.HasValue ? $" (Last: {LastHealed:HH:mm:ss})" : "";
            StatusText = $"Ready - {TotalHeals} heals{cooldownText}";
        }
    }

    public bool IsConfigured => XStart != XStop && Y > 0;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}