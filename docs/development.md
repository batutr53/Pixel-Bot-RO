# 🔧 Development Guide

> Contributing guidelines and development setup for BabeMakro project.

## 📋 Table of Contents

- [Development Setup](#development-setup)
- [Project Architecture](#project-architecture)
- [Building and Testing](#building-and-testing)
- [Code Style Guidelines](#code-style-guidelines)
- [Contributing Workflow](#contributing-workflow)
- [Adding New Features](#adding-new-features)
- [Debugging Guide](#debugging-guide)
- [Performance Optimization](#performance-optimization)

## Development Setup

### Prerequisites

- **Windows 10/11** - Required for Windows Graphics Capture API
- **.NET 8.0 SDK** - Latest version recommended
- **Visual Studio 2022** or **Visual Studio Code** with C# extension
- **Git** for version control

### Environment Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/yourusername/babemakro.git
   cd babemakro
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Verify build:**
   ```bash
   dotnet build -c Debug
   ```

4. **Run tests:**
   ```bash
   dotnet test
   ```

### Development Tools

**Recommended Extensions:**
- C# for Visual Studio Code
- GitLens for Git integration
- Bracket Pair Colorizer
- Material Icon Theme

**Debugging Tools:**
- **Process Monitor** - Track file/registry access
- **Spy++** - Inspect Windows messages
- **Performance Profiler** - Monitor CPU/memory usage

## Project Architecture

### Solution Structure

```
BabeMakro/
├── src/
│   ├── Core/                   # Core interfaces and models
│   │   ├── Interfaces/         # Service contracts
│   │   ├── Models/             # Data models
│   │   └── Services/           # Core services
│   ├── Capture.Win/            # Windows capture implementations
│   │   ├── Backends/           # Capture backends (WGC, PrintWindow, GetPixel)
│   │   └── Native/             # Win32 API interop
│   ├── BabeMakro/             # Main WPF application
│   │   ├── Views/              # XAML views
│   │   ├── ViewModels/         # MVVM view models
│   │   ├── Controls/           # Custom user controls
│   │   └── Services/           # UI-specific services
│   ├── Host.Console/           # CLI application (deprecated)
│   └── Tool.Overlay.WPF/       # Overlay configuration tool
├── tests/                      # Unit and integration tests
├── docs/                       # Documentation
└── config.json               # Configuration file
```

### Core Architecture Patterns

#### MVVM (Model-View-ViewModel)

**Models** (`Core/Models/`):
- Data structures for configuration
- Window, probe, and event definitions
- Serializable configuration objects

**Views** (`BabeMakro/Views/`):
- XAML user interface definitions
- No business logic in code-behind
- Data binding to ViewModels

**ViewModels** (`BabeMakro/ViewModels/`):
- UI state management
- Command handling
- Property change notifications

#### Service Layer Architecture

**Core Services** (`Core/Services/`):
- `ICaptureService` - Screen capture abstraction
- `IMonitoringService` - Pixel monitoring logic
- `IConfigurationService` - Config management

**Implementation Services** (`Capture.Win/`):
- Platform-specific implementations
- Windows Graphics Capture integration
- Input simulation services

### Key Components

#### 1. Capture System

```csharp
public interface ICaptureService
{
    Task<CaptureResult> CaptureWindowAsync(WindowTarget target);
    bool SupportsWindow(WindowTarget target);
    CaptureCapabilities GetCapabilities();
}
```

**Backends:**
- **WGC (Windows Graphics Capture)** - Modern, fast
- **PrintWindow** - Compatible fallback
- **GetPixel** - Legacy support

#### 2. Monitoring Engine

```csharp
public interface IMonitoringService
{
    Task StartAsync(MonitoringConfiguration config);
    Task StopAsync();
    event EventHandler<ProbeTriggeredEventArgs> ProbeTriggered;
}
```

**Features:**
- Multi-threaded probe processing
- Event-driven probe triggers
- Configurable monitoring rates

#### 3. Configuration System

```csharp
public class Configuration
{
    public Dictionary<string, Profile> Profiles { get; set; }
    public Dictionary<string, HotkeySet> HotkeySets { get; set; }
    public string ActiveProfile { get; set; }
}
```

## Building and Testing

### Build Commands

```bash
# Debug build
dotnet build -c Debug

# Release build
dotnet build -c Release

# Clean build
dotnet clean && dotnet build

# Build specific project
dotnet build src/BabeMakro -c Release
```

### Running Applications

```bash
# Main WPF application
dotnet run --project src/BabeMakro

# Console host (deprecated)
dotnet run --project src/Host.Console

# Overlay tool
dotnet run --project src/Tool.Overlay.WPF
```

### Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test tests/Core.Tests
```

### Deployment

```bash
# Publish self-contained executable
dotnet publish src/BabeMakro -c Release -r win-x64 --self-contained

# Create portable package
dotnet publish src/BabeMakro -c Release --no-self-contained
```

## Code Style Guidelines

### C# Conventions

**Naming:**
- PascalCase for classes, methods, properties
- camelCase for local variables, parameters
- UPPER_CASE for constants
- Prefix interfaces with 'I'

**Example:**
```csharp
public class MonitoringService : IMonitoringService
{
    private readonly ICaptureService _captureService;
    private const int DEFAULT_TIMEOUT = 5000;
    
    public async Task<bool> StartMonitoringAsync(string profileName)
    {
        var profile = await LoadProfileAsync(profileName);
        return await ProcessProfileAsync(profile);
    }
}
```

### XAML Conventions

**Naming:**
- PascalCase for element names
- Descriptive names with element type suffix

**Example:**
```xml
<Grid x:Name="MainContentGrid">
    <Button x:Name="StartMonitoringButton" 
            Content="Start" 
            Command="{Binding StartCommand}" />
</Grid>
```

### File Organization

**Source Files:**
- One class per file
- Filename matches class name
- Organize by feature/responsibility

**XAML Files:**
- Code-behind minimal
- Use data binding over code
- Separate styles into ResourceDictionaries

## Contributing Workflow

### 1. Fork and Branch

```bash
# Fork repository on GitHub
git clone https://github.com/yourusername/babemakro.git
cd babemakro

# Create feature branch
git checkout -b feature/amazing-feature
```

### 2. Development Process

1. **Write tests first** (TDD approach)
2. **Implement feature** with minimal code
3. **Refactor** for clarity and performance
4. **Update documentation** as needed

### 3. Code Review Process

1. **Self-review** - Check your own changes
2. **Run tests** - Ensure all tests pass
3. **Check formatting** - Follow style guidelines
4. **Create PR** - Clear description of changes

### 4. Pull Request Template

```markdown
## Description
Brief description of changes

## Type of Change
- [ ] Bug fix
- [ ] New feature  
- [ ] Breaking change
- [ ] Documentation update

## Testing
- [ ] Tests added/updated
- [ ] Manual testing completed
- [ ] Performance impact assessed

## Checklist
- [ ] Code follows style guidelines
- [ ] Self-review completed
- [ ] Documentation updated
```

## Adding New Features

### 1. Capture Backend

To add a new capture method:

```csharp
public class NewCaptureBackend : ICaptureService
{
    public async Task<CaptureResult> CaptureWindowAsync(WindowTarget target)
    {
        // Implement capture logic
        var bitmap = await CaptureWindowBitmapAsync(target);
        return new CaptureResult(bitmap);
    }
    
    public bool SupportsWindow(WindowTarget target)
    {
        // Check if this backend can capture the target
        return target.ProcessName == "SupportedProcess";
    }
}
```

**Registration:**
```csharp
// In ServiceConfiguration
services.AddTransient<ICaptureService, NewCaptureBackend>();
```

### 2. Probe Type

To add a new probe type:

```csharp
public class CustomProbe : ProbeBase
{
    public override async Task<ProbeResult> EvaluateAsync(CaptureResult capture)
    {
        // Implement custom evaluation logic
        var result = await ProcessCaptureAsync(capture);
        return new ProbeResult(result.IsTriggered, result.Value);
    }
}
```

**Configuration:**
```json
{
  "probes": [{
    "name": "CustomProbe1",
    "kind": "custom",
    "customProperty": "value"
  }]
}
```

### 3. UI Component

To add a new UI control:

```xml
<!-- Views/CustomControl.xaml -->
<UserControl x:Class="BabeMakro.Views.CustomControl">
    <Grid>
        <!-- Control layout -->
    </Grid>
</UserControl>
```

```csharp
// ViewModels/CustomControlViewModel.cs
public class CustomControlViewModel : ViewModelBase
{
    private string _customProperty;
    
    public string CustomProperty
    {
        get => _customProperty;
        set => SetProperty(ref _customProperty, value);
    }
    
    public ICommand CustomCommand { get; }
}
```

## Debugging Guide

### 1. Logging Configuration

Enable detailed logging in `config.json`:

```json
{
  "global": {
    "logLevel": "Debug",
    "enableTelemetry": true
  }
}
```

### 2. Debugging Capture Issues

**Visual Studio Debugger:**
```csharp
// Set breakpoints in capture methods
public async Task<CaptureResult> CaptureWindowAsync(WindowTarget target)
{
    var hwnd = FindWindow(target); // Breakpoint here
    var bitmap = await CaptureAsync(hwnd); // And here
    return new CaptureResult(bitmap);
}
```

**Capture Preview:**
```csharp
// Save capture to file for inspection
var result = await captureService.CaptureWindowAsync(target);
result.Bitmap.Save("debug_capture.png");
```

### 3. Probe Debugging

**Log probe evaluations:**
```csharp
protected override async Task<ProbeResult> EvaluateAsync(CaptureResult capture)
{
    var result = await base.EvaluateAsync(capture);
    _logger.LogDebug("Probe {Name}: {Result}", Name, result.IsTriggered);
    return result;
}
```

**Dry run mode:**
```json
{
  "global": {
    "dryRun": true
  }
}
```

### 4. Performance Debugging

**Monitor CPU usage:**
```csharp
// Add performance counters
private readonly PerformanceCounter _cpuCounter = new("Processor", "% Processor Time", "_Total");

public void LogPerformance()
{
    var cpu = _cpuCounter.NextValue();
    _logger.LogInformation("CPU Usage: {Usage}%", cpu);
}
```

**Memory profiling:**
```csharp
// Track memory usage
var memoryBefore = GC.GetTotalMemory(false);
// Perform operation
var memoryAfter = GC.GetTotalMemory(true);
var allocated = memoryAfter - memoryBefore;
```

## Performance Optimization

### 1. Capture Optimization

**Prefer WGC over other backends:**
```csharp
// Configure capture priority
var services = new ServiceCollection()
    .AddTransient<WGCCaptureService>()      // Fastest
    .AddTransient<PrintWindowCaptureService>() // Fallback
    .AddTransient<GetPixelCaptureService>();   // Legacy
```

**Region of Interest (ROI):**
```json
{
  "probes": [{
    "x": 100, "y": 100,
    "width": 50, "height": 50  // Only capture this region
  }]
}
```

### 2. Threading Optimization

**Use async/await properly:**
```csharp
// Good - non-blocking
public async Task ProcessMultipleWindowsAsync(IEnumerable<WindowTarget> targets)
{
    var tasks = targets.Select(ProcessWindowAsync);
    await Task.WhenAll(tasks);
}

// Bad - blocking
public void ProcessMultipleWindows(IEnumerable<WindowTarget> targets)
{
    foreach (var target in targets)
    {
        ProcessWindowAsync(target).Wait(); // Blocks thread
    }
}
```

**Configure thread pool:**
```csharp
// Optimize for high-frequency operations
ThreadPool.SetMinThreads(Environment.ProcessorCount * 2, Environment.ProcessorCount);
```

### 3. Memory Management

**Dispose capture results:**
```csharp
using var capture = await captureService.CaptureWindowAsync(target);
var result = probe.Evaluate(capture);
// Bitmap automatically disposed
```

**Pool objects when possible:**
```csharp
private readonly ObjectPool<StringBuilder> _stringBuilderPool;

public string FormatLog(string message)
{
    var sb = _stringBuilderPool.Get();
    try
    {
        sb.AppendLine(message);
        return sb.ToString();
    }
    finally
    {
        _stringBuilderPool.Return(sb);
    }
}
```

### 4. Configuration Optimization

**Cache frequently accessed data:**
```csharp
private readonly ConcurrentDictionary<string, CompiledRegex> _regexCache = new();

public bool MatchesTitle(string title, string pattern)
{
    var regex = _regexCache.GetOrAdd(pattern, p => new Regex(p, RegexOptions.Compiled));
    return regex.IsMatch(title);
}
```

**Lazy load heavy resources:**
```csharp
private readonly Lazy<ExpensiveResource> _resource = new(() => new ExpensiveResource());

public void UseResource()
{
    var resource = _resource.Value; // Only created when first accessed
}
```

## Best Practices Summary

### Development

1. **Follow SOLID principles** - Single responsibility, open/closed, etc.
2. **Use dependency injection** - Easier testing and maintenance
3. **Write testable code** - Small, focused methods
4. **Handle errors gracefully** - User-friendly error messages
5. **Document public APIs** - XML documentation comments

### Performance

1. **Profile before optimizing** - Measure actual bottlenecks
2. **Prefer async operations** - Don't block UI thread
3. **Cache expensive calculations** - Avoid repeated work
4. **Use appropriate data structures** - Dictionary vs List performance
5. **Monitor resource usage** - Memory leaks and CPU spikes

### Security

1. **Validate all inputs** - Prevent injection attacks
2. **Use secure defaults** - Minimal permissions
3. **Log security events** - Audit trail for issues
4. **Keep dependencies updated** - Security patches
5. **Review sensitive operations** - Careful with automation permissions