using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PixelAutomation.Core.Interfaces;

namespace PixelAutomation.Core.Implementations;

public class WindowsMessageClickProvider : IClickProvider
{
    private readonly ILogger<WindowsMessageClickProvider> _logger;

    public string Name => "Windows Message";
    public ClickMode Mode => ClickMode.Message;

    public WindowsMessageClickProvider(ILogger<WindowsMessageClickProvider> logger)
    {
        _logger = logger;
    }

    public Task<bool> ClickAsync(IntPtr hwnd, Point clientCoordinate, ClickOptions? options = null)
    {
        return Task.Run(() => PerformClick(hwnd, clientCoordinate, options ?? new ClickOptions()));
    }

    public async Task<bool> SendTextAsync(IntPtr hwnd, string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        try
        {
            // Focus the window first
            SetForegroundWindow(hwnd);
            await Task.Delay(100);

            // Send each character
            foreach (char c in text)
            {
                if (char.IsControl(c))
                    continue;

                // Send WM_CHAR message
                SendMessage(hwnd, WM_CHAR, (IntPtr)c, IntPtr.Zero);
                await Task.Delay(10); // Small delay between characters
            }

            _logger.LogDebug("Sent text '{Text}' to window {Hwnd}", text, hwnd);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send text to window {Hwnd}", hwnd);
            return false;
        }
    }

    public async Task<bool> SendKeyAsync(IntPtr hwnd, string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        try
        {
            Console.WriteLine($"[WindowsMessageClickProvider] 🔑 SendKeyAsync: key='{key}' hwnd=0x{hwnd:X8}");
            
            var virtualKey = GetVirtualKeyCode(key.ToLower());
            if (virtualKey == 0)
            {
                _logger.LogWarning("Unknown key: {Key}", key);
                Console.WriteLine($"[WindowsMessageClickProvider] ❌ Unknown key: {key}");
                return false;
            }

            Console.WriteLine($"[WindowsMessageClickProvider] ✅ Key '{key}' mapped to VK={virtualKey:X2}");

            // Focus the window first
            Console.WriteLine($"[WindowsMessageClickProvider] 🎯 Setting foreground window 0x{hwnd:X8}");
            SetForegroundWindow(hwnd);
            await Task.Delay(50);

            // Send key down
            var lParam = MakeLParam(1, virtualKey, 0, 0, 0, 0);
            Console.WriteLine($"[WindowsMessageClickProvider] ⬇️ Sending WM_KEYDOWN VK={virtualKey:X2} to 0x{hwnd:X8}");
            SendMessage(hwnd, WM_KEYDOWN, (IntPtr)virtualKey, lParam);
            
            await Task.Delay(10);
            
            // Send key up
            lParam = MakeLParam(1, virtualKey, 0, 0, 0, 1);
            Console.WriteLine($"[WindowsMessageClickProvider] ⬆️ Sending WM_KEYUP VK={virtualKey:X2} to 0x{hwnd:X8}");
            SendMessage(hwnd, WM_KEYUP, (IntPtr)virtualKey, lParam);

            Console.WriteLine($"[WindowsMessageClickProvider] ✅ Successfully sent key '{key}' to window 0x{hwnd:X8}");
            _logger.LogDebug("Sent key '{Key}' to window {Hwnd}", key, hwnd);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send key '{Key}' to window {Hwnd}", key, hwnd);
            return false;
        }
    }

    public void SetFallbackProvider(IClickProvider fallback)
    {
        // Message provider doesn't need fallback
    }

    private bool PerformClick(IntPtr hwnd, Point location, ClickOptions options)
    {
        try
        {
            var lParam = MakeLParam(location.X, location.Y);
            var button = GetMouseButton(options.Button);

            if (options.DoubleClick)
            {
                // Send double click
                SendMessage(hwnd, button.down, IntPtr.Zero, lParam);
                SendMessage(hwnd, button.up, IntPtr.Zero, lParam);
                Thread.Sleep(10);
                SendMessage(hwnd, button.doubleClick, IntPtr.Zero, lParam);
                SendMessage(hwnd, button.up, IntPtr.Zero, lParam);
            }
            else
            {
                // Send single click
                SendMessage(hwnd, button.down, IntPtr.Zero, lParam);
                if (options.DelayMs.HasValue)
                    Thread.Sleep(options.DelayMs.Value);
                SendMessage(hwnd, button.up, IntPtr.Zero, lParam);
            }

            _logger.LogDebug("Clicked at ({X}, {Y}) on window {Hwnd}", location.X, location.Y, hwnd);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to click at ({X}, {Y}) on window {Hwnd}", location.X, location.Y, hwnd);
            return false;
        }
    }

    private static IntPtr MakeLParam(int loWord, int hiWord)
    {
        return (IntPtr)((hiWord << 16) | (loWord & 0xFFFF));
    }

    private static IntPtr MakeLParam(ushort repeatCount, ushort virtualKeyCode, 
        ushort extendedKey, ushort contextCode, ushort previousState, ushort transitionState)
    {
        uint lParam = 0;
        lParam |= repeatCount;
        lParam |= (uint)(virtualKeyCode << 16);
        lParam |= (uint)(extendedKey << 24);
        lParam |= (uint)(contextCode << 29);
        lParam |= (uint)(previousState << 30);
        lParam |= (uint)(transitionState << 31);
        return (IntPtr)lParam;
    }

    private static (uint down, uint up, uint doubleClick) GetMouseButton(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => (WM_LBUTTONDOWN, WM_LBUTTONUP, WM_LBUTTONDBLCLK),
            MouseButton.Right => (WM_RBUTTONDOWN, WM_RBUTTONUP, WM_RBUTTONDBLCLK),
            MouseButton.Middle => (WM_MBUTTONDOWN, WM_MBUTTONUP, WM_MBUTTONDBLCLK),
            _ => (WM_LBUTTONDOWN, WM_LBUTTONUP, WM_LBUTTONDBLCLK)
        };
    }

    private static ushort GetVirtualKeyCode(string key)
    {
        return key switch
        {
            "return" or "enter" => VK_RETURN,
            "space" => VK_SPACE,
            "escape" or "esc" => VK_ESCAPE,
            "tab" => VK_TAB,
            "backspace" => VK_BACK,
            "delete" or "del" => VK_DELETE,
            "f1" => VK_F1,
            "f2" => VK_F2,
            "f3" => VK_F3,
            "f4" => VK_F4,
            "f5" => VK_F5,
            "f6" => VK_F6,
            "f7" => VK_F7,
            "f8" => VK_F8,
            "f9" => VK_F9,
            "f10" => VK_F10,
            "f11" => VK_F11,
            "f12" => VK_F12,
            "shift" => VK_SHIFT,
            "ctrl" or "control" => VK_CONTROL,
            "alt" => VK_MENU,
            "home" => VK_HOME,
            "end" => VK_END,
            "pageup" => VK_PRIOR,
            "pagedown" => VK_NEXT,
            "up" => VK_UP,
            "down" => VK_DOWN,
            "left" => VK_LEFT,
            "right" => VK_RIGHT,
            _ when key.Length == 1 => GetCharacterVirtualKey(key[0]),
            _ => 0
        };
    }

    private static ushort GetCharacterVirtualKey(char c)
    {
        if (c >= 'a' && c <= 'z')
            return (ushort)('A' + (c - 'a'));
        if (c >= '0' && c <= '9')
            return (ushort)('0' + (c - '0'));
        
        return c switch
        {
            '1' => 0x31, '2' => 0x32, '3' => 0x33, '4' => 0x34, '5' => 0x35,
            '6' => 0x36, '7' => 0x37, '8' => 0x38, '9' => 0x39, '0' => 0x30,
            _ => 0
        };
    }

    #region Win32 API

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    // Mouse Messages
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const uint WM_RBUTTONDOWN = 0x0204;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_RBUTTONDBLCLK = 0x0206;
    private const uint WM_MBUTTONDOWN = 0x0207;
    private const uint WM_MBUTTONUP = 0x0208;
    private const uint WM_MBUTTONDBLCLK = 0x0209;

    // Keyboard Messages
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;
    private const uint WM_CHAR = 0x0102;

    // Virtual Key Codes
    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_SPACE = 0x20;
    private const ushort VK_ESCAPE = 0x1B;
    private const ushort VK_TAB = 0x09;
    private const ushort VK_BACK = 0x08;
    private const ushort VK_DELETE = 0x2E;
    private const ushort VK_SHIFT = 0x10;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_MENU = 0x12;
    private const ushort VK_HOME = 0x24;
    private const ushort VK_END = 0x23;
    private const ushort VK_PRIOR = 0x21;
    private const ushort VK_NEXT = 0x22;
    private const ushort VK_UP = 0x26;
    private const ushort VK_DOWN = 0x28;
    private const ushort VK_LEFT = 0x25;
    private const ushort VK_RIGHT = 0x27;
    private const ushort VK_F1 = 0x70;
    private const ushort VK_F2 = 0x71;
    private const ushort VK_F3 = 0x72;
    private const ushort VK_F4 = 0x73;
    private const ushort VK_F5 = 0x74;
    private const ushort VK_F6 = 0x75;
    private const ushort VK_F7 = 0x76;
    private const ushort VK_F8 = 0x77;
    private const ushort VK_F9 = 0x78;
    private const ushort VK_F10 = 0x79;
    private const ushort VK_F11 = 0x7A;
    private const ushort VK_F12 = 0x7B;

    #endregion
}