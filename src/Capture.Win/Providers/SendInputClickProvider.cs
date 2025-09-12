using System.Runtime.InteropServices;
using Vanara.PInvoke;
using PixelAutomation.Core.Interfaces;
using static Vanara.PInvoke.User32;

namespace PixelAutomation.Capture.Win.Providers;

public class SendInputClickProvider : IClickProvider
{
    private readonly bool _returnCursor;
    private IClickProvider? _fallbackProvider;
    
    public string Name => _returnCursor ? "Cursor Return Click" : "Cursor Jump Click";
    public ClickMode Mode => _returnCursor ? ClickMode.CursorReturn : ClickMode.CursorJump;

    public SendInputClickProvider(bool returnCursor = false)
    {
        _returnCursor = returnCursor;
    }

    public async Task<bool> ClickAsync(IntPtr hwnd, Point clientCoordinate, ClickOptions? options = null)
    {
        try
        {
            if (hwnd == IntPtr.Zero)
                return false;

            options ??= new ClickOptions();
            
            var clientPoint = new POINT { x = clientCoordinate.X, y = clientCoordinate.Y };
            ClientToScreen(hwnd, ref clientPoint);
            
            GetCursorPos(out var originalPos);
            
            var screenBounds = GetVirtualScreenBounds();
            var absoluteX = (clientPoint.x * 65535) / screenBounds.Width;
            var absoluteY = (clientPoint.y * 65535) / screenBounds.Height;

            var inputs = new List<INPUT>();
            
            inputs.Add(new INPUT
            {
                type = INPUTTYPE.INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dx = absoluteX,
                    dy = absoluteY,
                    dwFlags = MOUSEEVENTF.MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF.MOUSEEVENTF_MOVE
                }
            });

            var downFlag = options.Button switch
            {
                MouseButton.Right => MOUSEEVENTF.MOUSEEVENTF_RIGHTDOWN,
                MouseButton.Middle => MOUSEEVENTF.MOUSEEVENTF_MIDDLEDOWN,
                _ => MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN
            };

            var upFlag = options.Button switch
            {
                MouseButton.Right => MOUSEEVENTF.MOUSEEVENTF_RIGHTUP,
                MouseButton.Middle => MOUSEEVENTF.MOUSEEVENTF_MIDDLEUP,
                _ => MOUSEEVENTF.MOUSEEVENTF_LEFTUP
            };

            inputs.Add(new INPUT
            {
                type = INPUTTYPE.INPUT_MOUSE,
                mi = new MOUSEINPUT { dwFlags = downFlag }
            });

            inputs.Add(new INPUT
            {
                type = INPUTTYPE.INPUT_MOUSE,
                mi = new MOUSEINPUT { dwFlags = upFlag }
            });

            if (options.DoubleClick)
            {
                inputs.Add(new INPUT
                {
                    type = INPUTTYPE.INPUT_MOUSE,
                    mi = new MOUSEINPUT { dwFlags = downFlag }
                });

                inputs.Add(new INPUT
                {
                    type = INPUTTYPE.INPUT_MOUSE,
                    mi = new MOUSEINPUT { dwFlags = upFlag }
                });
            }

            if (_returnCursor)
            {
                var returnX = (originalPos.x * 65535) / screenBounds.Width;
                var returnY = (originalPos.y * 65535) / screenBounds.Height;
                
                inputs.Add(new INPUT
                {
                    type = INPUTTYPE.INPUT_MOUSE,
                    mi = new MOUSEINPUT
                    {
                        dx = returnX,
                        dy = returnY,
                        dwFlags = MOUSEEVENTF.MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF.MOUSEEVENTF_MOVE
                    }
                });
            }

            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
            
            if (options.DelayMs > 0)
                await Task.Delay(options.DelayMs.Value);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SendInput click failed: {ex.Message}");
            
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

            var inputs = new List<INPUT>();
            foreach (char c in text)
            {
                if (char.IsControl(c)) continue;
                
                inputs.Add(new INPUT
                {
                    type = INPUTTYPE.INPUT_KEYBOARD,
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = c,
                        dwFlags = KEYEVENTF.KEYEVENTF_UNICODE
                    }
                });
            }

            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
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

            var inputs = new INPUT[]
            {
                new INPUT
                {
                    type = INPUTTYPE.INPUT_KEYBOARD,
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)virtualKey,
                        dwFlags = 0
                    }
                },
                new INPUT
                {
                    type = INPUTTYPE.INPUT_KEYBOARD,
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)virtualKey,
                        dwFlags = KEYEVENTF.KEYEVENTF_KEYUP
                    }
                }
            };

            SendInput(2, inputs, Marshal.SizeOf<INPUT>());
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

    private static VK GetVirtualKeyCode(string key)
    {
        return key switch
        {
            "return" or "enter" => VK.VK_RETURN,
            "space" => VK.VK_SPACE,
            "escape" or "esc" => VK.VK_ESCAPE,
            "tab" => VK.VK_TAB,
            "backspace" => VK.VK_BACK,
            "delete" or "del" => VK.VK_DELETE,
            _ when key.Length == 1 => GetCharVK(key[0]),
            _ => 0
        };
    }

    private static VK GetCharVK(char c)
    {
        if (c >= 'a' && c <= 'z') return (VK)('A' + (c - 'a'));
        if (c >= '0' && c <= '9') return (VK)c;
        return 0;
    }

    private static Rectangle GetVirtualScreenBounds()
    {
        var left = GetSystemMetrics(SystemMetric.SM_XVIRTUALSCREEN);
        var top = GetSystemMetrics(SystemMetric.SM_YVIRTUALSCREEN);
        var width = GetSystemMetrics(SystemMetric.SM_CXVIRTUALSCREEN);
        var height = GetSystemMetrics(SystemMetric.SM_CYVIRTUALSCREEN);
        
        return new Rectangle(left, top, width, height);
    }
}