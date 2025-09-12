using CommunityToolkit.Mvvm.ComponentModel;
using PixelAutomation.Core.Models;
using PixelAutomation.Tool.Overlay.WPF.Models;

namespace PixelAutomation.Tool.Overlay.WPF.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string activeProfile = "Default";

    [ObservableProperty]
    private string captureMode = "WGC";

    [ObservableProperty]
    private string clickMode = "message";

    [ObservableProperty]
    private string targetWindow = "None";

    [ObservableProperty]
    private ProbeInfo? selectedProbe;

    public void UpdateFromConfig(Configuration config)
    {
        if (config.Profiles.Any())
        {
            ActiveProfile = config.Profiles.Keys.First();
            
            if (config.Profiles.TryGetValue(ActiveProfile, out var profile))
            {
                CaptureMode = profile.Global.CaptureMode;
                ClickMode = profile.Global.ClickMode;
            }
        }
    }

    public void UpdateSelectedProbe(ProbeShape? shape)
    {
        if (shape == null)
        {
            SelectedProbe = null;
            return;
        }

        SelectedProbe = new ProbeInfo
        {
            Name = shape.Name,
            Position = $"{shape.X}, {shape.Y}",
            CurrentColor = shape.CurrentColor != null 
                ? $"RGB({shape.CurrentColor.Value.R}, {shape.CurrentColor.Value.G}, {shape.CurrentColor.Value.B})"
                : "N/A"
        };
    }
}

public class ProbeInfo
{
    public string Name { get; set; } = "";
    public string Position { get; set; } = "";
    public string CurrentColor { get; set; } = "";
}