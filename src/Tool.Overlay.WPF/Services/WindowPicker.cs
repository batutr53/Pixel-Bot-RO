using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Vanara.PInvoke;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace PixelAutomation.Tool.Overlay.WPF.Services;

public class WindowPicker : Window
{
    private IntPtr _selectedHwnd = IntPtr.Zero;
    private Canvas _overlayCanvas;
    private TextBlock _infoText;
    private Rectangle _highlightRect;

    public WindowPicker()
    {
        Title = "Window Picker";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 0, 0, 0));
        Topmost = true;
        ShowInTaskbar = false;
        Cursor = System.Windows.Input.Cursors.Cross;
        Focusable = true;
        
        // Set bounds to cover all monitors
        SetMultiMonitorBounds();

        _overlayCanvas = new Canvas
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0))
        };

        _infoText = new TextBlock
        {
            Text = "🎯 WINDOW PICKER\n\nMove mouse over a window\nClick to select • ESC to cancel",
            Foreground = Brushes.White,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 0, 0, 0)),
            Padding = new Thickness(10),
            FontSize = 14,
            Visibility = Visibility.Visible
        };
        Canvas.SetLeft(_infoText, 50);
        Canvas.SetTop(_infoText, 50);
        _overlayCanvas.Children.Add(_infoText);

        _highlightRect = new Rectangle
        {
            Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 0)),
            StrokeThickness = 4,
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 255, 165, 0)),
            Visibility = Visibility.Collapsed,
            StrokeDashArray = new DoubleCollection { 10, 5 }
        };
        _overlayCanvas.Children.Add(_highlightRect);

        Content = _overlayCanvas;

        MouseMove += OnMouseMove;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseDown += OnMouseDown;
        KeyDown += OnKeyDown;
        
        Focus();
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
        Console.WriteLine($"Multi-monitor bounds: ({leftmost},{topmost}) - ({rightmost},{bottommost}) Size: {Width}x{Height}");
    }

    public IntPtr PickWindow()
    {
        ShowDialog();
        return _selectedHwnd;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        // Get cursor position directly from system
        User32.GetCursorPos(out var cursorPoint);
        
        // Get all windows at cursor position using EnumChildWindows
        var hwnd = GetWindowUnderCursor(cursorPoint);
        
        // Always update debug info
        _infoText.Text = $"🎯 WINDOW PICKER\n\nCursor: ({cursorPoint.x}, {cursorPoint.y})\nHWND: 0x{hwnd:X8}\n\nMove mouse over a window to highlight it\nClick to select • ESC to cancel";
        
        if (hwnd != IntPtr.Zero)
        {
            // For gameloop with multiple child windows, we want to detect which specific child was clicked
            var originalHwnd = hwnd;
            var rootHwnd = GetTopLevelWindow(hwnd);
            
            // Skip our own window
            var ourHwnd = (IntPtr)new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (rootHwnd == ourHwnd || originalHwnd == ourHwnd)
                return;
            
            // Check if this is a gameloop child window by looking at process and class
            var processName = GetProcessName(originalHwnd);
            var className = GetWindowClassName(originalHwnd);
            var title = GetWindowTitle(originalHwnd);
            
            // For MuMu Player, we need to get the specific instance window
            IntPtr targetHwnd = originalHwnd;
            
            if (processName.Contains("MuMu") || processName.Contains("Nemu"))
            {
                // MuMu Player: Find the actual game instance window
                var mumuInstanceHwnd = FindMuMuInstanceWindow(cursorPoint);
                if (mumuInstanceHwnd != IntPtr.Zero)
                {
                    targetHwnd = mumuInstanceHwnd;
                    Console.WriteLine($"Found MuMu instance window: HWND=0x{mumuInstanceHwnd:X8} at cursor position");
                }
                else
                {
                    targetHwnd = originalHwnd;
                    Console.WriteLine($"Using original MuMu window: HWND=0x{originalHwnd:X8} Process={processName}");
                }
            }
            else if (processName.Contains("GameLoop") || className.Contains("Chrome") || title.Contains("RO"))
            {
                Console.WriteLine($"Detected emulator child window: HWND=0x{originalHwnd:X8} Process={processName} Class={className}");
                targetHwnd = originalHwnd; // Use the specific child window
            }
            else
            {
                targetHwnd = rootHwnd; // Fallback to root window
            }
            
            // Get window rectangle
            if (User32.GetWindowRect(targetHwnd, out var rect))
            {
                // Convert to WPF coordinates
                var topLeft = PointFromScreen(new System.Windows.Point(rect.left, rect.top));
                var bottomRight = PointFromScreen(new System.Windows.Point(rect.right, rect.bottom));
                
                // Update highlight
                Canvas.SetLeft(_highlightRect, topLeft.X);
                Canvas.SetTop(_highlightRect, topLeft.Y);
                _highlightRect.Width = Math.Max(0, bottomRight.X - topLeft.X);
                _highlightRect.Height = Math.Max(0, bottomRight.Y - topLeft.Y);
                _highlightRect.Visibility = Visibility.Visible;
                
                // Update info  
                _infoText.Text = $"🎯 WINDOW PICKER\n\nWindow: {title}\nProcess: {processName}\nClass: {className}\nChild HWND: 0x{originalHwnd:X8}\nTarget HWND: 0x{targetHwnd:X8}\n\n✅ Click to SELECT • ESC to CANCEL";
            }
        }
        else
        {
            _highlightRect.Visibility = Visibility.Collapsed;
            _infoText.Text = "🎯 WINDOW PICKER\n\nMove mouse over a window to highlight it\nClick to select • ESC to cancel";
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            // Get cursor position directly from system
            User32.GetCursorPos(out var cursorPoint);
            
            var hwnd = GetWindowUnderCursor(cursorPoint);
            
            if (hwnd != IntPtr.Zero)
            {
                var originalHwnd = hwnd;
                var rootHwnd = GetTopLevelWindow(hwnd);
                
                // Skip our own window
                var ourHwnd = (IntPtr)new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (rootHwnd == ourHwnd)
                {
                    return;
                }
                
                // Apply window detection for MuMu Player instances
                var processName = GetProcessName(originalHwnd);
                var className = GetWindowClassName(originalHwnd);
                var title = GetWindowTitle(originalHwnd);
                
                if (processName.Contains("MuMu") || processName.Contains("Nemu"))
                {
                    // Find specific MuMu instance
                    var mumuInstanceHwnd = FindMuMuInstanceWindow(cursorPoint);
                    if (mumuInstanceHwnd != IntPtr.Zero)
                    {
                        _selectedHwnd = mumuInstanceHwnd;
                        Console.WriteLine($"Selected MuMu instance window: HWND=0x{mumuInstanceHwnd:X8}");
                    }
                    else
                    {
                        _selectedHwnd = originalHwnd;
                        Console.WriteLine($"Selected MuMu original window: HWND=0x{originalHwnd:X8} Process={processName}");
                    }
                }
                else if (processName.Contains("GameLoop") || className.Contains("Chrome") || title.Contains("RO"))
                {
                    _selectedHwnd = originalHwnd; // Use the specific child window
                    Console.WriteLine($"Selected emulator child window: HWND=0x{originalHwnd:X8} Process={processName}");
                }
                else
                {
                    _selectedHwnd = rootHwnd; // Fallback to root window
                    Console.WriteLine($"Selected root window: HWND=0x{rootHwnd:X8}");
                }
                
                Close();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WindowPicker error: {ex.Message}");
        }
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        OnMouseLeftButtonDown(sender, e as MouseButtonEventArgs);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _selectedHwnd = IntPtr.Zero;
            Close();
        }
    }

    private IntPtr GetTopLevelWindow(IntPtr hwnd)
    {
        IntPtr rootHwnd = hwnd;
        
        // Get the actual top-level window
        while (true)
        {
            var parent = (IntPtr)User32.GetParent((Vanara.PInvoke.HWND)rootHwnd);
            if (parent == IntPtr.Zero)
                break;
            rootHwnd = parent;
        }
        
        return rootHwnd;
    }

    private string GetWindowTitle(IntPtr hwnd)
    {
        try
        {
            var length = User32.GetWindowTextLength(hwnd);
            if (length == 0) return "(No Title)";
            
            var sb = new StringBuilder(length + 1);
            User32.GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }
        catch
        {
            return "(Error getting title)";
        }
    }

    private string GetWindowClassName(IntPtr hwnd)
    {
        try
        {
            var sb = new StringBuilder(256);
            User32.GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }
        catch
        {
            return "(Unknown)";
        }
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
            return "(Unknown)";
        }
    }
    
    private IntPtr GetWindowUnderCursor(Vanara.PInvoke.POINT cursorPoint)
    {
        var ourHwnd = (IntPtr)new System.Windows.Interop.WindowInteropHelper(this).Handle;
        
        // Get all windows at cursor position, exclude our overlay
        var windows = new List<IntPtr>();
        
        // Start with desktop and enumerate all children
        EnumWindows((hwnd, lParam) =>
        {
            if (hwnd != ourHwnd && User32.IsWindowVisible(hwnd))
            {
                User32.GetWindowRect(hwnd, out var rect);
                if (cursorPoint.x >= rect.left && cursorPoint.x < rect.right &&
                    cursorPoint.y >= rect.top && cursorPoint.y < rect.bottom)
                {
                    windows.Add(hwnd);
                }
            }
            return true;
        }, IntPtr.Zero);
        
        // Return the topmost window (last in Z-order)
        return windows.LastOrDefault();
    }
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
    
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    
    private IntPtr FindMuMuInstanceWindow(Vanara.PInvoke.POINT cursorPoint)
    {
        var candidateWindows = new List<(IntPtr hwnd, string title, string className)>();
        
        // Enumerate all top-level windows
        EnumWindows((hwnd, lParam) =>
        {
            var processName = GetProcessName(hwnd);
            if (processName.Contains("MuMu") || processName.Contains("Nemu"))
            {
                var title = GetWindowTitle(hwnd);
                var className = GetWindowClassName(hwnd);
                
                // Check if window contains cursor position
                if (User32.GetWindowRect(hwnd, out var rect))
                {
                    if (cursorPoint.x >= rect.left && cursorPoint.x < rect.right &&
                        cursorPoint.y >= rect.top && cursorPoint.y < rect.bottom)
                    {
                        candidateWindows.Add((hwnd, title, className));
                        Console.WriteLine($"MuMu candidate: HWND=0x{hwnd:X8} Title='{title}' Class='{className}'");
                    }
                }
            }
            return true;
        }, IntPtr.Zero);
        
        // Prioritize windows with specific titles/classes
        foreach (var (hwnd, title, className) in candidateWindows)
        {
            // Look for RO, game, or instance-specific titles
            if (title.Contains("RO") || title.Contains("Ragnarok") || 
                title.Contains("MuMu") && (title.Contains("-") || char.IsDigit(title.Last())))
            {
                Console.WriteLine($"Selected MuMu instance: HWND=0x{hwnd:X8} Title='{title}'");
                return hwnd;
            }
        }
        
        // If no specific match, return the first candidate with visible content
        return candidateWindows.FirstOrDefault().hwnd;
    }
}