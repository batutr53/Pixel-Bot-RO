using System.Windows;
using System.Windows.Shapes;
using PixelAutomation.Core.Models;

namespace PixelAutomation.Tool.Overlay.WPF.Models;

public class ProbeShape
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "point";
    public int X { get; set; }
    public int Y { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int Box { get; set; } = 5;
    public FrameworkElement? Visual { get; set; }
    public List<FrameworkElement> Handles { get; set; } = new();
    public Color? CurrentColor { get; set; }
    public Color? ReferenceColor { get; set; }
    public int Tolerance { get; set; } = 20;
    public string Mode { get; set; } = "level";
    public string Metric { get; set; } = "rgb";

    public ProbeConfig ToProbeConfig()
    {
        return new ProbeConfig
        {
            Name = Name,
            Kind = Kind,
            X = X,
            Y = Y,
            Width = Width,
            Height = Height,
            Box = Box,
            Mode = Mode,
            Metric = Metric,
            RefColor = ReferenceColor.HasValue 
                ? new[] { (int)ReferenceColor.Value.R, (int)ReferenceColor.Value.G, (int)ReferenceColor.Value.B }
                : new[] { 0, 0, 0 },
            Tolerance = Tolerance
        };
    }
}