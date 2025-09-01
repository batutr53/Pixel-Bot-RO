using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Vanara.PInvoke;
using PixelAutomation.Core.Services;

namespace PixelAutomation.Tool.Overlay.WPF.Services;

public class CoordinatePicker : Window
{
    private IntPtr _targetHwnd;
    private bool _isPickingCoordinate = false;
    private Canvas _overlayCanvas;
    private TextBlock _infoText;
    private Rectangle _crosshair;

    public event Action<int, int>? CoordinatePicked;

    public CoordinatePicker(IntPtr targetHwnd, string title)
    {
        _targetHwnd = targetHwnd;
        
        Title = title;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 0, 0, 0));
        Topmost = true;
        ShowInTaskbar = false;
        Cursor = Cursors.Cross;
        
        // Set bounds to cover all monitors
        SetMultiMonitorBounds();

        _overlayCanvas = new Canvas
        {
            Background = Brushes.Transparent
        };

        _infoText = new TextBlock
        {
            Text = "",
            Foreground = Brushes.White,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 0, 0, 0)),
            Padding = new Thickness(10),
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Visibility = Visibility.Visible
        };
        Canvas.SetLeft(_infoText, 20);
        Canvas.SetTop(_infoText, 20);

        _crosshair = new Rectangle
        {
            Width = 20,
            Height = 20,
            Stroke = Brushes.Red,
            StrokeThickness = 2,
            Fill = Brushes.Transparent,
            Visibility = Visibility.Collapsed
        };
        _overlayCanvas.Children.Add(_crosshair);
        _overlayCanvas.Children.Add(_infoText);

        Content = _overlayCanvas;

        MouseMove += OnMouseMove;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        KeyDown += OnKeyDown;
        
        _isPickingCoordinate = true;
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
        
        // Multi-monitor bounds set
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPickingCoordinate) return;

        var position = e.GetPosition(_overlayCanvas);
        
        // Convert screen coordinates to client coordinates
        var screenPoint = PointToScreen(position);
        var clientPoint = ScreenToClientCoordinate((int)screenPoint.X, (int)screenPoint.Y);

        // Update crosshair position
        Canvas.SetLeft(_crosshair, position.X - 10);
        Canvas.SetTop(_crosshair, position.Y - 10);
        _crosshair.Visibility = Visibility.Visible;

        // Get color at this position for HP/MP probe feedback
        var currentColor = ColorSampler.GetColorAt(_targetHwnd, clientPoint.X, clientPoint.Y);
        
        // Position info text near mouse cursor but avoid going off screen
        double infoX = Math.Min(position.X + 20, Width - 300);
        double infoY = Math.Max(position.Y - 100, 20);
        Canvas.SetLeft(_infoText, infoX);
        Canvas.SetTop(_infoText, infoY);
        
        // Update info text with coordinate and color information
        _infoText.Text = $"{Title}\nScreen: ({(int)screenPoint.X}, {(int)screenPoint.Y})\nClient: ({clientPoint.X}, {clientPoint.Y})\n🎨 RGB({currentColor.R},{currentColor.G},{currentColor.B})\n\nClick to select • ESC to cancel";
        _infoText.Visibility = Visibility.Visible;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isPickingCoordinate) return;

        var position = e.GetPosition(_overlayCanvas);
        var screenPoint = PointToScreen(position);
        var clientPoint = ScreenToClientCoordinate((int)screenPoint.X, (int)screenPoint.Y);

        CoordinatePicked?.Invoke(clientPoint.X, clientPoint.Y);
        Close();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private System.Drawing.Point ScreenToClientCoordinate(int screenX, int screenY)
    {
        // Get window rect to understand the offset
        User32.GetWindowRect(_targetHwnd, out var windowRect);
        User32.GetClientRect(_targetHwnd, out var clientRect);
        
        // Calculate border/title bar offsets
        int borderWidth = ((windowRect.right - windowRect.left) - (clientRect.right - clientRect.left)) / 2;
        int titleHeight = ((windowRect.bottom - windowRect.top) - (clientRect.bottom - clientRect.top)) - borderWidth;
        
        // Method 1: Use ScreenToClient (standard way)
        var point = new Vanara.PInvoke.POINT { x = screenX, y = screenY };
        User32.ScreenToClient(_targetHwnd, ref point);
        
        // Method 2: Manual calculation for verification
        int manualX = screenX - windowRect.left - borderWidth;
        int manualY = screenY - windowRect.top - titleHeight;
        
        // Dynamic offset compensation based on process name
        var processName = GetProcessName(_targetHwnd);
        int offsetX = 0;
        int offsetY = 0;
        
        if (processName.Contains("GameLoop"))
        {
            // GameLoop specific offset
            offsetX = 4;
            offsetY = 23;
        }
        else if (processName.Contains("NemuPlayer") || processName.Contains("MuMuPlayer") || processName.Contains("MuMuNxDevice"))
        {
            // MuMu Player offset - adjusted for coordinate accuracy
            offsetX = 8;
            offsetY = 50; // Increased Y offset since clicking below selected point
        }
        
        int compensatedX = point.x - offsetX;
        int compensatedY = point.y - offsetY;
        
        return new System.Drawing.Point(compensatedX, compensatedY);
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
}