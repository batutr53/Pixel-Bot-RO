using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PixelAutomation.Tool.Overlay.WPF.Services;
using PixelAutomation.Tool.Overlay.WPF.Models;
using Vanara.PInvoke;
using System.Collections.Generic;

namespace PixelAutomation.Tool.Overlay.WPF.Controls;

public partial class ClientCard : UserControl
{
    public int ClientId { get; set; }
    public ClientViewModel ViewModel { get; set; }
    
    private CoordinatePicker? _coordinatePicker;
    private bool _isRunning = false;
    private DispatcherTimer? _yClickTimer;
    private DispatcherTimer? _extra1Timer;
    private DispatcherTimer? _extra2Timer;
    private DispatcherTimer? _extra3Timer;
    private DispatcherTimer? _monitoringTimer;
    private DispatcherTimer? _hpTriggerTimer;
    private DispatcherTimer? _mpTriggerTimer;
    private FastColorSampler? _fastSampler;

    public ClientCard()
    {
        InitializeComponent();
        ViewModel = new ClientViewModel();
        DataContext = ViewModel;
        AttachTextBoxHandlers();
        _fastSampler = new FastColorSampler();
    }
    
    private void AttachTextBoxHandlers()
    {
        // HP/MP coordinate and tolerance handlers
        HpX.TextChanged += (s, e) => { if (int.TryParse(HpX.Text, out var v)) ViewModel.HpProbe.X = v; };
        HpY.TextChanged += (s, e) => { if (int.TryParse(HpY.Text, out var v)) ViewModel.HpProbe.Y = v; };
        HpTolerance.TextChanged += (s, e) => { if (int.TryParse(HpTolerance.Text, out var v)) ViewModel.HpProbe.Tolerance = v; };
        MpX.TextChanged += (s, e) => { if (int.TryParse(MpX.Text, out var v)) ViewModel.MpProbe.X = v; };
        MpY.TextChanged += (s, e) => { if (int.TryParse(MpY.Text, out var v)) ViewModel.MpProbe.Y = v; };
        MpTolerance.TextChanged += (s, e) => { if (int.TryParse(MpTolerance.Text, out var v)) ViewModel.MpProbe.Tolerance = v; };
        
        // Trigger coordinate, cooldown and enable handlers
        HpTriggerX.TextChanged += (s, e) => { if (int.TryParse(HpTriggerX.Text, out var v)) ViewModel.HpTrigger.X = v; };
        HpTriggerY.TextChanged += (s, e) => { if (int.TryParse(HpTriggerY.Text, out var v)) ViewModel.HpTrigger.Y = v; };
        HpTriggerCooldown.TextChanged += (s, e) => { if (int.TryParse(HpTriggerCooldown.Text, out var v)) ViewModel.HpTrigger.CooldownMs = v; };
        HpTriggerEnabled.Checked += (s, e) => ViewModel.HpTrigger.Enabled = true;
        HpTriggerEnabled.Unchecked += (s, e) => ViewModel.HpTrigger.Enabled = false;
        
        MpTriggerX.TextChanged += (s, e) => { if (int.TryParse(MpTriggerX.Text, out var v)) ViewModel.MpTrigger.X = v; };
        MpTriggerY.TextChanged += (s, e) => { if (int.TryParse(MpTriggerY.Text, out var v)) ViewModel.MpTrigger.Y = v; };
        MpTriggerCooldown.TextChanged += (s, e) => { if (int.TryParse(MpTriggerCooldown.Text, out var v)) ViewModel.MpTrigger.CooldownMs = v; };
        MpTriggerEnabled.Checked += (s, e) => ViewModel.MpTrigger.Enabled = true;
        MpTriggerEnabled.Unchecked += (s, e) => ViewModel.MpTrigger.Enabled = false;
        
        // Periodic click handlers
        YClickX.TextChanged += (s, e) => { if (int.TryParse(YClickX.Text, out var v)) ViewModel.YClick.X = v; };
        YClickY.TextChanged += (s, e) => { if (int.TryParse(YClickY.Text, out var v)) ViewModel.YClick.Y = v; };
        YClickPeriod.TextChanged += (s, e) => { if (int.TryParse(YClickPeriod.Text, out var v)) ViewModel.YClick.PeriodMs = v; };
        YClickEnabled.Checked += (s, e) => ViewModel.YClick.Enabled = true;
        YClickEnabled.Unchecked += (s, e) => ViewModel.YClick.Enabled = false;
        
        Extra1X.TextChanged += (s, e) => { if (int.TryParse(Extra1X.Text, out var v)) ViewModel.Extra1Click.X = v; };
        Extra1Y.TextChanged += (s, e) => { if (int.TryParse(Extra1Y.Text, out var v)) ViewModel.Extra1Click.Y = v; };
        Extra1Period.TextChanged += (s, e) => { if (int.TryParse(Extra1Period.Text, out var v)) ViewModel.Extra1Click.PeriodMs = v; };
        Extra1Enabled.Checked += (s, e) => ViewModel.Extra1Click.Enabled = true;
        Extra1Enabled.Unchecked += (s, e) => ViewModel.Extra1Click.Enabled = false;
        
        Extra2X.TextChanged += (s, e) => { if (int.TryParse(Extra2X.Text, out var v)) ViewModel.Extra2Click.X = v; };
        Extra2Y.TextChanged += (s, e) => { if (int.TryParse(Extra2Y.Text, out var v)) ViewModel.Extra2Click.Y = v; };
        Extra2Period.TextChanged += (s, e) => { if (int.TryParse(Extra2Period.Text, out var v)) ViewModel.Extra2Click.PeriodMs = v; };
        Extra2Enabled.Checked += (s, e) => ViewModel.Extra2Click.Enabled = true;
        Extra2Enabled.Unchecked += (s, e) => ViewModel.Extra2Click.Enabled = false;
        
        Extra3X.TextChanged += (s, e) => { if (int.TryParse(Extra3X.Text, out var v)) ViewModel.Extra3Click.X = v; };
        Extra3Y.TextChanged += (s, e) => { if (int.TryParse(Extra3Y.Text, out var v)) ViewModel.Extra3Click.Y = v; };
        Extra3Period.TextChanged += (s, e) => { if (int.TryParse(Extra3Period.Text, out var v)) ViewModel.Extra3Click.PeriodMs = v; };
        Extra3Enabled.Checked += (s, e) => ViewModel.Extra3Click.Enabled = true;
        Extra3Enabled.Unchecked += (s, e) => ViewModel.Extra3Click.Enabled = false;
    }

    public void Initialize(int clientId, string clientName)
    {
        ClientId = clientId;
        ViewModel.ClientName = clientName;
        ClientNameText.Text = clientName;
        UpdateUI();
    }

    private void SelectWindow_Click(object sender, RoutedEventArgs e)
    {
        var picker = new WindowPicker();
        var hwnd = picker.PickWindow();
        
        if (hwnd != IntPtr.Zero)
        {
            ViewModel.TargetHwnd = hwnd;
            ViewModel.WindowTitle = WindowHelper.GetWindowTitle(hwnd);
            WindowTitleText.Text = ViewModel.WindowTitle;
            StatusIndicator.Fill = new SolidColorBrush(Colors.LimeGreen);
            StatusIndicator.ToolTip = $"Connected: {ViewModel.WindowTitle} (0x{hwnd:X8})";
        }
        else
        {
            StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
            StatusIndicator.ToolTip = "No window selected";
        }
    }

    private void PickHpCoord_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("HP Bar Position", (x, y) =>
        {
            HpX.Text = x.ToString();
            HpY.Text = y.ToString();
            ViewModel.HpProbe.X = x;
            ViewModel.HpProbe.Y = y;
            
            // Immediately read the color at the selected position
            if (ViewModel.TargetHwnd != IntPtr.Zero)
            {
                var currentColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, x, y);
                ViewModel.HpProbe.ExpectedColor = currentColor;
                ViewModel.HpProbe.ReferenceColor = currentColor;
                HpColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(currentColor.R, currentColor.G, currentColor.B));
                HpColorText.Text = $"{currentColor.R},{currentColor.G},{currentColor.B}";
                Console.WriteLine($"[{ViewModel.ClientName}] HP COORDINATE SELECTED: Position=({x},{y}) Color=RGB({currentColor.R},{currentColor.G},{currentColor.B})");
            }
        });
    }

    private void PickMpCoord_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("MP Bar Position", (x, y) =>
        {
            MpX.Text = x.ToString();
            MpY.Text = y.ToString();
            ViewModel.MpProbe.X = x;
            ViewModel.MpProbe.Y = y;
            
            // Immediately read the color at the selected position
            if (ViewModel.TargetHwnd != IntPtr.Zero)
            {
                var currentColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, x, y);
                ViewModel.MpProbe.ExpectedColor = currentColor;
                ViewModel.MpProbe.ReferenceColor = currentColor;
                MpColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(currentColor.R, currentColor.G, currentColor.B));
                MpColorText.Text = $"{currentColor.R},{currentColor.G},{currentColor.B}";
                Console.WriteLine($"[{ViewModel.ClientName}] MP COORDINATE SELECTED: Position=({x},{y}) Color=RGB({currentColor.R},{currentColor.G},{currentColor.B})");
            }
        });
    }
    
    private void ReadHpColor_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] No target window selected for HP color read");
            return;
        }
        
        if (ViewModel.HpProbe.X <= 0 || ViewModel.HpProbe.Y <= 0)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] No HP probe coordinates set. Use 📍 Pick first.");
            return;
        }
        
        // Read current color at HP probe position
        var currentColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, ViewModel.HpProbe.X, ViewModel.HpProbe.Y);
        
        // Update reference color and display
        ViewModel.HpProbe.ExpectedColor = currentColor;
        ViewModel.HpProbe.ReferenceColor = currentColor;
        HpColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(currentColor.R, currentColor.G, currentColor.B));
        HpColorText.Text = $"{currentColor.R},{currentColor.G},{currentColor.B}";
        
        Console.WriteLine($"[{ViewModel.ClientName}] HP COLOR READ: RGB({currentColor.R},{currentColor.G},{currentColor.B}) at ({ViewModel.HpProbe.X},{ViewModel.HpProbe.Y})");
    }
    
    private void ReadMpColor_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] No target window selected for MP color read");
            return;
        }
        
        if (ViewModel.MpProbe.X <= 0 || ViewModel.MpProbe.Y <= 0)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] No MP probe coordinates set. Use 📍 Pick first.");
            return;
        }
        
        // Read current color at MP probe position
        var currentColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, ViewModel.MpProbe.X, ViewModel.MpProbe.Y);
        
        // Update reference color and display
        ViewModel.MpProbe.ExpectedColor = currentColor;
        ViewModel.MpProbe.ReferenceColor = currentColor;
        MpColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(currentColor.R, currentColor.G, currentColor.B));
        MpColorText.Text = $"{currentColor.R},{currentColor.G},{currentColor.B}";
        
        Console.WriteLine($"[{ViewModel.ClientName}] MP COLOR READ: RGB({currentColor.R},{currentColor.G},{currentColor.B}) at ({ViewModel.MpProbe.X},{ViewModel.MpProbe.Y})");
    }

    private void PickHpTrigger_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("HP Potion Click Position", (x, y) =>
        {
            HpTriggerX.Text = x.ToString();
            HpTriggerY.Text = y.ToString();
            ViewModel.HpTrigger.X = x;
            ViewModel.HpTrigger.Y = y;
        });
    }

    private void PickMpTrigger_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("MP Potion Click Position", (x, y) =>
        {
            MpTriggerX.Text = x.ToString();
            MpTriggerY.Text = y.ToString();
            ViewModel.MpTrigger.X = x;
            ViewModel.MpTrigger.Y = y;
        });
    }

    private void PickYCoord_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("Y Periodic Click Position", (x, y) =>
        {
            YClickX.Text = x.ToString();
            YClickY.Text = y.ToString();
            ViewModel.YClick.X = x;
            ViewModel.YClick.Y = y;
        });
    }

    private void PickExtra1Coord_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("Extra1 Click Position", (x, y) =>
        {
            Extra1X.Text = x.ToString();
            Extra1Y.Text = y.ToString();
            ViewModel.Extra1Click.X = x;
            ViewModel.Extra1Click.Y = y;
        });
    }

    private void PickExtra2Coord_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("Extra2 Click Position", (x, y) =>
        {
            Extra2X.Text = x.ToString();
            Extra2Y.Text = y.ToString();
            ViewModel.Extra2Click.X = x;
            ViewModel.Extra2Click.Y = y;
        });
    }

    private void PickExtra3Coord_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("Extra3 Click Position", (x, y) =>
        {
            Extra3X.Text = x.ToString();
            Extra3Y.Text = y.ToString();
            ViewModel.Extra3Click.X = x;
            ViewModel.Extra3Click.Y = y;
        });
    }

    private void PickCoordinate(string title, Action<int, int> onPicked)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
            StatusIndicator.ToolTip = "Please select a window first!";
            return;
        }

        _coordinatePicker = new CoordinatePicker(ViewModel.TargetHwnd, title);
        _coordinatePicker.CoordinatePicked += (x, y) => onPicked(x, y);
        _coordinatePicker.ShowDialog();
    }
    
    private void PickRectangle(string title, Action<int, int, int, int> onPicked)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
            StatusIndicator.ToolTip = "Please select a window first!";
            return;
        }

        var rectanglePicker = new RectanglePicker(ViewModel.TargetHwnd, title);
        rectanglePicker.RectanglePicked += (x, y, w, h) => onPicked(x, y, w, h);
        rectanglePicker.ShowDialog();
    }

    private void UpdateHpColor(System.Drawing.Color color)
    {
        // DON'T update ExpectedColor here! It should only be set when picking coordinate
        // This method is only for syncing to other clients
        
        // Sync HP color to all other clients
        var mainWindow = Application.Current.MainWindow as MainWindow;
        mainWindow?.SyncHpColorToAllClients(color, this);
    }

    private void UpdateMpColor(System.Drawing.Color color)
    {
        // DON'T update ExpectedColor here! It should only be set when picking coordinate
        // This method is only for syncing to other clients
        
        // Sync MP color to all other clients
        var mainWindow = Application.Current.MainWindow as MainWindow;
        mainWindow?.SyncMpColorToAllClients(color, this);
    }
    
    public void SetHpColorFromSync(System.Drawing.Color color)
    {
        // When syncing, this becomes the FULL HP reference color
        ViewModel.HpProbe.ExpectedColor = color;
        ViewModel.HpProbe.ReferenceColor = color;
        HpColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
        HpColorText.Text = $"{color.R},{color.G},{color.B}";
        Console.WriteLine($"[{ViewModel.ClientName}] HP reference synced: RGB({color.R},{color.G},{color.B})");
    }
    
    public void SetMpColorFromSync(System.Drawing.Color color)
    {
        // When syncing, this becomes the FULL MP reference color
        ViewModel.MpProbe.ExpectedColor = color;
        ViewModel.MpProbe.ReferenceColor = color;
        MpColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
        MpColorText.Text = $"{color.R},{color.G},{color.B}";
        Console.WriteLine($"[{ViewModel.ClientName}] MP reference synced: RGB({color.R},{color.G},{color.B})");
    }

    private void StartClient_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
            StatusIndicator.ToolTip = "Please select a window first!";
            return;
        }

        _isRunning = true;
        ViewModel.IsRunning = true;
        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        StatusIndicator.Fill = new SolidColorBrush(Colors.Green);
        StatusIndicator.ToolTip = $"Running automation for {ViewModel.ClientName}";
        
        StartPeriodicClicks();
        StartMonitoring();
        
        // Debug HP/MP settings
        Console.WriteLine($"[{ViewModel.ClientName}] START: HP Enabled={ViewModel.HpTrigger.Enabled}, Coords=({ViewModel.HpTrigger.X},{ViewModel.HpTrigger.Y}), Tolerance={ViewModel.HpProbe.Tolerance}");
        Console.WriteLine($"[{ViewModel.ClientName}] START: MP Enabled={ViewModel.MpTrigger.Enabled}, Coords=({ViewModel.MpTrigger.X},{ViewModel.MpTrigger.Y}), Tolerance={ViewModel.MpProbe.Tolerance}");
    }

    private void StopClient_Click(object sender, RoutedEventArgs e)
    {
        _isRunning = false;
        ViewModel.IsRunning = false;
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        StatusIndicator.Fill = new SolidColorBrush(Colors.Orange);
        StatusIndicator.ToolTip = "Stopped";
        
        StopPeriodicClicks();
        StopMonitoring();
    }

    private void TestClient_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
            StatusIndicator.ToolTip = "Please select a window first!";
            return;
        }

        // Test color sampling - DON'T update reference colors!
        var hpColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, ViewModel.HpProbe.X, ViewModel.HpProbe.Y);
        var mpColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, ViewModel.MpProbe.X, ViewModel.MpProbe.Y);
        
        Console.WriteLine($"[{ViewModel.ClientName}] TEST - Current HP: RGB({hpColor.R},{hpColor.G},{hpColor.B}) vs Reference: RGB({ViewModel.HpProbe.ExpectedColor.R},{ViewModel.HpProbe.ExpectedColor.G},{ViewModel.HpProbe.ExpectedColor.B})");
        Console.WriteLine($"[{ViewModel.ClientName}] TEST - Current MP: RGB({mpColor.R},{mpColor.G},{mpColor.B}) vs Reference: RGB({ViewModel.MpProbe.ExpectedColor.R},{ViewModel.MpProbe.ExpectedColor.G},{ViewModel.MpProbe.ExpectedColor.B})");
        
        // Test HP trigger click (ONLY IF ENABLED)
        if (ViewModel.HpTrigger.Enabled && ViewModel.HpTrigger.X > 0 && ViewModel.HpTrigger.Y > 0)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: HP trigger ENABLED - testing click at ({ViewModel.HpTrigger.X}, {ViewModel.HpTrigger.Y})");
            PerformBackgroundClick(ViewModel.HpTrigger.X, ViewModel.HpTrigger.Y, "TEST_HP_BACKGROUND");
            PerformPostMessageTest(ViewModel.HpTrigger.X, ViewModel.HpTrigger.Y, "TEST_HP_POSTMESSAGE");
        }
        else if (!ViewModel.HpTrigger.Enabled)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: HP trigger DISABLED - skipping test");
        }
        
        // Test MP trigger click (ONLY IF ENABLED)
        if (ViewModel.MpTrigger.Enabled && ViewModel.MpTrigger.X > 0 && ViewModel.MpTrigger.Y > 0)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: MP trigger ENABLED - testing click at ({ViewModel.MpTrigger.X}, {ViewModel.MpTrigger.Y})");
            PerformBackgroundClick(ViewModel.MpTrigger.X, ViewModel.MpTrigger.Y, "TEST_MP_BACKGROUND");
            PerformPostMessageTest(ViewModel.MpTrigger.X, ViewModel.MpTrigger.Y, "TEST_MP_POSTMESSAGE");
        }
        else if (!ViewModel.MpTrigger.Enabled)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: MP trigger DISABLED - skipping test");
        }
        
        // Test periodic clicks (BACKGROUND ONLY - NO MOUSE MOVEMENT)
        Console.WriteLine($"[{ViewModel.ClientName}] TEST: Testing background clicks only...");
        
        if (ViewModel.YClick.Enabled && ViewModel.YClick.X > 0 && ViewModel.YClick.Y > 0)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: Y periodic click ENABLED - testing at ({ViewModel.YClick.X}, {ViewModel.YClick.Y})");
            PerformBackgroundClick(ViewModel.YClick.X, ViewModel.YClick.Y, "TEST_Y_BACKGROUND");
            PerformPostMessageTest(ViewModel.YClick.X, ViewModel.YClick.Y, "TEST_Y_POSTMESSAGE");
        }
        else if (!ViewModel.YClick.Enabled)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: Y periodic click DISABLED - skipping test");
        }
        
        if (ViewModel.Extra1Click.Enabled && ViewModel.Extra1Click.X > 0 && ViewModel.Extra1Click.Y > 0)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: Extra1 click ENABLED - testing at ({ViewModel.Extra1Click.X}, {ViewModel.Extra1Click.Y})");
            PerformBackgroundClick(ViewModel.Extra1Click.X, ViewModel.Extra1Click.Y, "TEST_EXTRA1_BACKGROUND");
            PerformPostMessageTest(ViewModel.Extra1Click.X, ViewModel.Extra1Click.Y, "TEST_EXTRA1_POSTMESSAGE");
        }
        else if (!ViewModel.Extra1Click.Enabled)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: Extra1 click DISABLED - skipping test");
        }
        
        if (ViewModel.Extra2Click.Enabled && ViewModel.Extra2Click.X > 0 && ViewModel.Extra2Click.Y > 0)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: Extra2 click ENABLED - testing at ({ViewModel.Extra2Click.X}, {ViewModel.Extra2Click.Y})");
            PerformBackgroundClick(ViewModel.Extra2Click.X, ViewModel.Extra2Click.Y, "TEST_EXTRA2_BACKGROUND");
            PerformPostMessageTest(ViewModel.Extra2Click.X, ViewModel.Extra2Click.Y, "TEST_EXTRA2_POSTMESSAGE");
        }
        else if (!ViewModel.Extra2Click.Enabled)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: Extra2 click DISABLED - skipping test");
        }
        
        if (ViewModel.Extra3Click.Enabled && ViewModel.Extra3Click.X > 0 && ViewModel.Extra3Click.Y > 0)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: Extra3 click ENABLED - testing at ({ViewModel.Extra3Click.X}, {ViewModel.Extra3Click.Y})");
            PerformBackgroundClick(ViewModel.Extra3Click.X, ViewModel.Extra3Click.Y, "TEST_EXTRA3_BACKGROUND");
            PerformPostMessageTest(ViewModel.Extra3Click.X, ViewModel.Extra3Click.Y, "TEST_EXTRA3_POSTMESSAGE");
        }
        else if (!ViewModel.Extra3Click.Enabled)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: Extra3 click DISABLED - skipping test");
        }
        
        // Test completed - no ADB needed
        
        // Test enabled/disabled status
        Console.WriteLine($"[{ViewModel.ClientName}] TEST: HP Trigger Enabled={ViewModel.HpTrigger.Enabled}, MP Trigger Enabled={ViewModel.MpTrigger.Enabled}");
        Console.WriteLine($"[{ViewModel.ClientName}] TEST: Y={ViewModel.YClick.Enabled}, Extra1={ViewModel.Extra1Click.Enabled}, Extra2={ViewModel.Extra2Click.Enabled}, Extra3={ViewModel.Extra3Click.Enabled}");
        
        StatusIndicator.ToolTip = $"Test completed - HP: RGB({hpColor.R},{hpColor.G},{hpColor.B}) MP: RGB({mpColor.R},{mpColor.G},{mpColor.B})";
    }


    public void UpdateUI()
    {
        // Update UI with current ViewModel values
        HpX.Text = ViewModel.HpProbe.X.ToString();
        HpY.Text = ViewModel.HpProbe.Y.ToString();
        HpTolerance.Text = ViewModel.HpProbe.Tolerance.ToString();
        MpX.Text = ViewModel.MpProbe.X.ToString();
        MpY.Text = ViewModel.MpProbe.Y.ToString();
        MpTolerance.Text = ViewModel.MpProbe.Tolerance.ToString();
        
        HpTriggerX.Text = ViewModel.HpTrigger.X.ToString();
        HpTriggerY.Text = ViewModel.HpTrigger.Y.ToString();
        HpTriggerCooldown.Text = ViewModel.HpTrigger.CooldownMs.ToString();
        HpTriggerEnabled.IsChecked = ViewModel.HpTrigger.Enabled;
        
        MpTriggerX.Text = ViewModel.MpTrigger.X.ToString();
        MpTriggerY.Text = ViewModel.MpTrigger.Y.ToString();
        MpTriggerCooldown.Text = ViewModel.MpTrigger.CooldownMs.ToString();
        MpTriggerEnabled.IsChecked = ViewModel.MpTrigger.Enabled;
        
        YClickX.Text = ViewModel.YClick.X.ToString();
        YClickY.Text = ViewModel.YClick.Y.ToString();
        YClickPeriod.Text = ViewModel.YClick.PeriodMs.ToString();
        YClickEnabled.IsChecked = ViewModel.YClick.Enabled;
        
        Extra1X.Text = ViewModel.Extra1Click.X.ToString();
        Extra1Y.Text = ViewModel.Extra1Click.Y.ToString();
        Extra1Period.Text = ViewModel.Extra1Click.PeriodMs.ToString();
        Extra1Enabled.IsChecked = ViewModel.Extra1Click.Enabled;
        
        Extra2X.Text = ViewModel.Extra2Click.X.ToString();
        Extra2Y.Text = ViewModel.Extra2Click.Y.ToString();
        Extra2Period.Text = ViewModel.Extra2Click.PeriodMs.ToString();
        Extra2Enabled.IsChecked = ViewModel.Extra2Click.Enabled;
        
        Extra3X.Text = ViewModel.Extra3Click.X.ToString();
        Extra3Y.Text = ViewModel.Extra3Click.Y.ToString();
        Extra3Period.Text = ViewModel.Extra3Click.PeriodMs.ToString();
        Extra3Enabled.IsChecked = ViewModel.Extra3Click.Enabled;
    }

    public void UpdateStats(double fps, long clicks, long triggers)
    {
        Dispatcher.Invoke(() =>
        {
            FpsValue.Text = fps.ToString("F1");
            ClicksValue.Text = clicks.ToString();
            TriggersValue.Text = triggers.ToString();
        });
    }

    private void StartPeriodicClicks()
    {
        StopPeriodicClicks(); // Stop any existing timers
        
        // Y Click Timer
        if (ViewModel.YClick.Enabled && ViewModel.YClick.PeriodMs > 0)
        {
            _yClickTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ViewModel.YClick.PeriodMs)
            };
            _yClickTimer.Tick += (s, e) => {
                // Background click without mouse movement for simultaneous clients
                PerformBackgroundClick(ViewModel.YClick.X, ViewModel.YClick.Y, "Y-PERIODIC");
            };
            _yClickTimer.Start();
            Console.WriteLine($"[{ViewModel.ClientName}] Y periodic click STARTED: ({ViewModel.YClick.X},{ViewModel.YClick.Y}) every {ViewModel.YClick.PeriodMs}ms");
        }
        else
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Y periodic click DISABLED: Enabled={ViewModel.YClick.Enabled}, Period={ViewModel.YClick.PeriodMs}ms");
        }
        
        // Extra1 Timer
        if (ViewModel.Extra1Click.Enabled && ViewModel.Extra1Click.PeriodMs > 0)
        {
            _extra1Timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ViewModel.Extra1Click.PeriodMs)
            };
            _extra1Timer.Tick += (s, e) => PerformBackgroundClick(ViewModel.Extra1Click.X, ViewModel.Extra1Click.Y, "Extra1");
            _extra1Timer.Start();
            Console.WriteLine($"[{ViewModel.ClientName}] Extra1 periodic click STARTED: ({ViewModel.Extra1Click.X},{ViewModel.Extra1Click.Y}) every {ViewModel.Extra1Click.PeriodMs}ms");
        }
        else
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Extra1 periodic click DISABLED: Enabled={ViewModel.Extra1Click.Enabled}, Period={ViewModel.Extra1Click.PeriodMs}ms");
        }
        
        // Extra2 Timer
        if (ViewModel.Extra2Click.Enabled && ViewModel.Extra2Click.PeriodMs > 0)
        {
            _extra2Timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ViewModel.Extra2Click.PeriodMs)
            };
            _extra2Timer.Tick += (s, e) => PerformBackgroundClick(ViewModel.Extra2Click.X, ViewModel.Extra2Click.Y, "Extra2");
            _extra2Timer.Start();
            Console.WriteLine($"[{ViewModel.ClientName}] Extra2 periodic click STARTED: ({ViewModel.Extra2Click.X},{ViewModel.Extra2Click.Y}) every {ViewModel.Extra2Click.PeriodMs}ms");
        }
        else
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Extra2 periodic click DISABLED: Enabled={ViewModel.Extra2Click.Enabled}, Period={ViewModel.Extra2Click.PeriodMs}ms");
        }
        
        // Extra3 Timer
        if (ViewModel.Extra3Click.Enabled && ViewModel.Extra3Click.PeriodMs > 0)
        {
            _extra3Timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ViewModel.Extra3Click.PeriodMs)
            };
            _extra3Timer.Tick += (s, e) => PerformBackgroundClick(ViewModel.Extra3Click.X, ViewModel.Extra3Click.Y, "Extra3");
            _extra3Timer.Start();
            Console.WriteLine($"[{ViewModel.ClientName}] Extra3 periodic click STARTED: ({ViewModel.Extra3Click.X},{ViewModel.Extra3Click.Y}) every {ViewModel.Extra3Click.PeriodMs}ms");
        }
        else
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Extra3 periodic click DISABLED: Enabled={ViewModel.Extra3Click.Enabled}, Period={ViewModel.Extra3Click.PeriodMs}ms");
        }
    }
    
    private void StopPeriodicClicks()
    {
        if (_yClickTimer != null)
        {
            _yClickTimer.Stop();
            _yClickTimer = null;
        }
        if (_extra1Timer != null)
        {
            _extra1Timer.Stop();
            _extra1Timer = null;
        }
        if (_extra2Timer != null)
        {
            _extra2Timer.Stop();
            _extra2Timer = null;
        }
        if (_extra3Timer != null)
        {
            _extra3Timer.Stop();
            _extra3Timer = null;
        }
        Console.WriteLine($"[{ViewModel.ClientName}] All periodic timers STOPPED and disposed");
    }
    
    private void StartMonitoring()
    {
        StopMonitoring();
        
        // Only start monitoring if HP or MP triggers are enabled
        if (ViewModel.HpTrigger.Enabled || ViewModel.MpTrigger.Enabled)
        {
            // Real-time monitoring for HP/MP changes
            _monitoringTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // 20Hz for responsive detection
            };
            _monitoringTimer.Tick += MonitoringTimer_Tick;
            _monitoringTimer.Start();
            Console.WriteLine($"[{ViewModel.ClientName}] HP/MP monitoring STARTED: HP enabled={ViewModel.HpTrigger.Enabled}, MP enabled={ViewModel.MpTrigger.Enabled}");
        }
        else
        {
            Console.WriteLine($"[{ViewModel.ClientName}] HP/MP monitoring DISABLED: Both HP and MP triggers are disabled");
        }
    }
    
    private void StopMonitoring()
    {
        if (_monitoringTimer != null)
        {
            _monitoringTimer.Stop();
            _monitoringTimer = null;
        }
        if (_hpTriggerTimer != null)
        {
            _hpTriggerTimer.Stop();
            _hpTriggerTimer = null;
        }
        if (_mpTriggerTimer != null)
        {
            _mpTriggerTimer.Stop();
            _mpTriggerTimer = null;
        }
        
        // Reset trigger states
        if (ViewModel.HpTrigger != null)
        {
            ViewModel.HpTrigger.IsTriggered = false;
            ViewModel.HpTrigger.KeepClicking = false;
        }
        if (ViewModel.MpTrigger != null)
        {
            ViewModel.MpTrigger.IsTriggered = false;
            ViewModel.MpTrigger.KeepClicking = false;
        }
        
        // Reset monitoring state
        _isMonitoringBusy = false;
        
        Console.WriteLine($"[{ViewModel.ClientName}] All monitoring timers STOPPED and disposed");
    }
    
    private bool _isMonitoringBusy = false;
    
    private async void MonitoringTimer_Tick(object? sender, EventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero) return;
        if (_isMonitoringBusy) return; // Skip if previous monitoring still running
        
        _isMonitoringBusy = true;
        
        try
        {
            // Background thread'de color sampling yap - UI thread'i bloke etme
            await Task.Run(() =>
            {
            try
            {
                // Average color sampling for gradient HP/MP bars (5x5 area)
                var currentHpColor = ColorSampler.GetAverageColorInArea(ViewModel.TargetHwnd, ViewModel.HpProbe.X, ViewModel.HpProbe.Y, 5);
                var currentMpColor = ColorSampler.GetAverageColorInArea(ViewModel.TargetHwnd, ViewModel.MpProbe.X, ViewModel.MpProbe.Y, 5);
                
                ViewModel.HpProbe.CurrentColor = currentHpColor;
                ViewModel.MpProbe.CurrentColor = currentMpColor;
                
                // Calculate HP/MP percentages using full bar analysis
                var hpPercentage = ColorSampler.CalculateBarPercentage(
                    ViewModel.TargetHwnd, ViewModel.HpProbe.X, ViewModel.HpProbe.Y, 
                    ViewModel.HpProbe.Width, ViewModel.HpProbe.Height,
                    ViewModel.HpProbe.ExpectedColor, ViewModel.HpProbe.TriggerColor);
                    
                var mpPercentage = ColorSampler.CalculateBarPercentage(
                    ViewModel.TargetHwnd, ViewModel.MpProbe.X, ViewModel.MpProbe.Y,
                    ViewModel.MpProbe.Width, ViewModel.MpProbe.Height, 
                    ViewModel.MpProbe.ExpectedColor, ViewModel.MpProbe.TriggerColor);
                
                // UI updates UI thread'de yap
                Dispatcher.BeginInvoke(() =>
                {
                    // Update UI percentage display
                    HpPercentageText.Text = $"{hpPercentage:F0}%";
                    MpPercentageText.Text = $"{mpPercentage:F0}%";
                    
                    // Color coding for percentages
                    HpPercentageText.Foreground = hpPercentage > 70 ? new SolidColorBrush(Colors.Green) : 
                                                 hpPercentage > 30 ? new SolidColorBrush(Colors.Orange) : 
                                                 new SolidColorBrush(Colors.Red);
                    MpPercentageText.Foreground = mpPercentage > 50 ? new SolidColorBrush(Colors.CornflowerBlue) : 
                                                 mpPercentage > 20 ? new SolidColorBrush(Colors.Orange) : 
                                                 new SolidColorBrush(Colors.Red);
                });
                
                // Debug current status every 5 seconds
                if (DateTime.Now.Second % 5 == 0)
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] MONITOR: HP={hpPercentage:F1}% (threshold={ViewModel.HpProbe.Tolerance}%) MP={mpPercentage:F1}% (threshold={ViewModel.MpProbe.Tolerance}%)");
                    Console.WriteLine($"[{ViewModel.ClientName}] COLORS: HP=RGB({currentHpColor.R},{currentHpColor.G},{currentHpColor.B}) MP=RGB({currentMpColor.R},{currentMpColor.G},{currentMpColor.B})");
                    Console.WriteLine($"[{ViewModel.ClientName}] REFERENCE: HP=RGB({ViewModel.HpProbe.ExpectedColor.R},{ViewModel.HpProbe.ExpectedColor.G},{ViewModel.HpProbe.ExpectedColor.B}) MP=RGB({ViewModel.MpProbe.ExpectedColor.R},{ViewModel.MpProbe.ExpectedColor.G},{ViewModel.MpProbe.ExpectedColor.B})");
                    Console.WriteLine($"[{ViewModel.ClientName}] ENABLED: HP={ViewModel.HpTrigger.Enabled} MP={ViewModel.MpTrigger.Enabled}");
                    Console.WriteLine($"[{ViewModel.ClientName}] TRIGGERED: HP={ViewModel.HpTrigger.IsTriggered} MP={ViewModel.MpTrigger.IsTriggered}");
                }
                
                // Trigger checks background'da yap
                CheckHpTriggerByPercentage(hpPercentage);
                CheckMpTriggerByPercentage(mpPercentage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] Monitoring error: {ex.Message}");
            }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Async monitoring error: {ex.Message}");
        }
        finally
        {
            _isMonitoringBusy = false;
        }
    }
    
    private void CheckHpTrigger(System.Drawing.Color currentColor)
    {
        if (!ViewModel.HpTrigger.Enabled) return;
        
        // Calculate distance from original reference color (full HP color)
        var distanceFromReference = ColorSampler.CalculateColorDistance(currentColor, ViewModel.HpProbe.ExpectedColor);
        
        // HP decreased if color is different from reference (full HP)
        bool hpLow = distanceFromReference > ViewModel.HpProbe.Tolerance;
        
        if (hpLow && !ViewModel.HpTrigger.IsTriggered)
        {
            // HP dropped, start clicking immediately
            ViewModel.HpTrigger.IsTriggered = true;
            ViewModel.HpTrigger.KeepClicking = true;
            StartHpTriggerClicking();
            Console.WriteLine($"[{ViewModel.ClientName}] HP LOW DETECTED (distance: {distanceFromReference:F1}) - Starting potion clicks");
        }
        else if (!hpLow && ViewModel.HpTrigger.IsTriggered)
        {
            // HP restored to full, stop clicking
            ViewModel.HpTrigger.IsTriggered = false;
            ViewModel.HpTrigger.KeepClicking = false;
            StopHpTriggerClicking();
            Console.WriteLine($"[{ViewModel.ClientName}] HP FULL (distance: {distanceFromReference:F1}) - Stopping potion clicks");
        }
    }
    
    private void CheckMpTrigger(System.Drawing.Color currentColor)
    {
        if (!ViewModel.MpTrigger.Enabled) return;
        
        // Calculate distance from original reference color (full MP color)
        var distanceFromReference = ColorSampler.CalculateColorDistance(currentColor, ViewModel.MpProbe.ExpectedColor);
        
        // MP decreased if color is different from reference (full MP)
        bool mpLow = distanceFromReference > ViewModel.MpProbe.Tolerance;
        
        if (mpLow && !ViewModel.MpTrigger.IsTriggered)
        {
            // MP dropped, start clicking immediately
            ViewModel.MpTrigger.IsTriggered = true;
            ViewModel.MpTrigger.KeepClicking = true;
            StartMpTriggerClicking();
            Console.WriteLine($"[{ViewModel.ClientName}] MP LOW DETECTED (distance: {distanceFromReference:F1}) - Starting potion clicks");
        }
        else if (!mpLow && ViewModel.MpTrigger.IsTriggered)
        {
            // MP restored to full, stop clicking
            ViewModel.MpTrigger.IsTriggered = false;
            ViewModel.MpTrigger.KeepClicking = false;
            StopMpTriggerClicking();
            Console.WriteLine($"[{ViewModel.ClientName}] MP FULL (distance: {distanceFromReference:F1}) - Stopping potion clicks");
        }
    }
    
    private void StartHpTriggerClicking()
    {
        if (_hpTriggerTimer != null) return;
        
        _hpTriggerTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(ViewModel.HpTrigger.CooldownMs)
        };
        _hpTriggerTimer.Tick += (s, e) =>
        {
            if (ViewModel.HpTrigger.KeepClicking)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] HP TRIGGER CLICK at ({ViewModel.HpTrigger.X},{ViewModel.HpTrigger.Y})");
                PerformBackgroundClick(ViewModel.HpTrigger.X, ViewModel.HpTrigger.Y, "HP_TRIGGER");
                ViewModel.HpTrigger.ExecutionCount++;
                ViewModel.TriggerCount++;
            }
        };
        _hpTriggerTimer.Start();
    }
    
    private void StopHpTriggerClicking()
    {
        _hpTriggerTimer?.Stop();
        _hpTriggerTimer = null;
    }
    
    private void StartMpTriggerClicking()
    {
        if (_mpTriggerTimer != null) return;
        
        _mpTriggerTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(ViewModel.MpTrigger.CooldownMs)
        };
        _mpTriggerTimer.Tick += (s, e) =>
        {
            if (ViewModel.MpTrigger.KeepClicking)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] MP TRIGGER CLICK at ({ViewModel.MpTrigger.X},{ViewModel.MpTrigger.Y})");
                PerformBackgroundClick(ViewModel.MpTrigger.X, ViewModel.MpTrigger.Y, "MP_TRIGGER");
                ViewModel.MpTrigger.ExecutionCount++;
                ViewModel.TriggerCount++;
            }
        };
        _mpTriggerTimer.Start();
    }
    
    private void StopMpTriggerClicking()
    {
        _mpTriggerTimer?.Stop();
        _mpTriggerTimer = null;
    }
    
    private void CheckHpTriggerByPercentage(double hpPercentage)
    {
        if (!ViewModel.HpTrigger.Enabled) 
        {
            // Debug why HP trigger is disabled
            if (DateTime.Now.Millisecond < 100) // Log once per second roughly
            {
                Console.WriteLine($"[{ViewModel.ClientName}] HP TRIGGER DISABLED: Enabled={ViewModel.HpTrigger.Enabled}, Coords=({ViewModel.HpTrigger.X},{ViewModel.HpTrigger.Y})");
            }
            return;
        }
        
        // Use tolerance field as percentage threshold (e.g., 70 = 70%)
        bool hpLow = hpPercentage < ViewModel.HpProbe.Tolerance;
        
        // Debug trigger logic
        if (DateTime.Now.Millisecond < 100) // Log once per second roughly
        {
            Console.WriteLine($"[{ViewModel.ClientName}] HP CHECK: {hpPercentage:F1}% < {ViewModel.HpProbe.Tolerance}% = {hpLow}, Already Triggered={ViewModel.HpTrigger.IsTriggered}");
        }
        
        if (hpLow && !ViewModel.HpTrigger.IsTriggered)
        {
            ViewModel.HpTrigger.IsTriggered = true;
            ViewModel.HpTrigger.KeepClicking = true;
            Console.WriteLine($"[{ViewModel.ClientName}] HP LOW ({hpPercentage:F1}%) - Starting potion clicks at ({ViewModel.HpTrigger.X},{ViewModel.HpTrigger.Y}) cooldown={ViewModel.HpTrigger.CooldownMs}ms");
            StartHpTriggerClicking();
        }
        else if (!hpLow && ViewModel.HpTrigger.IsTriggered)
        {
            ViewModel.HpTrigger.IsTriggered = false;
            ViewModel.HpTrigger.KeepClicking = false;
            StopHpTriggerClicking();
            Console.WriteLine($"[{ViewModel.ClientName}] HP OK ({hpPercentage:F1}%) - Stopping potion clicks");
        }
    }
    
    private void CheckMpTriggerByPercentage(double mpPercentage)
    {
        if (!ViewModel.MpTrigger.Enabled) 
        {
            // Debug why MP trigger is disabled
            if (DateTime.Now.Millisecond < 100) // Log once per second roughly
            {
                Console.WriteLine($"[{ViewModel.ClientName}] MP TRIGGER DISABLED: Enabled={ViewModel.MpTrigger.Enabled}, Coords=({ViewModel.MpTrigger.X},{ViewModel.MpTrigger.Y})");
            }
            return;
        }
        
        // Use tolerance field as percentage threshold (e.g., 50 = 50%)
        bool mpLow = mpPercentage < ViewModel.MpProbe.Tolerance;
        
        // Debug trigger logic
        if (DateTime.Now.Millisecond < 100) // Log once per second roughly
        {
            Console.WriteLine($"[{ViewModel.ClientName}] MP CHECK: {mpPercentage:F1}% < {ViewModel.MpProbe.Tolerance}% = {mpLow}, Already Triggered={ViewModel.MpTrigger.IsTriggered}");
        }
        
        if (mpLow && !ViewModel.MpTrigger.IsTriggered)
        {
            ViewModel.MpTrigger.IsTriggered = true;
            ViewModel.MpTrigger.KeepClicking = true;
            Console.WriteLine($"[{ViewModel.ClientName}] MP LOW ({mpPercentage:F1}%) - Starting potion clicks at ({ViewModel.MpTrigger.X},{ViewModel.MpTrigger.Y}) cooldown={ViewModel.MpTrigger.CooldownMs}ms");
            StartMpTriggerClicking();
        }
        else if (!mpLow && ViewModel.MpTrigger.IsTriggered)
        {
            ViewModel.MpTrigger.IsTriggered = false;
            ViewModel.MpTrigger.KeepClicking = false;
            StopMpTriggerClicking();
            Console.WriteLine($"[{ViewModel.ClientName}] MP OK ({mpPercentage:F1}%) - Stopping potion clicks");
        }
    }
    
    private void UpdateHpColorDisplay(System.Drawing.Color color)
    {
        HpColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
        HpColorText.Text = $"{color.R},{color.G},{color.B}";
    }
    
    private void UpdateMpColorDisplay(System.Drawing.Color color)
    {
        MpColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
        MpColorText.Text = $"{color.R},{color.G},{color.B}";
    }
    
    private void PerformClick(int x, int y, string channel)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero) return;
        
        try
        {
            // Get selected click mode from main window
            var mainWindow = Application.Current.MainWindow as MainWindow;
            var selectedItem = mainWindow?.ClickModeComboBox.SelectedItem;
            var clickMode = "";
            
            if (selectedItem is ComboBoxItem comboBoxItem)
            {
                clickMode = comboBoxItem.Content?.ToString() ?? "message";
            }
            else
            {
                clickMode = selectedItem?.ToString() ?? "message";
            }
            
            // Debug what was selected
            Console.WriteLine($"DEBUG: Selected item: {selectedItem}, Content: {clickMode}, Type: {selectedItem?.GetType().Name}");
            
            // Route to appropriate click method based on dropdown selection
            switch (clickMode.ToLower())
            {
                case "message":
                    PerformMessageClick(x, y, channel);
                    break;
                case "postmessage":
                    PerformPostMessageClick(x, y, channel);
                    break;
                case "sendmessage":
                    PerformSendMessageClick(x, y, channel);
                    break;
                case "cursor-jump":
                    PerformCursorJumpClick(x, y, channel);
                    break;
                case "cursor-return":
                    PerformCursorReturnClick(x, y, channel);
                    break;
                case "sendinput":
                    PerformSendInputClick(x, y, channel);
                    break;
                case "mouse-event":
                    PerformMouseEventClick(x, y, channel);
                    break;
                case "direct-input":
                    PerformDirectInputClick(x, y, channel);
                    break;
                case "focus-click":
                    PerformFocusClick(x, y, channel);
                    break;
                case "child-window":
                default:
                    PerformChildWindowClick(x, y, channel);
                    break;
            }
            
            // Update click count
            ViewModel.ClickCount++;
            ClicksValue.Text = ViewModel.ClickCount.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Click failed: {ex.Message}");
        }
    }
    
    private void PerformMessageClick(int x, int y, string channel)
    {
        // NEW: Gameloop now uses borderless mode - no title bar or borders!
        // Use coordinates directly (ScreenToClient handles any remaining offset)
        int fixedX = x;  // No border compensation needed
        int fixedY = y;  // No title bar compensation needed
        
        // Verify coordinates by converting back to screen
        var testPoint = new Vanara.PInvoke.POINT { x = fixedX, y = fixedY };
        User32.ClientToScreen(ViewModel.TargetHwnd, ref testPoint);
        Console.WriteLine($"[{ViewModel.ClientName}] Original({x},{y}) -> Fixed({fixedX},{fixedY}) -> Screen({testPoint.x},{testPoint.y})");
        
        // Pure background click - no cursor movement
        var lParam = (fixedY << 16) | (fixedX & 0xFFFF);
        
        var smResult1 = User32.SendMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, IntPtr.Zero, (IntPtr)lParam);
        var smResult2 = User32.SendMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
        var pmResult1 = User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, IntPtr.Zero, (IntPtr)lParam);
        var pmResult2 = User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
        
        Console.WriteLine($"[{ViewModel.ClientName}] {channel} MESSAGE click at Fixed({fixedX}, {fixedY}) lParam:0x{lParam:X8} SM:{smResult1:X}/{smResult2:X} PM:{pmResult1}/{pmResult2}");
    }
    
    private void PerformCursorJumpClick(int x, int y, string channel)
    {
        // Move cursor, click, don't restore
        var point = new POINT { x = x, y = y };
        User32.ClientToScreen(ViewModel.TargetHwnd, ref point);
        
        User32.SetCursorPos(point.x, point.y);
        System.Threading.Thread.Sleep(10);
        
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(10);
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        
        Console.WriteLine($"[{ViewModel.ClientName}] {channel} CURSOR-JUMP click at client({x}, {y}) -> screen({point.x}, {point.y})");
    }
    
    private void PerformCursorReturnClick(int x, int y, string channel)
    {
        // Move cursor, click, restore position
        var point = new POINT { x = x, y = y };
        User32.ClientToScreen(ViewModel.TargetHwnd, ref point);
        
        User32.GetCursorPos(out var oldPos);
        User32.SetCursorPos(point.x, point.y);
        System.Threading.Thread.Sleep(10);
        
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(10);
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        
        System.Threading.Thread.Sleep(20);
        User32.SetCursorPos(oldPos.x, oldPos.y);
        
        Console.WriteLine($"[{ViewModel.ClientName}] {channel} CURSOR-RETURN click at client({x}, {y}) -> screen({point.x}, {point.y})");
    }
    
    private void PerformSendMessageClick(int x, int y, string channel)
    {
        // NEW: Gameloop borderless mode - no offset needed
        int fixedX = x;
        int fixedY = y;
        
        // Pure SendMessage only
        var lParam = (fixedY << 16) | (fixedX & 0xFFFF);
        var result1 = User32.SendMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, IntPtr.Zero, (IntPtr)lParam);
        var result2 = User32.SendMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
        
        Console.WriteLine($"[{ViewModel.ClientName}] {channel} SENDMESSAGE click at Fixed({fixedX}, {fixedY}) Result:{result1:X}/{result2:X}");
    }
    
    private void PerformPostMessageClick(int x, int y, string channel)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero) return;
        
        try
        {
            // Gameloop child window coordinates - use raw coordinates  
            var lParam = (y << 16) | (x & 0xFFFF);
            
            // Multiple message approach for reliability
            // Try both PostMessage and SendMessage for better compatibility
            var postDown = User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, IntPtr.Zero, (IntPtr)lParam);
            var postUp = User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
            
            // Fallback to SendMessage if PostMessage fails
            if (!postDown || !postUp)
            {
                User32.SendMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, IntPtr.Zero, (IntPtr)lParam);
                System.Threading.Thread.Sleep(5);
                User32.SendMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
            }
            
            // Debug for important clicks only
            if (channel.Contains("TEST") || channel.Contains("TRIGGER"))
            {
                Console.WriteLine($"[{ViewModel.ClientName}] {channel} click HWND:0x{ViewModel.TargetHwnd:X8} ({x},{y}) Post:{postDown}/{postUp}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Click error: {ex.Message}");
        }
    }
    
    private void PerformSendInputClick(int x, int y, string channel)
    {
        // SendInput with absolute coordinates (no cursor save/restore)
        var point = new POINT { x = x, y = y };
        User32.ClientToScreen(ViewModel.TargetHwnd, ref point);
        
        // Calculate relative coordinates for SendInput (0-65535 range)
        var screenWidth = User32.GetSystemMetrics(User32.SystemMetric.SM_CXSCREEN);
        var screenHeight = User32.GetSystemMetrics(User32.SystemMetric.SM_CYSCREEN);
        var relativeX = (point.x * 65535) / screenWidth;
        var relativeY = (point.y * 65535) / screenHeight;
        
        // Use simpler SendInput approach
        var inputs = new User32.INPUT[2];
        
        // Mouse down
        inputs[0] = new User32.INPUT
        {
            type = User32.INPUTTYPE.INPUT_MOUSE,
            mi = new User32.MOUSEINPUT
            {
                dx = relativeX,
                dy = relativeY,
                dwFlags = User32.MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN | User32.MOUSEEVENTF.MOUSEEVENTF_ABSOLUTE
            }
        };
        
        // Mouse up
        inputs[1] = new User32.INPUT
        {
            type = User32.INPUTTYPE.INPUT_MOUSE,
            mi = new User32.MOUSEINPUT
            {
                dx = relativeX,
                dy = relativeY,
                dwFlags = User32.MOUSEEVENTF.MOUSEEVENTF_LEFTUP | User32.MOUSEEVENTF.MOUSEEVENTF_ABSOLUTE
            }
        };
        
        // Send the inputs
        User32.SendInput(2, inputs, System.Runtime.InteropServices.Marshal.SizeOf<User32.INPUT>());
        
        Console.WriteLine($"[{ViewModel.ClientName}] {channel} SENDINPUT click at client({x}, {y}) -> screen({point.x}, {point.y}) -> relative({relativeX}, {relativeY})");
    }
    
    private void PerformMouseEventClick(int x, int y, string channel)
    {
        // mouse_event with absolute coordinates
        var point = new POINT { x = x, y = y };
        User32.ClientToScreen(ViewModel.TargetHwnd, ref point);
        
        var screenWidth = User32.GetSystemMetrics(User32.SystemMetric.SM_CXSCREEN);
        var screenHeight = User32.GetSystemMetrics(User32.SystemMetric.SM_CYSCREEN);
        var relativeX = (point.x * 65535) / screenWidth;
        var relativeY = (point.y * 65535) / screenHeight;
        
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN | User32.MOUSEEVENTF.MOUSEEVENTF_ABSOLUTE, relativeX, relativeY, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(10);
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTUP | User32.MOUSEEVENTF.MOUSEEVENTF_ABSOLUTE, relativeX, relativeY, 0, IntPtr.Zero);
        
        Console.WriteLine($"[{ViewModel.ClientName}] {channel} MOUSE-EVENT click at client({x}, {y}) -> screen({point.x}, {point.y}) -> relative({relativeX}, {relativeY})");
    }
    
    private void PerformChildWindowClick(int x, int y, string channel)
    {
        // For MuMu Player, try multiple methods since PostMessage might not work
        int fixedX = x;
        int fixedY = y;
        
        var point = new POINT { x = fixedX, y = fixedY };
        User32.ClientToScreen(ViewModel.TargetHwnd, ref point);
        var childHwnd = User32.ChildWindowFromPoint(ViewModel.TargetHwnd, new POINT { x = fixedX, y = fixedY });
        
        Console.WriteLine($"[{ViewModel.ClientName}] Original({x},{y}) -> Fixed({fixedX},{fixedY}) -> Screen({point.x},{point.y})");
        Console.WriteLine($"[{ViewModel.ClientName}] Target HWND: 0x{ViewModel.TargetHwnd:X8}, Child HWND: 0x{childHwnd:X8}");
        
        var targetHwnd = (childHwnd != IntPtr.Zero && childHwnd != ViewModel.TargetHwnd) ? childHwnd : ViewModel.TargetHwnd;
        var lParam = (fixedY << 16) | (fixedX & 0xFFFF);
        
        // Method 1: PostMessage (fastest, but might not work with MuMu)
        var postResult1 = User32.PostMessage(targetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, IntPtr.Zero, (IntPtr)lParam);
        var postResult2 = User32.PostMessage(targetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
        
        // Method 2: SendMessage (more reliable, synchronous)
        var sendResult1 = User32.SendMessage(targetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, IntPtr.Zero, (IntPtr)lParam);
        var sendResult2 = User32.SendMessage(targetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
        
        // Method 3: Hardware click - use same coordinate conversion as CoordinatePicker
        User32.GetCursorPos(out var oldPos);
        
        // Convert client coordinates to screen coordinates properly for MuMu Player
        var clickPoint = new POINT { x = fixedX, y = fixedY };
        User32.ClientToScreen(ViewModel.TargetHwnd, ref clickPoint);
        
        // Apply MuMu Player specific offset compensation (same as CoordinatePicker)
        var processName = GetProcessName(ViewModel.TargetHwnd);
        int offsetX = 0, offsetY = 0;
        
        if (processName.Contains("GameLoop"))
        {
            offsetX = 4; offsetY = 23;
        }
        else if (processName.Contains("NemuPlayer") || processName.Contains("MuMuPlayer"))
        {
            offsetX = 1; offsetY = 1;
        }
        
        // For debugging - show original screen conversion
        Console.WriteLine($"[{ViewModel.ClientName}] ClientToScreen conversion: ({fixedX},{fixedY}) -> ({clickPoint.x},{clickPoint.y})");
        
        // EXPERIMENTAL: Try different approaches
        
        // Approach 1: No offset
        var noOffsetPoint = new POINT { x = fixedX, y = fixedY };
        User32.ClientToScreen(ViewModel.TargetHwnd, ref noOffsetPoint);
        
        // Approach 2: Inverse offset  
        clickPoint.x -= offsetX;
        clickPoint.y -= offsetY;
        
        // Approach 3: Raw screen coordinates (where you actually clicked during coordinate selection)
        User32.GetWindowRect(ViewModel.TargetHwnd, out var windowRect);
        var rawScreenX = windowRect.left + fixedX;
        var rawScreenY = windowRect.top + fixedY;
        
        Console.WriteLine($"[{ViewModel.ClientName}] Approach 1 (no offset): ({noOffsetPoint.x},{noOffsetPoint.y})");
        Console.WriteLine($"[{ViewModel.ClientName}] Approach 2 (inverse): ({clickPoint.x},{clickPoint.y})"); 
        Console.WriteLine($"[{ViewModel.ClientName}] Approach 3 (raw): ({rawScreenX},{rawScreenY})");
        
        // Try approach 3 first (raw screen coordinates)
        User32.SetCursorPos(rawScreenX, rawScreenY);
        System.Threading.Thread.Sleep(50);
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(30);
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(50);
        User32.SetCursorPos(oldPos.x, oldPos.y);
        
        Console.WriteLine($"[{ViewModel.ClientName}] {channel} MULTI-METHOD click: Target:0x{targetHwnd:X8} Post:{postResult1}/{postResult2} Send:0x{sendResult1:X}/0x{sendResult2:X} + Hardware at ({clickPoint.x},{clickPoint.y}) offset:({offsetX},{offsetY})");
    }
    
    private string GetProcessName(IntPtr hwnd)
    {
        try
        {
            User32.GetWindowThreadProcessId(hwnd, out var processId);
            var process = System.Diagnostics.Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return "Unknown";
        }
    }
    
    private void PerformBackgroundClick(int x, int y, string channel)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero) return;
        
        try
        {
            // PostMessage uses client coordinates directly - no conversion needed
            // The coordinates we receive are already client coordinates from CoordinatePicker
            var processName = GetProcessName(ViewModel.TargetHwnd);
            var lParam = (y << 16) | (x & 0xFFFF);
            
            // Debug coordinate info
            if (channel.Contains("TEST"))
            {
                var targetProcessName = GetProcessName(ViewModel.TargetHwnd);
                Console.WriteLine($"[{ViewModel.ClientName}] {channel} PostMessage: Process={targetProcessName} ClientCoords=({x},{y}) lParam=0x{lParam:X8}");
            }
            
            // Try all message combinations
            User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_MOUSEMOVE, IntPtr.Zero, (IntPtr)lParam);
            User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, (IntPtr)1, (IntPtr)lParam);
            User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
            
            User32.SendMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, (IntPtr)1, (IntPtr)lParam);
            User32.SendMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
            
            // Method 3: Try child windows
            var childWindows = new List<IntPtr>();
            User32.EnumChildWindows(ViewModel.TargetHwnd, (hwnd, lParam) =>
            {
                childWindows.Add((IntPtr)hwnd);
                return true;
            }, IntPtr.Zero);
            
            foreach (var childHwnd in childWindows)
            {
                User32.PostMessage(childHwnd, User32.WindowMessage.WM_LBUTTONDOWN, (IntPtr)1, (IntPtr)lParam);
                User32.PostMessage(childHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
            }
            
            // Method 4: Skip hardware input injection for test mode to avoid mouse movement
            
            // Logging
            if (channel.Contains("TEST") || DateTime.Now.Second % 10 == 0)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] {channel} MULTI-METHOD click: ({x},{y}) Process:{processName} Children:{childWindows.Count}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Background click error: {ex.Message}");
        }
    }
    
    private void PerformPostMessageTest(int x, int y, string channel)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero) return;
        
        try
        {
            // PostMessage only implementation for testing
            var lParam = (y << 16) | (x & 0xFFFF);
            var processName = GetProcessName(ViewModel.TargetHwnd);
            
            Console.WriteLine($"[{ViewModel.ClientName}] {channel} - PostMessage Only Test:");
            Console.WriteLine($"[{ViewModel.ClientName}] Process: {processName}, Client Coords: ({x},{y}), lParam: 0x{lParam:X8}");
            
            // PostMessage approach only
            User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_MOUSEMOVE, IntPtr.Zero, (IntPtr)lParam);
            System.Threading.Thread.Sleep(10);
            User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, (IntPtr)1, (IntPtr)lParam);
            System.Threading.Thread.Sleep(10);
            User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
            
            Console.WriteLine($"[{ViewModel.ClientName}] {channel} PostMessage sequence sent to HWND: 0x{ViewModel.TargetHwnd:X8}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] PostMessage test error: {ex.Message}");
        }
    }
    
    private bool TryAdbClick(int x, int y, string channel)
    {
        try
        {
            // MuMu Player genellikle 7555 portunda ADB dinler
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = $"-s 127.0.0.1:7555 shell input tap {x} {y}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            
            process.Start();
            process.WaitForExit(1000); // 1 saniye timeout
            
            if (process.ExitCode == 0)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] {channel} ADB click SUCCESS: ({x},{y})");
                return true;
            }
        }
        catch (Exception ex)
        {
            // ADB yoksa sessizce fail et
            Console.WriteLine($"[{ViewModel.ClientName}] ADB not available: {ex.Message}");
        }
        
        return false;
    }
    
    private void TestAdbConnection()
    {
        try
        {
            var processName = GetProcessName(ViewModel.TargetHwnd);
            Console.WriteLine($"[{ViewModel.ClientName}] Process: {processName}");
            
            // Test ADB executable
            var adbTest = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = "version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            
            adbTest.Start();
            string output = adbTest.StandardOutput.ReadToEnd();
            adbTest.WaitForExit(2000);
            
            if (adbTest.ExitCode == 0)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] ADB available: {output.Split('\n')[0]}");
                
                // Test devices
                TestAdbDevices();
                
                // Test direct click if MuMu detected
                if (processName.Contains("MuMu") || processName.Contains("Nemu"))
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] Testing ADB click on MuMu Player...");
                    var success = TryAdbClick(100, 100, "TEST_ADB_CONNECTION");
                    if (!success)
                    {
                        // Try different ports
                        Console.WriteLine($"[{ViewModel.ClientName}] Trying alternative ADB ports...");
                        TestAlternativeAdbPorts();
                    }
                }
            }
            else
            {
                Console.WriteLine($"[{ViewModel.ClientName}] ADB not found or not working");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] ADB test error: {ex.Message}");
        }
    }
    
    private void TestAdbDevices()
    {
        try
        {
            var devicesTest = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = "devices",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            
            devicesTest.Start();
            string devices = devicesTest.StandardOutput.ReadToEnd();
            devicesTest.WaitForExit(2000);
            
            Console.WriteLine($"[{ViewModel.ClientName}] ADB devices:");
            Console.WriteLine(devices);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] ADB devices error: {ex.Message}");
        }
    }
    
    private void TestAlternativeAdbPorts()
    {
        var ports = new[] { "127.0.0.1:5555", "127.0.0.1:5556", "127.0.0.1:7555", "127.0.0.1:21503" };
        
        foreach (var port in ports)
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "adb",
                        Arguments = $"-s {port} shell echo 'test'",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                
                process.Start();
                process.WaitForExit(1000);
                
                if (process.ExitCode == 0)
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] Found active ADB device at {port}");
                    
                    // Test a simple tap
                    var tapProcess = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "adb",
                            Arguments = $"-s {port} shell input tap 100 100",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    
                    tapProcess.Start();
                    tapProcess.WaitForExit(1000);
                    
                    if (tapProcess.ExitCode == 0)
                    {
                        Console.WriteLine($"[{ViewModel.ClientName}] ADB tap SUCCESS on {port}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] Port {port} test failed: {ex.Message}");
            }
        }
    }
    
    private void TryLowLevelInput(int x, int y, string channel)
    {
        try
        {
            // Convert client coordinates to screen coordinates properly
            var clientPoint = new POINT { x = x, y = y };
            User32.ClientToScreen(ViewModel.TargetHwnd, ref clientPoint);
            
            var screenX = clientPoint.x;
            var screenY = clientPoint.y;
            
            Console.WriteLine($"[{ViewModel.ClientName}] {channel} ClientToScreen: ({x},{y}) -> ({screenX},{screenY})");
            
            // Use SendInput with absolute positioning (no cursor movement visible)
            var inputs = new User32.INPUT[2];
            
            // Calculate screen relative coordinates
            var screenWidth = User32.GetSystemMetrics(User32.SystemMetric.SM_CXSCREEN);
            var screenHeight = User32.GetSystemMetrics(User32.SystemMetric.SM_CYSCREEN);
            var relativeX = (screenX * 65535) / screenWidth;
            var relativeY = (screenY * 65535) / screenHeight;
            
            // Mouse down at specific location
            inputs[0] = new User32.INPUT
            {
                type = User32.INPUTTYPE.INPUT_MOUSE,
                mi = new User32.MOUSEINPUT
                {
                    dx = relativeX,
                    dy = relativeY,
                    dwFlags = User32.MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN | User32.MOUSEEVENTF.MOUSEEVENTF_ABSOLUTE | User32.MOUSEEVENTF.MOUSEEVENTF_MOVE
                }
            };
            
            // Mouse up
            inputs[1] = new User32.INPUT
            {
                type = User32.INPUTTYPE.INPUT_MOUSE,
                mi = new User32.MOUSEINPUT
                {
                    dx = relativeX,
                    dy = relativeY,
                    dwFlags = User32.MOUSEEVENTF.MOUSEEVENTF_LEFTUP | User32.MOUSEEVENTF.MOUSEEVENTF_ABSOLUTE
                }
            };
            
            User32.SendInput(2, inputs, System.Runtime.InteropServices.Marshal.SizeOf<User32.INPUT>());
            Console.WriteLine($"[{ViewModel.ClientName}] {channel} SENDINPUT click: ({x},{y}) -> screen({screenX},{screenY})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] SendInput error: {ex.Message}");
        }
    }
    
    private void PerformRawScreenClick(int x, int y, string channel)
    {
        // Keep this for manual testing only
        User32.GetWindowRect(ViewModel.TargetHwnd, out var windowRect);
        var screenX = windowRect.left + x;
        var screenY = windowRect.top + y;
        
        User32.GetCursorPos(out var oldPos);
        User32.SetCursorPos(screenX, screenY);
        System.Threading.Thread.Sleep(50);
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(30);
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(50);
        User32.SetCursorPos(oldPos.x, oldPos.y);
        
        Console.WriteLine($"[{ViewModel.ClientName}] {channel} RAW-SCREEN click: client({x},{y}) -> screen({screenX},{screenY}) windowRect({windowRect.left},{windowRect.top})");
    }
    
    private void PerformDirectInputClick(int x, int y, string channel)
    {
        // Hybrid approach: Focus + SendInput with small delay
        User32.SetForegroundWindow(ViewModel.TargetHwnd);
        System.Threading.Thread.Sleep(50);
        
        var point = new POINT { x = x, y = y };
        User32.ClientToScreen(ViewModel.TargetHwnd, ref point);
        
        // Use GetCursorPos to save current position
        User32.GetCursorPos(out var originalPos);
        
        // Set cursor position
        User32.SetCursorPos(point.x, point.y);
        System.Threading.Thread.Sleep(30);
        
        // Direct hardware click
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(20);
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        
        System.Threading.Thread.Sleep(50);
        User32.SetCursorPos(originalPos.x, originalPos.y);
        
        Console.WriteLine($"[{ViewModel.ClientName}] {channel} DIRECT-INPUT click at client({x}, {y}) -> screen({point.x}, {point.y})");
    }
    
    private void PerformFocusClick(int x, int y, string channel)
    {
        // Force focus and use multiple methods
        User32.BringWindowToTop(ViewModel.TargetHwnd);
        User32.SetForegroundWindow(ViewModel.TargetHwnd);
        User32.SetActiveWindow(ViewModel.TargetHwnd);
        System.Threading.Thread.Sleep(100);
        
        // Try both message and hardware click
        var lParam = (y << 16) | (x & 0xFFFF);
        User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, IntPtr.Zero, (IntPtr)lParam);
        User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
        
        // Also try hardware click
        var point = new POINT { x = x, y = y };
        User32.ClientToScreen(ViewModel.TargetHwnd, ref point);
        User32.SetCursorPos(point.x, point.y);
        System.Threading.Thread.Sleep(20);
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(10);
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        
        Console.WriteLine($"[{ViewModel.ClientName}] {channel} FOCUS-CLICK click at client({x}, {y}) -> screen({point.x}, {point.y})");
    }
}