using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Vanara.PInvoke;

namespace PixelAutomation.Tool.Overlay.WPF.Services;

public class HotkeyManager : IDisposable
{
    private readonly Window _window;
    private readonly Dictionary<int, Action> _hotkeyActions = new();
    private int _currentId = 0x0001;
    private HwndSource? _source;

    public HotkeyManager(Window window)
    {
        _window = window;
        _window.SourceInitialized += Window_SourceInitialized;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(_window);
        _source = HwndSource.FromHwnd(helper.Handle);
        _source?.AddHook(HwndHook);
    }

    public bool RegisterHotkey(Key key, ModifierKeys modifiers, Action action)
    {
        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        var modifierFlags = 0u;
        
        if ((modifiers & ModifierKeys.Alt) != 0)
            modifierFlags |= 0x0001;
        if ((modifiers & ModifierKeys.Control) != 0)
            modifierFlags |= 0x0002;
        if ((modifiers & ModifierKeys.Shift) != 0)
            modifierFlags |= 0x0004;
        if ((modifiers & ModifierKeys.Windows) != 0)
            modifierFlags |= 0x0008;

        var id = _currentId++;
        _hotkeyActions[id] = action;
        
        var helper = new WindowInteropHelper(_window);
        return User32.RegisterHotKey(helper.Handle, id, (User32.HotKeyModifiers)modifierFlags, (uint)virtualKey);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_hotkeyActions.TryGetValue(id, out var action))
            {
                action.Invoke();
                handled = true;
            }
        }
        
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        var helper = new WindowInteropHelper(_window);
        foreach (var id in _hotkeyActions.Keys)
        {
            User32.UnregisterHotKey(helper.Handle, id);
        }
        
        _source?.RemoveHook(HwndHook);
        _hotkeyActions.Clear();
    }
}