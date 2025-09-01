using System.Windows;
using Vanara.PInvoke;
using System.Runtime.InteropServices;

namespace PixelAutomation.Tool.Overlay.WPF;

public partial class App : Application
{
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();
    
    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();
    
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Konsol penceresi aç
        AllocConsole();
        Console.WriteLine("🎯 PIXEL AUTOMATION DEBUG CONSOLE");
        Console.WriteLine("===============================");
        Console.WriteLine("Debug mesajları burada görünecek...");
        Console.WriteLine();
        
        SHCore.SetProcessDpiAwareness(SHCore.PROCESS_DPI_AWARENESS.PROCESS_PER_MONITOR_DPI_AWARE);
    }
    
    protected override void OnExit(ExitEventArgs e)
    {
        FreeConsole();
        base.OnExit(e);
    }
}