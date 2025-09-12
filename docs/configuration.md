# ⚙️ Configuration Guide

> Complete guide for configuring BabeMakro for your specific game setup and monitoring needs.

## 📋 Table of Contents

- [Configuration File Structure](#configuration-file-structure)
- [Profile Management](#profile-management)
- [Window Configuration](#window-configuration)
- [Probe Setup](#probe-setup)
- [Event Configuration](#event-configuration)
- [Periodic Clicks](#periodic-clicks)
- [Global Settings](#global-settings)
- [Hotkey Configuration](#hotkey-configuration)

## Configuration File Structure

BabeMakro uses `config.json` in the root directory for all configuration:

```json
{
  "profiles": {
    "ProfileName": {
      "global": { /* Global settings */ },
      "windows": [ /* Array of window configs */ ],
      "hotkeys": { /* Hotkey definitions */ }
    }
  },
  "hotkeySets": { /* Named hotkey sets */ },
  "activeHotkeys": "Default"
}
```

## Profile Management

### Creating a New Profile

1. Copy an existing profile in `config.json`
2. Rename the profile key
3. Modify settings as needed
4. Save the file

Example:
```json
{
  "profiles": {
    "MyGameSetup": {
      "global": {
        "captureMode": "WGC",
        "defaultHz": 80
      }
    }
  }
}
```

### Profile Selection

- Use CLI: `dotnet run --project src/Host.Console -- --profile MyGameSetup`
- Use WPF: Select from dropdown in the application

## Window Configuration

### Basic Window Setup

```json
{
  "titleRegex": "Client 1",
  "hwndString": null,
  "processName": "MuMu",
  "dpiAware": true
}
```

**Parameters:**
- `titleRegex`: Window title pattern to match
- `hwndString`: Specific window handle (optional)
- `processName`: Process name to target (optional)
- `dpiAware`: Enable DPI scaling awareness

### Auto-Assignment

BabeMakro can automatically detect MuMu Player instances:
- Searches for windows matching `prest121` through `prest128`
- Assigns to clients 1-8 automatically
- No manual window configuration needed

## Probe Setup

### Point Probes

Monitor specific pixel coordinates for color changes:

```json
{
  "name": "HP1",
  "kind": "point",
  "x": 150,
  "y": 67,
  "box": 5,
  "mode": "edge",
  "metric": "rgb",
  "refColor": [255, 0, 0],
  "toColor": [0, 0, 0],
  "tolerance": 70,
  "debounceMs": 30
}
```

**Parameters:**
- `x`, `y`: Pixel coordinates to monitor
- `box`: Size of sampling area around the point
- `mode`: Detection mode (`edge` or `level`)
- `refColor`: Expected color when condition is true
- `toColor`: Color when condition changes (edge mode)
- `tolerance`: Color matching tolerance (0-255)
- `debounceMs`: Minimum time between triggers

### Percentage Probes (BabeBot Style)

Monitor HP/MP bars as percentage:

```json
{
  "name": "BabeBotHP1",
  "type": "HP",
  "startX": 1,
  "endX": 90,
  "y": 12,
  "monitorPercentage": 30,
  "expectedColor": [255, 0, 0],
  "tolerance": 30
}
```

**Parameters:**
- `startX`, `endX`: Bar start and end coordinates
- `y`: Vertical position of the bar
- `monitorPercentage`: Trigger when below this percentage
- `expectedColor`: Color of the filled portion
- `emptyColor`: Color of empty portion (optional)

### Rectangle Probes

Monitor larger areas:

```json
{
  "name": "CaptchaArea",
  "kind": "rectangle",
  "x": 100,
  "y": 100,
  "width": 200,
  "height": 50,
  "mode": "level",
  "metric": "rgb"
}
```

## Event Configuration

Events trigger actions when probe conditions are met:

```json
{
  "when": "HP1:edge-down",
  "click": {
    "x": 400,
    "y": 300
  },
  "key": "F1",
  "cooldownMs": 120,
  "priority": 1
}
```

**Event Triggers:**
- `edge-down`: Probe transitions from true to false
- `edge-up`: Probe transitions from false to true  
- `level-true`: Probe is currently true
- `level-false`: Probe is currently false

**Actions:**
- `click`: Mouse click at coordinates
- `key`: Keyboard key press
- `cooldownMs`: Minimum time between actions
- `priority`: Execution priority (lower = higher priority)

## Periodic Clicks

Automatic clicking at regular intervals:

```json
{
  "name": "Y",
  "x": 540,
  "y": 760,
  "periodMs": 120,
  "enabled": true,
  "key": "y"
}
```

**Parameters:**
- `x`, `y`: Click coordinates (if using mouse)
- `key`: Keyboard key to press (if using keyboard)
- `periodMs`: Interval between actions in milliseconds
- `enabled`: Whether this action is active

## Global Settings

Configure capture and performance options:

```json
{
  "captureMode": "WGC",
  "clickMode": "message",
  "defaultHz": 80,
  "logLevel": "Info",
  "enableTelemetry": true,
  "dryRun": false
}
```

**Capture Modes:**
- `WGC`: Windows Graphics Capture (fastest, Windows 10+)
- `PrintWindow`: Print Window API (compatible)
- `GetPixel`: Direct pixel access (slowest, most compatible)

**Click Modes:**
- `message`: Send window messages (works in background)
- `input`: Simulate hardware input (requires foreground)

**Log Levels:**
- `Trace`: Everything
- `Debug`: Detailed information
- `Info`: General information
- `Warning`: Important issues
- `Error`: Critical errors
- `Fatal`: Application crashes

## Hotkey Configuration

### Global Hotkeys

```json
{
  "hotkeys": {
    "emergencyStop": {
      "key": "F12",
      "modifiers": ["Ctrl"],
      "action": "stopAll"
    }
  }
}
```

### Hotkey Sets

Create different hotkey profiles:

```json
{
  "hotkeySets": {
    "Streamer": {
      "muteAll": {
        "key": "F9",
        "action": "toggleMute"
      }
    }
  }
}
```

## Best Practices

### Performance Optimization

1. **Use WGC capture mode** for best performance
2. **Set appropriate Hz** (60-120 for most games)
3. **Minimize probe count** - only monitor what you need
4. **Use larger tolerance values** to reduce false triggers
5. **Set proper debounce times** to avoid spam

### Reliability Tips

1. **Test probes thoroughly** before production use
2. **Use calibration features** to auto-detect colors
3. **Set backup triggers** for critical actions
4. **Monitor logs** for any detection issues
5. **Keep configs backed up** before major changes

### Multi-Client Setup

1. **Use consistent naming** (Client 1, Client 2, etc.)
2. **Configure identical probes** across all clients
3. **Stagger periodic actions** to avoid conflicts
4. **Test one client first** then copy to others
5. **Use profile inheritance** for common settings

## Troubleshooting

### Common Issues

**Probes not triggering:**
- Check coordinates are correct for your resolution
- Verify colors match with calibration
- Increase tolerance if colors vary slightly
- Check DPI scaling settings

**High CPU usage:**
- Reduce monitoring frequency (lower Hz)
- Use fewer probes per window
- Switch to WGC capture mode
- Close unnecessary background applications

**Actions not working:**
- Verify window is detected correctly
- Check click coordinates
- Try different click modes
- Ensure proper permissions

### Debug Mode

Enable detailed logging:
```json
{
  "global": {
    "logLevel": "Debug",
    "dryRun": true
  }
}
```

This will log all actions without executing them, perfect for testing configurations.