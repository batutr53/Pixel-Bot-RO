using Vanara.PInvoke;
using PixelAutomation.Core.Interfaces;
using static Vanara.PInvoke.User32;

namespace PixelAutomation.Capture.Win.Providers;

public class MessageClickProvider : IClickProvider
{
    private IClickProvider? _fallbackProvider;
    
    public string Name => "Message Click Provider";
    public ClickMode Mode => ClickMode.Message;

    public async Task<bool> ClickAsync(IntPtr hwnd, Point clientCoordinate, ClickOptions? options = null)
    {
        try
        {
            if (hwnd == IntPtr.Zero)
                return false;

            options ??= new ClickOptions();
            
            var lParam = MakeLParam(clientCoordinate.X, clientCoordinate.Y);
            var button = options.Button switch
            {
                MouseButton.Right => (WindowMessage.WM_RBUTTONDOWN, WindowMessage.WM_RBUTTONUP),
                MouseButton.Middle => (WindowMessage.WM_MBUTTONDOWN, WindowMessage.WM_MBUTTONUP),
                _ => (WindowMessage.WM_LBUTTONDOWN, WindowMessage.WM_LBUTTONUP)
            };

            PostMessage(hwnd, (uint)button.Item1, IntPtr.Zero, lParam);
            
            if (options.DelayMs > 0)
                await Task.Delay(options.DelayMs.Value);
            else
                await Task.Delay(10);
            
            PostMessage(hwnd, (uint)button.Item2, IntPtr.Zero, lParam);

            if (options.DoubleClick)
            {
                await Task.Delay(50);
                PostMessage(hwnd, (uint)button.Item1, IntPtr.Zero, lParam);
                await Task.Delay(10);
                PostMessage(hwnd, (uint)button.Item2, IntPtr.Zero, lParam);
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Message click failed: {ex.Message}");
            
            if (_fallbackProvider != null)
            {
                Console.WriteLine($"Falling back to {_fallbackProvider.Name}");
                return await _fallbackProvider.ClickAsync(hwnd, clientCoordinate, options);
            }
            
            return false;
        }
    }

    public async Task<bool> SendTextAsync(IntPtr hwnd, string text)
    {
        if (string.IsNullOrEmpty(text) || hwnd == IntPtr.Zero)
            return false;

        try
        {
            SetForegroundWindow(hwnd);
            await Task.Delay(100);

            foreach (char c in text)
            {
                if (char.IsControl(c)) continue;
                PostMessage(hwnd, (uint)WindowMessage.WM_CHAR, (IntPtr)c, IntPtr.Zero);
                await Task.Delay(10);
            }
            return true;
        }
        catch
        {
            return await (_fallbackProvider?.SendTextAsync(hwnd, text) ?? Task.FromResult(false));
        }
    }

    public async Task<bool> SendKeyAsync(IntPtr hwnd, string key)
    {
        if (string.IsNullOrEmpty(key) || hwnd == IntPtr.Zero)
            return false;

        try
        {
            var virtualKey = GetVirtualKeyCode(key.ToLower());
            if (virtualKey == 0) return false;

            SetForegroundWindow(hwnd);
            await Task.Delay(50);

            PostMessage(hwnd, (uint)WindowMessage.WM_KEYDOWN, (IntPtr)virtualKey, IntPtr.Zero);
            await Task.Delay(10);
            PostMessage(hwnd, (uint)WindowMessage.WM_KEYUP, (IntPtr)virtualKey, IntPtr.Zero);
            
            return true;
        }
        catch
        {
            return await (_fallbackProvider?.SendKeyAsync(hwnd, key) ?? Task.FromResult(false));
        }
    }

    public void SetFallbackProvider(IClickProvider fallback)
    {
        _fallbackProvider = fallback;
    }

    private static ushort GetVirtualKeyCode(string key)
    {
        return key switch
        {
            "return" or "enter" => 0x0D,
            "space" => 0x20,
            "escape" or "esc" => 0x1B,
            "tab" => 0x09,
            "backspace" => 0x08,
            "delete" or "del" => 0x2E,
            _ when key.Length == 1 => GetCharVK(key[0]),
            _ => 0
        };
    }

    private static ushort GetCharVK(char c)
    {
        if (c >= 'a' && c <= 'z') return (ushort)('A' + (c - 'a'));
        if (c >= '0' && c <= '9') return (ushort)(c);
        return 0;
    }

    private static IntPtr MakeLParam(int x, int y)
    {
        return new IntPtr((y << 16) | (x & 0xFFFF));
    }
}