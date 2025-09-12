# Anti-Captcha System for PixelAutomation

## Overview

This implementation provides a complete anti-captcha system ported from the Python BabeMakro project to .NET. The system can automatically detect, solve, and submit captcha challenges in game windows.

## Features

### 🔍 **Captcha Detection**
- **Color-based Detection**: Identifies captcha areas by analyzing color patterns typical of captcha backgrounds
- **Rectangular Pattern Detection**: Finds captcha-like rectangular regions with specific dimensions
- **Contrast Analysis**: Detects high-contrast areas that typically contain text
- **Template Matching**: Support for predefined captcha templates

### 🧠 **OCR Engine**
- **Tesseract Integration**: Uses Tesseract OCR for text recognition
- **Multiple Processing Modes**: 
  - Original image processing
  - Enhanced image processing with filters
  - Multiple attempt processing with different parameters
- **Image Enhancement**:
  - Contrast adjustment (1.0-5.0x)
  - Sharpness enhancement
  - Brightness correction
  - Scaling (1-8x)
  - Grayscale conversion
  - Histogram equalization

### ⚡ **Automatic Solving**
- **Real-time Monitoring**: Continuously monitors specified screen areas
- **Auto-submission**: Automatically clicks textbox, enters solution, and submits
- **Bot Integration**: Pauses/resumes other automation when captcha is detected
- **Configurable Intervals**: Adjustable detection frequency

## Installation

### Prerequisites
1. **Tesseract OCR**: Download and install from [GitHub Tesseract](https://github.com/UB-Mannheim/tesseract/wiki)
   - Install to `C:\Program Files\Tesseract-OCR\`
   - Or add to system PATH

### Configuration
Add captcha configuration to your `config.json`:

```json
{
  "windows": [
    {
      "captchaConfig": {
        "enabled": true,
        "captchaArea": {
          "X": 400,
          "Y": 350, 
          "Width": 300,
          "Height": 100
        },
        "textBoxLocation": { "X": 700, "Y": 460 },
        "submitButtonLocation": { "X": 700, "Y": 490 },
        "detectionIntervalMs": 5000,
        "processingOptions": {
          "processingMode": "Enhanced",
          "contrastFactor": 3.5,
          "sharpnessFactor": 3.0,
          "scaleFactor": 4
        }
      }
    }
  ]
}
```

## Architecture

### Core Components

1. **ICaptchaSolver**: Interface for OCR engines
   - `TesseractCaptchaSolver`: Tesseract implementation

2. **ICaptchaDetector**: Interface for captcha detection
   - `ColorBasedCaptchaDetector`: Multi-method detection

3. **CaptchaService**: Main orchestration service
   - Monitors screen areas
   - Coordinates detection and solving
   - Handles bot pause/resume

### Processing Pipeline

```
Screen Capture → Detection → Image Enhancement → OCR → Auto-Submit
      ↓              ↓            ↓              ↓         ↓
  PrintWindow/  Color/Pattern  Contrast/Scale/ Tesseract  SendText/
  WGC/GetPixel  Recognition   Sharpen/Filter   Processing Click Submit
```

### Event System

The system publishes events through the existing `IEventBus`:

- `CaptchaDetectedEvent`: When captcha is found
- `CaptchaSolvedEvent`: When solution is found and submitted

## Usage Examples

### Basic Usage
```csharp
// Register services
services.AddCaptchaServices();

// Configure in WindowWorker
var captchaService = serviceProvider.GetService<CaptchaService>();
var worker = new WindowWorker(
    workerId, hwnd, config, captureBackend, 
    clickProvider, scheduler, eventBus, logger, 
    globalConfig, captchaService);
```

### Advanced Configuration
```csharp
services.AddCaptchaServices(options =>
{
    options.TesseractPath = @"C:\Program Files\Tesseract-OCR\tesseract.exe";
    options.DefaultTimeout = 30000;
    options.DefaultProcessingOptions.ContrastFactor = 4.0;
    options.DefaultProcessingOptions.ScaleFactor = 6;
});
```

## Performance Characteristics

- **Detection Speed**: ~100-200ms per screen analysis
- **OCR Processing**: 500ms-2s depending on image complexity and enhancement settings
- **Memory Usage**: ~50-100MB additional for image processing
- **CPU Impact**: Moderate during captcha solving, minimal during monitoring

## Supported Captcha Types

### ✅ **Working Well**
- Simple text captchas (letters/numbers)
- High contrast black/white text
- Clear fonts without distortion
- Mathematical expressions (basic)

### ⚠️ **Partial Support**  
- Slightly distorted text
- Colored backgrounds
- Simple noise patterns

### ❌ **Not Supported**
- Heavy visual distortions
- Complex background patterns
- Image-based captchas (select objects)
- Audio captchas

## Configuration Options

### CaptchaConfig Properties
- `Enabled`: Enable/disable captcha detection
- `CaptchaArea`: Screen rectangle to monitor for captchas
- `TextBoxLocation`: Where to click to input text
- `SubmitButtonLocation`: Where to click to submit
- `DetectionIntervalMs`: How often to check for captchas (default: 5000ms)
- `SolveTimeoutMs`: Max time to spend solving (default: 30000ms)

### CaptchaOptions (Image Processing)
- `ProcessingMode`: Original, Enhanced, or MultipleAttempts
- `PsmMode`: Tesseract Page Segmentation Mode
- `ContrastFactor`: Image contrast multiplier (1.0-5.0)
- `SharpnessFactor`: Sharpening intensity (1.0-5.0)  
- `BrightnessFactor`: Brightness adjustment (0.5-2.0)
- `ScaleFactor`: Image scaling (1-8x)
- `UseGrayscale`: Convert to grayscale before OCR
- `UseHistogramEqualization`: Apply histogram equalization

## Troubleshooting

### Common Issues

1. **Tesseract Not Found**
   - Install Tesseract OCR
   - Add to PATH or specify full path in config

2. **Poor OCR Accuracy**
   - Adjust `ContrastFactor` (try 2.5-4.0)
   - Increase `ScaleFactor` (try 4-6x)
   - Enable `UseGrayscale` and `UseHistogramEqualization`

3. **Captcha Not Detected**
   - Verify `CaptchaArea` coordinates are correct
   - Check if captcha appears in the specified region
   - Lower detection interval for faster checking

4. **Wrong Click Locations**
   - Update `TextBoxLocation` and `SubmitButtonLocation` coordinates
   - Use screen coordinate tools to get precise positions

### Debug Tips

1. **Enable Debug Logging**:
   ```json
   {
     "global": {
       "logLevel": "Debug"
     }
   }
   ```

2. **Test Coordinates**: Use the overlay tool to verify captcha area and click positions

3. **Manual Testing**: Start with `"dryRun": true` to see detection without actual clicking

## Integration with Existing System

The captcha system integrates seamlessly with the existing PixelAutomation architecture:

- **WindowWorker**: Each worker can have its own captcha service
- **EventBus**: Publishes captcha events for monitoring
- **Configuration**: Uses existing JSON config system  
- **Logging**: Uses existing Serilog setup
- **Dependency Injection**: Follows existing DI patterns

## Future Enhancements

- [ ] Machine learning-based captcha detection
- [ ] Support for more OCR engines (EasyOCR, Azure OCR)
- [ ] Advanced image preprocessing filters
- [ ] Captcha template learning system
- [ ] Multi-language OCR support
- [ ] Performance optimizations for real-time processing

## Legal Notice

This captcha system is intended for legitimate automation of owned applications and testing purposes only. Users are responsible for complying with terms of service of any applications they automate.