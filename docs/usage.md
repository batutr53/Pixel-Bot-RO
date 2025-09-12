# 🎮 Usage Guide

> Step-by-step guide for using BabeMakro effectively for game automation and monitoring.

## 📋 Table of Contents

- [Quick Start](#quick-start)
- [Interface Overview](#interface-overview)
- [Setting Up Your First Client](#setting-up-your-first-client)
- [Main Tab Features](#main-tab-features)
- [Party Heal System](#party-heal-system)
- [Settings Tab](#settings-tab)
- [Master Controls](#master-controls)
- [Common Workflows](#common-workflows)
- [Tips & Best Practices](#tips--best-practices)

## Quick Start

### 1. Initial Setup

1. **Launch the application:**
   ```bash
   dotnet run --project src/BabeMakro
   ```

2. **Verify window detection:**
   - Application auto-detects MuMu Player windows (prest121-prest128)
   - Each client appears in its own tab (👸 Client 1, 👸 Client 2, etc.)

3. **Start with Client 1:**
   - Click on "👸 Client 1" tab
   - Configure basic HP/MP monitoring first

### 2. Basic HP/MP Setup

1. **Position your game window** where you can see it
2. **In Client 1 tab, locate HP/MP coordinates:**
   - Note where your HP bar appears on screen
   - Note where your MP bar appears on screen
3. **Configure point probes:**
   - Set HP1 coordinates (e.g., X: 150, Y: 67)
   - Set MP1 coordinates (e.g., X: 150, Y: 84)
4. **Test the setup:**
   - Click "▶️ Start" to begin monitoring
   - Check logs for detection events

## Interface Overview

### Main Window Layout

```
┌─────────────────────────────────────┐
│  BabeMakro - Multi-Client Manager  │
├─────────────────────────────────────┤
│ 👸 Client 1 │ 👸 Client 2 │ ... │ 8 │
├─────────────────────────────────────┤
│                                     │
│         Client Configuration        │
│                                     │
│  [Main] [Party Heal] [Settings]     │
│                                     │
│         Master Controls             │
│  [Panic Stop] [Master Attack]       │
└─────────────────────────────────────┘
```

### Tab Navigation

- **Client Tabs (1-8):** Independent configuration for each game client
- **Sub-tabs per Client:**
  - **Main:** Basic HP/MP monitoring and periodic actions
  - **Party Heal:** 8-member party healing system
  - **Settings:** Anti-captcha, attack rotations, buffs

## Setting Up Your First Client

### Step 1: Window Detection

The application automatically finds MuMu Player windows. If your window isn't detected:

1. **Check window title** - Should contain "prest121" through "prest128"
2. **Manual configuration** - Edit `config.json` if needed:
   ```json
   {
     "titleRegex": "Your Game Window Title",
     "processName": "YourGameProcess"
   }
   ```

### Step 2: HP/MP Monitoring Setup

#### Point-based Monitoring

1. **Find HP indicator pixel:**
   - Look for a pixel that changes color when HP is low
   - Note the exact coordinates (X, Y)

2. **Configure HP1 probe:**
   - Set coordinates in Main tab
   - Set reference color (e.g., red: 255, 0, 0)
   - Set tolerance (start with 70)
   - Set debounce time (30ms recommended)

3. **Test the probe:**
   - Start monitoring
   - Check if probe triggers when HP changes
   - Adjust tolerance if needed

#### Bar-based Monitoring (BabeBot Style)

1. **Find HP bar coordinates:**
   - Start X: Left edge of HP bar
   - End X: Right edge of HP bar  
   - Y: Vertical position of bar

2. **Configure BabeBotHP:**
   - Set start/end coordinates
   - Set monitor percentage (e.g., 30%)
   - Set expected color (usually red)
   - Use calibrate button to auto-detect

### Step 3: Action Configuration

#### Emergency Healing

Configure actions to trigger when HP is low:

1. **In Events section:**
   - When: "HP1:edge-down"
   - Click coordinates for heal potion/skill
   - Set cooldown (120ms recommended)

#### Periodic Actions

Set up regular clicking for basic gameplay:

1. **Y Click (default action):**
   - Coordinates for attack/pickup
   - Period: 120ms for responsive gameplay
   - Enable/disable as needed

## Main Tab Features

### HP/MP Monitoring

**Point Probes:**
- Monitor specific pixels for color changes
- Best for UI elements that change color
- Edge detection for state changes

**Percentage Probes:**
- Monitor bar fill percentage
- Auto-calculate health/mana levels
- More reliable for bar-style UI

### Periodic Clicks

**Built-in Actions:**
- **Y Click:** Primary action (attack, pickup)
- **Extra 1-3:** Additional periodic actions
- **BabeBot HP/MP:** Potion use automation

**Configuration:**
- Set coordinates for each action
- Adjust timing (periodMs)
- Enable/disable individual actions

### Event System

**Trigger Types:**
- `edge-down`: Condition becomes false
- `edge-up`: Condition becomes true
- `level-true`: Condition is currently true
- `level-false`: Condition is currently false

**Actions:**
- Mouse clicks at specified coordinates
- Keyboard key presses
- Cooldown management
- Priority-based execution

## Party Heal System

### 8-Member Party Setup

Each party member can be configured independently:

1. **User Key:** Key to select party member (F1-F8)
2. **Skill Key:** Key for heal spell (usually heal hotkey)
3. **HP Threshold:** Percentage to trigger healing
4. **Monitor Area:** Screen coordinates for member's HP

### Party Heal Workflow

1. **Configure each party member:**
   - Set selection key (F1 for member 1, etc.)
   - Set heal skill key
   - Set HP threshold (usually 50-70%)

2. **Calibrate HP positions:**
   - Use calibrate button for each member
   - System learns HP bar colors automatically

3. **Enable party healing:**
   - Check "Enable" for each member
   - Use "Enable All" for bulk activation
   - Monitor status indicators

### Master Party Controls

- **Calibrate All:** Auto-setup all 8 members
- **Enable All:** Activate all party healing
- **Individual toggles:** Fine-tune specific members

## Settings Tab

### Anti-Captcha System

**Setup:**
1. **Define captcha area** - Screen region where captchas appear
2. **Configure OCR** - Tesseract text recognition
3. **Set auto-pause** - Stop actions during captcha
4. **Test detection** - Use preview to verify

**Workflow:**
1. System detects text in captcha area
2. Automatically pauses all actions
3. Waits for manual captcha resolution
4. Resumes automation after clearance

### Attack/Skills System

**Skill Rotation Setup:**
1. **Add skills (up to 10):**
   - Set key binding for each skill
   - Set cooldown time
   - Set priority order

2. **Configure rotation:**
   - Skills execute by priority
   - Cooldown management prevents spam
   - Auto-retry failed casts

**Example Rotation:**
```
Priority 1: Main Attack (Z key, 1000ms cooldown)
Priority 2: Buff Skill (X key, 30000ms cooldown)  
Priority 3: Heal Self (C key, 5000ms cooldown)
```

### Buff/AC Management

**Buff System:**
1. **Sequential processing** - Buffs applied in order
2. **Timer-based rebuffing** - Auto-maintain buffs
3. **Group buff support** - Apply to party members
4. **AC (Armor Class) management** - Defensive buffs

## Master Controls

### Emergency Controls

- **Panic Stop:** Immediately stop all clients
- **Master Attack:** Start attack rotations on all clients
- **Master Heal:** Enable healing on all clients
- **Master Buff:** Start buff rotations

### Profile Management

- **Save:** Store current configuration
- **Load:** Restore saved settings
- **Export:** Backup configuration files
- **Import:** Restore from backup

### Overlay Mode

- **Transparent overlay** - See through application
- **Always on top** - Stay above game windows
- **Minimal interface** - Reduce visual clutter

## Common Workflows

### Single Client Gaming

1. **Configure Client 1** with basic HP/MP monitoring
2. **Set up emergency healing** for low HP situations
3. **Configure attack rotation** for combat
4. **Enable anti-captcha** for unattended play
5. **Start monitoring** and adjust as needed

### Multi-Client Management

1. **Set up Client 1** completely
2. **Copy configuration** to other clients
3. **Adjust coordinates** for each client window
4. **Configure party healing** for group play
5. **Use master controls** for synchronized actions

### Farming/Grinding Setup

1. **Minimal monitoring** - Basic HP/MP only
2. **Aggressive periodic clicks** - Fast attack intervals
3. **Auto-potion use** - Maintain resources
4. **Anti-captcha enabled** - Handle interruptions
5. **Long-term stability** - Conservative settings

### PvP/Competitive Setup

1. **Responsive monitoring** - High Hz rates
2. **Quick reactions** - Low debounce times
3. **Manual override ready** - Quick panic stop
4. **Defensive priorities** - Healing over attacking
5. **Minimal automation** - Stay within rules

## Tips & Best Practices

### Performance Optimization

1. **Start with one client** - Test thoroughly before scaling
2. **Use appropriate Hz** - 60-80 Hz for most games
3. **Minimize probe count** - Only monitor essential elements
4. **Optimize coordinates** - Precise positioning reduces CPU load
5. **Regular breaks** - Prevent system strain

### Reliability Guidelines

1. **Test all triggers** - Verify detection works correctly
2. **Set conservative timings** - Avoid overwhelming the game
3. **Use backup systems** - Multiple ways to handle emergencies
4. **Monitor logs** - Watch for detection issues
5. **Keep configs backed up** - Save working configurations

### Gaming Etiquette

1. **Respect game rules** - Stay within terms of service
2. **Avoid detection** - Use human-like timings
3. **Don't impact others** - Be considerate in multiplayer
4. **Take breaks** - Maintain reasonable play patterns
5. **Stay updated** - Adapt to game changes

### Troubleshooting Common Issues

**Probes not triggering:**
- Check coordinates match your screen resolution
- Verify colors with calibration tools
- Increase tolerance for color matching
- Check DPI scaling settings

**Actions not working:**
- Verify game window is active
- Check click coordinates are correct
- Try different click modes (message vs input)
- Ensure proper game permissions

**High CPU usage:**
- Reduce monitoring frequency
- Use fewer active probes
- Close unnecessary background apps
- Switch to more efficient capture mode

**Intermittent failures:**
- Increase debounce times
- Check for timing conflicts
- Verify stable game performance
- Monitor system resources