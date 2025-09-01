using System.Drawing;

namespace PixelAutomation.Core.Interfaces;

public interface IPercentageProbe : IProbe
{
    int StartX { get; }
    int EndX { get; }
    double MonitorPercentage { get; }
    int CalculatedX { get; }
    PercentageProbeType ProbeType { get; }
}

public enum PercentageProbeType
{
    HP,
    MP,
    Custom
}

public class PercentageProbeResult : ProbeResult
{
    public double CurrentPercentage { get; init; }
    public bool ThresholdCrossed { get; init; }
    public string? CrossDirection { get; init; }
}