using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using PixelAutomation.Core.Models;
using PixelAutomation.Tool.Overlay.WPF.Services;
using PixelAutomation.Tool.Overlay.WPF.ViewModels;
using PixelAutomation.Tool.Overlay.WPF.Controls;
using PixelAutomation.Tool.Overlay.WPF.Models;
using Vanara.PInvoke;
using System.Diagnostics;

namespace PixelAutomation.Tool.Overlay.WPF;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ConfigurationManager _configManager;
    private readonly List<ClientCard> _clientCards = new();
    private readonly DispatcherTimer _updateTimer;
    private bool _isOverlayMode = false;

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
        
        InitializeClientCards();
        InitializeComboBoxes();
        LoadProfiles();
        AutoAssignMuMuWindows(); // Auto-assign MuMu prest windows
    }

    private void InitializeClientCards()
    {
        for (int i = 1; i <= 8; i++)
        {
            var clientCard = new ClientCard();
            clientCard.Initialize(i, $"Client {i}");
            _clientCards.Add(clientCard);
            ClientGrid.Children.Add(clientCard);
        }
    }
    
    private void InitializeComboBoxes()
    {
        // Set default values for dropdown
        CaptureModeComboBox.SelectedIndex = 0; // WGC
        ClickModeComboBox.SelectedIndex = 0; // message
    }

    private void LoadProfiles()
    {
        try
        {
            var config = _configManager.LoadConfiguration("config.json");
            if (config?.Profiles != null)
            {
                ProfileComboBox.ItemsSource = config.Profiles.Keys;
                if (config.Profiles.Any())
                {
                    ProfileComboBox.SelectedIndex = 0;
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
        if (ProfileComboBox.SelectedItem is string profileName)
        {
            try
            {
                var config = _configManager.LoadConfiguration("config.json");
                if (config?.Profiles.TryGetValue(profileName, out var profile) == true)
                {
                    _viewModel.ActiveProfile = profileName;
                    SetComboBoxSelectedValue(CaptureModeComboBox, profile.Global.CaptureMode);
                    SetComboBoxSelectedValue(ClickModeComboBox, profile.Global.ClickMode);
                    
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
        }

        var mpEvent = windowConfig.Events.FirstOrDefault(e => e.When.Contains("B") || e.When.Contains("MP"));
        if (mpEvent != null)
        {
            card.ViewModel.MpTrigger.X = mpEvent.Click.X;
            card.ViewModel.MpTrigger.Y = mpEvent.Click.Y;
            card.ViewModel.MpTrigger.CooldownMs = mpEvent.CooldownMs ?? 120;
            card.ViewModel.MpTrigger.Enabled = true;
        }

        // Load periodic clicks
        var yClick = windowConfig.PeriodicClicks.FirstOrDefault(p => p.Name == "Y");
        if (yClick != null)
        {
            card.ViewModel.YClick.X = yClick.X;
            card.ViewModel.YClick.Y = yClick.Y;
            card.ViewModel.YClick.PeriodMs = yClick.PeriodMs ?? (int)(yClick.PeriodSec * 1000 ?? 1000);
            card.ViewModel.YClick.Enabled = yClick.Enabled;
        }

        var extra1Click = windowConfig.PeriodicClicks.FirstOrDefault(p => p.Name == "Extra1");
        if (extra1Click != null)
        {
            card.ViewModel.Extra1Click.X = extra1Click.X;
            card.ViewModel.Extra1Click.Y = extra1Click.Y;
            card.ViewModel.Extra1Click.PeriodMs = extra1Click.PeriodMs ?? (int)(extra1Click.PeriodSec * 1000 ?? 1000);
            card.ViewModel.Extra1Click.Enabled = extra1Click.Enabled;
        }

        var extra2Click = windowConfig.PeriodicClicks.FirstOrDefault(p => p.Name == "Extra2");
        if (extra2Click != null)
        {
            card.ViewModel.Extra2Click.X = extra2Click.X;
            card.ViewModel.Extra2Click.Y = extra2Click.Y;
            card.ViewModel.Extra2Click.PeriodMs = extra2Click.PeriodMs ?? (int)(extra2Click.PeriodSec * 1000 ?? 1000);
            card.ViewModel.Extra2Click.Enabled = extra2Click.Enabled;
        }

        var extra3Click = windowConfig.PeriodicClicks.FirstOrDefault(p => p.Name == "Extra3");
        if (extra3Click != null)
        {
            card.ViewModel.Extra3Click.X = extra3Click.X;
            card.ViewModel.Extra3Click.Y = extra3Click.Y;
            card.ViewModel.Extra3Click.PeriodMs = extra3Click.PeriodMs ?? (int)(extra3Click.PeriodSec * 1000 ?? 1000);
            card.ViewModel.Extra3Click.Enabled = extra3Click.Enabled;
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
                CaptureMode = GetComboBoxSelectedValue(CaptureModeComboBox) ?? "WGC",
                ClickMode = GetComboBoxSelectedValue(ClickModeComboBox) ?? "message",
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
                        Click = new ClickTarget { X = card.ViewModel.HpTrigger.X, Y = card.ViewModel.HpTrigger.Y },
                        CooldownMs = card.ViewModel.HpTrigger.CooldownMs,
                        Priority = 1
                    },
                    new EventConfig
                    {
                        When = $"MP{card.ClientId}:edge-down",
                        Click = new ClickTarget { X = card.ViewModel.MpTrigger.X, Y = card.ViewModel.MpTrigger.Y },
                        CooldownMs = card.ViewModel.MpTrigger.CooldownMs,
                        Priority = 1
                    }
                },
                PeriodicClicks = new List<PeriodicClickConfig>
                {
                    new PeriodicClickConfig
                    {
                        Name = "Y",
                        X = card.ViewModel.YClick.X,
                        Y = card.ViewModel.YClick.Y,
                        PeriodMs = card.ViewModel.YClick.PeriodMs,
                        Enabled = card.ViewModel.YClick.Enabled
                    },
                    new PeriodicClickConfig
                    {
                        Name = "Extra1",
                        X = card.ViewModel.Extra1Click.X,
                        Y = card.ViewModel.Extra1Click.Y,
                        PeriodMs = card.ViewModel.Extra1Click.PeriodMs,
                        Enabled = card.ViewModel.Extra1Click.Enabled
                    },
                    new PeriodicClickConfig
                    {
                        Name = "Extra2",
                        X = card.ViewModel.Extra2Click.X,
                        Y = card.ViewModel.Extra2Click.Y,
                        PeriodMs = card.ViewModel.Extra2Click.PeriodMs,
                        Enabled = card.ViewModel.Extra2Click.Enabled
                    },
                    new PeriodicClickConfig
                    {
                        Name = "Extra3",
                        X = card.ViewModel.Extra3Click.X,
                        Y = card.ViewModel.Extra3Click.Y,
                        PeriodMs = card.ViewModel.Extra3Click.PeriodMs,
                        Enabled = card.ViewModel.Extra3Click.Enabled
                    }
                }
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
        if (ProfileComboBox.SelectedIndex > 0)
        {
            ProfileComboBox.SelectedIndex--;
            LoadSelectedProfile();
        }
    }

    private void NextProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileComboBox.SelectedIndex < ProfileComboBox.Items.Count - 1)
        {
            ProfileComboBox.SelectedIndex++;
            LoadSelectedProfile();
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
        StatusText.Text = "Overlay mode activated - ESC to exit";
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
    }

    private void PanicStop_Click(object sender, RoutedEventArgs e)
    {
        foreach (var card in _clientCards)
        {
            card.ViewModel.IsRunning = false;
        }
        
        StatusText.Text = "🚨 PANIC STOP - All automation stopped";
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);
        
        if (e.Key == System.Windows.Input.Key.Escape && _isOverlayMode)
        {
            OverlayModeCheckBox.IsChecked = false;
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
    
    private string? GetComboBoxSelectedValue(System.Windows.Controls.ComboBox comboBox)
    {
        if (comboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item)
        {
            return item.Content?.ToString();
        }
        return comboBox.SelectedItem?.ToString();
    }
    
    private void SetComboBoxSelectedValue(System.Windows.Controls.ComboBox comboBox, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        
        foreach (System.Windows.Controls.ComboBoxItem item in comboBox.Items)
        {
            if (item.Content?.ToString()?.Equals(value, StringComparison.OrdinalIgnoreCase) == true)
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
        
        // Fallback: try to set by index if string match
        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i].ToString()?.Equals(value, StringComparison.OrdinalIgnoreCase) == true)
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }
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
                            // Find the WindowTitleText and StatusIndicator controls
                            var windowTitleText = FindChild<System.Windows.Controls.TextBlock>(clientCard, "WindowTitleText");
                            var statusIndicator = FindChild<System.Windows.Shapes.Ellipse>(clientCard, "StatusIndicator");
                            
                            if (windowTitleText != null)
                            {
                                windowTitleText.Text = title;
                                Console.WriteLine($"[AUTO-ASSIGN] Updated window title for Client {kvp.Key}: {title}");
                            }
                            else
                            {
                                Console.WriteLine($"[AUTO-ASSIGN] WARNING: WindowTitleText not found for Client {kvp.Key}");
                            }
                            
                            if (statusIndicator != null)
                            {
                                statusIndicator.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);
                                statusIndicator.ToolTip = $"Auto-assigned: {title} (0x{hwnd:X8})";
                                Console.WriteLine($"[AUTO-ASSIGN] Updated status to GREEN for Client {kvp.Key}");
                            }
                            else
                            {
                                Console.WriteLine($"[AUTO-ASSIGN] WARNING: StatusIndicator not found for Client {kvp.Key}");
                            }
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
}