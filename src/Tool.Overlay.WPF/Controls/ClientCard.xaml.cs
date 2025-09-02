using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    // private bool _isRunning = false; // Unused field removed
    private DispatcherTimer? _yClickTimer;
    private DispatcherTimer? _extra1Timer;
    private DispatcherTimer? _extra2Timer;
    private DispatcherTimer? _extra3Timer;
    private DispatcherTimer? _monitoringTimer;
    private DispatcherTimer? _hpTriggerTimer;
    private DispatcherTimer? _mpTriggerTimer;
    
    // BabeBot Style Timers
    private DispatcherTimer? _babeBotTimer;
    private FastColorSampler? _fastSampler;
    private int _debugCounter = 0;
    
    // Party Heal System
    private DispatcherTimer? _multiHpTimer;
    private bool _multiHpRunning = false;
    private int _currentMultiHpIndex = 0; // Current HP client being checked
    
    // HP/MP Shape management
    private System.Windows.Shapes.Ellipse? _hpShape;
    private System.Windows.Shapes.Ellipse? _mpShape;
    private System.Windows.Shapes.Rectangle? _hpPercentageShape;
    private System.Windows.Shapes.Rectangle? _mpPercentageShape;
    private bool _isDraggingHp = false;
    private bool _isDraggingMp = false;
    private bool _isDraggingHpPercentage = false;
    private bool _isDraggingMpPercentage = false;
    private System.Windows.Point _dragStartPoint;

    public ClientCard()
    {
        InitializeComponent();
        ViewModel = new ClientViewModel();
        DataContext = ViewModel;
        AttachTextBoxHandlers();
        _fastSampler = new FastColorSampler();
        SetupBabeBotUI();
        SetupMultiHpUI();
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
        
        // Percentage-based HP/MP handlers
        HpPercentageStartX.TextChanged += (s, e) => { if (int.TryParse(HpPercentageStartX.Text, out var v)) { ViewModel.HpPercentageProbe.StartX = v; UpdatePercentageMonitorPosition(); } };
        HpPercentageEndX.TextChanged += (s, e) => { if (int.TryParse(HpPercentageEndX.Text, out var v)) { ViewModel.HpPercentageProbe.EndX = v; UpdatePercentageMonitorPosition(); } };
        HpPercentageY.TextChanged += (s, e) => { if (int.TryParse(HpPercentageY.Text, out var v)) ViewModel.HpPercentageProbe.Y = v; };
        HpPercentageThreshold.TextChanged += (s, e) => { if (double.TryParse(HpPercentageThreshold.Text, out var v)) ViewModel.HpPercentageProbe.MonitorPercentage = v; UpdatePercentageMonitorPosition(); };
        HpPercentageTolerance.TextChanged += (s, e) => { if (int.TryParse(HpPercentageTolerance.Text, out var v)) ViewModel.HpPercentageProbe.Tolerance = v; };
        
        MpPercentageStartX.TextChanged += (s, e) => { if (int.TryParse(MpPercentageStartX.Text, out var v)) { ViewModel.MpPercentageProbe.StartX = v; UpdatePercentageMonitorPosition(); } };
        MpPercentageEndX.TextChanged += (s, e) => { if (int.TryParse(MpPercentageEndX.Text, out var v)) { ViewModel.MpPercentageProbe.EndX = v; UpdatePercentageMonitorPosition(); } };
        MpPercentageY.TextChanged += (s, e) => { if (int.TryParse(MpPercentageY.Text, out var v)) ViewModel.MpPercentageProbe.Y = v; };
        MpPercentageThreshold.TextChanged += (s, e) => { if (double.TryParse(MpPercentageThreshold.Text, out var v)) ViewModel.MpPercentageProbe.MonitorPercentage = v; UpdatePercentageMonitorPosition(); };
        MpPercentageTolerance.TextChanged += (s, e) => { if (int.TryParse(MpPercentageTolerance.Text, out var v)) ViewModel.MpPercentageProbe.Tolerance = v; };
        
        // Percentage monitoring enable/disable
        PercentageMonitoringEnabled.Checked += (s, e) => { ViewModel.HpPercentageProbe.Enabled = true; ViewModel.MpPercentageProbe.Enabled = true; };
        PercentageMonitoringEnabled.Unchecked += (s, e) => { ViewModel.HpPercentageProbe.Enabled = false; ViewModel.MpPercentageProbe.Enabled = false; };
        
        // Python-style potion coordinate handlers
        PythonHpPotionX.TextChanged += (s, e) => { if (int.TryParse(PythonHpPotionX.Text, out var v)) ViewModel.PythonHpPotionClick.X = v; };
        PythonHpPotionY.TextChanged += (s, e) => { if (int.TryParse(PythonHpPotionY.Text, out var v)) ViewModel.PythonHpPotionClick.Y = v; };
        PythonHpPotionCooldown.TextChanged += (s, e) => { if (int.TryParse(PythonHpPotionCooldown.Text, out var v)) ViewModel.PythonHpPotionClick.CooldownMs = v; };
        
        PythonMpPotionX.TextChanged += (s, e) => { if (int.TryParse(PythonMpPotionX.Text, out var v)) ViewModel.PythonMpPotionClick.X = v; };
        PythonMpPotionY.TextChanged += (s, e) => { if (int.TryParse(PythonMpPotionY.Text, out var v)) ViewModel.PythonMpPotionClick.Y = v; };
        PythonMpPotionCooldown.TextChanged += (s, e) => { if (int.TryParse(PythonMpPotionCooldown.Text, out var v)) ViewModel.PythonMpPotionClick.CooldownMs = v; };
        
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
        
        // Initialize draggable shapes when overlay mode is active
        InitializeDraggableShapes();
    }
    
    private void InitializeDraggableShapes()
    {
        // Create HP shape (red circle)
        _hpShape = new System.Windows.Shapes.Ellipse
        {
            Width = 20,
            Height = 20,
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 255, 0, 0)),
            Stroke = new SolidColorBrush(System.Windows.Media.Colors.Red),
            StrokeThickness = 2,
            Cursor = System.Windows.Input.Cursors.SizeAll,
            ToolTip = $"HP Monitor - Client {ClientId} (Drag to move)",
            Visibility = Visibility.Collapsed
        };
        
        // Create MP shape (blue circle)
        _mpShape = new System.Windows.Shapes.Ellipse
        {
            Width = 20,
            Height = 20,
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 0, 0, 255)),
            Stroke = new SolidColorBrush(System.Windows.Media.Colors.Blue),
            StrokeThickness = 2,
            Cursor = System.Windows.Input.Cursors.SizeAll,
            ToolTip = $"MP Monitor - Client {ClientId} (Drag to move)",
            Visibility = Visibility.Collapsed
        };
        
        // Create HP percentage bar shape (red rectangle)
        _hpPercentageShape = new System.Windows.Shapes.Rectangle
        {
            Width = 150,
            Height = 8,
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(64, 255, 0, 0)),
            Stroke = new SolidColorBrush(System.Windows.Media.Colors.Red),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 2, 2 },
            Cursor = System.Windows.Input.Cursors.SizeAll,
            ToolTip = $"HP Bar - Client {ClientId} (Drag to move, resize edges)",
            Visibility = Visibility.Collapsed
        };
        
        // Create MP percentage bar shape (blue rectangle)
        _mpPercentageShape = new System.Windows.Shapes.Rectangle
        {
            Width = 150,
            Height = 8,
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(64, 0, 0, 255)),
            Stroke = new SolidColorBrush(System.Windows.Media.Colors.Blue),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 2, 2 },
            Cursor = System.Windows.Input.Cursors.SizeAll,
            ToolTip = $"MP Bar - Client {ClientId} (Drag to move, resize edges)",
            Visibility = Visibility.Collapsed
        };
        
        // Add mouse event handlers
        _hpShape.MouseLeftButtonDown += HpShape_MouseLeftButtonDown;
        _hpShape.MouseMove += HpShape_MouseMove;
        _hpShape.MouseLeftButtonUp += HpShape_MouseLeftButtonUp;
        
        _mpShape.MouseLeftButtonDown += MpShape_MouseLeftButtonDown;
        _mpShape.MouseMove += MpShape_MouseMove;
        _mpShape.MouseLeftButtonUp += MpShape_MouseLeftButtonUp;
        
        _hpPercentageShape.MouseLeftButtonDown += HpPercentageShape_MouseLeftButtonDown;
        _hpPercentageShape.MouseMove += HpPercentageShape_MouseMove;
        _hpPercentageShape.MouseLeftButtonUp += HpPercentageShape_MouseLeftButtonUp;
        
        _mpPercentageShape.MouseLeftButtonDown += MpPercentageShape_MouseLeftButtonDown;
        _mpPercentageShape.MouseMove += MpPercentageShape_MouseMove;
        _mpPercentageShape.MouseLeftButtonUp += MpPercentageShape_MouseLeftButtonUp;
    }

    private void SelectWindow_Click(object sender, RoutedEventArgs e)
    {
        var picker = new WindowPicker();
        var hwnd = picker.PickWindow();
        
        if (hwnd != IntPtr.Zero)
        {
            ViewModel.TargetHwnd = hwnd;
            ViewModel.WindowTitle = WindowHelper.GetWindowTitle(hwnd);
            WindowTitleText.Text = $"{ViewModel.WindowTitle} - 0x{hwnd:X8}";
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

    private async void PickCoordinate(string title, Action<int, int> onPicked)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
            StatusIndicator.ToolTip = "Please select a window first!";
            return;
        }

        try
        {
            _coordinatePicker = new CoordinatePicker(ViewModel.TargetHwnd, title);
            _coordinatePicker.CoordinatePicked += (x, y) => onPicked(x, y);
            
            // Use Show() instead of ShowDialog() to prevent UI freezing
            _coordinatePicker.Show();
            
            // Optional: Add timeout to prevent indefinite waiting
            await Task.Delay(100); // Small delay to ensure window is shown
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error opening coordinate picker: {ex.Message}");
        }
    }
    
    private async void PickRectangle(string title, Action<int, int, int, int> onPicked)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
            StatusIndicator.ToolTip = "Please select a window first!";
            return;
        }

        try
        {
            var rectanglePicker = new RectanglePicker(ViewModel.TargetHwnd, title);
            rectanglePicker.RectanglePicked += (x, y, w, h) => onPicked(x, y, w, h);
            
            // Use Show() instead of ShowDialog() to prevent UI freezing
            rectanglePicker.Show();
            
            // Small delay to ensure window is shown
            await Task.Delay(100);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error opening rectangle picker: {ex.Message}");
        }
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
    
    private void PickHpPercentageBar_Click(object sender, RoutedEventArgs e)
    {
        PickRectangle("HP Bar Area (Python Style)", (x, y, w, h) =>
        {
            Console.WriteLine($"[{ViewModel.ClientName}] HP Bar Selected: Raw coordinates ({x},{y}) size ({w}x{h})");
            
            HpPercentageStartX.Text = x.ToString();
            HpPercentageEndX.Text = (x + w).ToString();
            HpPercentageY.Text = y.ToString();
            
            ViewModel.HpPercentageProbe.StartX = x;
            ViewModel.HpPercentageProbe.EndX = x + w;
            ViewModel.HpPercentageProbe.Y = y;
            
            // OFFSET TEST: Try multiple offset combinations to find correct one
            if (ViewModel.TargetHwnd != IntPtr.Zero)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] 🔍 OFFSET TEST - Finding correct MuMu offset:");
                Console.WriteLine($"[{ViewModel.ClientName}] Selected HP Bar: ({x},{y}) size ({w}x{h})");
                
                var testOffsets = new List<(int dx, int dy, string desc)>
                {
                    (0, 0, "NO_OFFSET"),
                    (8, 50, "CURRENT_+8+50"),
                    (-8, -50, "REVERSE_-8-50"),
                    (16, 100, "DOUBLE_+16+100"),
                    (-16, -100, "DOUBLE_NEG"),
                    (8, 0, "ONLY_X+8"),
                    (0, 50, "ONLY_Y+50")
                };
                
                int middleX = x + w/2;
                
                Console.WriteLine($"[{ViewModel.ClientName}] Testing offset combinations at HP middle position ({middleX},{y}):");
                
                foreach (var (dx, dy, desc) in testOffsets)
                {
                    try
                    {
                        // Temporarily modify offset for this test
                        var testColor = TestColorSampler.GetColorAtWithOffset(ViewModel.TargetHwnd, middleX, y, dx, dy);
                        Console.WriteLine($"  {desc}: RGB({testColor.R},{testColor.G},{testColor.B})");
                        
                        // Check if it looks like HP color (reddish)
                        bool looksLikeHP = testColor.R > 100 && testColor.R > testColor.G && testColor.R > testColor.B;
                        if (looksLikeHP)
                        {
                            Console.WriteLine($"    ✅ POSSIBLE HP COLOR! (Red dominant)");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  {desc}: ERROR - {ex.Message}");
                    }
                }
                
                // Use current offset for now
                var middleColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, middleX, y);
                ViewModel.HpPercentageProbe.ExpectedColor = middleColor;
                HpPercentageColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(middleColor.R, middleColor.G, middleColor.B));
                Console.WriteLine($"[{ViewModel.ClientName}] Current offset used: RGB({middleColor.R},{middleColor.G},{middleColor.B})");
                
                UpdatePercentageMonitorPosition();
            }
        });
    }
    
    private void PickMpPercentageBar_Click(object sender, RoutedEventArgs e)
    {
        PickRectangle("MP Bar Area (Python Style)", (x, y, w, h) =>
        {
            Console.WriteLine($"[{ViewModel.ClientName}] MP Bar Selected: Raw coordinates ({x},{y}) size ({w}x{h})");
            
            MpPercentageStartX.Text = x.ToString();
            MpPercentageEndX.Text = (x + w).ToString();
            MpPercentageY.Text = y.ToString();
            
            ViewModel.MpPercentageProbe.StartX = x;
            ViewModel.MpPercentageProbe.EndX = x + w;
            ViewModel.MpPercentageProbe.Y = y;
            
            // TEST: Sample colors at selected coordinates to verify coordinate mapping
            if (ViewModel.TargetHwnd != IntPtr.Zero)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] COORDINATE VERIFICATION - Testing selected MP area:");
                
                // Test start, middle, end of selected area
                var startColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, x, y);
                var middleColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, x + w/2, y);
                var endColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, x + w - 1, y);
                
                Console.WriteLine($"  SELECTED START ({x},{y}) = RGB({startColor.R},{startColor.G},{startColor.B})");
                Console.WriteLine($"  SELECTED MIDDLE ({x + w/2},{y}) = RGB({middleColor.R},{middleColor.G},{middleColor.B})");
                Console.WriteLine($"  SELECTED END ({x + w - 1},{y}) = RGB({endColor.R},{endColor.G},{endColor.B})");
                
                // For now, use middle color as expected (you can manually verify this is MP blue)
                ViewModel.MpPercentageProbe.ExpectedColor = middleColor;
                MpPercentageColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(middleColor.R, middleColor.G, middleColor.B));
                Console.WriteLine($"[{ViewModel.ClientName}] MP Expected Color set to MIDDLE: RGB({middleColor.R},{middleColor.G},{middleColor.B})");
                Console.WriteLine($"[{ViewModel.ClientName}] ❗ VERIFY: Is RGB({middleColor.R},{middleColor.G},{middleColor.B}) your MP bar color?");
                
                UpdatePercentageMonitorPosition();
            }
        });
    }
    
    private void UpdatePercentageMonitorPosition()
    {
        try
        {
            var hpCalcX = ViewModel.HpPercentageProbe.CalculatedX;
            var mpCalcX = ViewModel.MpPercentageProbe.CalculatedX;
            
            PercentageMonitorPosition.Text = $"HP: {hpCalcX} ({ViewModel.HpPercentageProbe.MonitorPercentage:F0}%) MP: {mpCalcX} ({ViewModel.MpPercentageProbe.MonitorPercentage:F0}%)";
        }
        catch
        {
            PercentageMonitorPosition.Text = "Error calculating position";
        }
    }
    
    private void PickPythonHpPotion_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("Python-Style HP Potion Position", (x, y) =>
        {
            PythonHpPotionX.Text = x.ToString();
            PythonHpPotionY.Text = y.ToString();
            ViewModel.PythonHpPotionClick.X = x;
            ViewModel.PythonHpPotionClick.Y = y;
            Console.WriteLine($"[{ViewModel.ClientName}] Python HP Potion Click set to: ({x},{y})");
        });
    }
    
    private void PickPythonMpPotion_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("Python-Style MP Potion Position", (x, y) =>
        {
            PythonMpPotionX.Text = x.ToString();
            PythonMpPotionY.Text = y.ToString();
            ViewModel.PythonMpPotionClick.X = x;
            ViewModel.PythonMpPotionClick.Y = y;
            Console.WriteLine($"[{ViewModel.ClientName}] Python MP Potion Click set to: ({x},{y})");
        });
    }
    
    private void FindHpMpBars_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] No window selected");
            return;
        }
        
        try
        {
            Console.WriteLine($"[{ViewModel.ClientName}] 🔍 AUTO-DETECTING HP/MP bars...");
            
            // HP Bar Detection
            var hpBar = DetectBar(true); // true = HP (red)
            if (hpBar != null)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] ✅ HP BAR FOUND!");
                Console.WriteLine($"[{ViewModel.ClientName}] HP Bar: StartX={hpBar.Value.startX}, EndX={hpBar.Value.endX}, Y={hpBar.Value.y}, Width={hpBar.Value.endX - hpBar.Value.startX}");
                Console.WriteLine($"[{ViewModel.ClientName}] HP Color: RGB({hpBar.Value.color.R},{hpBar.Value.color.G},{hpBar.Value.color.B})");
                
                // Auto-fill HP coordinates on UI thread
                Dispatcher.BeginInvoke(() =>
                {
                    HpPercentageStartX.Text = hpBar.Value.startX.ToString();
                    HpPercentageEndX.Text = hpBar.Value.endX.ToString();
                    HpPercentageY.Text = hpBar.Value.y.ToString();
                    
                    ViewModel.HpPercentageProbe.StartX = hpBar.Value.startX;
                    ViewModel.HpPercentageProbe.EndX = hpBar.Value.endX;
                    ViewModel.HpPercentageProbe.Y = hpBar.Value.y;
                    ViewModel.HpPercentageProbe.ExpectedColor = hpBar.Value.color;
                    
                    HpPercentageColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(hpBar.Value.color.R, hpBar.Value.color.G, hpBar.Value.color.B));
                    
                    // Show visual HP bar indicator
                    ShowBarIndicator("HP", hpBar.Value.startX, hpBar.Value.endX, hpBar.Value.y, System.Windows.Media.Colors.Red);
                });
            }
            else
            {
                Console.WriteLine($"[{ViewModel.ClientName}] ❌ HP BAR NOT FOUND!");
            }
            
            // MP Bar Detection - if HP found, search near it
            int mpSearchStartY = 30;
            int mpSearchEndY = 120;
            if (hpBar != null)
            {
                // Search MP bar RIGHT BELOW HP bar (mini bars are very close)
                mpSearchStartY = hpBar.Value.y + 1;  // Start right after HP
                mpSearchEndY = hpBar.Value.y + 15;   // Only search 15 pixels below HP
                Console.WriteLine($"[{ViewModel.ClientName}] HP found at Y={hpBar.Value.y}, searching MP in Y range {mpSearchStartY}-{mpSearchEndY} (right below HP)");
            }
            
            var mpBar = DetectBarInRange(false, mpSearchStartY, mpSearchEndY); // false = MP (blue)
            if (mpBar != null)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] ✅ MP BAR FOUND!");
                Console.WriteLine($"[{ViewModel.ClientName}] MP Bar: StartX={mpBar.Value.startX}, EndX={mpBar.Value.endX}, Y={mpBar.Value.y}, Width={mpBar.Value.endX - mpBar.Value.startX}");
                Console.WriteLine($"[{ViewModel.ClientName}] MP Color: RGB({mpBar.Value.color.R},{mpBar.Value.color.G},{mpBar.Value.color.B})");
                
                // Auto-fill MP coordinates on UI thread
                Dispatcher.BeginInvoke(() =>
                {
                    MpPercentageStartX.Text = mpBar.Value.startX.ToString();
                    MpPercentageEndX.Text = mpBar.Value.endX.ToString();
                    MpPercentageY.Text = mpBar.Value.y.ToString();
                    
                    ViewModel.MpPercentageProbe.StartX = mpBar.Value.startX;
                    ViewModel.MpPercentageProbe.EndX = mpBar.Value.endX;
                    ViewModel.MpPercentageProbe.Y = mpBar.Value.y;
                    ViewModel.MpPercentageProbe.ExpectedColor = mpBar.Value.color;
                    
                    MpPercentageColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(mpBar.Value.color.R, mpBar.Value.color.G, mpBar.Value.color.B));
                    
                    // Show visual MP bar indicator
                    ShowBarIndicator("MP", mpBar.Value.startX, mpBar.Value.endX, mpBar.Value.y, System.Windows.Media.Colors.Blue);
                });
            }
            else
            {
                Console.WriteLine($"[{ViewModel.ClientName}] ❌ MP BAR NOT FOUND!");
            }
            
            if (hpBar != null || mpBar != null)
            {
                UpdatePercentageMonitorPosition();
                Console.WriteLine($"[{ViewModel.ClientName}] 🎯 AUTO-DETECTION COMPLETE! Coordinates filled automatically.");
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Auto-detection error: {ex.Message}");
        }
    }
    
    private (int startX, int endX, int y, System.Drawing.Color color)? DetectBar(bool isHP)
    {
        return DetectBarInRange(isHP, 30, 120);
    }
    
    private (int startX, int endX, int y, System.Drawing.Color color)? DetectBarInRange(bool isHP, int startY, int endY)
    {
        string barType = isHP ? "HP" : "MP";
        Console.WriteLine($"[{ViewModel.ClientName}] Detecting {barType} bar in Y range {startY}-{endY}...");
        
        // Search in specified Y range - MINI BARS (very thin)
        for (int y = startY; y <= endY; y += 1) // Every 1 pixel vertically for thin bars
        {
            System.Drawing.Color? barColor = null;
            int? startX = null;
            int? endX = null;
            int consecutivePixels = 0;
            int minBarLength = isHP ? 20 : 10; // Even smaller minimum for MP mini bars
            
            // Scan horizontally to find bar
            for (int x = 50; x <= 500; x++)
            {
                try
                {
                    var color = ColorSampler.GetColorAt(ViewModel.TargetHwnd, x, y);
                    bool isBarColor = false;
                    
                    if (isHP)
                    {
                        // HP: RED bar detection for mini UI bars
                        isBarColor = (color.R > 50 && color.R > color.G + 5 && color.R > color.B + 5) ||  // Any reddish
                                   (color.R > color.G + 30 && color.R > color.B + 30) ||                // Red dominant 
                                   (color.R > 80 && color.G < 80 && color.B < 80);                       // Bright red
                    }
                    else
                    {
                        // MP: ULTRA AGGRESSIVE detection - ANY non-black, non-white color that might be MP
                        isBarColor = (color.B > 15) ||                                                   // ANY blue at all (lowered threshold)
                                   (color.B > color.R && color.B > color.G) ||                          // Blue is highest
                                   (color.B > color.R + 3) ||                                           // Even slightly more blue
                                   (color.R < 120 && color.G < 120 && color.B > 15) ||                // Dark with some blue
                                   (color.B > 25 && color.R < 100 && color.G < 100) ||                // Any bluish tone
                                   // Purple/Violet MP bars
                                   (color.B > 40 && color.R > 40 && color.G < 60) ||                   // Purple (R+B, low G)
                                   (color.B + color.R > color.G + 40) ||                               // Purple/Magenta dominant
                                   // Dark colored bars (any non-background color)
                                   (color.R + color.G + color.B > 60 && color.R + color.G + color.B < 600) || // Any moderate color
                                   // Specific MP bar colors that might appear
                                   (color.B > 20 && Math.Abs(color.R - color.B) < 30) ||              // Blueish-purple
                                   (color.G > 15 && color.B > 15 && color.R < 80);                     // Cyan-ish colors
                    }
                    
                    // DEBUG: For MP detection, print every pixel in the critical area where MP should be
                    if (!isHP && y <= startY + 5) // Only first 5 rows of MP search to avoid spam
                    {
                        if (x % 10 == 0) // Every 10th pixel horizontally
                        {
                            Console.WriteLine($"[{ViewModel.ClientName}] MP SCAN Y={y} X={x}: RGB({color.R},{color.G},{color.B}) -> {(isBarColor ? "✅MATCH" : "❌no")}");
                        }
                    }
                    // DEBUG: Print every 20th pixel for HP to see what colors we're getting
                    else if (isHP && x % 20 == 0 && (y % 5 == 0)) // More frequent sampling for HP
                    {
                        Console.WriteLine($"[{ViewModel.ClientName}] HP SCAN Y={y} X={x}: RGB({color.R},{color.G},{color.B}) -> {(isBarColor ? "✅MATCH" : "❌no")}");
                    }
                    
                    if (isBarColor)
                    {
                        if (startX == null)
                        {
                            startX = x;
                            barColor = color;
                        }
                        consecutivePixels++;
                        endX = x;
                    }
                    else
                    {
                        // Check if we found a valid bar
                        if (startX != null && consecutivePixels >= minBarLength)
                        {
                            Console.WriteLine($"[{ViewModel.ClientName}] {barType} bar found at Y={y}: X({startX}-{endX}) length={consecutivePixels} color=RGB({barColor?.R},{barColor?.G},{barColor?.B})");
                            return (startX.Value, endX.Value, y, barColor.Value);
                        }
                        
                        // Reset for next potential bar
                        startX = null;
                        endX = null;
                        consecutivePixels = 0;
                        barColor = null;
                    }
                }
                catch { /* Skip errors */ }
            }
            
            // Check if bar extends to edge
            if (startX != null && consecutivePixels >= minBarLength)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] {barType} bar found at Y={y}: X({startX}-{endX}) length={consecutivePixels} color=RGB({barColor?.R},{barColor?.G},{barColor?.B})");
                return (startX.Value, endX.Value, y, barColor.Value);
            }
        }
        
        Console.WriteLine($"[{ViewModel.ClientName}] {barType} bar not found in search area");
        return null;
    }
    
    private void ShowBarIndicator(string barType, int startX, int endX, int y, System.Windows.Media.Color color)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero) return;
        
        try
        {
            // Create overlay window to show the bar location
            var overlayWindow = new Window
            {
                Title = $"{ViewModel.ClientName} - {barType} Bar Indicator",
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                ResizeMode = ResizeMode.NoResize
            };
            
            // Get target window position
            User32.GetWindowRect(ViewModel.TargetHwnd, out var windowRect);
            User32.GetClientRect(ViewModel.TargetHwnd, out var clientRect);
            
            // Calculate border/title offsets
            int borderWidth = ((windowRect.right - windowRect.left) - (clientRect.right - clientRect.left)) / 2;
            int titleHeight = ((windowRect.bottom - windowRect.top) - (clientRect.bottom - clientRect.top)) - borderWidth;
            
            // Position overlay window over the target window's client area
            overlayWindow.Left = windowRect.left + borderWidth;
            overlayWindow.Top = windowRect.top + titleHeight;
            overlayWindow.Width = clientRect.right - clientRect.left;
            overlayWindow.Height = clientRect.bottom - clientRect.top;
            
            // Create canvas for drawing
            var canvas = new Canvas
            {
                Background = Brushes.Transparent
            };
            
            // Create draggable and resizable bar indicator
            var barContainer = new Border
            {
                Width = endX - startX,
                Height = 12, // Make it taller for easier interaction
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(2),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(80, color.R, color.G, color.B)),
                Cursor = System.Windows.Input.Cursors.SizeAll,
                ToolTip = $"Drag to move {barType} bar, drag edges to resize"
            };
            
            // Position the container - EXPLICIT double cast
            double initialLeft = (double)startX;
            double initialTop = (double)(y - 4);
            Canvas.SetLeft(barContainer, initialLeft);
            Canvas.SetTop(barContainer, initialTop); // Center it around the detected Y
            
            // Add resize handles (left and right)
            var leftHandle = new System.Windows.Shapes.Rectangle
            {
                Width = 8,
                Height = 12,
                Fill = new SolidColorBrush(color),
                Cursor = System.Windows.Input.Cursors.SizeWE,
                ToolTip = "Drag to resize left edge"
            };
            Canvas.SetLeft(leftHandle, startX - 4);
            Canvas.SetTop(leftHandle, y - 4);
            
            var rightHandle = new System.Windows.Shapes.Rectangle
            {
                Width = 8,
                Height = 12,
                Fill = new SolidColorBrush(color),
                Cursor = System.Windows.Input.Cursors.SizeWE,
                ToolTip = "Drag to resize right edge"
            };
            Canvas.SetLeft(rightHandle, endX - 4);
            Canvas.SetTop(rightHandle, y - 4);
            
            // Create label that updates with coordinates
            var label = new TextBlock
            {
                Text = $"{barType} ({startX}-{endX},{y}) - Drag to adjust",
                Foreground = new SolidColorBrush(color),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 0, 0, 0)),
                Padding = new Thickness(4),
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            
            Canvas.SetLeft(label, startX);
            Canvas.SetTop(label, y - 30); // Above the bar
            
            // Add elements to canvas
            canvas.Children.Add(barContainer);
            canvas.Children.Add(leftHandle);
            canvas.Children.Add(rightHandle);
            canvas.Children.Add(label);
            
            // Variables for dragging - EXPLICIT initialization
            bool isDragging = false;
            bool isResizingLeft = false;
            bool isResizingRight = false;
            System.Windows.Point dragStartPos = new System.Windows.Point();
            double originalLeft = initialLeft; // Use the same values we set
            double originalTop = initialTop;
            double originalWidth = (double)(endX - startX);
            
            // Helper function to update coordinates
            Action updateCoordinates = () =>
            {
                var newStartX = (int)Canvas.GetLeft(barContainer);
                var newEndX = newStartX + (int)barContainer.Width;
                var newY = (int)(Canvas.GetTop(barContainer) + 4); // Adjust for container offset
                
                // Update label
                label.Text = $"{barType} ({newStartX}-{newEndX},{newY}) - Drag to adjust";
                Canvas.SetLeft(label, newStartX);
                Canvas.SetTop(label, newY - 30);
                
                // Update handle positions
                Canvas.SetLeft(leftHandle, newStartX - 4);
                Canvas.SetTop(leftHandle, newY - 4);
                Canvas.SetLeft(rightHandle, newEndX - 4);
                Canvas.SetTop(rightHandle, newY - 4);
                
                // Update the actual probe coordinates if this is our client
                Dispatcher.BeginInvoke(() =>
                {
                    if (barType == "HP")
                    {
                        HpPercentageStartX.Text = newStartX.ToString();
                        HpPercentageEndX.Text = newEndX.ToString();
                        HpPercentageY.Text = newY.ToString();
                        ViewModel.HpPercentageProbe.StartX = newStartX;
                        ViewModel.HpPercentageProbe.EndX = newEndX;
                        ViewModel.HpPercentageProbe.Y = newY;
                    }
                    else if (barType == "MP")
                    {
                        MpPercentageStartX.Text = newStartX.ToString();
                        MpPercentageEndX.Text = newEndX.ToString();
                        MpPercentageY.Text = newY.ToString();
                        ViewModel.MpPercentageProbe.StartX = newStartX;
                        ViewModel.MpPercentageProbe.EndX = newEndX;
                        ViewModel.MpPercentageProbe.Y = newY;
                    }
                });
            };
            
            // Bar container drag events - SAFE NaN handling
            barContainer.MouseLeftButtonDown += (s, e) =>
            {
                isDragging = true;
                dragStartPos = e.GetPosition(canvas);
                
                // SAFE way to get current position
                var currentLeft = Canvas.GetLeft(barContainer);
                var currentTop = Canvas.GetTop(barContainer);
                
                // Handle NaN values
                originalLeft = double.IsNaN(currentLeft) ? initialLeft : currentLeft;
                originalTop = double.IsNaN(currentTop) ? initialTop : currentTop;
                
                barContainer.CaptureMouse();
                Console.WriteLine($"[{ViewModel.ClientName}] Drag START at canvas pos: {dragStartPos}, bar pos: ({originalLeft},{originalTop})");
                e.Handled = true;
            };
            
            barContainer.MouseLeftButtonUp += (s, e) =>
            {
                if (isDragging)
                {
                    isDragging = false;
                    barContainer.ReleaseMouseCapture();
                    var currentPos = e.GetPosition(canvas);
                    Console.WriteLine($"[{ViewModel.ClientName}] Drag END at canvas pos: {currentPos}");
                }
                e.Handled = true;
            };
            
            // Canvas-level mouse move - SIMPLIFIED LOGIC
            canvas.MouseMove += (s, e) =>
            {
                if (isDragging)
                {
                    var currentMousePos = e.GetPosition(canvas);
                    
                    // Calculate how much mouse moved since drag started
                    var deltaX = currentMousePos.X - dragStartPos.X;
                    var deltaY = currentMousePos.Y - dragStartPos.Y;
                    
                    // FIRST - Debug current values to see what's happening
                    if ((int)currentMousePos.X % 10 == 0)
                    {
                        Console.WriteLine($"[{ViewModel.ClientName}] DEBUG VALUES: Mouse({currentMousePos.X:F0},{currentMousePos.Y:F0}) startPos({dragStartPos.X:F0},{dragStartPos.Y:F0}) delta({deltaX:F0},{deltaY:F0}) original({originalLeft:F0},{originalTop:F0})");
                    }
                    
                    // SECOND - Calculate new position based on original position + delta  
                    var newLeft = originalLeft + deltaX;
                    var newTop = originalTop + deltaY;
                    
                    // THIRD - NaN safety check with detailed info
                    if (double.IsNaN(newLeft) || double.IsNaN(newTop) || double.IsNaN(originalLeft) || double.IsNaN(originalTop))
                    {
                        Console.WriteLine($"[{ViewModel.ClientName}] ❌ NaN DETECTED! originalLeft={originalLeft}, originalTop={originalTop}, deltaX={deltaX}, deltaY={deltaY}, newLeft={newLeft}, newTop={newTop}");
                        return; // Skip this frame
                    }
                    
                    // FOURTH - Keep within canvas bounds
                    newLeft = Math.Max(0, Math.Min(newLeft, canvas.Width - barContainer.Width));
                    newTop = Math.Max(0, Math.Min(newTop, canvas.Height - barContainer.Height));
                    
                    // FIFTH - Set new position
                    Canvas.SetLeft(barContainer, newLeft);
                    Canvas.SetTop(barContainer, newTop);
                    
                    // SIXTH - Update coordinates in UI
                    updateCoordinates();
                    
                    // FINAL - Success debug output
                    if ((int)currentMousePos.X % 10 == 0)
                    {
                        Console.WriteLine($"[{ViewModel.ClientName}] ✅ SUCCESS: Bar moved to ({newLeft:F0},{newTop:F0})");
                    }
                }
                else if (isResizingLeft)
                {
                    var currentMousePos = e.GetPosition(canvas);
                    var deltaX = currentMousePos.X - dragStartPos.X;
                    
                    var newLeft = originalLeft + deltaX;
                    var newWidth = originalWidth - deltaX;
                    
                    if (newWidth >= 20 && newLeft >= 0)
                    {
                        Canvas.SetLeft(barContainer, newLeft);
                        barContainer.Width = newWidth;
                        updateCoordinates();
                    }
                }
                else if (isResizingRight)
                {
                    var currentMousePos = e.GetPosition(canvas);
                    var deltaX = currentMousePos.X - dragStartPos.X;
                    
                    var newWidth = originalWidth + deltaX;
                    var maxWidth = canvas.Width - originalLeft;
                    
                    if (newWidth >= 20 && newWidth <= maxWidth)
                    {
                        barContainer.Width = newWidth;
                        updateCoordinates();
                    }
                }
            };
            
            // Left handle resize events - NaN SAFE
            leftHandle.MouseLeftButtonDown += (s, e) =>
            {
                isResizingLeft = true;
                dragStartPos = e.GetPosition(canvas);
                
                var currentLeft = Canvas.GetLeft(barContainer);
                originalLeft = double.IsNaN(currentLeft) ? initialLeft : currentLeft;
                originalWidth = barContainer.Width;
                
                leftHandle.CaptureMouse();
                Console.WriteLine($"[{ViewModel.ClientName}] LEFT RESIZE started - originalLeft={originalLeft}, originalWidth={originalWidth}");
                e.Handled = true;
            };
            
            leftHandle.MouseLeftButtonUp += (s, e) =>
            {
                if (isResizingLeft)
                {
                    isResizingLeft = false;
                    leftHandle.ReleaseMouseCapture();
                    Console.WriteLine($"[{ViewModel.ClientName}] LEFT RESIZE ended");
                }
                e.Handled = true;
            };
            
            // Right handle resize events - NaN SAFE
            rightHandle.MouseLeftButtonDown += (s, e) =>
            {
                isResizingRight = true;
                dragStartPos = e.GetPosition(canvas);
                
                var currentLeft = Canvas.GetLeft(barContainer);
                originalLeft = double.IsNaN(currentLeft) ? initialLeft : currentLeft;
                originalWidth = barContainer.Width;
                
                rightHandle.CaptureMouse();
                Console.WriteLine($"[{ViewModel.ClientName}] RIGHT RESIZE started - originalLeft={originalLeft}, originalWidth={originalWidth}");
                e.Handled = true;
            };
            
            rightHandle.MouseLeftButtonUp += (s, e) =>
            {
                if (isResizingRight)
                {
                    isResizingRight = false;
                    rightHandle.ReleaseMouseCapture();
                    Console.WriteLine($"[{ViewModel.ClientName}] RIGHT RESIZE ended");
                }
                e.Handled = true;
            };
            
            overlayWindow.Content = canvas;
            
            overlayWindow.Show();
            
            // Auto-close after 30 seconds (more time to position)
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                overlayWindow.Close();
            };
            timer.Start();
            
            // Close controls
            overlayWindow.Focusable = true; // Enable keyboard focus
            overlayWindow.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    overlayWindow.Close();
                }
            };
            
            // Double-click label to close
            label.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    overlayWindow.Close();
                }
            };
            
            // Set focus to enable keyboard input
            overlayWindow.Activated += (s, e) => overlayWindow.Focus();
            
            Console.WriteLine($"[{ViewModel.ClientName}] 🎯 {barType} bar indicator shown - INTERACTIVE CONTROLS:");
            Console.WriteLine($"  • DRAG the bar to move position");
            Console.WriteLine($"  • DRAG LEFT/RIGHT edges to resize");  
            Console.WriteLine($"  • DOUBLE-CLICK label or press ESC to close");
            Console.WriteLine($"  • Coordinates auto-update in UI as you adjust");
            Console.WriteLine($"  • Auto-closes in 30 seconds");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error showing {barType} indicator: {ex.Message}");
        }
    }
    
    private void CaptureCurrentColors_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] No window selected for color capture");
            return;
        }
        
        try
        {
            Console.WriteLine($"[{ViewModel.ClientName}] === COLOR CAPTURE DEBUG ===");
            Console.WriteLine($"[{ViewModel.ClientName}] HP Bar: StartX={ViewModel.HpPercentageProbe.StartX} EndX={ViewModel.HpPercentageProbe.EndX} Y={ViewModel.HpPercentageProbe.Y} Threshold={ViewModel.HpPercentageProbe.MonitorPercentage}%");
            Console.WriteLine($"[{ViewModel.ClientName}] MP Bar: StartX={ViewModel.MpPercentageProbe.StartX} EndX={ViewModel.MpPercentageProbe.EndX} Y={ViewModel.MpPercentageProbe.Y} Threshold={ViewModel.MpPercentageProbe.MonitorPercentage}%");
            
            // Capture current HP color at calculated position
            var hpX = ViewModel.HpPercentageProbe.CalculatedX;
            var hpY = ViewModel.HpPercentageProbe.Y;
            Console.WriteLine($"[{ViewModel.ClientName}] HP Monitor Position: X={hpX} (calculated from {ViewModel.HpPercentageProbe.MonitorPercentage}%)");
            
            var hpColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, hpX, hpY);
            
            // Capture current MP color at calculated position  
            var mpX = ViewModel.MpPercentageProbe.CalculatedX;
            var mpY = ViewModel.MpPercentageProbe.Y;
            Console.WriteLine($"[{ViewModel.ClientName}] MP Monitor Position: X={mpX} (calculated from {ViewModel.MpPercentageProbe.MonitorPercentage}%)");
            
            var mpColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, mpX, mpY);
            
            // Quick verification - just check center pixel
            // (Detailed sampling removed to reduce log spam)
            
            // Update expected colors with current colors
            ViewModel.HpPercentageProbe.ExpectedColor = hpColor;
            ViewModel.MpPercentageProbe.ExpectedColor = mpColor;
            
            // Update UI displays
            HpPercentageColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(hpColor.R, hpColor.G, hpColor.B));
            MpPercentageColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(mpColor.R, mpColor.G, mpColor.B));
            
            Console.WriteLine($"[{ViewModel.ClientName}] === COLORS CAPTURED ===");
            Console.WriteLine($"[{ViewModel.ClientName}] HP Expected Color: RGB({hpColor.R},{hpColor.G},{hpColor.B}) at ({hpX},{hpY})");
            Console.WriteLine($"[{ViewModel.ClientName}] MP Expected Color: RGB({mpColor.R},{mpColor.G},{mpColor.B}) at ({mpX},{mpY})");
            
            // Reset triggered states
            ViewModel.HpPercentageProbe.IsTriggered = false;
            ViewModel.MpPercentageProbe.IsTriggered = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Color capture error: {ex.Message}");
        }
    }

    // Public methods for panic buttons
    public void StartClient()
    {
        StartClient_Click(null, null);
    }
    
    public void StopClient()
    {
        StopClient_Click(null, null);
    }
    
    // Public methods for getting ComboBox values
    public double GetBabeBotHpThreshold()
    {
        var selectedItem = BabeBotHpThreshold?.SelectedItem as System.Windows.Controls.ComboBoxItem;
        if (selectedItem?.Content?.ToString() is string value && double.TryParse(value, out var threshold))
        {
            return threshold;
        }
        return 90.0; // Default
    }
    
    public double GetBabeBotMpThreshold()
    {
        var selectedItem = BabeBotMpThreshold?.SelectedItem as System.Windows.Controls.ComboBoxItem;
        if (selectedItem?.Content?.ToString() is string value && double.TryParse(value, out var threshold))
        {
            return threshold;
        }
        return 90.0; // Default
    }
    
    private void StartClient_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
            StatusIndicator.ToolTip = "Please select a window first!";
            return;
        }

        // _isRunning = true; // Removed - using ViewModel.IsRunning instead
        ViewModel.IsRunning = true;
        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        StatusIndicator.Fill = new SolidColorBrush(Colors.Green);
        StatusIndicator.ToolTip = $"Running automation for {ViewModel.ClientName}";
        
        StartPeriodicClicks();
        StartMonitoring();
        
        // Auto-enable BabeBot HP/MP when starting client
        ViewModel.BabeBotHp.Enabled = true;
        ViewModel.BabeBotMp.Enabled = true;
        StartBabeBotMonitoring();
        
        Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot HP/MP auto-enabled on start");
        
        // Debug HP/MP settings
        Console.WriteLine($"[{ViewModel.ClientName}] START: HP Enabled={ViewModel.HpTrigger.Enabled}, Coords=({ViewModel.HpTrigger.X},{ViewModel.HpTrigger.Y}), Tolerance={ViewModel.HpProbe.Tolerance}");
        Console.WriteLine($"[{ViewModel.ClientName}] START: MP Enabled={ViewModel.MpTrigger.Enabled}, Coords=({ViewModel.MpTrigger.X},{ViewModel.MpTrigger.Y}), Tolerance={ViewModel.MpProbe.Tolerance}");
    }

    private void StopClient_Click(object sender, RoutedEventArgs e)
    {
        // _isRunning = false; // Removed - using ViewModel.IsRunning instead
        ViewModel.IsRunning = false;
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        StatusIndicator.Fill = new SolidColorBrush(Colors.Orange);
        StatusIndicator.ToolTip = "Stopped";
        
        StopPeriodicClicks();
        StopMonitoring();
        
        // Auto-disable BabeBot HP/MP when stopping client
        ViewModel.BabeBotHp.Enabled = false;
        ViewModel.BabeBotMp.Enabled = false;
        StopBabeBotMonitoring();
        
        Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot HP/MP auto-disabled on stop");
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
        
        // Update percentage probe UI
        HpPercentageStartX.Text = ViewModel.HpPercentageProbe.StartX.ToString();
        HpPercentageEndX.Text = ViewModel.HpPercentageProbe.EndX.ToString();
        HpPercentageY.Text = ViewModel.HpPercentageProbe.Y.ToString();
        HpPercentageThreshold.Text = ViewModel.HpPercentageProbe.MonitorPercentage.ToString();
        HpPercentageTolerance.Text = ViewModel.HpPercentageProbe.Tolerance.ToString();
        HpPercentageColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(
            ViewModel.HpPercentageProbe.ExpectedColor.R,
            ViewModel.HpPercentageProbe.ExpectedColor.G,
            ViewModel.HpPercentageProbe.ExpectedColor.B));
            
        MpPercentageStartX.Text = ViewModel.MpPercentageProbe.StartX.ToString();
        MpPercentageEndX.Text = ViewModel.MpPercentageProbe.EndX.ToString();
        MpPercentageY.Text = ViewModel.MpPercentageProbe.Y.ToString();
        MpPercentageThreshold.Text = ViewModel.MpPercentageProbe.MonitorPercentage.ToString();
        MpPercentageTolerance.Text = ViewModel.MpPercentageProbe.Tolerance.ToString();
        MpPercentageColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(
            ViewModel.MpPercentageProbe.ExpectedColor.R,
            ViewModel.MpPercentageProbe.ExpectedColor.G,
            ViewModel.MpPercentageProbe.ExpectedColor.B));
            
        PercentageMonitoringEnabled.IsChecked = ViewModel.HpPercentageProbe.Enabled || ViewModel.MpPercentageProbe.Enabled;
        
        // Update Python-style potion coordinates UI
        PythonHpPotionX.Text = ViewModel.PythonHpPotionClick.X.ToString();
        PythonHpPotionY.Text = ViewModel.PythonHpPotionClick.Y.ToString();
        PythonHpPotionCooldown.Text = ViewModel.PythonHpPotionClick.CooldownMs.ToString();
        
        PythonMpPotionX.Text = ViewModel.PythonMpPotionClick.X.ToString();
        PythonMpPotionY.Text = ViewModel.PythonMpPotionClick.Y.ToString();
        PythonMpPotionCooldown.Text = ViewModel.PythonMpPotionClick.CooldownMs.ToString();
        
        // Update BabeBot UI elements
        BabeBotHpStart.Text = ViewModel.BabeBotHp.StartX.ToString();
        BabeBotHpEnd.Text = ViewModel.BabeBotHp.EndX.ToString();
        BabeBotHpY.Text = ViewModel.BabeBotHp.Y.ToString();
        
        // Set HP threshold dropdown
        var hpThresholdValue = ViewModel.BabeBotHp.ThresholdPercentage.ToString();
        foreach (System.Windows.Controls.ComboBoxItem item in BabeBotHpThreshold.Items)
        {
            if (item.Content?.ToString() == hpThresholdValue)
            {
                BabeBotHpThreshold.SelectedItem = item;
                break;
            }
        }
        
        BabeBotHpPotionX.Text = ViewModel.BabeBotHp.PotionX.ToString();
        BabeBotHpPotionY.Text = ViewModel.BabeBotHp.PotionY.ToString();
        
        BabeBotMpStart.Text = ViewModel.BabeBotMp.StartX.ToString();
        BabeBotMpEnd.Text = ViewModel.BabeBotMp.EndX.ToString();
        BabeBotMpY.Text = ViewModel.BabeBotMp.Y.ToString();
        
        // Set MP threshold dropdown
        var mpThresholdValue = ViewModel.BabeBotMp.ThresholdPercentage.ToString();
        foreach (System.Windows.Controls.ComboBoxItem item in BabeBotMpThreshold.Items)
        {
            if (item.Content?.ToString() == mpThresholdValue)
            {
                BabeBotMpThreshold.SelectedItem = item;
                break;
            }
        }
        
        BabeBotMpPotionX.Text = ViewModel.BabeBotMp.PotionX.ToString();
        BabeBotMpPotionY.Text = ViewModel.BabeBotMp.PotionY.ToString();
        
        UpdatePercentageMonitorPosition();
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
        
        // Stop BabeBot timer
        if (_babeBotTimer != null)
        {
            _babeBotTimer.Stop();
            _babeBotTimer = null;
        }
        
        Console.WriteLine($"[{ViewModel.ClientName}] All periodic timers STOPPED and disposed");
    }
    
    private void StartMonitoring()
    {
        StopMonitoring();
        
        // Only start monitoring if HP, MP triggers, or percentage monitoring are enabled
        if (ViewModel.HpTrigger.Enabled || ViewModel.MpTrigger.Enabled || ViewModel.HpPercentageProbe.Enabled || ViewModel.MpPercentageProbe.Enabled)
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
                
                // Standard HP/MP trigger checks
                CheckHpTriggerByPercentage(hpPercentage);
                CheckMpTriggerByPercentage(mpPercentage);
                
                // Python-style percentage monitoring 
                CheckPercentageBasedTriggers();
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
            Console.WriteLine($"[{ViewModel.ClientName}] HP TIMER TICK - KeepClicking={ViewModel.HpTrigger.KeepClicking}, Coords=({ViewModel.HpTrigger.X},{ViewModel.HpTrigger.Y})");
            
            if (ViewModel.HpTrigger.KeepClicking)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] HP TRIGGER CLICK at ({ViewModel.HpTrigger.X},{ViewModel.HpTrigger.Y})");
                PerformBackgroundClick(ViewModel.HpTrigger.X, ViewModel.HpTrigger.Y, "HP_TRIGGER");
                ViewModel.HpTrigger.ExecutionCount++;
                ViewModel.TriggerCount++;
            }
            else
            {
                Console.WriteLine($"[{ViewModel.ClientName}] HP TIMER: KeepClicking is FALSE - stopping timer");
                StopHpTriggerClicking();
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
            Console.WriteLine($"[{ViewModel.ClientName}] MP TIMER TICK - KeepClicking={ViewModel.MpTrigger.KeepClicking}, Coords=({ViewModel.MpTrigger.X},{ViewModel.MpTrigger.Y})");
            
            if (ViewModel.MpTrigger.KeepClicking)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] MP TRIGGER CLICK at ({ViewModel.MpTrigger.X},{ViewModel.MpTrigger.Y})");
                PerformBackgroundClick(ViewModel.MpTrigger.X, ViewModel.MpTrigger.Y, "MP_TRIGGER");
                ViewModel.MpTrigger.ExecutionCount++;
                ViewModel.TriggerCount++;
            }
            else
            {
                Console.WriteLine($"[{ViewModel.ClientName}] MP TIMER: KeepClicking is FALSE - stopping timer");
                StopMpTriggerClicking();
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
    
    private void CheckPercentageBasedTriggers()
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero) return;
        
        try
        {
            // Check HP percentage probe
            if (ViewModel.HpPercentageProbe.Enabled)
            {
                var hpX = ViewModel.HpPercentageProbe.CalculatedX;
                var hpY = ViewModel.HpPercentageProbe.Y;
                
                var currentColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, hpX, hpY);
                var distance = ColorSampler.CalculateColorDistance(currentColor, ViewModel.HpPercentageProbe.ExpectedColor);
                
                // Python logic: if pixel_color != expected_color then trigger
                bool hpColorChanged = distance > ViewModel.HpPercentageProbe.Tolerance;
                
                // Update current color in ViewModel for real-time tracking
                ViewModel.HpPercentageProbe.CurrentColor = currentColor;
                
                // Update UI with real-time color and status
                Dispatcher.BeginInvoke(() =>
                {
                    // Update current color display
                    HpCurrentColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(currentColor.R, currentColor.G, currentColor.B));
                    HpCurrentColorText.Text = $"{currentColor.R},{currentColor.G},{currentColor.B}";
                    
                    if (hpColorChanged)
                    {
                        HpPercentageStatus.Text = $"LOW ({distance:F1})";
                        HpPercentageStatus.Foreground = new SolidColorBrush(Colors.Red);
                    }
                    else
                    {
                        HpPercentageStatus.Text = $"OK ({distance:F1})";
                        HpPercentageStatus.Foreground = new SolidColorBrush(Colors.LimeGreen);
                    }
                    
                    // Debug info every 200 cycles (roughly every 10 seconds) and only when interesting
                    if (_debugCounter % 200 == 0 && (hpColorChanged || distance > 50))
                    {
                        Console.WriteLine($"[{ViewModel.ClientName}] HP-DEBUG: Current=RGB({currentColor.R},{currentColor.G},{currentColor.B}) Distance={distance:F1} Triggered={hpColorChanged}");
                    }
                });
                
                // Trigger logic with cooldown check
                if (hpColorChanged && !ViewModel.HpPercentageProbe.IsTriggered && ViewModel.PythonHpPotionClick.Enabled)
                {
                    var now = DateTime.UtcNow;
                    if ((now - ViewModel.PythonHpPotionClick.LastExecution).TotalMilliseconds >= ViewModel.PythonHpPotionClick.CooldownMs)
                    {
                        ViewModel.HpPercentageProbe.IsTriggered = true;
                        ViewModel.PythonHpPotionClick.LastExecution = now;
                        Console.WriteLine($"[{ViewModel.ClientName}] PYTHON-HP: Color changed at {hpX},{hpY} (threshold {ViewModel.HpPercentageProbe.MonitorPercentage}%) - RGB({currentColor.R},{currentColor.G},{currentColor.B}) distance={distance:F1}");
                        
                        // Trigger Python-style HP potion click
                        if (ViewModel.PythonHpPotionClick.X > 0 && ViewModel.PythonHpPotionClick.Y > 0)
                        {
                            Console.WriteLine($"[{ViewModel.ClientName}] PYTHON-HP: Triggering potion click at ({ViewModel.PythonHpPotionClick.X},{ViewModel.PythonHpPotionClick.Y}) cooldown={ViewModel.PythonHpPotionClick.CooldownMs}ms");
                            PerformBackgroundClick(ViewModel.PythonHpPotionClick.X, ViewModel.PythonHpPotionClick.Y, "PYTHON_HP_TRIGGER");
                            ViewModel.TriggerCount++;
                            ViewModel.PythonHpPotionClick.ExecutionCount++;
                        }
                    }
                    else
                    {
                        var remainingCooldown = ViewModel.PythonHpPotionClick.CooldownMs - (now - ViewModel.PythonHpPotionClick.LastExecution).TotalMilliseconds;
                        Console.WriteLine($"[{ViewModel.ClientName}] PYTHON-HP: On cooldown, {remainingCooldown:F0}ms remaining");
                    }
                }
                else if (!hpColorChanged && ViewModel.HpPercentageProbe.IsTriggered)
                {
                    ViewModel.HpPercentageProbe.IsTriggered = false;
                    Console.WriteLine($"[{ViewModel.ClientName}] PYTHON-HP: Color restored at {hpX},{hpY}");
                }
            }
            
            // Check MP percentage probe
            if (ViewModel.MpPercentageProbe.Enabled)
            {
                var mpX = ViewModel.MpPercentageProbe.CalculatedX;
                var mpY = ViewModel.MpPercentageProbe.Y;
                
                var currentColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, mpX, mpY);
                var distance = ColorSampler.CalculateColorDistance(currentColor, ViewModel.MpPercentageProbe.ExpectedColor);
                
                // Python logic: if pixel_color != expected_color then trigger
                bool mpColorChanged = distance > ViewModel.MpPercentageProbe.Tolerance;
                
                // Update current color in ViewModel for real-time tracking
                ViewModel.MpPercentageProbe.CurrentColor = currentColor;
                
                // Update UI with real-time color and status
                Dispatcher.BeginInvoke(() =>
                {
                    // Update current color display
                    MpCurrentColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(currentColor.R, currentColor.G, currentColor.B));
                    MpCurrentColorText.Text = $"{currentColor.R},{currentColor.G},{currentColor.B}";
                    
                    if (mpColorChanged)
                    {
                        MpPercentageStatus.Text = $"LOW ({distance:F1})";
                        MpPercentageStatus.Foreground = new SolidColorBrush(Colors.Red);
                    }
                    else
                    {
                        MpPercentageStatus.Text = $"OK ({distance:F1})";
                        MpPercentageStatus.Foreground = new SolidColorBrush(Colors.CornflowerBlue);
                    }
                    
                    // Debug info every 200 cycles (roughly every 10 seconds) and only when interesting
                    if (_debugCounter % 200 == 0 && (mpColorChanged || distance > 50))
                    {
                        Console.WriteLine($"[{ViewModel.ClientName}] MP-DEBUG: Current=RGB({currentColor.R},{currentColor.G},{currentColor.B}) Distance={distance:F1} Triggered={mpColorChanged}");
                    }
                });
                
                // Trigger logic with cooldown check
                if (mpColorChanged && !ViewModel.MpPercentageProbe.IsTriggered && ViewModel.PythonMpPotionClick.Enabled)
                {
                    var now = DateTime.UtcNow;
                    if ((now - ViewModel.PythonMpPotionClick.LastExecution).TotalMilliseconds >= ViewModel.PythonMpPotionClick.CooldownMs)
                    {
                        ViewModel.MpPercentageProbe.IsTriggered = true;
                        ViewModel.PythonMpPotionClick.LastExecution = now;
                        Console.WriteLine($"[{ViewModel.ClientName}] PYTHON-MP: Color changed at {mpX},{mpY} (threshold {ViewModel.MpPercentageProbe.MonitorPercentage}%) - RGB({currentColor.R},{currentColor.G},{currentColor.B}) distance={distance:F1}");
                        
                        // Trigger Python-style MP potion click
                        if (ViewModel.PythonMpPotionClick.X > 0 && ViewModel.PythonMpPotionClick.Y > 0)
                        {
                            Console.WriteLine($"[{ViewModel.ClientName}] PYTHON-MP: Triggering potion click at ({ViewModel.PythonMpPotionClick.X},{ViewModel.PythonMpPotionClick.Y}) cooldown={ViewModel.PythonMpPotionClick.CooldownMs}ms");
                            PerformBackgroundClick(ViewModel.PythonMpPotionClick.X, ViewModel.PythonMpPotionClick.Y, "PYTHON_MP_TRIGGER");
                            ViewModel.TriggerCount++;
                            ViewModel.PythonMpPotionClick.ExecutionCount++;
                        }
                    }
                    else
                    {
                        var remainingCooldown = ViewModel.PythonMpPotionClick.CooldownMs - (now - ViewModel.PythonMpPotionClick.LastExecution).TotalMilliseconds;
                        Console.WriteLine($"[{ViewModel.ClientName}] PYTHON-MP: On cooldown, {remainingCooldown:F0}ms remaining");
                    }
                }
                else if (!mpColorChanged && ViewModel.MpPercentageProbe.IsTriggered)
                {
                    ViewModel.MpPercentageProbe.IsTriggered = false;
                    Console.WriteLine($"[{ViewModel.ClientName}] PYTHON-MP: Color restored at {mpX},{mpY}");
                }
            }
            
            // Increment debug counter
            _debugCounter++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Percentage monitoring error: {ex.Message}");
        }
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
        Console.WriteLine($"[{ViewModel.ClientName}] PerformBackgroundClick called: ({x},{y}) channel={channel} hwnd={ViewModel.TargetHwnd}");
        
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] ERROR: TargetHwnd is zero, cannot perform click");
            return;
        }
        
        try
        {
            // PostMessage uses client coordinates directly - no conversion needed
            // The coordinates we receive are already client coordinates from CoordinatePicker
            var processName = GetProcessName(ViewModel.TargetHwnd);
            var lParam = (y << 16) | (x & 0xFFFF);
            
            // ALWAYS debug coordinate info for trigger clicks
            Console.WriteLine($"[{ViewModel.ClientName}] {channel} PostMessage: Process={processName} ClientCoords=({x},{y}) lParam=0x{lParam:X8}");
            
            // Check if window still exists
            if (!User32.IsWindow(ViewModel.TargetHwnd))
            {
                Console.WriteLine($"[{ViewModel.ClientName}] ERROR: Target window no longer exists");
                return;
            }
            
            // Try all message combinations with detailed logging
            Console.WriteLine($"[{ViewModel.ClientName}] Sending WM_MOUSEMOVE...");
            User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_MOUSEMOVE, IntPtr.Zero, (IntPtr)lParam);
            
            Console.WriteLine($"[{ViewModel.ClientName}] Sending WM_LBUTTONDOWN (PostMessage)...");
            bool result1 = User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, (IntPtr)1, (IntPtr)lParam);
            Console.WriteLine($"[{ViewModel.ClientName}] WM_LBUTTONDOWN result: {result1}");
            
            Console.WriteLine($"[{ViewModel.ClientName}] Sending WM_LBUTTONUP (PostMessage)...");
            bool result2 = User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
            Console.WriteLine($"[{ViewModel.ClientName}] WM_LBUTTONUP result: {result2}");
            
            Console.WriteLine($"[{ViewModel.ClientName}] Sending WM_LBUTTONDOWN (SendMessage)...");
            User32.SendMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, (IntPtr)1, (IntPtr)lParam);
            
            Console.WriteLine($"[{ViewModel.ClientName}] Sending WM_LBUTTONUP (SendMessage)...");
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

    // HP Shape Mouse Event Handlers
    private void HpShape_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_hpShape != null)
        {
            _isDraggingHp = true;
            _dragStartPoint = e.GetPosition(GetOverlayCanvas());
            _hpShape.CaptureMouse();
            e.Handled = true;
            Console.WriteLine($"[{ViewModel.ClientName}] Started dragging HP shape");
        }
    }
    
    private void HpShape_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingHp && _hpShape != null && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            var canvas = GetOverlayCanvas();
            if (canvas != null)
            {
                var currentPoint = e.GetPosition(canvas);
                
                // Calculate new position directly from mouse position
                var newX = currentPoint.X - 10; // Center the circle on mouse
                var newY = currentPoint.Y - 10;
                
                // Update shape position
                Canvas.SetLeft(_hpShape, newX);
                Canvas.SetTop(_hpShape, newY);
                
                // Update ViewModel coordinates (center of circle)
                ViewModel.HpProbe.X = (int)(newX + 10);
                ViewModel.HpProbe.Y = (int)(newY + 10);
                
                // Update UI text boxes
                HpX.Text = ViewModel.HpProbe.X.ToString();
                HpY.Text = ViewModel.HpProbe.Y.ToString();
                
                Console.WriteLine($"[{ViewModel.ClientName}] HP shape moved to ({ViewModel.HpProbe.X},{ViewModel.HpProbe.Y})");
            }
        }
    }
    
    private void HpShape_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingHp && _hpShape != null)
        {
            _isDraggingHp = false;
            _hpShape.ReleaseMouseCapture();
            Console.WriteLine($"[{ViewModel.ClientName}] Finished dragging HP shape at ({ViewModel.HpProbe.X},{ViewModel.HpProbe.Y})");
            
            // Optionally read color at new position
            if (ViewModel.TargetHwnd != IntPtr.Zero)
            {
                var newColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, ViewModel.HpProbe.X, ViewModel.HpProbe.Y);
                ViewModel.HpProbe.ExpectedColor = newColor;
                ViewModel.HpProbe.ReferenceColor = newColor;
                HpColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(newColor.R, newColor.G, newColor.B));
                HpColorText.Text = $"{newColor.R},{newColor.G},{newColor.B}";
                Console.WriteLine($"[{ViewModel.ClientName}] HP color updated: RGB({newColor.R},{newColor.G},{newColor.B})");
            }
        }
    }
    
    // MP Shape Mouse Event Handlers
    private void MpShape_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_mpShape != null)
        {
            _isDraggingMp = true;
            _dragStartPoint = e.GetPosition(GetOverlayCanvas());
            _mpShape.CaptureMouse();
            e.Handled = true;
            Console.WriteLine($"[{ViewModel.ClientName}] Started dragging MP shape");
        }
    }
    
    private void MpShape_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingMp && _mpShape != null && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            var canvas = GetOverlayCanvas();
            if (canvas != null)
            {
                var currentPoint = e.GetPosition(canvas);
                
                // Calculate new position directly from mouse position
                var newX = currentPoint.X - 10; // Center the circle on mouse
                var newY = currentPoint.Y - 10;
                
                // Update shape position
                Canvas.SetLeft(_mpShape, newX);
                Canvas.SetTop(_mpShape, newY);
                
                // Update ViewModel coordinates (center of circle)
                ViewModel.MpProbe.X = (int)(newX + 10);
                ViewModel.MpProbe.Y = (int)(newY + 10);
                
                // Update UI text boxes
                MpX.Text = ViewModel.MpProbe.X.ToString();
                MpY.Text = ViewModel.MpProbe.Y.ToString();
                
                Console.WriteLine($"[{ViewModel.ClientName}] MP shape moved to ({ViewModel.MpProbe.X},{ViewModel.MpProbe.Y})");
            }
        }
    }
    
    private void MpShape_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingMp && _mpShape != null)
        {
            _isDraggingMp = false;
            _mpShape.ReleaseMouseCapture();
            Console.WriteLine($"[{ViewModel.ClientName}] Finished dragging MP shape at ({ViewModel.MpProbe.X},{ViewModel.MpProbe.Y})");
            
            // Optionally read color at new position
            if (ViewModel.TargetHwnd != IntPtr.Zero)
            {
                var newColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, ViewModel.MpProbe.X, ViewModel.MpProbe.Y);
                ViewModel.MpProbe.ExpectedColor = newColor;
                ViewModel.MpProbe.ReferenceColor = newColor;
                MpColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(newColor.R, newColor.G, newColor.B));
                MpColorText.Text = $"{newColor.R},{newColor.G},{newColor.B}";
                Console.WriteLine($"[{ViewModel.ClientName}] MP color updated: RGB({newColor.R},{newColor.G},{newColor.B})");
            }
        }
    }
    
    // HP Percentage Bar Mouse Event Handlers
    private void HpPercentageShape_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_hpPercentageShape != null)
        {
            _isDraggingHpPercentage = true;
            _dragStartPoint = e.GetPosition(GetOverlayCanvas());
            _hpPercentageShape.CaptureMouse();
            e.Handled = true;
            Console.WriteLine($"[{ViewModel.ClientName}] Started dragging HP percentage bar");
        }
    }
    
    private void HpPercentageShape_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingHpPercentage && _hpPercentageShape != null && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            var canvas = GetOverlayCanvas();
            if (canvas != null)
            {
                var currentPoint = e.GetPosition(canvas);
                
                // Calculate new position directly from mouse position
                var newX = currentPoint.X - (_hpPercentageShape.Width / 2);
                var newY = currentPoint.Y - (_hpPercentageShape.Height / 2);
                
                // Update shape position
                Canvas.SetLeft(_hpPercentageShape, newX);
                Canvas.SetTop(_hpPercentageShape, newY);
                
                // Update ViewModel coordinates
                ViewModel.HpPercentageProbe.StartX = (int)newX;
                ViewModel.HpPercentageProbe.EndX = (int)(newX + _hpPercentageShape.Width);
                ViewModel.HpPercentageProbe.Y = (int)(newY + _hpPercentageShape.Height / 2);
                
                // Update UI text boxes
                HpPercentageStartX.Text = ViewModel.HpPercentageProbe.StartX.ToString();
                HpPercentageEndX.Text = ViewModel.HpPercentageProbe.EndX.ToString();
                HpPercentageY.Text = ViewModel.HpPercentageProbe.Y.ToString();
                
                UpdatePercentageMonitorPosition();
                Console.WriteLine($"[{ViewModel.ClientName}] HP bar moved to ({ViewModel.HpPercentageProbe.StartX}-{ViewModel.HpPercentageProbe.EndX},{ViewModel.HpPercentageProbe.Y})");
            }
        }
    }
    
    private void HpPercentageShape_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingHpPercentage && _hpPercentageShape != null)
        {
            _isDraggingHpPercentage = false;
            _hpPercentageShape.ReleaseMouseCapture();
            Console.WriteLine($"[{ViewModel.ClientName}] Finished dragging HP percentage bar");
        }
    }
    
    // MP Percentage Bar Mouse Event Handlers
    private void MpPercentageShape_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_mpPercentageShape != null)
        {
            _isDraggingMpPercentage = true;
            _dragStartPoint = e.GetPosition(GetOverlayCanvas());
            _mpPercentageShape.CaptureMouse();
            e.Handled = true;
            Console.WriteLine($"[{ViewModel.ClientName}] Started dragging MP percentage bar");
        }
    }
    
    private void MpPercentageShape_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingMpPercentage && _mpPercentageShape != null)
        {
            var canvas = GetOverlayCanvas();
            if (canvas != null)
            {
                var currentPoint = e.GetPosition(canvas);
                var deltaX = currentPoint.X - _dragStartPoint.X;
                var deltaY = currentPoint.Y - _dragStartPoint.Y;
                
                var newX = (int)(Canvas.GetLeft(_mpPercentageShape) + deltaX);
                var newY = (int)(Canvas.GetTop(_mpPercentageShape) + deltaY);
                
                // Update shape position
                Canvas.SetLeft(_mpPercentageShape, newX);
                Canvas.SetTop(_mpPercentageShape, newY);
                
                // Update ViewModel coordinates
                ViewModel.MpPercentageProbe.StartX = newX;
                ViewModel.MpPercentageProbe.EndX = newX + (int)_mpPercentageShape.Width;
                ViewModel.MpPercentageProbe.Y = newY + (int)_mpPercentageShape.Height / 2;
                
                // Update UI text boxes
                MpPercentageStartX.Text = ViewModel.MpPercentageProbe.StartX.ToString();
                MpPercentageEndX.Text = ViewModel.MpPercentageProbe.EndX.ToString();
                MpPercentageY.Text = ViewModel.MpPercentageProbe.Y.ToString();
                
                _dragStartPoint = currentPoint;
                UpdatePercentageMonitorPosition();
                Console.WriteLine($"[{ViewModel.ClientName}] MP bar moved to ({ViewModel.MpPercentageProbe.StartX}-{ViewModel.MpPercentageProbe.EndX},{ViewModel.MpPercentageProbe.Y})");
            }
        }
    }
    
    private void MpPercentageShape_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingMpPercentage && _mpPercentageShape != null)
        {
            _isDraggingMpPercentage = false;
            _mpPercentageShape.ReleaseMouseCapture();
            Console.WriteLine($"[{ViewModel.ClientName}] Finished dragging MP percentage bar");
        }
    }
    
    // Helper method to get overlay canvas
    private System.Windows.Controls.Canvas? GetOverlayCanvas()
    {
        var mainWindow = Application.Current.MainWindow as MainWindow;
        return mainWindow?.GetOverlayCanvas();
    }
    
    // Public methods to show/hide shapes in overlay mode
    public void ShowOverlayShapes()
    {
        var canvas = GetOverlayCanvas();
        if (canvas == null) return;
        
        // Add shapes to overlay canvas if not already added
        if (_hpShape != null && !canvas.Children.Contains(_hpShape))
        {
            canvas.Children.Add(_hpShape);
            // Position HP shape based on current coordinates
            Canvas.SetLeft(_hpShape, ViewModel.HpProbe.X - 10);
            Canvas.SetTop(_hpShape, ViewModel.HpProbe.Y - 10);
            _hpShape.Visibility = Visibility.Visible;
        }
        
        if (_mpShape != null && !canvas.Children.Contains(_mpShape))
        {
            canvas.Children.Add(_mpShape);
            // Position MP shape based on current coordinates
            Canvas.SetLeft(_mpShape, ViewModel.MpProbe.X - 10);
            Canvas.SetTop(_mpShape, ViewModel.MpProbe.Y - 10);
            _mpShape.Visibility = Visibility.Visible;
        }
        
        if (_hpPercentageShape != null && !canvas.Children.Contains(_hpPercentageShape))
        {
            canvas.Children.Add(_hpPercentageShape);
            // Position HP percentage bar
            Canvas.SetLeft(_hpPercentageShape, ViewModel.HpPercentageProbe.StartX);
            Canvas.SetTop(_hpPercentageShape, ViewModel.HpPercentageProbe.Y - 4);
            _hpPercentageShape.Width = ViewModel.HpPercentageProbe.EndX - ViewModel.HpPercentageProbe.StartX;
            _hpPercentageShape.Visibility = Visibility.Visible;
        }
        
        if (_mpPercentageShape != null && !canvas.Children.Contains(_mpPercentageShape))
        {
            canvas.Children.Add(_mpPercentageShape);
            // Position MP percentage bar
            Canvas.SetLeft(_mpPercentageShape, ViewModel.MpPercentageProbe.StartX);
            Canvas.SetTop(_mpPercentageShape, ViewModel.MpPercentageProbe.Y - 4);
            _mpPercentageShape.Width = ViewModel.MpPercentageProbe.EndX - ViewModel.MpPercentageProbe.StartX;
            _mpPercentageShape.Visibility = Visibility.Visible;
        }
        
        Console.WriteLine($"[{ViewModel.ClientName}] Overlay shapes shown");
    }
    
    public void HideOverlayShapes()
    {
        var canvas = GetOverlayCanvas();
        if (canvas == null) return;
        
        if (_hpShape != null)
        {
            _hpShape.Visibility = Visibility.Collapsed;
            canvas.Children.Remove(_hpShape);
        }
        
        if (_mpShape != null)
        {
            _mpShape.Visibility = Visibility.Collapsed;
            canvas.Children.Remove(_mpShape);
        }
        
        if (_hpPercentageShape != null)
        {
            _hpPercentageShape.Visibility = Visibility.Collapsed;
            canvas.Children.Remove(_hpPercentageShape);
        }
        
        if (_mpPercentageShape != null)
        {
            _mpPercentageShape.Visibility = Visibility.Collapsed;
            canvas.Children.Remove(_mpPercentageShape);
        }
        
        Console.WriteLine($"[{ViewModel.ClientName}] Overlay shapes hidden");
    }
   
    // Show Draggable Shapes Button Event Handler
    private void ShowDraggableShapes_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
            StatusIndicator.ToolTip = "Please select a window first!";
            Console.WriteLine($"[{ViewModel.ClientName}] Cannot show shapes - no window selected");
            return;
        }

        // Show overlay shapes for manual positioning
        ShowOverlayShapesEnhanced();
        
        // Also enable overlay mode in main window if not already enabled
        var mainWindow = Application.Current.MainWindow as MainWindow;
        if (mainWindow != null)
        {
            // Check if overlay mode is already active
            var overlayCheckBox = mainWindow.FindName("OverlayModeCheckBox") as CheckBox;
            if (overlayCheckBox != null && overlayCheckBox.IsChecked != true)
            {
                overlayCheckBox.IsChecked = true; // This will trigger overlay mode
            }
        }
        
        Console.WriteLine($"[{ViewModel.ClientName}] 🎯 DRAGGABLE SHAPES ACTIVATED!");
        Console.WriteLine($"[{ViewModel.ClientName}] ❤️ RED CIRCLE = HP monitoring point - drag to HP bar");
        Console.WriteLine($"[{ViewModel.ClientName}] 💙 BLUE CIRCLE = MP monitoring point - drag to MP bar");  
        Console.WriteLine($"[{ViewModel.ClientName}] 📏 RED RECTANGLE = HP percentage bar - drag and resize");
        Console.WriteLine($"[{ViewModel.ClientName}] 📏 BLUE RECTANGLE = MP percentage bar - drag and resize");
        Console.WriteLine($"[{ViewModel.ClientName}] ⏰ Shapes stay visible for 30 seconds - click to close early");
        Console.WriteLine($"[{ViewModel.ClientName}] 🎯 When you drag shapes, coordinates auto-update in UI!");
        
        StatusIndicator.Fill = new SolidColorBrush(Colors.Orange);
        StatusIndicator.ToolTip = "Draggable shapes active - position them on your HP/MP bars";
    }


    
    // Enhanced ShowOverlayShapes with better positioning and visual feedback
    private void ShowOverlayShapesEnhanced()
    {
        var canvas = GetOverlayCanvas();
        if (canvas == null) 
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Cannot access overlay canvas");
            return;
        }
        
        // Clear existing shapes first
        HideOverlayShapes();
        
        // Get window bounds for better initial positioning
        var windowRect = GetWindowBounds();
        var offsetX = windowRect.Left + 50; // Start shapes 50px from window left
        var offsetY = windowRect.Top + 50;  // Start shapes 50px from window top
        
        // Create and position HP shape (red circle)
        if (_hpShape != null)
        {
            canvas.Children.Add(_hpShape);
            Canvas.SetLeft(_hpShape, offsetX);
            Canvas.SetTop(_hpShape, offsetY);
            _hpShape.Visibility = Visibility.Visible;
            
            // Update ViewModel coordinates
            ViewModel.HpProbe.X = (int)(offsetX + 10);
            ViewModel.HpProbe.Y = (int)(offsetY + 10);
            HpX.Text = ViewModel.HpProbe.X.ToString();
            HpY.Text = ViewModel.HpProbe.Y.ToString();
        }
        
        // Create and position MP shape (blue circle) - slightly below HP
        if (_mpShape != null)
        {
            canvas.Children.Add(_mpShape);
            Canvas.SetLeft(_mpShape, offsetX);
            Canvas.SetTop(_mpShape, offsetY + 30);
            _mpShape.Visibility = Visibility.Visible;
            
            // Update ViewModel coordinates
            ViewModel.MpProbe.X = (int)(offsetX + 10);
            ViewModel.MpProbe.Y = (int)(offsetY + 40);
            MpX.Text = ViewModel.MpProbe.X.ToString();
            MpY.Text = ViewModel.MpProbe.Y.ToString();
        }
        
        // Create and position HP percentage bar (red rectangle)
        if (_hpPercentageShape != null)
        {
            canvas.Children.Add(_hpPercentageShape);
            Canvas.SetLeft(_hpPercentageShape, offsetX + 200);
            Canvas.SetTop(_hpPercentageShape, offsetY);
            _hpPercentageShape.Visibility = Visibility.Visible;
            
            // Update ViewModel coordinates
            ViewModel.HpPercentageProbe.StartX = (int)(offsetX + 200);
            ViewModel.HpPercentageProbe.EndX = (int)(offsetX + 350);
            ViewModel.HpPercentageProbe.Y = (int)(offsetY + 4);
            HpPercentageStartX.Text = ViewModel.HpPercentageProbe.StartX.ToString();
            HpPercentageEndX.Text = ViewModel.HpPercentageProbe.EndX.ToString();
            HpPercentageY.Text = ViewModel.HpPercentageProbe.Y.ToString();
        }
        
        // Create and position MP percentage bar (blue rectangle) - below HP bar
        if (_mpPercentageShape != null)
        {
            canvas.Children.Add(_mpPercentageShape);
            Canvas.SetLeft(_mpPercentageShape, offsetX + 200);
            Canvas.SetTop(_mpPercentageShape, offsetY + 20);
            _mpPercentageShape.Visibility = Visibility.Visible;
            
            // Update ViewModel coordinates
            ViewModel.MpPercentageProbe.StartX = (int)(offsetX + 200);
            ViewModel.MpPercentageProbe.EndX = (int)(offsetX + 350);
            ViewModel.MpPercentageProbe.Y = (int)(offsetY + 24);
            MpPercentageStartX.Text = ViewModel.MpPercentageProbe.StartX.ToString();
            MpPercentageEndX.Text = ViewModel.MpPercentageProbe.EndX.ToString();
            MpPercentageY.Text = ViewModel.MpPercentageProbe.Y.ToString();
        }
        
        UpdatePercentageMonitorPosition();
        Console.WriteLine($"[{ViewModel.ClientName}] Overlay shapes positioned at window offset ({offsetX},{offsetY})");
    }
    
    // Helper method to get target window bounds
    private RECT GetWindowBounds()
    {
        if (ViewModel.TargetHwnd != IntPtr.Zero)
        {
            User32.GetWindowRect(ViewModel.TargetHwnd, out RECT rect);
            return rect;
        }
        return new RECT { Left = 100, Top = 100, Right = 500, Bottom = 400 }; // Default fallback
    }

    #region BabeBot Style HP/MP Event Handlers
    
    private void BabeBotHpCalibrate_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] No window selected for BabeBot HP calibration");
            return;
        }
        
        Task.Run(() =>
        {
            try
            {
                Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot HP Calibration started...");
                
                // Clear existing reference colors
                ViewModel.BabeBotHp.ReferenceColors.Clear();
                
                // BabeBot calibration logic - sample colors at %5-%95
                for (int percentage = 5; percentage <= 95; percentage += 5)
                {
                    int sampleX = ViewModel.BabeBotHp.CalculateXForPercentage(percentage);
                    var color = ColorSampler.GetColorAt(ViewModel.TargetHwnd, sampleX, ViewModel.BabeBotHp.Y);
                    
                    ViewModel.BabeBotHp.ReferenceColors[percentage] = color;
                    Console.WriteLine($"[{ViewModel.ClientName}] BabeBot HP {percentage}%: X={sampleX}, Color=RGB({color.R},{color.G},{color.B})");
                    
                    Thread.Sleep(50); // Small delay between samples
                }
                
                // Set reference color to the threshold percentage
                var thresholdX = ViewModel.BabeBotHp.MonitorX;
                var thresholdColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, thresholdX, ViewModel.BabeBotHp.Y);
                ViewModel.BabeBotHp.ReferenceColor = thresholdColor;
                
                Dispatcher.BeginInvoke(() =>
                {
                    BabeBotHpCurrentColor.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(thresholdColor.R, thresholdColor.G, thresholdColor.B));
                    BabeBotHpCurrentText.Text = $"{thresholdColor.R},{thresholdColor.G},{thresholdColor.B}";
                    ViewModel.BabeBotHp.Status = "Calibrated";
                });
                
                Console.WriteLine($"[{ViewModel.ClientName}] ✅ BabeBot HP Calibration complete! Monitor X={thresholdX}, Threshold={ViewModel.BabeBotHp.ThresholdPercentage}%");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] BabeBot HP calibration error: {ex.Message}");
            }
        });
    }
    
    private void BabeBotMpCalibrate_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] No window selected for BabeBot MP calibration");
            return;
        }
        
        Task.Run(() =>
        {
            try
            {
                Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot MP Calibration started...");
                
                // Clear existing reference colors
                ViewModel.BabeBotMp.ReferenceColors.Clear();
                
                // BabeBot calibration logic - sample colors at %5-%95
                for (int percentage = 5; percentage <= 95; percentage += 5)
                {
                    int sampleX = ViewModel.BabeBotMp.CalculateXForPercentage(percentage);
                    var color = ColorSampler.GetColorAt(ViewModel.TargetHwnd, sampleX, ViewModel.BabeBotMp.Y);
                    
                    ViewModel.BabeBotMp.ReferenceColors[percentage] = color;
                    Console.WriteLine($"[{ViewModel.ClientName}] BabeBot MP {percentage}%: X={sampleX}, Color=RGB({color.R},{color.G},{color.B})");
                    
                    Thread.Sleep(50); // Small delay between samples
                }
                
                // Set reference color to the threshold percentage
                var thresholdX = ViewModel.BabeBotMp.MonitorX;
                var thresholdColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, thresholdX, ViewModel.BabeBotMp.Y);
                ViewModel.BabeBotMp.ReferenceColor = thresholdColor;
                
                Dispatcher.BeginInvoke(() =>
                {
                    BabeBotMpCurrentColor.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(thresholdColor.R, thresholdColor.G, thresholdColor.B));
                    BabeBotMpCurrentText.Text = $"{thresholdColor.R},{thresholdColor.G},{thresholdColor.B}";
                    ViewModel.BabeBotMp.Status = "Calibrated";
                });
                
                Console.WriteLine($"[{ViewModel.ClientName}] ✅ BabeBot MP Calibration complete! Monitor X={thresholdX}, Threshold={ViewModel.BabeBotMp.ThresholdPercentage}%");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] BabeBot MP calibration error: {ex.Message}");
            }
        });
    }
    
    private void PickBabeBotHpPotion_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("BabeBot HP Potion Position", (x, y) =>
        {
            BabeBotHpPotionX.Text = x.ToString();
            BabeBotHpPotionY.Text = y.ToString();
            ViewModel.BabeBotHp.PotionX = x;
            ViewModel.BabeBotHp.PotionY = y;
            Console.WriteLine($"[{ViewModel.ClientName}] BabeBot HP Potion Click set to: ({x},{y})");
        });
    }
    
    private void PickBabeBotMpPotion_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("BabeBot MP Potion Position", (x, y) =>
        {
            BabeBotMpPotionX.Text = x.ToString();
            BabeBotMpPotionY.Text = y.ToString();
            ViewModel.BabeBotMp.PotionX = x;
            ViewModel.BabeBotMp.PotionY = y;
            Console.WriteLine($"[{ViewModel.ClientName}] BabeBot MP Potion Click set to: ({x},{y})");
        });
    }
    
    private void BabeBotHpDetect_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] No window selected for BabeBot HP detection");
            return;
        }
        
        Task.Run(() =>
        {
            try
            {
                Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot HP Bar Detection started...");
                
                // Use existing HP bar detection system
                var hpBar = DetectBar(true); // true = HP (red)
                if (hpBar != null)
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] ✅ BabeBot HP BAR DETECTED!");
                    Console.WriteLine($"[{ViewModel.ClientName}] HP Bar: StartX={hpBar.Value.startX}, EndX={hpBar.Value.endX}, Y={hpBar.Value.y}");
                    
                    // Auto-fill BabeBot HP coordinates
                    Dispatcher.BeginInvoke(() =>
                    {
                        BabeBotHpStart.Text = hpBar.Value.startX.ToString();
                        BabeBotHpEnd.Text = hpBar.Value.endX.ToString();
                        BabeBotHpY.Text = hpBar.Value.y.ToString();
                        
                        ViewModel.BabeBotHp.StartX = hpBar.Value.startX;
                        ViewModel.BabeBotHp.EndX = hpBar.Value.endX;
                        ViewModel.BabeBotHp.Y = hpBar.Value.y;
                        
                        // Also set reference color
                        ViewModel.BabeBotHp.ReferenceColor = hpBar.Value.color;
                        BabeBotHpCurrentColor.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(hpBar.Value.color.R, hpBar.Value.color.G, hpBar.Value.color.B));
                        BabeBotHpCurrentText.Text = $"{hpBar.Value.color.R},{hpBar.Value.color.G},{hpBar.Value.color.B}";
                        ViewModel.BabeBotHp.Status = "Detected";
                    });
                    
                    // Show visual indicator
                    Dispatcher.BeginInvoke(() =>
                    {
                        ShowBarIndicator("BabeBot HP", hpBar.Value.startX, hpBar.Value.endX, hpBar.Value.y, System.Windows.Media.Colors.Red);
                    });
                }
                else
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] ❌ BabeBot HP BAR NOT FOUND!");
                    Dispatcher.BeginInvoke(() =>
                    {
                        ViewModel.BabeBotHp.Status = "Not Found";
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] BabeBot HP detection error: {ex.Message}");
            }
        });
    }
    
    private void BabeBotMpDetect_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] No window selected for BabeBot MP detection");
            return;
        }
        
        Task.Run(() =>
        {
            try
            {
                Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot MP Bar Detection started...");
                
                // First try to detect HP to get better MP search range
                var hpBar = DetectBar(true);
                int mpSearchStartY = 30;
                int mpSearchEndY = 120;
                
                if (hpBar != null)
                {
                    // Search MP bar right below HP bar
                    mpSearchStartY = hpBar.Value.y + 1;
                    mpSearchEndY = hpBar.Value.y + 25;
                    Console.WriteLine($"[{ViewModel.ClientName}] HP found at Y={hpBar.Value.y}, searching MP in Y range {mpSearchStartY}-{mpSearchEndY}");
                }
                
                var mpBar = DetectBarInRange(false, mpSearchStartY, mpSearchEndY); // false = MP (blue)
                if (mpBar != null)
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] ✅ BabeBot MP BAR DETECTED!");
                    Console.WriteLine($"[{ViewModel.ClientName}] MP Bar: StartX={mpBar.Value.startX}, EndX={mpBar.Value.endX}, Y={mpBar.Value.y}");
                    
                    // Auto-fill BabeBot MP coordinates
                    Dispatcher.BeginInvoke(() =>
                    {
                        BabeBotMpStart.Text = mpBar.Value.startX.ToString();
                        BabeBotMpEnd.Text = mpBar.Value.endX.ToString();
                        BabeBotMpY.Text = mpBar.Value.y.ToString();
                        
                        ViewModel.BabeBotMp.StartX = mpBar.Value.startX;
                        ViewModel.BabeBotMp.EndX = mpBar.Value.endX;
                        ViewModel.BabeBotMp.Y = mpBar.Value.y;
                        
                        // Also set reference color
                        ViewModel.BabeBotMp.ReferenceColor = mpBar.Value.color;
                        BabeBotMpCurrentColor.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(mpBar.Value.color.R, mpBar.Value.color.G, mpBar.Value.color.B));
                        BabeBotMpCurrentText.Text = $"{mpBar.Value.color.R},{mpBar.Value.color.G},{mpBar.Value.color.B}";
                        ViewModel.BabeBotMp.Status = "Detected";
                    });
                    
                    // Show visual indicator
                    Dispatcher.BeginInvoke(() =>
                    {
                        ShowBarIndicator("BabeBot MP", mpBar.Value.startX, mpBar.Value.endX, mpBar.Value.y, System.Windows.Media.Colors.Blue);
                    });
                }
                else
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] ❌ BabeBot MP BAR NOT FOUND!");
                    Dispatcher.BeginInvoke(() =>
                    {
                        ViewModel.BabeBotMp.Status = "Not Found";
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] BabeBot MP detection error: {ex.Message}");
            }
        });
    }
    
    // BabeBot Timer Management
    private void StartBabeBotMonitoring()
    {
        if (_babeBotTimer != null) return;
        
        _babeBotTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(120) // Same as BabeBot timer
        };
        _babeBotTimer.Tick += BabeBotTimer_Tick;
        _babeBotTimer.Start();
        
        Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot monitoring started");
    }
    
    private void StopBabeBotMonitoring()
    {
        _babeBotTimer?.Stop();
        _babeBotTimer = null;
        
        Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot monitoring stopped");
    }
    
    private void BabeBotTimer_Tick(object sender, EventArgs e)
    {
        try
        {
            // Check HP if enabled
            if (ViewModel.BabeBotHp.Enabled)
            {
                CheckBabeBotHp();
            }
            
            // Check MP if enabled
            if (ViewModel.BabeBotMp.Enabled)
            {
                CheckBabeBotMp();
            }
            
            // If neither is enabled, stop the timer
            if (!ViewModel.BabeBotHp.Enabled && !ViewModel.BabeBotMp.Enabled)
            {
                StopBabeBotMonitoring();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] BabeBot timer error: {ex.Message}");
        }
    }
    
    private void CheckBabeBotHp()
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero) return;
        
        try
        {
            // Sample current color at threshold position
            var currentColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, ViewModel.BabeBotHp.MonitorX, ViewModel.BabeBotHp.Y);
            ViewModel.BabeBotHp.CurrentColor = currentColor;
            
            // BabeBot logic: if current color != reference color then trigger
            bool colorChanged = !ColorsMatch(currentColor, ViewModel.BabeBotHp.ReferenceColor);
            
            // Update UI
            Dispatcher.BeginInvoke(() =>
            {
                BabeBotHpCurrentColor.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(currentColor.R, currentColor.G, currentColor.B));
                BabeBotHpCurrentText.Text = $"{currentColor.R},{currentColor.G},{currentColor.B}";
                
                if (colorChanged)
                {
                    ViewModel.BabeBotHp.Status = $"LOW {ViewModel.BabeBotHp.ThresholdPercentage}%";
                }
                else
                {
                    ViewModel.BabeBotHp.Status = $"OK {ViewModel.BabeBotHp.ThresholdPercentage}%";
                }
            });
            
            // Trigger logic - BabeBot style (simplified for testing)
            if (colorChanged)
            {
                var now = DateTime.UtcNow;
                if ((now - ViewModel.BabeBotHp.LastExecution).TotalMilliseconds >= 500) // 500ms cooldown
                {
                    ViewModel.BabeBotHp.LastExecution = now;
                    
                    Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot HP TRIGGER: Color changed! Clicking ({ViewModel.BabeBotHp.PotionX},{ViewModel.BabeBotHp.PotionY})");
                    
                    // Send mouse click to potion coordinates
                    PerformBackgroundClick(ViewModel.BabeBotHp.PotionX, ViewModel.BabeBotHp.PotionY, "BABEBOT_HP");
                    ViewModel.BabeBotHp.ExecutionCount++;
                    ViewModel.TriggerCount++;
                }
                else
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot HP on cooldown, {(500 - (now - ViewModel.BabeBotHp.LastExecution).TotalMilliseconds):F0}ms left");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] BabeBot HP check error: {ex.Message}");
        }
    }
    
    private void CheckBabeBotMp()
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero) return;
        
        try
        {
            // Sample current color at threshold position
            var currentColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, ViewModel.BabeBotMp.MonitorX, ViewModel.BabeBotMp.Y);
            ViewModel.BabeBotMp.CurrentColor = currentColor;
            
            // BabeBot logic: if current color != reference color then trigger
            bool colorChanged = !ColorsMatch(currentColor, ViewModel.BabeBotMp.ReferenceColor);
            
            // Update UI
            Dispatcher.BeginInvoke(() =>
            {
                BabeBotMpCurrentColor.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(currentColor.R, currentColor.G, currentColor.B));
                BabeBotMpCurrentText.Text = $"{currentColor.R},{currentColor.G},{currentColor.B}";
                
                if (colorChanged)
                {
                    ViewModel.BabeBotMp.Status = $"LOW {ViewModel.BabeBotMp.ThresholdPercentage}%";
                }
                else
                {
                    ViewModel.BabeBotMp.Status = $"OK {ViewModel.BabeBotMp.ThresholdPercentage}%";
                }
            });
            
            // Trigger logic - BabeBot style (simplified for testing)
            if (colorChanged)
            {
                var now = DateTime.UtcNow;
                if ((now - ViewModel.BabeBotMp.LastExecution).TotalMilliseconds >= 500) // 500ms cooldown
                {
                    ViewModel.BabeBotMp.LastExecution = now;
                    
                    Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot MP TRIGGER: Color changed! Clicking ({ViewModel.BabeBotMp.PotionX},{ViewModel.BabeBotMp.PotionY})");
                    
                    // Send mouse click to potion coordinates
                    PerformBackgroundClick(ViewModel.BabeBotMp.PotionX, ViewModel.BabeBotMp.PotionY, "BABEBOT_MP");
                    ViewModel.BabeBotMp.ExecutionCount++;
                    ViewModel.TriggerCount++;
                }
                else
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot MP on cooldown, {(500 - (now - ViewModel.BabeBotMp.LastExecution).TotalMilliseconds):F0}ms left");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] BabeBot MP check error: {ex.Message}");
        }
    }
    
    // Helper method for color comparison (simple tolerance-based)
    private bool ColorsMatch(System.Drawing.Color c1, System.Drawing.Color c2, int tolerance = 15)
    {
        return Math.Abs(c1.R - c2.R) <= tolerance &&
               Math.Abs(c1.G - c2.G) <= tolerance &&
               Math.Abs(c1.B - c2.B) <= tolerance;
    }
    
    private void SetupBabeBotUI()
    {
        // Setup initial UI values from ViewModel
        BabeBotHpStart.Text = ViewModel.BabeBotHp.StartX.ToString();
        BabeBotHpEnd.Text = ViewModel.BabeBotHp.EndX.ToString();
        BabeBotHpY.Text = ViewModel.BabeBotHp.Y.ToString();
        BabeBotHpPotionX.Text = ViewModel.BabeBotHp.PotionX.ToString();
        BabeBotHpPotionY.Text = ViewModel.BabeBotHp.PotionY.ToString();
        BabeBotHpThreshold.SelectedValue = ViewModel.BabeBotHp.ThresholdPercentage.ToString();
        
        BabeBotMpStart.Text = ViewModel.BabeBotMp.StartX.ToString();
        BabeBotMpEnd.Text = ViewModel.BabeBotMp.EndX.ToString();
        BabeBotMpY.Text = ViewModel.BabeBotMp.Y.ToString();
        BabeBotMpPotionX.Text = ViewModel.BabeBotMp.PotionX.ToString();
        BabeBotMpPotionY.Text = ViewModel.BabeBotMp.PotionY.ToString();
        BabeBotMpThreshold.SelectedValue = ViewModel.BabeBotMp.ThresholdPercentage.ToString();
        
        // Attach event handlers for real-time updates
        BabeBotHpStart.TextChanged += (s, e) => UpdateBabeBotHpFromUI();
        BabeBotHpEnd.TextChanged += (s, e) => UpdateBabeBotHpFromUI();
        BabeBotHpY.TextChanged += (s, e) => UpdateBabeBotHpFromUI();
        BabeBotHpPotionX.TextChanged += (s, e) => UpdateBabeBotHpFromUI();
        BabeBotHpPotionY.TextChanged += (s, e) => UpdateBabeBotHpFromUI();
        BabeBotHpThreshold.SelectionChanged += (s, e) => UpdateBabeBotHpFromUI();
        
        BabeBotMpStart.TextChanged += (s, e) => UpdateBabeBotMpFromUI();
        BabeBotMpEnd.TextChanged += (s, e) => UpdateBabeBotMpFromUI();
        BabeBotMpY.TextChanged += (s, e) => UpdateBabeBotMpFromUI();
        BabeBotMpPotionX.TextChanged += (s, e) => UpdateBabeBotMpFromUI();
        BabeBotMpPotionY.TextChanged += (s, e) => UpdateBabeBotMpFromUI();
        BabeBotMpThreshold.SelectionChanged += (s, e) => UpdateBabeBotMpFromUI();
        
        // Enable/Disable checkbox handlers
        BabeBotHpEnabled.Checked += (s, e) =>
        {
            ViewModel.BabeBotHp.Enabled = true;
            StartBabeBotMonitoring();
            Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot HP monitoring ENABLED");
        };
        
        BabeBotHpEnabled.Unchecked += (s, e) =>
        {
            ViewModel.BabeBotHp.Enabled = false;
            Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot HP monitoring DISABLED");
        };
        
        BabeBotMpEnabled.Checked += (s, e) =>
        {
            ViewModel.BabeBotMp.Enabled = true;
            StartBabeBotMonitoring();
            Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot MP monitoring ENABLED");
        };
        
        BabeBotMpEnabled.Unchecked += (s, e) =>
        {
            ViewModel.BabeBotMp.Enabled = false;
            Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot MP monitoring DISABLED");
        };
    }
    
    private void UpdateBabeBotHpFromUI()
    {
        try
        {
            if (int.TryParse(BabeBotHpStart.Text, out int startX))
                ViewModel.BabeBotHp.StartX = startX;
            if (int.TryParse(BabeBotHpEnd.Text, out int endX))
                ViewModel.BabeBotHp.EndX = endX;
            if (int.TryParse(BabeBotHpY.Text, out int y))
                ViewModel.BabeBotHp.Y = y;
            if (int.TryParse(BabeBotHpPotionX.Text, out int potionX))
                ViewModel.BabeBotHp.PotionX = potionX;
            if (int.TryParse(BabeBotHpPotionY.Text, out int potionY))
                ViewModel.BabeBotHp.PotionY = potionY;
            
            if (BabeBotHpThreshold.SelectedItem is ComboBoxItem item && int.TryParse(item.Content.ToString(), out int threshold))
                ViewModel.BabeBotHp.ThresholdPercentage = threshold;
        }
        catch { /* Ignore parsing errors */ }
    }
    
    private void UpdateBabeBotMpFromUI()
    {
        try
        {
            if (int.TryParse(BabeBotMpStart.Text, out int startX))
                ViewModel.BabeBotMp.StartX = startX;
            if (int.TryParse(BabeBotMpEnd.Text, out int endX))
                ViewModel.BabeBotMp.EndX = endX;
            if (int.TryParse(BabeBotMpY.Text, out int y))
                ViewModel.BabeBotMp.Y = y;
            if (int.TryParse(BabeBotMpPotionX.Text, out int potionX))
                ViewModel.BabeBotMp.PotionX = potionX;
            if (int.TryParse(BabeBotMpPotionY.Text, out int potionY))
                ViewModel.BabeBotMp.PotionY = potionY;
            
            if (BabeBotMpThreshold.SelectedItem is ComboBoxItem item && int.TryParse(item.Content.ToString(), out int threshold))
                ViewModel.BabeBotMp.ThresholdPercentage = threshold;
        }
        catch { /* Ignore parsing errors */ }
    }
    
    #endregion

    #region Party Heal System

    private void SetupMultiHpUI()
    {
        // Animation delay handler
        AnimationDelay.TextChanged += (s, e) =>
        {
            if (int.TryParse(AnimationDelay.Text, out int delay))
                ViewModel.AnimationDelay = delay;
        };

        // Check interval handler
        MultiHpCheckInterval.TextChanged += (s, e) =>
        {
            if (int.TryParse(MultiHpCheckInterval.Text, out int interval))
                ViewModel.MultiHpCheckInterval = interval;
        };

        // Enable/disable handler
        MultiHpEnabled.Checked += (s, e) =>
        {
            ViewModel.MultiHpEnabled = true;
            Console.WriteLine($"[{ViewModel.ClientName}] 🎭 Party Heal monitoring ENABLED");
        };

        MultiHpEnabled.Unchecked += (s, e) =>
        {
            ViewModel.MultiHpEnabled = false;
            Console.WriteLine($"[{ViewModel.ClientName}] 🎭 Party Heal monitoring DISABLED");
        };

        // Setup event handlers for all 8 clients
        SetupMultiHpClientHandlers();
    }

    private void SetupMultiHpClientHandlers()
    {
        // HP Client 1
        MultiHp1StartX.TextChanged += (s, e) => UpdateMultiHpClient(0, "StartX", MultiHp1StartX.Text);
        MultiHp1EndX.TextChanged += (s, e) => UpdateMultiHpClient(0, "EndX", MultiHp1EndX.Text);
        MultiHp1Y.TextChanged += (s, e) => UpdateMultiHpClient(0, "Y", MultiHp1Y.Text);
        MultiHp1ClickX.TextChanged += (s, e) => UpdateMultiHpClient(0, "ClickX", MultiHp1ClickX.Text);
        MultiHp1ClickY.TextChanged += (s, e) => UpdateMultiHpClient(0, "ClickY", MultiHp1ClickY.Text);
        MultiHp1Threshold.SelectionChanged += (s, e) => UpdateMultiHpClientCombo(0, "Threshold", MultiHp1Threshold);
        MultiHp1Key.SelectionChanged += (s, e) => UpdateMultiHpClientCombo(0, "Key", MultiHp1Key);
        MultiHp1Enabled.Checked += (s, e) => ViewModel.MultiHpClients[0].Enabled = true;
        MultiHp1Enabled.Unchecked += (s, e) => ViewModel.MultiHpClients[0].Enabled = false;

        // HP Client 2
        MultiHp2StartX.TextChanged += (s, e) => UpdateMultiHpClient(1, "StartX", MultiHp2StartX.Text);
        MultiHp2EndX.TextChanged += (s, e) => UpdateMultiHpClient(1, "EndX", MultiHp2EndX.Text);
        MultiHp2Y.TextChanged += (s, e) => UpdateMultiHpClient(1, "Y", MultiHp2Y.Text);
        MultiHp2ClickX.TextChanged += (s, e) => UpdateMultiHpClient(1, "ClickX", MultiHp2ClickX.Text);
        MultiHp2ClickY.TextChanged += (s, e) => UpdateMultiHpClient(1, "ClickY", MultiHp2ClickY.Text);
        MultiHp2Threshold.SelectionChanged += (s, e) => UpdateMultiHpClientCombo(1, "Threshold", MultiHp2Threshold);
        MultiHp2Key.SelectionChanged += (s, e) => UpdateMultiHpClientCombo(1, "Key", MultiHp2Key);
        MultiHp2Enabled.Checked += (s, e) => ViewModel.MultiHpClients[1].Enabled = true;
        MultiHp2Enabled.Unchecked += (s, e) => ViewModel.MultiHpClients[1].Enabled = false;

        // HP Client 3
        MultiHp3StartX.TextChanged += (s, e) => UpdateMultiHpClient(2, "StartX", MultiHp3StartX.Text);
        MultiHp3EndX.TextChanged += (s, e) => UpdateMultiHpClient(2, "EndX", MultiHp3EndX.Text);
        MultiHp3Y.TextChanged += (s, e) => UpdateMultiHpClient(2, "Y", MultiHp3Y.Text);
        MultiHp3ClickX.TextChanged += (s, e) => UpdateMultiHpClient(2, "ClickX", MultiHp3ClickX.Text);
        MultiHp3ClickY.TextChanged += (s, e) => UpdateMultiHpClient(2, "ClickY", MultiHp3ClickY.Text);
        MultiHp3Threshold.SelectionChanged += (s, e) => UpdateMultiHpClientCombo(2, "Threshold", MultiHp3Threshold);
        MultiHp3Key.SelectionChanged += (s, e) => UpdateMultiHpClientCombo(2, "Key", MultiHp3Key);
        MultiHp3Enabled.Checked += (s, e) => ViewModel.MultiHpClients[2].Enabled = true;
        MultiHp3Enabled.Unchecked += (s, e) => ViewModel.MultiHpClients[2].Enabled = false;

        // HP Client 4
        MultiHp4StartX.TextChanged += (s, e) => UpdateMultiHpClient(3, "StartX", MultiHp4StartX.Text);
        MultiHp4EndX.TextChanged += (s, e) => UpdateMultiHpClient(3, "EndX", MultiHp4EndX.Text);
        MultiHp4Y.TextChanged += (s, e) => UpdateMultiHpClient(3, "Y", MultiHp4Y.Text);
        MultiHp4ClickX.TextChanged += (s, e) => UpdateMultiHpClient(3, "ClickX", MultiHp4ClickX.Text);
        MultiHp4ClickY.TextChanged += (s, e) => UpdateMultiHpClient(3, "ClickY", MultiHp4ClickY.Text);
        MultiHp4Threshold.SelectionChanged += (s, e) => UpdateMultiHpClientCombo(3, "Threshold", MultiHp4Threshold);
        MultiHp4Key.SelectionChanged += (s, e) => UpdateMultiHpClientCombo(3, "Key", MultiHp4Key);
        MultiHp4Enabled.Checked += (s, e) => ViewModel.MultiHpClients[3].Enabled = true;
        MultiHp4Enabled.Unchecked += (s, e) => ViewModel.MultiHpClients[3].Enabled = false;

        // HP Client 5
        MultiHp5StartX.TextChanged += (s, e) => UpdateMultiHpClient(4, "StartX", MultiHp5StartX.Text);
        MultiHp5EndX.TextChanged += (s, e) => UpdateMultiHpClient(4, "EndX", MultiHp5EndX.Text);
        MultiHp5Y.TextChanged += (s, e) => UpdateMultiHpClient(4, "Y", MultiHp5Y.Text);
        MultiHp5ClickX.TextChanged += (s, e) => UpdateMultiHpClient(4, "ClickX", MultiHp5ClickX.Text);
        MultiHp5ClickY.TextChanged += (s, e) => UpdateMultiHpClient(4, "ClickY", MultiHp5ClickY.Text);
        MultiHp5Threshold.SelectionChanged += (s, e) => UpdateMultiHpClientCombo(4, "Threshold", MultiHp5Threshold);
        MultiHp5Key.SelectionChanged += (s, e) => UpdateMultiHpClientCombo(4, "Key", MultiHp5Key);
        MultiHp5Enabled.Checked += (s, e) => ViewModel.MultiHpClients[4].Enabled = true;
        MultiHp5Enabled.Unchecked += (s, e) => ViewModel.MultiHpClients[4].Enabled = false;

        // HP Client 6
        MultiHp6StartX.TextChanged += (s, e) => UpdateMultiHpClient(5, "StartX", MultiHp6StartX.Text);
        MultiHp6EndX.TextChanged += (s, e) => UpdateMultiHpClient(5, "EndX", MultiHp6EndX.Text);
        MultiHp6Y.TextChanged += (s, e) => UpdateMultiHpClient(5, "Y", MultiHp6Y.Text);
        MultiHp6ClickX.TextChanged += (s, e) => UpdateMultiHpClient(5, "ClickX", MultiHp6ClickX.Text);
        MultiHp6ClickY.TextChanged += (s, e) => UpdateMultiHpClient(5, "ClickY", MultiHp6ClickY.Text);
        MultiHp6Threshold.SelectionChanged += (s, e) => UpdateMultiHpClientCombo(5, "Threshold", MultiHp6Threshold);
        MultiHp6Key.SelectionChanged += (s, e) => UpdateMultiHpClientCombo(5, "Key", MultiHp6Key);
        MultiHp6Enabled.Checked += (s, e) => ViewModel.MultiHpClients[5].Enabled = true;
        MultiHp6Enabled.Unchecked += (s, e) => ViewModel.MultiHpClients[5].Enabled = false;

        // HP Client 7
        MultiHp7StartX.TextChanged += (s, e) => UpdateMultiHpClient(6, "StartX", MultiHp7StartX.Text);
        MultiHp7EndX.TextChanged += (s, e) => UpdateMultiHpClient(6, "EndX", MultiHp7EndX.Text);
        MultiHp7Y.TextChanged += (s, e) => UpdateMultiHpClient(6, "Y", MultiHp7Y.Text);
        MultiHp7ClickX.TextChanged += (s, e) => UpdateMultiHpClient(6, "ClickX", MultiHp7ClickX.Text);
        MultiHp7ClickY.TextChanged += (s, e) => UpdateMultiHpClient(6, "ClickY", MultiHp7ClickY.Text);
        MultiHp7Threshold.SelectionChanged += (s, e) => UpdateMultiHpClientCombo(6, "Threshold", MultiHp7Threshold);
        MultiHp7Key.SelectionChanged += (s, e) => UpdateMultiHpClientCombo(6, "Key", MultiHp7Key);
        MultiHp7Enabled.Checked += (s, e) => ViewModel.MultiHpClients[6].Enabled = true;
        MultiHp7Enabled.Unchecked += (s, e) => ViewModel.MultiHpClients[6].Enabled = false;

        // HP Client 8
        MultiHp8StartX.TextChanged += (s, e) => UpdateMultiHpClient(7, "StartX", MultiHp8StartX.Text);
        MultiHp8EndX.TextChanged += (s, e) => UpdateMultiHpClient(7, "EndX", MultiHp8EndX.Text);
        MultiHp8Y.TextChanged += (s, e) => UpdateMultiHpClient(7, "Y", MultiHp8Y.Text);
        MultiHp8ClickX.TextChanged += (s, e) => UpdateMultiHpClient(7, "ClickX", MultiHp8ClickX.Text);
        MultiHp8ClickY.TextChanged += (s, e) => UpdateMultiHpClient(7, "ClickY", MultiHp8ClickY.Text);
        MultiHp8Threshold.SelectionChanged += (s, e) => UpdateMultiHpClientCombo(7, "Threshold", MultiHp8Threshold);
        MultiHp8Key.SelectionChanged += (s, e) => UpdateMultiHpClientCombo(7, "Key", MultiHp8Key);
        MultiHp8Enabled.Checked += (s, e) => ViewModel.MultiHpClients[7].Enabled = true;
        MultiHp8Enabled.Unchecked += (s, e) => ViewModel.MultiHpClients[7].Enabled = false;
    }

    private void UpdateMultiHpClient(int clientIndex, string property, string value)
    {
        try
        {
            if (clientIndex < 0 || clientIndex >= ViewModel.MultiHpClients.Count) return;

            var client = ViewModel.MultiHpClients[clientIndex];
            
            switch (property)
            {
                case "StartX":
                    if (int.TryParse(value, out int startX)) client.StartX = startX;
                    break;
                case "EndX":
                    if (int.TryParse(value, out int endX)) client.EndX = endX;
                    break;
                case "Y":
                    if (int.TryParse(value, out int y)) client.Y = y;
                    break;
                case "ClickX":
                    if (int.TryParse(value, out int clickX)) client.ClickX = clickX;
                    break;
                case "ClickY":
                    if (int.TryParse(value, out int clickY)) client.ClickY = clickY;
                    break;
            }
        }
        catch { /* Ignore parsing errors */ }
    }

    private void UpdateMultiHpClientCombo(int clientIndex, string property, ComboBox combo)
    {
        try
        {
            if (clientIndex < 0 || clientIndex >= ViewModel.MultiHpClients.Count) return;
            if (combo.SelectedItem is not ComboBoxItem item) return;

            var client = ViewModel.MultiHpClients[clientIndex];
            
            switch (property)
            {
                case "Threshold":
                    if (int.TryParse(item.Content.ToString(), out int threshold))
                        client.ThresholdPercentage = threshold;
                    break;
                case "Key":
                    client.Key = item.Content.ToString() ?? "1";
                    break;
            }
        }
        catch { /* Ignore parsing errors */ }
    }

    private void StartMultiHpSystem()
    {
        if (_multiHpRunning)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] 🎭 Party Heal system already running");
            return;
        }

        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ No window selected for Party Heal monitoring");
            return;
        }

        _multiHpRunning = true;
        _currentMultiHpIndex = 0;

        _multiHpTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(ViewModel.MultiHpCheckInterval)
        };
        _multiHpTimer.Tick += MultiHpTimer_Tick;
        _multiHpTimer.Start();

        StartMultiHpButton.IsEnabled = false;
        StopMultiHpButton.IsEnabled = true;
        
        Console.WriteLine($"[{ViewModel.ClientName}] 🎭 Party Heal system STARTED with {ViewModel.MultiHpCheckInterval}ms interval");
    }

    private void StopMultiHpSystem()
    {
        _multiHpRunning = false;
        _multiHpTimer?.Stop();
        _multiHpTimer = null;

        StartMultiHpButton.IsEnabled = true;
        StopMultiHpButton.IsEnabled = false;

        // Reset all client statuses
        foreach (var client in ViewModel.MultiHpClients)
        {
            client.Status = "Waiting...";
            client.IsWaitingForAnimation = false;
        }

        UpdateMultiHpStatusDisplay();

        Console.WriteLine($"[{ViewModel.ClientName}] 🎭 Party Heal system STOPPED");
    }

    private void MultiHpTimer_Tick(object? sender, EventArgs e)
    {
        if (!_multiHpRunning || !ViewModel.MultiHpEnabled || ViewModel.TargetHwnd == IntPtr.Zero)
            return;

        ProcessMultiHpClients();
    }

    private void ProcessMultiHpClients()
    {
        try
        {
            // Find all enabled clients that need checking (not waiting for animation)
            var enabledClients = ViewModel.MultiHpClients
                .Select((client, index) => new { Client = client, Index = index })
                .Where(x => x.Client.Enabled && !x.Client.IsWaitingForAnimation)
                .ToList();

            if (!enabledClients.Any()) return;

            // Round-robin through enabled clients
            var currentClientInfo = enabledClients.Skip(_currentMultiHpIndex % enabledClients.Count).FirstOrDefault();
            if (currentClientInfo == null) return;

            var client = currentClientInfo.Client;
            var clientIndex = currentClientInfo.Index;

            // Update the index for next time
            _currentMultiHpIndex = (_currentMultiHpIndex + 1) % enabledClients.Count;

            // Check HP for this client
            bool hpLow = CheckClientHp(client, clientIndex);
            
            if (hpLow)
            {
                // HP is low, execute action
                ExecuteMultiHpAction(client, clientIndex);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ Error in Party Heal processing: {ex.Message}");
        }
    }

    private bool CheckClientHp(MultiHpClientViewModel client, int clientIndex)
    {
        try
        {
            if (!client.ReferenceColors.Any())
            {
                client.Status = "Need Calibration";
                UpdateClientStatusDisplay(clientIndex);
                return false;
            }

            // Capture the target window first
            _fastSampler?.CaptureWindow(ViewModel.TargetHwnd);
            
            // Get current color at monitor position
            var monitorX = client.MonitorX;
            var currentColor = _fastSampler?.GetColorAt(monitorX, client.Y);
            
            if (currentColor == null)
            {
                client.Status = "Read Error";
                UpdateClientStatusDisplay(clientIndex);
                return false;
            }

            client.CurrentColor = currentColor.Value;

            // Calculate HP percentage using BabeBot method
            var percentage = CalculateHpPercentage(client, currentColor.Value);
            client.Percentage = percentage;
            
            // Update status
            client.Status = $"{percentage:F0}%";
            UpdateClientStatusDisplay(clientIndex);

            // Check if HP is below threshold
            bool isLow = percentage <= client.ThresholdPercentage;
            
            if (isLow && !client.IsTriggered)
            {
                client.IsTriggered = true;
                client.Status = "HP LOW!";
                UpdateClientStatusDisplay(clientIndex);
                return true;
            }
            else if (percentage > client.ThresholdPercentage + 10) // Add hysteresis
            {
                client.IsTriggered = false;
            }

            return false;
        }
        catch (Exception ex)
        {
            client.Status = $"Error: {ex.Message}";
            UpdateClientStatusDisplay(clientIndex);
            return false;
        }
    }

    private double CalculateHpPercentage(MultiHpClientViewModel client, System.Drawing.Color currentColor)
    {
        // Use BabeBot's percentage calculation method
        // Find the closest reference color match
        double bestPercentage = 100.0;
        double bestDistance = double.MaxValue;

        foreach (var reference in client.ReferenceColors)
        {
            var distance = Math.Sqrt(
                Math.Pow(currentColor.R - reference.Value.R, 2) +
                Math.Pow(currentColor.G - reference.Value.G, 2) +
                Math.Pow(currentColor.B - reference.Value.B, 2));

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPercentage = reference.Key;
            }
        }

        return bestPercentage;
    }

    private async void ExecuteMultiHpAction(MultiHpClientViewModel client, int clientIndex)
    {
        try
        {
            // Check cooldown
            var now = DateTime.Now;
            if ((now - client.LastExecution).TotalMilliseconds < ViewModel.AnimationDelay)
            {
                client.Status = "Cooling down...";
                UpdateClientStatusDisplay(clientIndex);
                return;
            }

            client.Status = "Healing...";
            client.IsWaitingForAnimation = true;
            UpdateClientStatusDisplay(clientIndex);

            // Click the coordinate
            await Task.Run(() =>
            {
                try
                {
                    // Send click
                    User32.SetCursorPos(client.ClickX, client.ClickY);
                    User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN, client.ClickX, client.ClickY, 0, IntPtr.Zero);
                    User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTUP, client.ClickX, client.ClickY, 0, IntPtr.Zero);
                    Thread.Sleep(50);

                    // Send key press
                    SendKeyPress(client.Key);
                    
                    Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ Party Member {client.ClientIndex}: Clicked ({client.ClickX},{client.ClickY}) + Key '{client.Key}'");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ Click/Key error for Party Member {client.ClientIndex}: {ex.Message}");
                }
            });

            client.LastExecution = now;
            client.ExecutionCount++;

            // Wait for animation delay
            await Task.Delay(ViewModel.AnimationDelay);

            client.IsWaitingForAnimation = false;
            client.Status = "Ready";
            UpdateClientStatusDisplay(clientIndex);

            Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ Party Member {client.ClientIndex} action completed, resuming monitoring");
        }
        catch (Exception ex)
        {
            client.Status = "Action Error";
            client.IsWaitingForAnimation = false;
            UpdateClientStatusDisplay(clientIndex);
            Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ Error executing action for Party Member {client.ClientIndex}: {ex.Message}");
        }
    }

    private void SendKeyPress(string key)
    {
        // Convert key to virtual key code
        byte vkCode = key switch
        {
            "0" => 0x30, "1" => 0x31, "2" => 0x32, "3" => 0x33, "4" => 0x34,
            "5" => 0x35, "6" => 0x36, "7" => 0x37, "8" => 0x38, "9" => 0x39,
            _ => 0x31 // Default to '1'
        };

        // Send key down and up
        User32.keybd_event(vkCode, 0, 0, IntPtr.Zero); // Key down
        Thread.Sleep(50);
        User32.keybd_event(vkCode, 0, User32.KEYEVENTF.KEYEVENTF_KEYUP, IntPtr.Zero); // Key up
    }

    private void CalibrateMultiHpClient(int clientIndex)
    {
        if (clientIndex < 0 || clientIndex >= ViewModel.MultiHpClients.Count) return;
        
        var client = ViewModel.MultiHpClients[clientIndex];
        
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ No window selected for Party Member {client.ClientIndex} calibration");
            return;
        }

        try
        {
            client.ReferenceColors.Clear();
            client.Status = "Calibrating...";
            UpdateClientStatusDisplay(clientIndex);

            // Capture the target window first
            _fastSampler?.CaptureWindow(ViewModel.TargetHwnd);
            
            // Calibrate HP bar colors at different percentages (5% to 95%)
            for (int percentage = 5; percentage <= 95; percentage += 5)
            {
                int x = client.CalculateXForPercentage(percentage);
                var color = _fastSampler?.GetColorAt(x, client.Y);
                
                if (color.HasValue)
                {
                    client.ReferenceColors[percentage] = color.Value;
                }
            }

            client.Status = $"Calibrated ({client.ReferenceColors.Count} points)";
            UpdateClientStatusDisplay(clientIndex);
            
            Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ Party Member {client.ClientIndex} calibrated with {client.ReferenceColors.Count} reference points");
        }
        catch (Exception ex)
        {
            client.Status = "Calibration Error";
            UpdateClientStatusDisplay(clientIndex);
            Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ Calibration error for Party Member {client.ClientIndex}: {ex.Message}");
        }
    }

    private void PickMultiHpClientClick(int clientIndex)
    {
        if (clientIndex < 0 || clientIndex >= ViewModel.MultiHpClients.Count) return;
        
        var client = ViewModel.MultiHpClients[clientIndex];
        
        _coordinatePicker = new CoordinatePicker(ViewModel.TargetHwnd, ViewModel.ClientName);
        _coordinatePicker.CoordinatePicked += (x, y) =>
        {
            client.ClickX = x;
            client.ClickY = y;
            UpdateMultiHpClientTextBox(clientIndex, "ClickX", x.ToString());
            UpdateMultiHpClientTextBox(clientIndex, "ClickY", y.ToString());
            Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ Party Member {client.ClientIndex} click position set to ({x}, {y})");
        };
        _coordinatePicker.Show();
    }

    private void UpdateMultiHpClientTextBox(int clientIndex, string property, string value)
    {
        switch (clientIndex)
        {
            case 0:
                if (property == "ClickX") MultiHp1ClickX.Text = value;
                else if (property == "ClickY") MultiHp1ClickY.Text = value;
                break;
            case 1:
                if (property == "ClickX") MultiHp2ClickX.Text = value;
                else if (property == "ClickY") MultiHp2ClickY.Text = value;
                break;
            case 2:
                if (property == "ClickX") MultiHp3ClickX.Text = value;
                else if (property == "ClickY") MultiHp3ClickY.Text = value;
                break;
            case 3:
                if (property == "ClickX") MultiHp4ClickX.Text = value;
                else if (property == "ClickY") MultiHp4ClickY.Text = value;
                break;
            case 4:
                if (property == "ClickX") MultiHp5ClickX.Text = value;
                else if (property == "ClickY") MultiHp5ClickY.Text = value;
                break;
            case 5:
                if (property == "ClickX") MultiHp6ClickX.Text = value;
                else if (property == "ClickY") MultiHp6ClickY.Text = value;
                break;
            case 6:
                if (property == "ClickX") MultiHp7ClickX.Text = value;
                else if (property == "ClickY") MultiHp7ClickY.Text = value;
                break;
            case 7:
                if (property == "ClickX") MultiHp8ClickX.Text = value;
                else if (property == "ClickY") MultiHp8ClickY.Text = value;
                break;
        }
    }

    private void UpdateClientStatusDisplay(int clientIndex)
    {
        Dispatcher.BeginInvoke(() =>
        {
            UpdateMultiHpStatusDisplay();
        });
    }

    private void UpdateMultiHpStatusDisplay()
    {
        try
        {
            for (int i = 0; i < ViewModel.MultiHpClients.Count; i++)
            {
                var client = ViewModel.MultiHpClients[i];
                UpdateSingleClientDisplay(i, client);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ Error updating Party Heal display: {ex.Message}");
        }
    }

    private void UpdateSingleClientDisplay(int clientIndex, MultiHpClientViewModel client)
    {
        var statusText = GetStatusTextBlock(clientIndex);
        var percentageText = GetPercentageTextBlock(clientIndex);
        var colorBar = GetColorBarRectangle(clientIndex);

        if (statusText != null)
            statusText.Text = client.Status;

        if (percentageText != null)
            percentageText.Text = $"{client.Percentage:F0}%";

        if (colorBar != null)
        {
            colorBar.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(
                client.CurrentColor.R,
                client.CurrentColor.G,
                client.CurrentColor.B));
        }
    }

    private TextBlock? GetStatusTextBlock(int clientIndex) => clientIndex switch
    {
        0 => MultiHp1Status,
        1 => MultiHp2Status,
        2 => MultiHp3Status,
        3 => MultiHp4Status,
        4 => MultiHp5Status,
        5 => MultiHp6Status,
        6 => MultiHp7Status,
        7 => MultiHp8Status,
        _ => null
    };

    private TextBlock? GetPercentageTextBlock(int clientIndex) => clientIndex switch
    {
        0 => MultiHp1Percentage,
        1 => MultiHp2Percentage,
        2 => MultiHp3Percentage,
        3 => MultiHp4Percentage,
        4 => MultiHp5Percentage,
        5 => MultiHp6Percentage,
        6 => MultiHp7Percentage,
        7 => MultiHp8Percentage,
        _ => null
    };

    private System.Windows.Shapes.Rectangle? GetColorBarRectangle(int clientIndex) => clientIndex switch
    {
        0 => MultiHp1ColorBar,
        1 => MultiHp2ColorBar,
        2 => MultiHp3ColorBar,
        3 => MultiHp4ColorBar,
        4 => MultiHp5ColorBar,
        5 => MultiHp6ColorBar,
        6 => MultiHp7ColorBar,
        7 => MultiHp8ColorBar,
        _ => null
    };

    #endregion

    #region Party Heal Event Handlers

    private void StartMultiHp_Click(object sender, RoutedEventArgs e)
    {
        StartMultiHpSystem();
    }

    private void StopMultiHp_Click(object sender, RoutedEventArgs e)
    {
        StopMultiHpSystem();
    }

    // Calibrate methods for each HP client
    private void CalibrateMultiHp1_Click(object sender, RoutedEventArgs e) => CalibrateMultiHpClient(0);
    private void CalibrateMultiHp2_Click(object sender, RoutedEventArgs e) => CalibrateMultiHpClient(1);
    private void CalibrateMultiHp3_Click(object sender, RoutedEventArgs e) => CalibrateMultiHpClient(2);
    private void CalibrateMultiHp4_Click(object sender, RoutedEventArgs e) => CalibrateMultiHpClient(3);
    private void CalibrateMultiHp5_Click(object sender, RoutedEventArgs e) => CalibrateMultiHpClient(4);
    private void CalibrateMultiHp6_Click(object sender, RoutedEventArgs e) => CalibrateMultiHpClient(5);
    private void CalibrateMultiHp7_Click(object sender, RoutedEventArgs e) => CalibrateMultiHpClient(6);
    private void CalibrateMultiHp8_Click(object sender, RoutedEventArgs e) => CalibrateMultiHpClient(7);

    // Pick click position methods for each HP client
    private void PickMultiHp1Click_Click(object sender, RoutedEventArgs e) => PickMultiHpClientClick(0);
    private void PickMultiHp2Click_Click(object sender, RoutedEventArgs e) => PickMultiHpClientClick(1);
    private void PickMultiHp3Click_Click(object sender, RoutedEventArgs e) => PickMultiHpClientClick(2);
    private void PickMultiHp4Click_Click(object sender, RoutedEventArgs e) => PickMultiHpClientClick(3);
    private void PickMultiHp5Click_Click(object sender, RoutedEventArgs e) => PickMultiHpClientClick(4);
    private void PickMultiHp6Click_Click(object sender, RoutedEventArgs e) => PickMultiHpClientClick(5);
    private void PickMultiHp7Click_Click(object sender, RoutedEventArgs e) => PickMultiHpClientClick(6);
    private void PickMultiHp8Click_Click(object sender, RoutedEventArgs e) => PickMultiHpClientClick(7);

    #endregion


}