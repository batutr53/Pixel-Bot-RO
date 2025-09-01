using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using PixelAutomation.Core.Models;
using PixelAutomation.Core.Services;
using PixelAutomation.Host.Console.Services;

namespace PixelAutomation.Host.Console;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Pixel Automation Tool - Multi-window real-time monitoring and clicking");

        var configOption = new Option<FileInfo>(
            aliases: new[] { "--config", "-c" },
            description: "Path to configuration file",
            getDefaultValue: () => new FileInfo("config.json"));

        var profileOption = new Option<string>(
            aliases: new[] { "--profile", "-p" },
            description: "Profile name to use");

        var captureOption = new Option<string>(
            aliases: new[] { "--capture" },
            description: "Capture mode (WGC, PRINT, GPIXEL)",
            getDefaultValue: () => "WGC");

        var clickOption = new Option<string>(
            aliases: new[] { "--click" },
            description: "Click mode (message, cursor-jump, cursor-return)",
            getDefaultValue: () => "message");

        var hzOption = new Option<int>(
            aliases: new[] { "--hz" },
            description: "Target capture frequency",
            getDefaultValue: () => 80);

        var dryRunOption = new Option<bool>(
            aliases: new[] { "--dry-run", "-d" },
            description: "Dry run mode (log clicks without executing)");

        var telemetryOption = new Option<bool>(
            aliases: new[] { "--telemetry", "-t" },
            description: "Enable telemetry output",
            getDefaultValue: () => true);

        rootCommand.AddOption(configOption);
        rootCommand.AddOption(profileOption);
        rootCommand.AddOption(captureOption);
        rootCommand.AddOption(clickOption);
        rootCommand.AddOption(hzOption);
        rootCommand.AddOption(dryRunOption);
        rootCommand.AddOption(telemetryOption);

        rootCommand.SetHandler(async (context) =>
        {
            var config = context.ParseResult.GetValueForOption(configOption)!;
            var profile = context.ParseResult.GetValueForOption(profileOption);
            var captureMode = context.ParseResult.GetValueForOption(captureOption)!;
            var clickMode = context.ParseResult.GetValueForOption(clickOption)!;
            var hz = context.ParseResult.GetValueForOption(hzOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var telemetry = context.ParseResult.GetValueForOption(telemetryOption);

            await RunHost(config, profile, captureMode, clickMode, hz, dryRun, telemetry, context.GetCancellationToken());
        });

        return await rootCommand.InvokeAsync(args);
    }

    static async Task RunHost(FileInfo configFile, string? profileName, string captureMode, 
        string clickMode, int hz, bool dryRun, bool telemetry, CancellationToken cancellationToken)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File("logs/automation-.txt", 
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        try
        {
            Configuration? configuration = null;
            ProfileConfig? activeProfile = null;

            if (configFile.Exists)
            {
                var json = await File.ReadAllTextAsync(configFile.FullName, cancellationToken);
                configuration = JsonSerializer.Deserialize<Configuration>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (configuration?.Profiles != null && !string.IsNullOrEmpty(profileName))
                {
                    if (configuration.Profiles.TryGetValue(profileName, out var profile))
                    {
                        activeProfile = profile;
                        Log.Information($"Loaded profile: {profileName}");
                    }
                    else
                    {
                        Log.Warning($"Profile '{profileName}' not found, using defaults");
                    }
                }
            }

            activeProfile ??= new ProfileConfig
            {
                Global = new GlobalConfig
                {
                    CaptureMode = captureMode,
                    ClickMode = clickMode,
                    DefaultHz = hz,
                    DryRun = dryRun,
                    EnableTelemetry = telemetry
                }
            };

            if (!string.IsNullOrEmpty(captureMode))
                activeProfile.Global.CaptureMode = captureMode;
            if (!string.IsNullOrEmpty(clickMode))
                activeProfile.Global.ClickMode = clickMode;
            if (hz > 0)
                activeProfile.Global.DefaultHz = hz;
            if (dryRun)
                activeProfile.Global.DryRun = dryRun;

            var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton(activeProfile);
                    services.AddSingleton<IEventBus, EventBus>();
                    services.AddSingleton<WorkerManager>();
                    services.AddHostedService<AutomationHostService>();
                    
                    if (telemetry)
                    {
                        services.AddHostedService<TelemetryService>();
                    }
                })
                .Build();

            Log.Information("Pixel Automation Host starting...");
            Log.Information($"Capture: {activeProfile.Global.CaptureMode}, Click: {activeProfile.Global.ClickMode}, Hz: {activeProfile.Global.DefaultHz}");
            
            if (dryRun)
            {
                Log.Warning("DRY RUN MODE - Clicks will be logged but not executed");
            }

            System.Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Log.Information("Shutdown requested...");
                host.StopAsync().Wait();
            };

            await host.RunAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}