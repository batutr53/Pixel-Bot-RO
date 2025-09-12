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
dotnet run --project src/BabeMakro.Console

# Run the overlay tool
dotnet run --project src/BabeMakro
```

### CLI Usage Examples
```bash
# Start with default config
dotnet run --project src/BabeMakro.Console

# Start with specific profile
dotnet run --project src/BabeMakro.Console -- --profile TwoMonitor-8Clients

# Start with capture/click mode settings
dotnet run --project src/BabeMakro.Console -- --capture WGC --click message --hz 80

# Dry run mode (log clicks without executing)
dotnet run --project src/BabeMakro.Console -- --dry-run
```

## Architecture Overview

BabeMakro is a C# .NET 8 Windows automation tool for pixel monitoring and automated clicking across multiple windows.

### Project Structure
- **Core/**: Base interfaces, models, and services - contains the fundamental abstractions
- **Capture.Win/**: Windows-specific capture backends (WGC, PrintWindow, GetPixel)
- **BabeMakro.Console/**: CLI application entry point with command-line parsing
- **BabeMakro/**: Interactive WPF overlay for visual probe configuration and client management

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

## Performance Optimization Architecture

The codebase has been extensively optimized with professional-grade performance enhancements:

### Master Timer System
- **MasterTimerManager**: Consolidates 15+ individual DispatcherTimers into a single high-frequency timer
- Achieves ~15x reduction in timer overhead through task-based scheduling
- Located in `src/BabeMakro/Services/MasterTimerManager.cs`
- Priority-based task execution with performance statistics

### Task Queue Management
- **BoundedTaskQueue**: Priority-based task scheduling system with concurrency control
- Prevents memory exhaustion with bounded queues and task coalescing
- Priority levels: Critical (HP/MP triggers) > High (monitoring) > Normal (skills) > Low (background) > Maintenance (logging)
- Located in `src/Core/Services/BoundedTaskQueue.cs`

### Color Sampling Optimization
- **ColorSamplingCache**: Intelligent caching system reducing Win32 API calls by 70-90%
- **OptimizedFastColorSampler**: High-performance cached sampler with memory pooling
- **FastColorSampler**: BitBlt-based screen capture with 50ms cache expiry
- Located in `src/BabeMakro/Services/`

### Memory Management
- **ObjectPoolManager**: Professional-grade object pooling for arrays and collections
- Uses Microsoft.Extensions.ObjectPool for zero-allocation scenarios
- ArrayPool<T> integration for high-frequency array operations
- Located in `src/BabeMakro/Services/ObjectPoolManager.cs`

### Image Processing
- **ImageProcessor**: Thread-safe image processing for CAPTCHA detection
- All parallel methods redirect to safe non-parallel versions to avoid thread safety issues
- Supports grayscale conversion, contrast adjustment, brightness modification
- Located in `src/BabeMakro/Services/ImageProcessor.cs`

### Performance Monitoring
- **PerformanceMonitor**: Real-time metrics collection and reporting
- 30-second reporting intervals with comprehensive statistics
- Tracks timer reduction ratios, allocation reductions, and processing speedups
- Located in `src/BabeMakro/Services/PerformanceMonitor.cs`

### CAPTCHA Integration
- **TesseractCaptchaSolver**: Professional OCR integration replacing placeholder implementations
- Thread-safe image processing pipeline with proper memory management
- Supports multiple image enhancement techniques for better OCR accuracy

## Critical Implementation Notes

### Thread Safety
- All image processing methods use safe, non-parallel implementations to prevent race conditions
- Color sampling is protected with semaphores and proper locking mechanisms
- Object pooling is implemented with thread-safe collections

### Memory Management
- Comprehensive IDisposable patterns throughout the codebase
- Automatic cleanup of native resources (HDC, bitmaps, timers)
- Memory pooling reduces GC pressure in high-frequency scenarios

### Performance vs Reliability
- The architecture prioritizes reliability over maximum performance
- CAPTCHA processing uses non-parallel methods for stability
- All optimizations maintain thread safety as the primary concern

## Key Files to Understand

When working with performance-critical code:
- `ClientCard.xaml.cs`: Main automation logic with optimized timer system
- `MasterTimerManager.cs`: Timer consolidation and task scheduling
- `ColorSamplingCache.cs`: Intelligent color sampling with caching
- `BoundedTaskQueue.cs`: Priority-based task management
- `ObjectPoolManager.cs`: Memory allocation optimization
- `ImageProcessor.cs`: Thread-safe image processing for CAPTCHA

The codebase follows a layered architecture where Core provides abstractions, Capture.Win implements Windows-specific functionality, and BabeMakro provides both CLI and GUI interfaces with extensive performance optimizations.