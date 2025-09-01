using System.Drawing;
using PixelAutomation.Core.Implementations;
using PixelAutomation.Core.Interfaces;
using PixelAutomation.Core.Models;

namespace PixelAutomation.Core.Services;

public static class PercentageProbeFactory
{
    public static PercentageProbe CreateFromConfig(PercentageProbeConfig config)
    {
        var probeType = config.Type.ToLowerInvariant() switch
        {
            "hp" => PercentageProbeType.HP,
            "mp" => PercentageProbeType.MP,
            _ => PercentageProbeType.Custom
        };

        var mode = config.Mode.ToLowerInvariant() switch
        {
            "level" => ProbeMode.Level,
            "edge" => ProbeMode.Edge,
            _ => ProbeMode.Edge
        };

        var metric = config.Metric.ToLowerInvariant() switch
        {
            "hsv" => ColorMetric.HSV,
            "deltae" => ColorMetric.DeltaE,
            _ => ColorMetric.RGB
        };

        var expectedColor = Color.FromArgb(
            Math.Clamp(config.ExpectedColor[0], 0, 255),
            Math.Clamp(config.ExpectedColor[1], 0, 255),
            Math.Clamp(config.ExpectedColor[2], 0, 255));

        Color? emptyColor = null;
        if (config.EmptyColor != null && config.EmptyColor.Length >= 3)
        {
            emptyColor = Color.FromArgb(
                Math.Clamp(config.EmptyColor[0], 0, 255),
                Math.Clamp(config.EmptyColor[1], 0, 255),
                Math.Clamp(config.EmptyColor[2], 0, 255));
        }

        return new PercentageProbe(
            config.Name,
            config.StartX,
            config.EndX,
            config.Y,
            config.MonitorPercentage,
            probeType,
            expectedColor,
            emptyColor,
            config.Tolerance,
            config.Box,
            mode,
            metric,
            config.DebounceMs);
    }

    public static PercentageProbeConfig CreateHPProbeConfig(
        string name,
        int startX,
        int endX,
        int y,
        double monitorPercentage = 50.0,
        int tolerance = 30)
    {
        return new PercentageProbeConfig
        {
            Name = name,
            Type = "HP",
            StartX = startX,
            EndX = endX,
            Y = y,
            MonitorPercentage = monitorPercentage,
            ExpectedColor = new[] { 16, 12, 255 }, // Your HP color from Python: 0x100CFF -> RGB(16, 12, 255)
            EmptyColor = new[] { 0, 0, 0 }, // Black for empty
            Tolerance = tolerance,
            Box = 1,
            Mode = "edge",
            Metric = "rgb",
            DebounceMs = 100
        };
    }

    public static PercentageProbeConfig CreateMPProbeConfig(
        string name,
        int startX,
        int endX,
        int y,
        double monitorPercentage = 50.0,
        int tolerance = 30)
    {
        return new PercentageProbeConfig
        {
            Name = name,
            Type = "MP",
            StartX = startX,
            EndX = endX,
            Y = y,
            MonitorPercentage = monitorPercentage,
            ExpectedColor = new[] { 255, 77, 23 }, // Your MP color from Python: 0xFF4D17 -> RGB(255, 77, 23)
            EmptyColor = new[] { 0, 0, 0 }, // Black for empty
            Tolerance = tolerance,
            Box = 1,
            Mode = "edge",
            Metric = "rgb",
            DebounceMs = 100
        };
    }
}