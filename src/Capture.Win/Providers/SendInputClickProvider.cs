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

    public void SetFallbackProvider(IClickProvider fallback)
    {
        _fallbackProvider = fallback;
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