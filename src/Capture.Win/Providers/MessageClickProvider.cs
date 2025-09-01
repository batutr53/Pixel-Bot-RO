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

    public void SetFallbackProvider(IClickProvider fallback)
    {
        _fallbackProvider = fallback;
    }

    private static IntPtr MakeLParam(int x, int y)
    {
        return new IntPtr((y << 16) | (x & 0xFFFF));
    }
}