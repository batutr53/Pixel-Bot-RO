# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

### Building and Running
```bash
# Restore packages
dotnet restore

# Build the solution
dotnet build -c Release

# Run the CLI application
dotnet run --project src/Host.Console

# Run the overlay tool
dotnet run --project src/Tool.Overlay.WPF
```

### CLI Usage Examples
```bash
# Start with default config
dotnet run --project src/Host.Console

# Start with specific profile
dotnet run --project src/Host.Console -- --profile TwoMonitor-8Clients

# Start with capture/click mode settings
dotnet run --project src/Host.Console -- --capture WGC --click message --hz 80

# Dry run mode (log clicks without executing)
dotnet run --project src/Host.Console -- --dry-run
```

## Architecture Overview

This is a C# .NET 8 Windows automation tool for pixel monitoring and automated clicking across multiple windows.

### Project Structure
- **Core/**: Base interfaces, models, and services - contains the fundamental abstractions
- **Capture.Win/**: Windows-specific capture backends (WGC, PrintWindow, GetPixel)
- **Host.Console/**: CLI application entry point with command-line parsing
- **Tool.Overlay.WPF/**: Interactive WPF overlay for visual probe configuration

### Key Concepts
- **Probes**: Pixel monitoring points (point/rectangle) that detect color changes
- **Events**: Triggered actions when probe conditions are met (edge/level detection)
- **Profiles**: Configuration presets for different multi-window scenarios
- **Backends**: Different capture methods (WGC > PrintWindow > GetPixel in performance order)

### Configuration System
- Main config: `config.json` in root directory
- Supports multiple profiles for different window layouts (e.g., "TwoMonitor-8Clients")
- Each profile defines windows, probes, events, and periodic clicks
- Hotkey sets for different usage scenarios (Default, Streamer)

### Core Technologies
- .NET 8.0 with Windows 10+ targeting
- Windows Graphics Capture (WGC) API for high-performance screen capture
- System.CommandLine for CLI parsing
- Serilog for logging
- WPF for overlay UI

### Performance Characteristics
- Target: 60-120 Hz pixel analysis
- Multi-threaded worker architecture (one thread per window)
- DPI-aware with Per-MonitorV2 support
- Optimized for minimal CPU usage with ROI (Region of Interest) support