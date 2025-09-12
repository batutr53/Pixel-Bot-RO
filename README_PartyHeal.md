# PartyHeal Module

## Overview

PartyHeal is a high-performance, automated healing system for 8-member parties. It monitors HP bars using pixel color detection and automatically executes healing sequences when party members fall below configured thresholds.

## Features

- **8-Member Party Support**: Monitor and heal up to 8 party members simultaneously
- **Color-Based HP Detection**: Uses baseline color calibration for accurate HP monitoring
- **Configurable Thresholds**: Set custom HP percentage thresholds per member
- **Priority-Based Healing**: Automatically prioritizes members with lower HP
- **Preemption Support**: Can interrupt current healing to prioritize critically low members
- **Humanized Input**: Randomized delays to simulate human behavior
- **Cooldown Management**: Prevents spam healing with configurable rearm times

## Installation

1. Build the solution:
   ```bash
   dotnet build -c Release
   ```

2. Run tests to verify functionality:
   ```bash
   dotnet test tests/PartyHealTests/
   ```

## Configuration

### Global Settings

- **Heal Skill Key**: The key to press for healing (default: F1)
- **Poll Interval**: How often to check HP in milliseconds (default: 10ms)
- **Animation Delay**: Time to wait after healing before next action (default: 1500ms)
- **Color Tolerance**: RGB distance threshold for HP detection (default: 25)
- **Baseline Color**: The expected color of full HP bars (default: Red #FF0000)
- **Humanize Delays**: Random delay range between key presses (20-60ms)
- **Action Spacing**: Minimum time between heal actions (default: 90ms)

### Member Settings

Each party member can be configured with:

- **Enable**: Whether to monitor this member
- **Select Key**: Key to press to target the member (Q, W, E, R, T, Y, U, I)
- **Threshold %**: HP percentage that triggers healing (default: 50%)
- **HP Bar Coordinates**: X Start, X Stop, Y position of the HP bar
- **Rearm Time**: Cooldown after healing before member can be healed again (default: 500ms)

## Calibration Process

1. **Position Setup**: Configure each member's HP bar coordinates (XStart, XStop, Y)
2. **Threshold Calculation**: The system calculates the threshold pixel as:
   ```
   ThresholdX = XStart + floor((XStop - XStart) * (ThresholdPercent / 100))
   ```
3. **Baseline Color**: Set the baseline HP color in the "Baseline Color (Hex)" field
4. **Optional Per-Member Calibration**: Use the "Calibrate" button to sample the actual color at the threshold pixel

## Usage

### Basic Setup

1. Open the PartyHeal tab (PH) in the main interface
2. Configure global settings (skill key, polling rate, etc.)
3. For each party member:
   - Check "Enable" 
   - Set HP bar coordinates (XStart, XStop, Y)
   - Adjust threshold percentage if needed
   - Optionally click "Calibrate" to sample the baseline color
4. Click "Start" to begin monitoring

### Advanced Configuration

**Color Tolerance**: Higher values make detection less sensitive but more forgiving to lighting changes. Lower values are more accurate but may miss HP changes due to anti-aliasing.

**Preemption**: When enabled, a member with significantly lower HP (>10 RGB distance difference) will interrupt healing of another member.

**Poll Interval**: Lower values (5-10ms) provide faster response but higher CPU usage. Higher values (15-25ms) reduce CPU load but slower response.

## File Structure

```
src/Core/
├── Interfaces/IPartyHealService.cs          # Main service interface
├── Models/PartyHealConfig.cs                # Configuration models
└── Services/PartyHealService.cs             # Core healing logic

src/BabeMakro/
├── ViewModels/PartyHealViewModel.cs         # UI binding logic
├── Controls/PartyHealControl.xaml(.cs)      # Main UI control
└── Converters/BoolToStatusConverter.cs     # UI converters

tests/PartyHealTests/
├── PartyHealServiceTests.cs                # Unit tests
└── PartyHealIntegrationTests.cs            # Integration tests
```

## Algorithm Details

### HP Detection Logic

1. **Pixel Sampling**: For each enabled member, sample the color at the threshold pixel
2. **Color Distance**: Calculate RGB distance from baseline color using Euclidean distance:
   ```csharp
   distance = sqrt((r1-r2)² + (g1-g2)² + (b1-b2)²)
   ```
3. **Threshold Check**: If distance > tolerance, consider HP below threshold
4. **Priority Calculation**: Higher color distance = lower HP = higher priority

### Healing Sequence

1. **Target Selection**: Choose highest priority member (lowest HP)
2. **Preemption Check**: If preemption enabled, check if another member has significantly lower HP
3. **Key Sequence**:
   - Press member's select key
   - Random humanize delay (20-60ms)
   - Press heal skill key
4. **Cooldown Management**: Set rearm timer for the healed member
5. **Animation Delay**: Wait for heal animation to complete

## Performance Considerations

- **CPU Usage**: Designed for minimal impact with optimized pixel sampling
- **Memory Management**: Uses object pooling where possible
- **Thread Safety**: All operations are thread-safe for concurrent access
- **Error Resilience**: Graceful handling of capture failures and window focus issues

## Troubleshooting

### Common Issues

**Heals not triggering**: 
- Check that HP bar coordinates are correct
- Verify baseline color matches actual HP bar color
- Increase color tolerance if bars have anti-aliasing

**Too many heals**:
- Increase rearm time for the affected member
- Decrease polling frequency
- Check animation delay is sufficient

**Wrong member prioritization**:
- Verify HP bar coordinates don't overlap
- Check threshold percentages are set correctly
- Enable debug logging to see color distance values

### Debug Information

Enable debug logging to see:
- Color distance calculations for each member
- Healing decisions and priority rankings
- Performance statistics and timing information

## Testing

Run the test suite to verify functionality:

```bash
# Unit tests
dotnet test tests/PartyHealTests/PartyHealServiceTests.cs

# Integration tests  
dotnet test tests/PartyHealTests/PartyHealIntegrationTests.cs

# All tests
dotnet test tests/PartyHealTests/
```

The test suite includes:
- Configuration validation
- HP detection logic
- Priority calculation algorithms
- Healing sequence timing
- Error handling scenarios