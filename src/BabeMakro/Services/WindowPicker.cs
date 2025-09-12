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
        
        // Debug info with screen details
        Console.WriteLine($"Multi-monitor setup:");
        Console.WriteLine($"  Virtual screen bounds: ({leftmost},{topmost}) - ({rightmost},{bottommost})");
        Console.WriteLine($"  Overlay window size: {Width}x{Height}");
        Console.WriteLine($"  Overlay position: ({Left},{Top})");
        
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            Console.WriteLine($"  Screen: {screen.Bounds} Primary: {screen.Primary}");
        }
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
                // Convert screen coordinates to overlay coordinates for multi-monitor support
                // Handle negative coordinates for left/secondary monitors
                var overlayLeftPos = Left;
                var overlayTopPos = Top;
                var highlightLeft = rect.left - overlayLeftPos;
                var highlightTop = rect.top - overlayTopPos;
                var highlightWidth = rect.right - rect.left;
                var highlightHeight = rect.bottom - rect.top;
                
                // Debug multi-monitor coordinate conversion with more detail
                var screenInfo = System.Windows.Forms.Screen.AllScreens.Select(s => $"[{s.Bounds}]").ToArray();
                Console.WriteLine($"Multi-monitor debug:");
                Console.WriteLine($"  Screens: {string.Join(", ", screenInfo)}");
                Console.WriteLine($"  Window rect: ({rect.left},{rect.top},{rect.right},{rect.bottom})");
                Console.WriteLine($"  Overlay bounds: ({Left},{Top}) Size: {Width}x{Height}");
                Console.WriteLine($"  Calculated highlight: ({highlightLeft},{highlightTop}) Size: {highlightWidth}x{highlightHeight}");
                
                // Ensure highlight is within overlay bounds
                if (highlightLeft < 0 || highlightTop < 0 || 
                    highlightLeft >= Width || highlightTop >= Height)
                {
                    Console.WriteLine($"  WARNING: Highlight position outside overlay bounds!");
                }
                
                // For primary monitor, check if coordinates need adjustment
                // Primary monitor might be at (0,0) but overlay starts at leftmost position
                var adjustedLeft = highlightLeft;
                var adjustedTop = highlightTop;
                
                // If this is primary monitor and we have negative overlay position, adjust
                if (overlayLeftPos < 0 && rect.left >= 0)
                {
                    adjustedLeft = rect.left + Math.Abs(overlayLeftPos);
                    Console.WriteLine($"  Primary monitor adjustment: rect.left={rect.left}, overlayLeft={overlayLeftPos}, adjusted={adjustedLeft}");
                }
                
                // Clamp highlight to overlay bounds to prevent out-of-bounds rendering
                var clampedLeft = Math.Max(0, Math.Min(adjustedLeft, Width - highlightWidth));
                var clampedTop = Math.Max(0, Math.Min(adjustedTop, Height - highlightHeight));
                var clampedWidth = Math.Max(0, Math.Min(highlightWidth, Width - clampedLeft));
                var clampedHeight = Math.Max(0, Math.Min(highlightHeight, Height - clampedTop));
                
                Console.WriteLine($"  Clamped highlight: ({clampedLeft},{clampedTop}) Size: {clampedWidth}x{clampedHeight}");
                
                // Update highlight with clamped coordinates
                Canvas.SetLeft(_highlightRect, clampedLeft);
                Canvas.SetTop(_highlightRect, clampedTop);
                _highlightRect.Width = clampedWidth;
                _highlightRect.Height = clampedHeight;
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
                
                // For manual selection, use direct window selection - no complex detection
                // This prevents selecting wrong windows like "programmer manager"
                if (processName.Contains("MuMu") || processName.Contains("Nemu"))
                {
                    _selectedHwnd = originalHwnd; // Direct selection - user knows what they're clicking
                    Console.WriteLine($"Selected MuMu window (direct): HWND=0x{originalHwnd:X8} Title='{title}' Process={processName}");
                }
                else if (processName.Contains("GameLoop") || className.Contains("Chrome") || title.Contains("RO"))
                {
                    _selectedHwnd = originalHwnd;
                    Console.WriteLine($"Selected emulator window (direct): HWND=0x{originalHwnd:X8} Process={processName}");
                }
                else
                {
                    _selectedHwnd = rootHwnd;
                    Console.WriteLine($"Selected window (root): HWND=0x{rootHwnd:X8}");
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
        Console.WriteLine($"Our overlay HWND: 0x{ourHwnd:X8}");
        
        // Temporarily make the overlay click-through during window detection
        WindowHelper.SetWindowTransparent(this);
        System.Threading.Thread.Sleep(10); // Small delay to ensure the change takes effect
        
        try
        {
            // Use WindowFromPoint to get the actual window under cursor
            var windowAtPoint = User32.WindowFromPoint(cursorPoint);
            Console.WriteLine($"WindowFromPoint result: 0x{(IntPtr)windowAtPoint:X8}");
        
            // Walk up the parent chain to find a suitable window
            var currentWindow = (IntPtr)windowAtPoint;
            var candidateWindows = new List<(IntPtr hwnd, string info)>();
            
            // Get all windows in Z-order at this point
            while (currentWindow != IntPtr.Zero)
            {
                if (currentWindow != ourHwnd && User32.IsWindowVisible(currentWindow))
                {
                    var title = GetWindowTitle(currentWindow);
                    var className = GetWindowClassName(currentWindow);
                    var processName = GetProcessName(currentWindow);
                    var info = $"Title:'{title}' Class:'{className}' Process:'{processName}'";
                    
                    candidateWindows.Add((currentWindow, info));
                    Console.WriteLine($"Candidate: 0x{currentWindow:X8} - {info}");
                    
                    // Check if this is a valid target window
                    if (IsValidTargetWindow(currentWindow, title, className, processName))
                    {
                        Console.WriteLine($"Selected valid target: 0x{currentWindow:X8} - {info}");
                        return currentWindow;
                    }
                }
                
                // Move to parent window
                currentWindow = (IntPtr)User32.GetParent(currentWindow);
            }
            
            // If no valid window found in parent chain, try alternative method
            var alternativeWindow = GetWindowFromEnumeration(cursorPoint, ourHwnd);
            if (alternativeWindow != IntPtr.Zero)
            {
                var title = GetWindowTitle(alternativeWindow);
                var className = GetWindowClassName(alternativeWindow);
                var processName = GetProcessName(alternativeWindow);
                Console.WriteLine($"Alternative selection: 0x{alternativeWindow:X8} - Title:'{title}' Process:'{processName}'");
                return alternativeWindow;
            }
            
            Console.WriteLine("No valid window found");
            return IntPtr.Zero;
        }
        finally
        {
            // Always restore the window to be interactive
            WindowHelper.SetWindowInteractive(this);
        }
    }
    
    private bool IsValidTargetWindow(IntPtr hwnd, string title, string className, string processName)
    {
        // Skip our own overlay
        var ourHwnd = (IntPtr)new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == ourHwnd) return false;
        
        // Skip system windows
        if (title.ToLower().Contains("program manager") ||
            title.ToLower().Contains("progman") ||
            className.ToLower().Contains("progman") ||
            processName.ToLower().Contains("dwm") ||
            processName.ToLower().Contains("explorer") && title.ToLower().Contains("program manager"))
        {
            return false;
        }
        
        // Skip our own process
        if (processName.ToLower().Contains("babemakro") || 
            processName.ToLower().Contains("overlay"))
        {
            return false;
        }
        
        // Skip transparent overlay windows (except game emulators)
        var exStyle = User32.GetWindowLongPtr(hwnd, User32.WindowLongFlags.GWL_EXSTYLE);
        if ((exStyle.ToInt64() & 0x80000) != 0) // WS_EX_LAYERED
        {
            if (!processName.ToLower().Contains("mumu") && 
                !processName.ToLower().Contains("nemu") &&
                !processName.ToLower().Contains("gameloop") &&
                !title.ToLower().Contains("ro"))
            {
                return false;
            }
        }
        
        // Valid target window
        return true;
    }
    
    private IntPtr GetWindowFromEnumeration(Vanara.PInvoke.POINT cursorPoint, IntPtr ourHwnd)
    {
        var windows = new List<IntPtr>();
        
        EnumWindows((hwnd, lParam) =>
        {
            if (hwnd != ourHwnd && User32.IsWindowVisible(hwnd))
            {
                var title = GetWindowTitle(hwnd);
                var className = GetWindowClassName(hwnd);
                var processName = GetProcessName(hwnd);
                
                if (IsValidTargetWindow(hwnd, title, className, processName))
                {
                    User32.GetWindowRect(hwnd, out var rect);
                    if (cursorPoint.x >= rect.left && cursorPoint.x < rect.right &&
                        cursorPoint.y >= rect.top && cursorPoint.y < rect.bottom)
                    {
                        windows.Add(hwnd);
                    }
                }
            }
            return true;
        }, IntPtr.Zero);
        
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
        
        // Prioritize windows with prest titles (auto-assignment pattern)
        foreach (var (hwnd, title, className) in candidateWindows)
        {
            // Look for prest121-128 pattern first (matches auto-assignment logic)
            if (title.StartsWith("prest") && title.Length >= 7)
            {
                Console.WriteLine($"Selected MuMu prest instance: HWND=0x{hwnd:X8} Title='{title}'");
                return hwnd;
            }
        }
        
        // Then look for RO, game, or instance-specific titles
        foreach (var (hwnd, title, className) in candidateWindows)
        {
            if (title.Contains("RO") || title.Contains("Ragnarok") || 
                title.Contains("MuMu") && (title.Contains("-") || char.IsDigit(title.Last())))
            {
                Console.WriteLine($"Selected MuMu game instance: HWND=0x{hwnd:X8} Title='{title}'");
                return hwnd;
            }
        }
        
        // Skip "programmer manager" and similar system windows
        foreach (var (hwnd, title, className) in candidateWindows)
        {
            if (!title.ToLower().Contains("programmer") && 
                !title.ToLower().Contains("manager") &&
                !title.ToLower().Contains("system"))
            {
                Console.WriteLine($"Selected MuMu fallback instance: HWND=0x{hwnd:X8} Title='{title}'");
                return hwnd;
            }
        }
        
        Console.WriteLine("No suitable MuMu instance found");
        return IntPtr.Zero;
    }
}