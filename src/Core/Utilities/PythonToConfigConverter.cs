using PixelAutomation.Core.Models;
using PixelAutomation.Core.Services;

namespace PixelAutomation.Core.Utilities;

/// <summary>
/// Utility class to convert Python HP/MP monitoring code to C# configuration
/// </summary>
public static class PythonToConfigConverter
{
    /// <summary>
    /// Convert Python-style HP/MP region to PercentageProbeConfig
    /// Example: HP_region = (100, 67, 250 - 100, 1) => (startX: 100, y: 67, width: 150)
    /// </summary>
    public static PercentageProbeConfig ConvertRegionToHPProbe(
        string name,
        int startX,
        int y, 
        int width,
        double monitorPercentage,
        int tolerance = 30)
    {
        return PercentageProbeFactory.CreateHPProbeConfig(
            name,
            startX,
            startX + width, // endX = startX + width
            y,
            monitorPercentage,
            tolerance
        );
    }

    /// <summary>
    /// Convert Python-style MP region to PercentageProbeConfig
    /// Example: MP_region = (100, 84, 250 - 100, 1) => (startX: 100, y: 84, width: 150)
    /// </summary>
    public static PercentageProbeConfig ConvertRegionToMPProbe(
        string name,
        int startX,
        int y,
        int width, 
        double monitorPercentage,
        int tolerance = 30)
    {
        return PercentageProbeFactory.CreateMPProbeConfig(
            name,
            startX,
            startX + width, // endX = startX + width
            y,
            monitorPercentage,
            tolerance
        );
    }

    /// <summary>
    /// Calculate X coordinate for a given percentage (mimics Python logic)
    /// Python: x_to_check = region[0] + int((percentage / 100) * region[2])
    /// </summary>
    public static int CalculateXForPercentage(int startX, int width, double percentage)
    {
        return startX + (int)((percentage / 100.0) * width);
    }

    /// <summary>
    /// Convert RGB hex to int array for configuration
    /// Example: 0x100CFF => [16, 12, 255]
    /// </summary>
    public static int[] HexToRgbArray(uint hexColor)
    {
        return new int[]
        {
            (int)((hexColor >> 16) & 0xFF), // R
            (int)((hexColor >> 8) & 0xFF),  // G
            (int)(hexColor & 0xFF)          // B
        };
    }

    /// <summary>
    /// Create complete HP/MP monitoring profile based on Python coordinates
    /// </summary>
    public static ProfileConfig CreateHPMPProfile(
        string profileName,
        string windowTitleRegex,
        (int startX, int y, int width) hpRegion,
        (int startX, int y, int width) mpRegion,
        (int x, int y) healingPotionClick,
        (int x, int y) manaPotionClick,
        double[] hpPercentages = null,
        double[] mpPercentages = null)
    {
        hpPercentages ??= new[] { 25.0, 50.0, 75.0 };
        mpPercentages ??= new[] { 25.0, 50.0, 75.0 };

        var percentageProbes = new List<PercentageProbeConfig>();
        var events = new List<EventConfig>();

        // Create HP probes
        foreach (var percentage in hpPercentages)
        {
            var probe = ConvertRegionToHPProbe(
                $"HP_Monitor_{percentage}",
                hpRegion.startX,
                hpRegion.y,
                hpRegion.width,
                percentage
            );
            percentageProbes.Add(probe);

            // Add event for HP below threshold
            events.Add(new EventConfig
            {
                When = $"HP_Monitor_{percentage}:threshold-below",
                Click = new ClickTarget { X = healingPotionClick.x, Y = healingPotionClick.y },
                CooldownMs = percentage <= 25 ? 200 : 500,
                Priority = percentage <= 25 ? 2 : 1
            });
        }

        // Create MP probes  
        foreach (var percentage in mpPercentages)
        {
            var probe = ConvertRegionToMPProbe(
                $"MP_Monitor_{percentage}",
                mpRegion.startX,
                mpRegion.y, 
                mpRegion.width,
                percentage
            );
            percentageProbes.Add(probe);

            // Add event for MP below threshold
            events.Add(new EventConfig
            {
                When = $"MP_Monitor_{percentage}:threshold-below",
                Click = new ClickTarget { X = manaPotionClick.x, Y = manaPotionClick.y },
                CooldownMs = percentage <= 25 ? 200 : 500,
                Priority = percentage <= 25 ? 2 : 1
            });
        }

        return new ProfileConfig
        {
            Global = new GlobalConfig
            {
                CaptureMode = "WGC",
                ClickMode = "message", 
                DefaultHz = 80,
                LogLevel = "Info",
                EnableTelemetry = true,
                DryRun = false
            },
            Windows = new List<WindowTarget>
            {
                new WindowTarget
                {
                    TitleRegex = windowTitleRegex,
                    DpiAware = true,
                    PercentageProbes = percentageProbes,
                    Events = events,
                    Limits = new RateLimitConfig
                    {
                        MaxBurstPerSec = 20,
                        GlobalCooldownMs = 50
                    }
                }
            },
            Hotkeys = "Default"
        };
    }
}