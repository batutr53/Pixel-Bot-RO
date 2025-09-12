# BabeMakro Specification Document

## Project Overview
BabeMakro is a Windows automation tool for multi-client game management with pixel monitoring and automated actions.

## Core Features

### 1. Multi-Client Management
- **8 Independent Clients**: Each client runs in a separate tab
- **Auto-Assignment**: Automatically detects and assigns MuMu Player windows (prest121-prest128)
- **Fixed Layout**: 1400x900px non-resizable window

### 2. Main Tab Features

#### HP/MP Monitoring
- **Point-based Probes**: Monitor specific pixel coordinates for color changes
- **Percentage-based Bars**: Monitor HP/MP bars with start/end coordinates
- **Threshold Triggers**: Activate actions when HP/MP falls below specified percentages
- **Color Detection**: RGB color matching with tolerance settings

#### Periodic Clicks
- **Y Click**: Default periodic action (120ms interval)
- **Extra 1-3**: Additional periodic actions with custom intervals
- **Coordinate/Key Mode**: Support for both mouse clicks and keyboard inputs

#### BabeBot Style System
- **HP Bar**: startX, endX, Y coordinates for bar monitoring
- **MP Bar**: Separate bar monitoring for mana/MP
- **Auto-calibration**: Calibrate button to auto-detect bar colors
- **Potion Triggers**: Automatic potion use at thresholds

### 3. Party Heal Tab
- **8 Party Members**: Monitor and heal up to 8 party members
- **Individual Controls**: 
  - User Key (party member selection)
  - Skill Key (heal skill activation)
  - HP Threshold (trigger percentage)
  - Enable/Disable toggle
  - Monitor status
  - Calibrate function
- **Master Controls**: Calibrate All, Enable All buttons

### 4. Settings Tab

#### Anti-Captcha System
- **OCR Integration**: Tesseract-based text recognition
- **Auto-Pause**: Stops client during captcha detection
- **Auto-Resume**: Resumes after captcha resolution
- **Preview/Test**: Real-time preview of captcha area
- **Retry Logic**: Automatic retry until successful

#### Attack/Skills System
- **Skill Rotation**: Up to 10 configurable skills
- **Priority System**: Skills execute in priority order
- **Cooldown Management**: Individual cooldown per skill
- **Key Binding**: Custom key for each skill

#### Buff/AC System
- **Sequential Processing**: Buffs applied in order
- **Timer-based**: Automatic rebuff at intervals
- **Group Buffs**: Apply to all party members
- **AC (Armor Class) Management**: Maintain defensive buffs

### 5. Master Control Panel
- **Panic Start/Stop**: Emergency control buttons
- **Master Attack**: Start all attack rotations
- **Master Heal/Buff**: Start all healing/buffing
- **Profile Management**: Save/Load configurations
- **Overlay Mode**: Transparent overlay option

## Technical Architecture

### Frontend (WPF)
- **MVVM Pattern**: Clean separation of concerns
- **Tab-based UI**: Each client in separate TabItem
- **Data Binding**: Two-way binding for all controls
- **Custom Controls**: ClientCard UserControl

### Backend (C#/.NET 8)
- **Multi-threading**: One worker thread per client
- **High Performance**: 
  - Multi-core optimization (24 cores)
  - Low latency GC
  - High priority process
- **Screen Capture**: Multiple backends (WGC, PrintWindow, GetPixel)
- **Input Simulation**: SendMessage/PostMessage for background operation

### Configuration System
- **JSON-based**: Human-readable configuration files
- **Profile Support**: Multiple profiles for different scenarios
- **Hot-reload**: Apply changes without restart
- **Auto-save**: Periodic configuration backup

## Performance Specifications
- **Target FPS**: 60-120 Hz monitoring
- **CPU Usage**: < 5% per client
- **Memory**: < 100MB per client
- **Latency**: < 10ms response time
- **DPI Aware**: Per-MonitorV2 support

## File Structure
```
E:\RO\
├── src/
│   ├── BabeMakro/          # Main WPF application
│   ├── Core/               # Core interfaces and models
│   ├── Capture.Win/        # Windows capture implementations
│   └── Host.Console/       # Console host (deprecated)
├── config.json             # Main configuration
├── config_backup.json      # Automatic backup
├── CLAUDE.md              # AI assistant instructions
└── SPECIFICATION.md       # This file
```

## Configuration Example
```json
{
  "profiles": {
    "Test": {
      "windows": [{
        "probes": [{
          "x": 150, "y": 67,
          "refColor": [255, 0, 0],
          "tolerance": 70
        }],
        "periodicClicks": [{
          "name": "Y",
          "x": 540, "y": 760,
          "periodMs": 120,
          "enabled": true
        }]
      }]
    }
  }
}
```

## Current Status
- ✅ Multi-client tabs implemented
- ✅ Auto-assignment working
- ✅ HP/MP monitoring functional
- ✅ Party Heal system complete
- ✅ Anti-Captcha integrated
- ✅ Attack/Skills system ready
- ✅ Buff/AC system operational
- ✅ Master controls active
- ✅ Performance optimizations enabled

## Known Issues
- UI coordinates may not update visually (backend works)
- Some warning messages in debug console
- Config migration needed when switching UI layouts

## Future Enhancements
- [ ] Visual coordinate picker
- [ ] Macro recording/playback
- [ ] Cloud profile sync
- [ ] Plugin system
- [ ] Mobile companion app