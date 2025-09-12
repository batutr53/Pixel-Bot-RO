using System.Drawing;
using System.Windows;
using PixelAutomation.Tool.Overlay.WPF.Models;
using PixelAutomation.Core.Models;

namespace PixelAutomation.Tool.Overlay.WPF.Services;

public static class DummyMethods
{
    public static void UpdateProbeValues(this ShapeManager manager, IntPtr hwnd)
    {
        // Placeholder for probe value updates
        // In full implementation, this would capture and analyze pixels
    }

    public static void UpdateSelectedProbe(this ViewModels.MainViewModel viewModel, ProbeShape? shape)
    {
        // Placeholder for selected probe updates
    }

    public static Configuration BuildConfigurationFromShapes(this Window window)
    {
        // Placeholder for config building
        return new Configuration();
    }

    public static void UpdateFromConfig(this ViewModels.MainViewModel viewModel, Configuration config)
    {
        // Placeholder for config updates
    }
}