using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.ComponentModel;
using Microsoft.Win32;
using PixelAutomation.Core.Models;
using PixelAutomation.Tool.Overlay.WPF.Services;
using PixelAutomation.Tool.Overlay.WPF.ViewModels;
using PixelAutomation.Tool.Overlay.WPF.Controls;
using PixelAutomation.Tool.Overlay.WPF.Models;
using Vanara.PInvoke;
using System.Diagnostics;
using PixelAutomation.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PixelAutomation.Tool.Overlay.WPF;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ConfigurationManager _configManager;
    private readonly List<ClientCard> _clientCards = new();
    private readonly DispatcherTimer _updateTimer;
    private bool _isOverlayMode = false;
    
    // Master Control System
    private bool _masterAttackRunning = false;
    private bool _masterHealRunning = false;
    private bool _panicModeActive = false;
    
    
    // Public access to overlay canvas for client cards
    public System.Windows.Controls.Canvas GetOverlayCanvas() => OverlayCanvas;

    public MainWindow()
    {
        InitializeComponent();
        
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        _configManager = new ConfigurationManager();
        
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();
        
        // Auto-save configuration on window closing
        this.Closing += MainWindow_Closing;
        
        InitializeClientCards();
        InitializeTextBoxes();
        LoadProfiles();
        
        // Auto-assign MuMu windows after UI is fully loaded
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // UI fully loaded, now auto-assign MuMu windows
        AutoAssignMuMuWindows();
    }

    private void InitializeClientCards()
    {
        // Get predefined tab items
        var tabItems = new[] { Client1Tab, Client2Tab, Client3Tab, Client4Tab, Client5Tab, Client6Tab, Client7Tab, Client8Tab };
        
        for (int i = 1; i <= 8; i++)
        {
            var clientCard = new ClientCard();
            clientCard.Initialize(i, $"Client {i}");
            _clientCards.Add(clientCard);
            
            // Set the ClientCard as the content of the corresponding tab
            var tabItem = tabItems[i - 1];
            tabItem.Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = clientCard,
                Padding = new Thickness(10)
            };
        }
    }
    
    private void InitializeTextBoxes()
    {
        // Set default values for TextBoxes (UI controls are hidden)
        // CaptureModeTextBox.Text = "WGC";
        // ClickModeTextBox.Text = "message";
    }

    private void LoadProfiles()
    {
        try
        {
            var config = _configManager.LoadConfiguration("config.json");
            if (config?.Profiles != null)
            {
                if (config.Profiles.Any())
                {
                    // ProfileTextBox.Text = config.Profiles.Keys.First();
                    _viewModel.ActiveProfile = config.Profiles.Keys.First();
                    LoadSelectedProfile();
                }
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Config load failed: {ex.Message}";
        }
    }

    private void LoadSelectedProfile()
    {
        var profileName = _viewModel.ActiveProfile ?? "Default";
        if (!string.IsNullOrEmpty(profileName))
        {
            try
            {
                var config = _configManager.LoadConfiguration("config.json");
                if (config?.Profiles.TryGetValue(profileName, out var profile) == true)
                {
                    _viewModel.ActiveProfile = profileName;
                    // CaptureModeTextBox.Text = profile.Global.CaptureMode ?? "WGC";
                    // ClickModeTextBox.Text = profile.Global.ClickMode ?? "message";
                    
                    // Load window configurations to client cards
                    for (int i = 0; i < Math.Min(profile.Windows.Count, _clientCards.Count); i++)
                    {
                        LoadWindowConfigToCard(_clientCards[i], profile.Windows[i]);
                        _clientCards[i].UpdateUI(); // Update UI after loading config
                    }
                    
                    StatusText.Text = $"Loaded profile: {profileName}";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Profile load failed: {ex.Message}";
            }
        }
    }

    private void LoadWindowConfigToCard(ClientCard card, WindowTarget windowConfig)
    {
        // Load HP probe
        var hpProbe = windowConfig.Probes.FirstOrDefault(p => p.Name.Contains("R") || p.Name.Contains("HP"));
        if (hpProbe != null)
        {
            card.ViewModel.HpProbe.X = hpProbe.X;
            card.ViewModel.HpProbe.Y = hpProbe.Y;
            if (hpProbe.RefColor.Length >= 3)
            {
                var color = System.Drawing.Color.FromArgb(hpProbe.RefColor[0], hpProbe.RefColor[1], hpProbe.RefColor[2]);
                card.ViewModel.HpProbe.ExpectedColor = color;
            }
            card.ViewModel.HpProbe.Tolerance = hpProbe.Tolerance;
        }

        // Load MP probe  
        var mpProbe = windowConfig.Probes.FirstOrDefault(p => p.Name.Contains("B") || p.Name.Contains("MP"));
        if (mpProbe != null)
        {
            card.ViewModel.MpProbe.X = mpProbe.X;
            card.ViewModel.MpProbe.Y = mpProbe.Y;
            if (mpProbe.RefColor.Length >= 3)
            {
                var color = System.Drawing.Color.FromArgb(mpProbe.RefColor[0], mpProbe.RefColor[1], mpProbe.RefColor[2]);
                card.ViewModel.MpProbe.ExpectedColor = color;
            }
            card.ViewModel.MpProbe.Tolerance = mpProbe.Tolerance;
        }

        // Load events (HP/MP trigger clicks)
        var hpEvent = windowConfig.Events.FirstOrDefault(e => e.When.Contains("R") || e.When.Contains("HP"));
        if (hpEvent != null)
        {
            card.ViewModel.HpTrigger.X = hpEvent.Click.X;
            card.ViewModel.HpTrigger.Y = hpEvent.Click.Y;
            card.ViewModel.HpTrigger.CooldownMs = hpEvent.CooldownMs ?? 120;
            card.ViewModel.HpTrigger.Enabled = true;
            card.ViewModel.HpTrigger.UseCoordinate = hpEvent.Click.UseCoordinate;
            card.ViewModel.HpTrigger.UseKeyPress = hpEvent.Click.UseKeyPress;
            card.ViewModel.HpTrigger.KeyToPress = hpEvent.Click.KeyToPress ?? "F1";
        }

        var mpEvent = windowConfig.Events.FirstOrDefault(e => e.When.Contains("B") || e.When.Contains("MP"));
        if (mpEvent != null)
        {
            card.ViewModel.MpTrigger.X = mpEvent.Click.X;
            card.ViewModel.MpTrigger.Y = mpEvent.Click.Y;
            card.ViewModel.MpTrigger.CooldownMs = mpEvent.CooldownMs ?? 120;
            card.ViewModel.MpTrigger.Enabled = true;
            card.ViewModel.MpTrigger.UseCoordinate = mpEvent.Click.UseCoordinate;
            card.ViewModel.MpTrigger.UseKeyPress = mpEvent.Click.UseKeyPress;
            card.ViewModel.MpTrigger.KeyToPress = mpEvent.Click.KeyToPress ?? "F2";
        }

        // Load periodic clicks
        var yClick = windowConfig.PeriodicClicks.FirstOrDefault(p => p.Name == "Y");
        if (yClick != null)
        {
            card.ViewModel.YClick.X = yClick.X;
            card.ViewModel.YClick.Y = yClick.Y;
            card.ViewModel.YClick.PeriodMs = yClick.PeriodMs ?? (int)(yClick.PeriodSec * 1000 ?? 1000);
            card.ViewModel.YClick.Enabled = yClick.Enabled;
            card.ViewModel.YClick.UseCoordinate = yClick.UseCoordinate;
            card.ViewModel.YClick.UseKeyPress = yClick.UseKeyPress;
            card.ViewModel.YClick.KeyToPress = yClick.KeyToPress ?? "Y";
        }

        var extra1Click = windowConfig.PeriodicClicks.FirstOrDefault(p => p.Name == "Extra1");
        if (extra1Click != null)
        {
            card.ViewModel.Extra1Click.X = extra1Click.X;
            card.ViewModel.Extra1Click.Y = extra1Click.Y;
            card.ViewModel.Extra1Click.PeriodMs = extra1Click.PeriodMs ?? (int)(extra1Click.PeriodSec * 1000 ?? 1000);
            card.ViewModel.Extra1Click.Enabled = extra1Click.Enabled;
            card.ViewModel.Extra1Click.UseCoordinate = extra1Click.UseCoordinate;
            card.ViewModel.Extra1Click.UseKeyPress = extra1Click.UseKeyPress;
            card.ViewModel.Extra1Click.KeyToPress = extra1Click.KeyToPress ?? "F3";
        }

        var extra2Click = windowConfig.PeriodicClicks.FirstOrDefault(p => p.Name == "Extra2");
        if (extra2Click != null)
        {
            card.ViewModel.Extra2Click.X = extra2Click.X;
            card.ViewModel.Extra2Click.Y = extra2Click.Y;
            card.ViewModel.Extra2Click.PeriodMs = extra2Click.PeriodMs ?? (int)(extra2Click.PeriodSec * 1000 ?? 1000);
            card.ViewModel.Extra2Click.Enabled = extra2Click.Enabled;
            card.ViewModel.Extra2Click.UseCoordinate = extra2Click.UseCoordinate;
            card.ViewModel.Extra2Click.UseKeyPress = extra2Click.UseKeyPress;
            card.ViewModel.Extra2Click.KeyToPress = extra2Click.KeyToPress ?? "F4";
        }

        var extra3Click = windowConfig.PeriodicClicks.FirstOrDefault(p => p.Name == "Extra3");
        if (extra3Click != null)
        {
            card.ViewModel.Extra3Click.X = extra3Click.X;
            card.ViewModel.Extra3Click.Y = extra3Click.Y;
            card.ViewModel.Extra3Click.PeriodMs = extra3Click.PeriodMs ?? (int)(extra3Click.PeriodSec * 1000 ?? 1000);
            card.ViewModel.Extra3Click.Enabled = extra3Click.Enabled;
            card.ViewModel.Extra3Click.UseCoordinate = extra3Click.UseCoordinate;
            card.ViewModel.Extra3Click.UseKeyPress = extra3Click.UseKeyPress;
            card.ViewModel.Extra3Click.KeyToPress = extra3Click.KeyToPress ?? "F5";
        }

        // Load BabeBot percentage probes
        var babeBotHp = windowConfig.PercentageProbes.FirstOrDefault(p => p.Name.Contains("BabeBotHP"));
        if (babeBotHp != null)
        {
            card.ViewModel.BabeBotHp.StartX = babeBotHp.StartX;
            card.ViewModel.BabeBotHp.EndX = babeBotHp.EndX;
            card.ViewModel.BabeBotHp.Y = babeBotHp.Y;
            card.ViewModel.BabeBotHp.ThresholdPercentage = (int)babeBotHp.MonitorPercentage;
            if (babeBotHp.ExpectedColor.Length >= 3)
            {
                var color = System.Drawing.Color.FromArgb(babeBotHp.ExpectedColor[0], babeBotHp.ExpectedColor[1], babeBotHp.ExpectedColor[2]);
                card.ViewModel.BabeBotHp.ReferenceColor = color;
            }
        }

        var babeBotMp = windowConfig.PercentageProbes.FirstOrDefault(p => p.Name.Contains("BabeBotMP"));
        if (babeBotMp != null)
        {
            card.ViewModel.BabeBotMp.StartX = babeBotMp.StartX;
            card.ViewModel.BabeBotMp.EndX = babeBotMp.EndX;
            card.ViewModel.BabeBotMp.Y = babeBotMp.Y;
            card.ViewModel.BabeBotMp.ThresholdPercentage = (int)babeBotMp.MonitorPercentage;
            if (babeBotMp.ExpectedColor.Length >= 3)
            {
                var color = System.Drawing.Color.FromArgb(babeBotMp.ExpectedColor[0], babeBotMp.ExpectedColor[1], babeBotMp.ExpectedColor[2]);
                card.ViewModel.BabeBotMp.ReferenceColor = color;
            }
        }
        
        // Load Python-style percentage probes
        var pythonHp = windowConfig.PercentageProbes.FirstOrDefault(p => p.Name.Contains("PythonHP"));
        if (pythonHp != null)
        {
            card.ViewModel.HpPercentageProbe.StartX = pythonHp.StartX;
            card.ViewModel.HpPercentageProbe.EndX = pythonHp.EndX;
            card.ViewModel.HpPercentageProbe.Y = pythonHp.Y;
            card.ViewModel.HpPercentageProbe.MonitorPercentage = pythonHp.MonitorPercentage;
            card.ViewModel.HpPercentageProbe.Tolerance = pythonHp.Tolerance;
            if (pythonHp.ExpectedColor.Length >= 3)
            {
                var color = System.Drawing.Color.FromArgb(pythonHp.ExpectedColor[0], pythonHp.ExpectedColor[1], pythonHp.ExpectedColor[2]);
                card.ViewModel.HpPercentageProbe.ExpectedColor = color;
            }
            if (pythonHp.EmptyColor?.Length >= 3)
            {
                var emptyColor = System.Drawing.Color.FromArgb(pythonHp.EmptyColor[0], pythonHp.EmptyColor[1], pythonHp.EmptyColor[2]);
                card.ViewModel.HpPercentageProbe.EmptyColor = emptyColor;
            }
        }

        var pythonMp = windowConfig.PercentageProbes.FirstOrDefault(p => p.Name.Contains("PythonMP"));
        if (pythonMp != null)
        {
            card.ViewModel.MpPercentageProbe.StartX = pythonMp.StartX;
            card.ViewModel.MpPercentageProbe.EndX = pythonMp.EndX;
            card.ViewModel.MpPercentageProbe.Y = pythonMp.Y;
            card.ViewModel.MpPercentageProbe.MonitorPercentage = pythonMp.MonitorPercentage;
            card.ViewModel.MpPercentageProbe.Tolerance = pythonMp.Tolerance;
            if (pythonMp.ExpectedColor.Length >= 3)
            {
                var color = System.Drawing.Color.FromArgb(pythonMp.ExpectedColor[0], pythonMp.ExpectedColor[1], pythonMp.ExpectedColor[2]);
                card.ViewModel.MpPercentageProbe.ExpectedColor = color;
            }
            if (pythonMp.EmptyColor?.Length >= 3)
            {
                var emptyColor = System.Drawing.Color.FromArgb(pythonMp.EmptyColor[0], pythonMp.EmptyColor[1], pythonMp.EmptyColor[2]);
                card.ViewModel.MpPercentageProbe.EmptyColor = emptyColor;
            }
        }

        // Load BabeBot potion clicks
        var babeBotHpClick = windowConfig.PeriodicClicks.FirstOrDefault(p => p.Name == "BabeBotHP");
        if (babeBotHpClick != null)
        {
            card.ViewModel.BabeBotHp.PotionX = babeBotHpClick.X;
            card.ViewModel.BabeBotHp.PotionY = babeBotHpClick.Y;
            card.ViewModel.BabeBotHp.UseCoordinate = babeBotHpClick.UseCoordinate;
            card.ViewModel.BabeBotHp.UseKeyPress = babeBotHpClick.UseKeyPress;
            card.ViewModel.BabeBotHp.KeyToPress = babeBotHpClick.KeyToPress ?? "F6";
        }

        var babeBotMpClick = windowConfig.PeriodicClicks.FirstOrDefault(p => p.Name == "BabeBotMP");
        if (babeBotMpClick != null)
        {
            card.ViewModel.BabeBotMp.PotionX = babeBotMpClick.X;
            card.ViewModel.BabeBotMp.PotionY = babeBotMpClick.Y;
            card.ViewModel.BabeBotMp.UseCoordinate = babeBotMpClick.UseCoordinate;
            card.ViewModel.BabeBotMp.UseKeyPress = babeBotMpClick.UseKeyPress;
            card.ViewModel.BabeBotMp.KeyToPress = babeBotMpClick.KeyToPress ?? "F7";
        }
        
        // Load Python-style potion clicks
        var pythonHpClick = windowConfig.PeriodicClicks.FirstOrDefault(p => p.Name == "PythonHpPotion");
        if (pythonHpClick != null)
        {
            card.ViewModel.PythonHpPotionClick.X = pythonHpClick.X;
            card.ViewModel.PythonHpPotionClick.Y = pythonHpClick.Y;
            card.ViewModel.PythonHpPotionClick.CooldownMs = pythonHpClick.PeriodMs ?? 500;
            card.ViewModel.PythonHpPotionClick.Enabled = pythonHpClick.Enabled;
            card.ViewModel.PythonHpPotionClick.UseCoordinate = pythonHpClick.UseCoordinate;
            card.ViewModel.PythonHpPotionClick.UseKeyPress = pythonHpClick.UseKeyPress;
            card.ViewModel.PythonHpPotionClick.KeyToPress = pythonHpClick.KeyToPress ?? "Q";
        }

        var pythonMpClick = windowConfig.PeriodicClicks.FirstOrDefault(p => p.Name == "PythonMpPotion");
        if (pythonMpClick != null)
        {
            card.ViewModel.PythonMpPotionClick.X = pythonMpClick.X;
            card.ViewModel.PythonMpPotionClick.Y = pythonMpClick.Y;
            card.ViewModel.PythonMpPotionClick.CooldownMs = pythonMpClick.PeriodMs ?? 500;
            card.ViewModel.PythonMpPotionClick.Enabled = pythonMpClick.Enabled;
            card.ViewModel.PythonMpPotionClick.UseCoordinate = pythonMpClick.UseCoordinate;
            card.ViewModel.PythonMpPotionClick.UseKeyPress = pythonMpClick.UseKeyPress;
            card.ViewModel.PythonMpPotionClick.KeyToPress = pythonMpClick.KeyToPress ?? "W";
        }
        
        // Load all UI settings from saved configuration
        if (windowConfig.UISettings?.Values != null && windowConfig.UISettings.Values.Any())
        {
            card.SetAllUIValues(windowConfig.UISettings.Values);
        }
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        // Update connected clients count
        var connectedCount = _clientCards.Count(c => c.ViewModel.TargetHwnd != IntPtr.Zero);
        ConnectedClientsText.Text = $"{connectedCount}/8";
        
        // Update total stats
        var totalClicks = _clientCards.Sum(c => c.ViewModel.ClickCount);
        var runningClients = _clientCards.Where(c => c.ViewModel.IsRunning).ToList();
        var averageFps = runningClients.Any() ? runningClients.Average(c => c.ViewModel.Fps) : 0.0;
        var activeWorkers = _clientCards.Count(c => c.ViewModel.IsRunning);
        
        TotalClicksText.Text = totalClicks.ToString();
        FpsText.Text = double.IsNaN(averageFps) ? "0" : averageFps.ToString("F1");
        ActiveWorkersText.Text = activeWorkers.ToString();
        
        // Update individual client cards with mock data for demo
        foreach (var card in _clientCards.Where(c => c.ViewModel.IsRunning))
        {
            var mockFps = 60 + (DateTime.Now.Millisecond % 40);
            var mockClicks = card.ViewModel.ClickCount + (DateTime.Now.Second % 3 == 0 ? 1 : 0);
            var mockTriggers = card.ViewModel.TriggerCount + (DateTime.Now.Second % 10 == 0 ? 1 : 0);
            
            card.UpdateStats(mockFps, mockClicks, mockTriggers);
        }
    }

    private void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var config = BuildConfigFromClientCards();
            _configManager.SaveConfiguration("config.json", config);
            StatusText.Text = "Configuration saved successfully";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Save failed: {ex.Message}";
            
            // Write error to file and console for easy copying
            var errorLog = $"SAVE ERROR - {DateTime.Now}\n\nMessage: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}\n\n" + new string('=', 80) + "\n\n";
            
            try
            {
                System.IO.File.AppendAllText("error_log.txt", errorLog);
                Console.WriteLine("=== SAVE ERROR ===");
                Console.WriteLine(errorLog);
            }
            catch
            {
                // File write failed, just show in console
                Console.WriteLine("=== SAVE ERROR ===");
                Console.WriteLine(errorLog);
            }
            
            // Error logged to console and file
        }
    }

    private Configuration BuildConfigFromClientCards()
    {
        var config = new Configuration();
        var profileName = _viewModel.ActiveProfile ?? "Default";
        
        var profile = new ProfileConfig
        {
            Global = new GlobalConfig
            {
                CaptureMode = "WGC", // Default since UI control is hidden
                ClickMode = "message", // Default since UI control is hidden
                DefaultHz = 80
            },
            Windows = new List<WindowTarget>()
        };

        foreach (var card in _clientCards)
        {
            var window = new WindowTarget
            {
                TitleRegex = card.ViewModel.ClientName,
                HwndString = card.ViewModel.TargetHwnd != IntPtr.Zero ? card.ViewModel.TargetHwnd.ToInt64().ToString() : null,
                Probes = new List<ProbeConfig>
                {
                    new ProbeConfig
                    {
                        Name = $"HP{card.ClientId}",
                        Kind = "point",
                        X = card.ViewModel.HpProbe.X,
                        Y = card.ViewModel.HpProbe.Y,
                        Box = 5,
                        Mode = "edge",
                        Metric = "rgb",
                        RefColor = new[] { (int)card.ViewModel.HpProbe.ExpectedColor.R, (int)card.ViewModel.HpProbe.ExpectedColor.G, (int)card.ViewModel.HpProbe.ExpectedColor.B },
                        ToColor = new[] { (int)card.ViewModel.HpProbe.TriggerColor.R, (int)card.ViewModel.HpProbe.TriggerColor.G, (int)card.ViewModel.HpProbe.TriggerColor.B },
                        Tolerance = card.ViewModel.HpProbe.Tolerance,
                        DebounceMs = 30
                    },
                    new ProbeConfig
                    {
                        Name = $"MP{card.ClientId}",
                        Kind = "point",
                        X = card.ViewModel.MpProbe.X,
                        Y = card.ViewModel.MpProbe.Y,
                        Box = 5,
                        Mode = "edge",
                        Metric = "rgb",
                        RefColor = new[] { (int)card.ViewModel.MpProbe.ExpectedColor.R, (int)card.ViewModel.MpProbe.ExpectedColor.G, (int)card.ViewModel.MpProbe.ExpectedColor.B },
                        ToColor = new[] { (int)card.ViewModel.MpProbe.TriggerColor.R, (int)card.ViewModel.MpProbe.TriggerColor.G, (int)card.ViewModel.MpProbe.TriggerColor.B },
                        Tolerance = card.ViewModel.MpProbe.Tolerance,
                        DebounceMs = 30
                    }
                },
                Events = new List<EventConfig>
                {
                    new EventConfig
                    {
                        When = $"HP{card.ClientId}:edge-down",
                        Click = new ClickTarget { 
                            X = card.ViewModel.HpTrigger.X, 
                            Y = card.ViewModel.HpTrigger.Y,
                            UseCoordinate = card.ViewModel.HpTrigger.UseCoordinate,
                            UseKeyPress = card.ViewModel.HpTrigger.UseKeyPress,
                            KeyToPress = card.ViewModel.HpTrigger.KeyToPress
                        },
                        CooldownMs = card.ViewModel.HpTrigger.CooldownMs,
                        Priority = 1
                    },
                    new EventConfig
                    {
                        When = $"MP{card.ClientId}:edge-down",
                        Click = new ClickTarget { 
                            X = card.ViewModel.MpTrigger.X, 
                            Y = card.ViewModel.MpTrigger.Y,
                            UseCoordinate = card.ViewModel.MpTrigger.UseCoordinate,
                            UseKeyPress = card.ViewModel.MpTrigger.UseKeyPress,
                            KeyToPress = card.ViewModel.MpTrigger.KeyToPress
                        },
                        CooldownMs = card.ViewModel.MpTrigger.CooldownMs,
                        Priority = 1
                    }
                },
                PercentageProbes = new List<PercentageProbeConfig>
                {
                    new PercentageProbeConfig
                    {
                        Name = $"BabeBotHP{card.ClientId}",
                        Type = "HP",
                        StartX = card.ViewModel.BabeBotHp.StartX,
                        EndX = card.ViewModel.BabeBotHp.EndX,
                        Y = card.ViewModel.BabeBotHp.Y,
                        MonitorPercentage = GetBabeBotThresholdValue(card, "HP"),
                        ExpectedColor = new[] { (int)card.ViewModel.BabeBotHp.ReferenceColor.R, (int)card.ViewModel.BabeBotHp.ReferenceColor.G, (int)card.ViewModel.BabeBotHp.ReferenceColor.B },
                        Tolerance = 30
                    },
                    new PercentageProbeConfig
                    {
                        Name = $"BabeBotMP{card.ClientId}",
                        Type = "MP",
                        StartX = card.ViewModel.BabeBotMp.StartX,
                        EndX = card.ViewModel.BabeBotMp.EndX,
                        Y = card.ViewModel.BabeBotMp.Y,
                        MonitorPercentage = GetBabeBotThresholdValue(card, "MP"),
                        ExpectedColor = new[] { (int)card.ViewModel.BabeBotMp.ReferenceColor.R, (int)card.ViewModel.BabeBotMp.ReferenceColor.G, (int)card.ViewModel.BabeBotMp.ReferenceColor.B },
                        Tolerance = 30
                    },
                    new PercentageProbeConfig
                    {
                        Name = $"PythonHP{card.ClientId}",
                        Type = "HP",
                        StartX = card.ViewModel.HpPercentageProbe.StartX,
                        EndX = card.ViewModel.HpPercentageProbe.EndX,
                        Y = card.ViewModel.HpPercentageProbe.Y,
                        MonitorPercentage = card.ViewModel.HpPercentageProbe.MonitorPercentage,
                        ExpectedColor = new[] { (int)card.ViewModel.HpPercentageProbe.ExpectedColor.R, (int)card.ViewModel.HpPercentageProbe.ExpectedColor.G, (int)card.ViewModel.HpPercentageProbe.ExpectedColor.B },
                        EmptyColor = card.ViewModel.HpPercentageProbe.EmptyColor.HasValue ? new[] { (int)card.ViewModel.HpPercentageProbe.EmptyColor.Value.R, (int)card.ViewModel.HpPercentageProbe.EmptyColor.Value.G, (int)card.ViewModel.HpPercentageProbe.EmptyColor.Value.B } : null,
                        Tolerance = card.ViewModel.HpPercentageProbe.Tolerance
                    },
                    new PercentageProbeConfig
                    {
                        Name = $"PythonMP{card.ClientId}",
                        Type = "MP",
                        StartX = card.ViewModel.MpPercentageProbe.StartX,
                        EndX = card.ViewModel.MpPercentageProbe.EndX,
                        Y = card.ViewModel.MpPercentageProbe.Y,
                        MonitorPercentage = card.ViewModel.MpPercentageProbe.MonitorPercentage,
                        ExpectedColor = new[] { (int)card.ViewModel.MpPercentageProbe.ExpectedColor.R, (int)card.ViewModel.MpPercentageProbe.ExpectedColor.G, (int)card.ViewModel.MpPercentageProbe.ExpectedColor.B },
                        EmptyColor = card.ViewModel.MpPercentageProbe.EmptyColor.HasValue ? new[] { (int)card.ViewModel.MpPercentageProbe.EmptyColor.Value.R, (int)card.ViewModel.MpPercentageProbe.EmptyColor.Value.G, (int)card.ViewModel.MpPercentageProbe.EmptyColor.Value.B } : null,
                        Tolerance = card.ViewModel.MpPercentageProbe.Tolerance
                    }
                }.Where(p => p != null).ToList(),
                PeriodicClicks = new List<PeriodicClickConfig>
                {
                    new PeriodicClickConfig
                    {
                        Name = "BabeBotHP",
                        X = card.ViewModel.BabeBotHp.PotionX,
                        Y = card.ViewModel.BabeBotHp.PotionY,
                        PeriodMs = 500, // Default cooldown
                        Enabled = card.ViewModel.BabeBotHp.Enabled,
                        UseCoordinate = card.ViewModel.BabeBotHp.UseCoordinate,
                        UseKeyPress = card.ViewModel.BabeBotHp.UseKeyPress,
                        KeyToPress = card.ViewModel.BabeBotHp.KeyToPress
                    },
                    new PeriodicClickConfig
                    {
                        Name = "BabeBotMP",
                        X = card.ViewModel.BabeBotMp.PotionX,
                        Y = card.ViewModel.BabeBotMp.PotionY,
                        PeriodMs = 500, // Default cooldown
                        Enabled = card.ViewModel.BabeBotMp.Enabled,
                        UseCoordinate = card.ViewModel.BabeBotMp.UseCoordinate,
                        UseKeyPress = card.ViewModel.BabeBotMp.UseKeyPress,
                        KeyToPress = card.ViewModel.BabeBotMp.KeyToPress
                    },
                    new PeriodicClickConfig
                    {
                        Name = "Y",
                        X = card.ViewModel.YClick.X,
                        Y = card.ViewModel.YClick.Y,
                        PeriodMs = card.ViewModel.YClick.PeriodMs,
                        Enabled = card.ViewModel.YClick.Enabled,
                        UseCoordinate = card.ViewModel.YClick.UseCoordinate,
                        UseKeyPress = card.ViewModel.YClick.UseKeyPress,
                        KeyToPress = card.ViewModel.YClick.KeyToPress
                    },
                    new PeriodicClickConfig
                    {
                        Name = "Extra1",
                        X = card.ViewModel.Extra1Click.X,
                        Y = card.ViewModel.Extra1Click.Y,
                        PeriodMs = card.ViewModel.Extra1Click.PeriodMs,
                        Enabled = card.ViewModel.Extra1Click.Enabled,
                        UseCoordinate = card.ViewModel.Extra1Click.UseCoordinate,
                        UseKeyPress = card.ViewModel.Extra1Click.UseKeyPress,
                        KeyToPress = card.ViewModel.Extra1Click.KeyToPress
                    },
                    new PeriodicClickConfig
                    {
                        Name = "Extra2",
                        X = card.ViewModel.Extra2Click.X,
                        Y = card.ViewModel.Extra2Click.Y,
                        PeriodMs = card.ViewModel.Extra2Click.PeriodMs,
                        Enabled = card.ViewModel.Extra2Click.Enabled,
                        UseCoordinate = card.ViewModel.Extra2Click.UseCoordinate,
                        UseKeyPress = card.ViewModel.Extra2Click.UseKeyPress,
                        KeyToPress = card.ViewModel.Extra2Click.KeyToPress
                    },
                    new PeriodicClickConfig
                    {
                        Name = "Extra3",
                        X = card.ViewModel.Extra3Click.X,
                        Y = card.ViewModel.Extra3Click.Y,
                        PeriodMs = card.ViewModel.Extra3Click.PeriodMs,
                        Enabled = card.ViewModel.Extra3Click.Enabled,
                        UseCoordinate = card.ViewModel.Extra3Click.UseCoordinate,
                        UseKeyPress = card.ViewModel.Extra3Click.UseKeyPress,
                        KeyToPress = card.ViewModel.Extra3Click.KeyToPress
                    },
                    new PeriodicClickConfig
                    {
                        Name = "PythonHpPotion",
                        X = card.ViewModel.PythonHpPotionClick.X,
                        Y = card.ViewModel.PythonHpPotionClick.Y,
                        PeriodMs = card.ViewModel.PythonHpPotionClick.CooldownMs,
                        Enabled = card.ViewModel.PythonHpPotionClick.Enabled,
                        UseCoordinate = card.ViewModel.PythonHpPotionClick.UseCoordinate,
                        UseKeyPress = card.ViewModel.PythonHpPotionClick.UseKeyPress,
                        KeyToPress = card.ViewModel.PythonHpPotionClick.KeyToPress
                    },
                    new PeriodicClickConfig
                    {
                        Name = "PythonMpPotion",
                        X = card.ViewModel.PythonMpPotionClick.X,
                        Y = card.ViewModel.PythonMpPotionClick.Y,
                        PeriodMs = card.ViewModel.PythonMpPotionClick.CooldownMs,
                        Enabled = card.ViewModel.PythonMpPotionClick.Enabled,
                        UseCoordinate = card.ViewModel.PythonMpPotionClick.UseCoordinate,
                        UseKeyPress = card.ViewModel.PythonMpPotionClick.UseKeyPress,
                        KeyToPress = card.ViewModel.PythonMpPotionClick.KeyToPress
                    }
                }.Where(p => p != null).ToList(),
                
                // Save all UI settings from ClientCard
                UISettings = new UISettings { Values = card.GetAllUIValues() }
            };
            
            profile.Windows.Add(window);
        }

        config.Profiles[profileName] = profile;
        return config;
    }

    private void LoadConfig_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var config = _configManager.LoadConfiguration(openFileDialog.FileName);
                if (config != null)
                {
                    _viewModel.UpdateFromConfig(config);
                    LoadProfiles();
                    StatusText.Text = "Configuration loaded successfully";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Load failed: {ex.Message}";
            }
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Settings: Use dropdown controls above for global settings";
    }

    private void PreviousProfile_Click(object sender, RoutedEventArgs e)
    {
        var config = _configManager.LoadConfiguration("config.json");
        if (config?.Profiles != null)
        {
            var profiles = config.Profiles.Keys.ToList();
            var currentIndex = profiles.IndexOf(_viewModel.ActiveProfile ?? "Default");
            if (currentIndex > 0)
            {
                _viewModel.ActiveProfile = profiles[currentIndex - 1];
                LoadSelectedProfile();
            }
        }
    }

    private void NextProfile_Click(object sender, RoutedEventArgs e)
    {
        var config = _configManager.LoadConfiguration("config.json");
        if (config?.Profiles != null)
        {
            var profiles = config.Profiles.Keys.ToList();
            var currentIndex = profiles.IndexOf(_viewModel.ActiveProfile ?? "Default");
            if (currentIndex < profiles.Count - 1)
            {
                _viewModel.ActiveProfile = profiles[currentIndex + 1];
                LoadSelectedProfile();
            }
        }
    }

    private void OverlayMode_Checked(object sender, RoutedEventArgs e)
    {
        _isOverlayMode = true;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Topmost = true;
        
        // Set bounds to cover all monitors instead of just maximizing
        SetMultiMonitorBounds();
        
        OverlayCanvas.Visibility = Visibility.Visible;
        StatusText.Text = "Overlay mode activated - ESC to exit, drag HP/MP shapes to adjust";
        
        // Show draggable HP/MP shapes for all client cards
        foreach (var clientCard in _clientCards)
        {
            clientCard.ShowOverlayShapes();
        }
    }

    private void OverlayMode_Unchecked(object sender, RoutedEventArgs e)
    {
        _isOverlayMode = false;
        WindowStyle = WindowStyle.SingleBorderWindow;
        AllowsTransparency = false;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1e, 0x1e, 0x1e));
        Topmost = false;
        WindowState = WindowState.Normal;
        OverlayCanvas.Visibility = Visibility.Collapsed;
        StatusText.Text = "Control panel mode";
        
        // Hide draggable HP/MP shapes for all client cards
        foreach (var clientCard in _clientCards)
        {
            clientCard.HideOverlayShapes();
        }
    }

    private void PanicToggle_Click(object sender, RoutedEventArgs e)
    {
        if (!_panicModeActive)
        {
            // PANIC START - Check if any window is selected
            bool anyWindowSelected = _clientCards.Any(card => card.ViewModel.TargetHwnd != IntPtr.Zero);
            if (!anyWindowSelected)
            {
                MessageBox.Show("Please select at least one window before using PANIC START!", "No Windows Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            int startedCount = 0;
            foreach (var card in _clientCards)
            {
                if (card.ViewModel.TargetHwnd != IntPtr.Zero)
                {
                    card.StartClient();
                    startedCount++;
                }
            }
            
            // Update button state
            _panicModeActive = true;
            PanicToggleButton.Content = "🚨 PANIC STOP";
            PanicToggleButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xdc, 0x35, 0x45)); // #dc3545 red
            StatusText.Text = $"🚀 PANIC START - {startedCount} clients started";
        }
        else
        {
            // PANIC STOP
            foreach (var card in _clientCards)
            {
                card.StopClient();
            }
            
            // Update button state
            _panicModeActive = false;
            PanicToggleButton.Content = "🚀 PANIC START";
            PanicToggleButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x28, 0xa7, 0x45)); // #28a745 green
            StatusText.Text = "🚨 PANIC STOP - All automation stopped";
        }
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);
        
        if (e.Key == System.Windows.Input.Key.Escape && _isOverlayMode)
        {
            // OverlayModeCheckBox.IsChecked = false; // Control is hidden
            // Manually trigger overlay mode off
            OverlayMode_Unchecked(this, new RoutedEventArgs());
        }
    }
    
    public void SyncHpColorToAllClients(System.Drawing.Color color, ClientCard sourceCard)
    {
        foreach (var card in _clientCards)
        {
            if (card != sourceCard) // Don't update the source card
            {
                card.SetHpColorFromSync(color);
            }
        }
        StatusText.Text = $"HP color synced to all clients: RGB({color.R},{color.G},{color.B})";
    }
    
    public void SyncMpColorToAllClients(System.Drawing.Color color, ClientCard sourceCard)
    {
        foreach (var card in _clientCards)
        {
            if (card != sourceCard) // Don't update the source card
            {
                card.SetMpColorFromSync(color);
            }
        }
        StatusText.Text = $"MP color synced to all clients: RGB({color.R},{color.G},{color.B})";
    }
    
    // TextBox helper methods for global configuration
    private void SetGlobalConfigFromTextBoxes()
    {
        // Global configuration is automatically saved via TextBox.Text properties
        // No additional logic needed since we directly read from TextBox.Text
    }
    
    private void SetMultiMonitorBounds()
    {
        // Calculate total bounds across all monitors
        var leftmost = System.Windows.Forms.Screen.AllScreens.Min(s => s.Bounds.Left);
        var topmost = System.Windows.Forms.Screen.AllScreens.Min(s => s.Bounds.Top);
        var rightmost = System.Windows.Forms.Screen.AllScreens.Max(s => s.Bounds.Right);
        var bottommost = System.Windows.Forms.Screen.AllScreens.Max(s => s.Bounds.Bottom);
        
        // Set window to cover entire virtual screen
        Left = leftmost;
        Top = topmost;
        Width = rightmost - leftmost;
        Height = bottommost - topmost;
        
        // Debug info
        Console.WriteLine($"MainWindow overlay bounds: ({leftmost},{topmost}) - ({rightmost},{bottommost}) Size: {Width}x{Height}");
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            Console.WriteLine($"Screen {screen.DeviceName}: {screen.Bounds} Primary: {screen.Primary}");
        }
    }
    
    private void AutoAssignMuMuWindows()
    {
        try
        {
            Console.WriteLine("[AUTO-ASSIGN] Searching for MuMu Player prest windows...");
            
            // Find all MuMu-related processes
            var mumuWindows = new Dictionary<int, (IntPtr hwnd, string title, string processName)>();
            
            // Enumerate all windows
            User32.EnumWindows((hwnd, lParam) =>
            {
                try
                {
                    // Get process info
                    User32.GetWindowThreadProcessId(hwnd, out var processId);
                    var process = Process.GetProcessById((int)processId);
                    var processName = process.ProcessName;
                    
                    // Check if it's a MuMu related process
                    if (processName.Contains("MuMu") || processName.Contains("prest") || processName.Contains("Nemu"))
                    {
                        // Get window title
                        var titleLength = User32.GetWindowTextLength(hwnd);
                        if (titleLength > 0)
                        {
                            var title = new System.Text.StringBuilder(titleLength + 1);
                            User32.GetWindowText(hwnd, title, title.Capacity);
                            var windowTitle = title.ToString();
                            
                            // Check if window is visible
                            if (User32.IsWindowVisible(hwnd))
                            {
                                Console.WriteLine($"[AUTO-ASSIGN] Found: HWND=0x{(IntPtr)hwnd:X8} Process='{processName}' Title='{windowTitle}'");
                                
                                // Try to extract instance number from process name or title
                                int instanceNumber = ExtractInstanceNumber(processName, windowTitle);
                                if (instanceNumber > 0 && instanceNumber <= 8)
                                {
                                    // Prioritize prest windows over Android Device windows
                                    if (!mumuWindows.ContainsKey(instanceNumber) || windowTitle.StartsWith("prest"))
                                    {
                                        mumuWindows[instanceNumber] = ((IntPtr)hwnd, windowTitle, processName);
                                        Console.WriteLine($"[AUTO-ASSIGN] Mapped to Client {instanceNumber} (Priority: {(windowTitle.StartsWith("prest") ? "HIGH" : "LOW")})");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"[AUTO-ASSIGN] Skipped duplicate for Client {instanceNumber}: {windowTitle}");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Skip invalid processes
                    Console.WriteLine($"[AUTO-ASSIGN] Error checking process: {ex.Message}");
                }
                
                return true;
            }, IntPtr.Zero);
            
            // Assign found windows to client cards
            int assignedCount = 0;
            foreach (var kvp in mumuWindows)
            {
                int clientIndex = kvp.Key - 1; // Convert to 0-based index
                if (clientIndex >= 0 && clientIndex < _clientCards.Count)
                {
                    var clientCard = _clientCards[clientIndex];
                    var (hwnd, title, processName) = kvp.Value;
                    
                    // Assign to client
                    clientCard.ViewModel.TargetHwnd = hwnd;
                    clientCard.ViewModel.WindowTitle = title;
                    
                    // Update UI on main thread
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // Update ViewModel directly - this is TabControl-independent
                            clientCard.ViewModel.WindowTitle = $"{title} - 0x{hwnd:X8}";
                            
                            Console.WriteLine($"[AUTO-ASSIGN] Updated Client {kvp.Key}: {title} (0x{hwnd:X8})");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[AUTO-ASSIGN] UI update error for Client {kvp.Key}: {ex.Message}");
                        }
                    }));
                    
                    Console.WriteLine($"[AUTO-ASSIGN] ✅ Client {kvp.Key} = HWND 0x{hwnd:X8} ({title})");
                    assignedCount++;
                }
            }
            
            Console.WriteLine($"[AUTO-ASSIGN] Successfully assigned {assignedCount} MuMu windows to clients");
            
            if (assignedCount == 0)
            {
                Console.WriteLine("[AUTO-ASSIGN] ⚠️ No MuMu Player windows found. Make sure MuMu Player instances are running.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AUTO-ASSIGN] Error: {ex.Message}");
        }
    }
    
    private int ExtractInstanceNumber(string processName, string windowTitle)
    {
        // Try to extract number from window title first (prest121, prest122, etc.)
        if (windowTitle.StartsWith("prest") && windowTitle.Length >= 8) // prest12X
        {
            var lastDigit = windowTitle.Substring(windowTitle.Length - 1);
            if (int.TryParse(lastDigit, out var instanceNum) && instanceNum >= 1 && instanceNum <= 8)
            {
                Console.WriteLine($"[AUTO-ASSIGN] Extracted from prest title '{windowTitle}' → Client {instanceNum}");
                return instanceNum;
            }
        }
        
        // Try to extract from window title
        if (windowTitle.Contains("RO"))
        {
            // Look for patterns like "RO - 1", "RO Client 2", etc.
            var words = windowTitle.Split(' ', '-', '_');
            foreach (var word in words)
            {
                if (int.TryParse(word.Trim(), out var num) && num >= 1 && num <= 8)
                {
                    return num;
                }
            }
        }
        
        // Try MuMu instance patterns
        if (processName.Contains("MuMu") || windowTitle.Contains("MuMu"))
        {
            // Look for MuMu-1, MuMu-2, etc.
            var parts = windowTitle.Split('-', '_', ' ');
            foreach (var part in parts)
            {
                if (int.TryParse(part.Trim(), out var num) && num >= 1 && num <= 8)
                {
                    return num;
                }
            }
        }
        
        return 0; // No valid instance number found
    }
    
    private T FindChild<T>(System.Windows.DependencyObject parent, string name) where T : System.Windows.DependencyObject
    {
        if (parent == null) return null;
        
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            
            if (child is T && (child as System.Windows.FrameworkElement)?.Name == name)
            {
                return (T)child;
            }
            
            var childOfChild = FindChild<T>(child, name);
            if (childOfChild != null)
                return childOfChild;
        }
        
        return null;
    }
    
    private double GetBabeBotThresholdValue(ClientCard card, string type)
    {
        if (type == "HP")
        {
            return card.GetBabeBotHpThreshold();
        }
        else if (type == "MP")
        {
            return card.GetBabeBotMpThreshold();
        }
        
        return 90.0; // Default threshold
    }

    #region Master Control System

    private void MasterAttack_Click(object sender, RoutedEventArgs e)
    {
        ToggleMasterAttack();
    }

    private void MasterHeal_Click(object sender, RoutedEventArgs e)
    {
        ToggleMasterHeal();
    }


    private void ToggleMasterAttack()
    {
        // Check if any client has a selected window
        var clientsWithWindow = _clientCards.Where(c => c.ViewModel.TargetHwnd != IntPtr.Zero).ToList();
        
        if (!clientsWithWindow.Any())
        {
            MessageBox.Show("Please select windows for clients first!", "No Windows Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            UpdateMasterAttackButton(false);
            return;
        }
        
        if (_masterAttackRunning)
        {
            // MASTER STOP - Stop all attack systems
            _masterAttackRunning = false;
            
            foreach (var card in _clientCards)
            {
                if (card.ViewModel.TargetHwnd != IntPtr.Zero)
                {
                    StopAttackForCard(card);
                }
            }
            
            UpdateMasterAttackButton(false);
            Console.WriteLine("🛑 MASTER ATTACK STOP - All attack systems disabled");
            
            // Show debug console message
            Application.Current.Dispatcher.Invoke(() =>
            {
                Console.WriteLine($"🛑 MASTER STOP - Stopped attack systems for {clientsWithWindow.Count} clients");
            });
        }
        else
        {
            // MASTER START - Start all attack systems
            _masterAttackRunning = true;
            
            int startedCount = 0;
            foreach (var card in _clientCards)
            {
                if (card.ViewModel.TargetHwnd != IntPtr.Zero)
                {
                    if (StartAttackForCard(card))
                    {
                        startedCount++;
                    }
                }
            }
            
            UpdateMasterAttackButton(true);
            
            Console.WriteLine($"🚀 MASTER ATTACK START - Attack systems activated for {startedCount}/{clientsWithWindow.Count} clients!");
            
            // Show debug console message
            Application.Current.Dispatcher.Invoke(() =>
            {
                Console.WriteLine($"🚀 MASTER START - Started attack systems for {startedCount} clients with enabled skills");
            });
        }
    }

    private void ToggleMasterHeal()
    {
        // Check if any client has a selected window
        var clientsWithWindow = _clientCards.Where(c => c.ViewModel.TargetHwnd != IntPtr.Zero).ToList();
        
        if (!clientsWithWindow.Any())
        {
            MessageBox.Show("Please select windows for clients first!", "No Windows Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            UpdateMasterHealButton(false);
            return;
        }
        
        if (_masterHealRunning)
        {
            // MASTER STOP - Stop all heal systems
            _masterHealRunning = false;
            
            foreach (var card in _clientCards)
            {
                if (card.ViewModel.TargetHwnd != IntPtr.Zero)
                {
                    StopHealForCard(card);
                }
            }
            
            UpdateMasterHealButton(false);
            Console.WriteLine("🛑 MASTER HEAL STOP - All heal systems disabled");
            
            // Show debug console message
            Application.Current.Dispatcher.Invoke(() =>
            {
                Console.WriteLine($"🛑 MASTER STOP - Stopped heal systems for {clientsWithWindow.Count} clients");
            });
        }
        else
        {
            // MASTER START - Start all heal systems
            _masterHealRunning = true;
            
            int startedCount = 0;
            foreach (var card in _clientCards)
            {
                if (card.ViewModel.TargetHwnd != IntPtr.Zero)
                {
                    if (StartHealForCard(card))
                    {
                        startedCount++;
                    }
                }
            }
            
            UpdateMasterHealButton(true);
            
            Console.WriteLine($"🚀 MASTER HEAL START - Heal systems activated for {startedCount}/{clientsWithWindow.Count} clients!");
            
            // Show debug console message  
            Application.Current.Dispatcher.Invoke(() =>
            {
                Console.WriteLine($"🚀 MASTER START - Started heal systems for {startedCount} clients with enabled members");
            });
        }
    }

    private bool StartAttackForCard(ClientCard card)
    {
        try
        {
            // Check if card has any enabled attack skills
            var enabledSkills = card.ViewModel.AttackSkills.Where(s => s.Enabled).ToList();
            if (!enabledSkills.Any())
            {
                return false; // No enabled skills
            }
            
            // Use reflection to call StartAttackSystem method
            var method = typeof(ClientCard).GetMethod("StartAttackSystem", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(card, null);
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to start attack for {card.ViewModel.ClientName}: {ex.Message}");
            return false;
        }
    }

    private void StopAttackForCard(ClientCard card)
    {
        try
        {
            // Use reflection to call StopAttackSystem method
            var method = typeof(ClientCard).GetMethod("StopAttackSystem", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(card, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to stop attack for {card.ViewModel.ClientName}: {ex.Message}");
        }
    }

    private bool StartHealForCard(ClientCard card)
    {
        try
        {
            // MultiHp system has been removed - always return false for now
            // TODO: Replace with equivalent non-MultiHp heal logic if needed
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to start heal for {card.ViewModel.ClientName}: {ex.Message}");
            return false;
        }
    }

    private void StopHealForCard(ClientCard card)
    {
        try
        {
            // MultiHp system has been removed - no action needed for now
            // TODO: Replace with equivalent non-MultiHp heal logic if needed
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to stop heal for {card.ViewModel.ClientName}: {ex.Message}");
        }
    }

    private void UpdateMasterAttackButton(bool active)
    {
        // UI control is hidden, just log the state
        Console.WriteLine($"[MASTER ATTACK] State changed to: {(active ? "RUNNING" : "STOPPED")}");
        // Application.Current.Dispatcher.Invoke(() =>
        // {
        //     if (active)
        //     {
        //         MasterAttackButton.Content = "⏹ Attack";
        //         ((Border)MasterAttackButton.Parent).Background = new System.Windows.Media.SolidColorBrush(
        //             System.Windows.Media.Color.FromRgb(76, 175, 80)); // Green #4CAF50
        //     }
        //     else
        //     {
        //         MasterAttackButton.Content = "▶ Attack";
        //         ((Border)MasterAttackButton.Parent).Background = new System.Windows.Media.SolidColorBrush(
        //             System.Windows.Media.Color.FromRgb(255, 87, 34)); // Orange #FF5722
        //     }
        // });
    }

    private void UpdateMasterHealButton(bool active)
    {
        // UI control is hidden, just log the state
        Console.WriteLine($"[MASTER HEAL] State changed to: {(active ? "RUNNING" : "STOPPED")}");
        // Application.Current.Dispatcher.Invoke(() =>
        // {
        //     if (active)
        //     {
        //         MasterHealButton.Content = "⏹ Heal/Buff";
        //         ((Border)MasterHealButton.Parent).Background = new System.Windows.Media.SolidColorBrush(
        //             System.Windows.Media.Color.FromRgb(76, 175, 80)); // Green #4CAF50
        //     }
        //     else
        //     {
        //         MasterHealButton.Content = "▶ Heal/Buff";
        //         ((Border)MasterHealButton.Parent).Background = new System.Windows.Media.SolidColorBrush(
        //             System.Windows.Media.Color.FromRgb(156, 39, 176)); // Purple #9C27B0
        //     }
        // });
    }
    
    private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            // Auto-save current configuration when closing
            var config = BuildConfigFromClientCards();
            _configManager.SaveConfiguration("config.json", config);
            Console.WriteLine("[MainWindow] ✅ Configuration auto-saved on exit");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainWindow] ❌ Error auto-saving configuration: {ex.Message}");
        }
    }

    #endregion
}