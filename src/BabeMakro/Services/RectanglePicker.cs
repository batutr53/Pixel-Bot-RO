using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Vanara.PInvoke;

namespace PixelAutomation.Tool.Overlay.WPF.Services;

public class RectanglePicker : Window
{
    private IntPtr _targetHwnd;
    private bool _isDrawing = false;
    private System.Windows.Point _startPoint;
    private Canvas _overlayCanvas;
    private TextBlock _infoText;
    private Rectangle? _selectionRect;

    public event Action<int, int, int, int>? RectanglePicked;

    public RectanglePicker(IntPtr targetHwnd, string title)
    {
        _targetHwnd = targetHwnd;
        
        Title = title;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 0, 0, 0));
        Topmost = true;
        ShowInTaskbar = false;
        Cursor = System.Windows.Input.Cursors.Cross;
        
        // Set bounds to cover all monitors
        SetMultiMonitorBounds();

        _overlayCanvas = new Canvas
        {
            Background = Brushes.Transparent
        };

        _infoText = new TextBlock
        {
            Text = "",
            Visibility = Visibility.Hidden
        };

        Content = _overlayCanvas;

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        KeyDown += OnKeyDown;
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

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDrawing = true;
        _startPoint = e.GetPosition(_overlayCanvas);
        
        _selectionRect = new Rectangle
        {
            Stroke = Brushes.Red,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 255, 0, 0))
        };
        
        _overlayCanvas.Children.Add(_selectionRect);
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDrawing && _selectionRect != null)
        {
            var currentPoint = e.GetPosition(_overlayCanvas);
            
            var left = Math.Min(_startPoint.X, currentPoint.X);
            var top = Math.Min(_startPoint.Y, currentPoint.Y);
            var width = Math.Abs(currentPoint.X - _startPoint.X);
            var height = Math.Abs(currentPoint.Y - _startPoint.Y);
            
            Canvas.SetLeft(_selectionRect, left);
            Canvas.SetTop(_selectionRect, top);
            _selectionRect.Width = width;
            _selectionRect.Height = height;
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDrawing && _selectionRect != null)
        {
            _isDrawing = false;
            ReleaseMouseCapture();
            
            var endPoint = e.GetPosition(_overlayCanvas);
            
            var left = (int)Math.Min(_startPoint.X, endPoint.X);
            var top = (int)Math.Min(_startPoint.Y, endPoint.Y);
            var width = (int)Math.Abs(endPoint.X - _startPoint.X);
            var height = (int)Math.Abs(endPoint.Y - _startPoint.Y);
            
            // Convert screen coordinates to client coordinates with detailed debug
            var screenTopLeft = PointToScreen(new System.Windows.Point(left, top));
            var topLeft = ConvertScreenToClient((int)screenTopLeft.X, (int)screenTopLeft.Y);
            
            RectanglePicked?.Invoke(topLeft.X, topLeft.Y, width, height);
            Close();
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
    
    private System.Drawing.Point ConvertScreenToClient(int screenX, int screenY)
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
        
        // Gameloop internal offset compensation for Y axis
        // Based on debug data: Delta (4,23) means ScreenToClient is 23px off on Y
        int compensatedY = point.y - 23; // Move up by 23 pixels  
        int compensatedX = point.x - 4;  // Move left by 4 pixels
        
        // Always use ScreenToClient result with gameloop offset compensation
        return new System.Drawing.Point(compensatedX, compensatedY);
    }
}