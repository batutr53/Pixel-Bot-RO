using System.Drawing;
using Microsoft.Extensions.Logging;
using PixelAutomation.Core.Interfaces;
using PixelAutomation.Core.Models;

namespace PixelAutomation.Core.Services;

public class CaptchaService : IDisposable
{
    private readonly ILogger<CaptchaService> _logger;
    private readonly ICaptchaSolver _captchaSolver;
    private readonly ICaptchaDetector _captchaDetector;
    private readonly IClickProvider _clickProvider;
    private readonly IEventBus _eventBus;
    
    private CaptchaConfig _config = new();
    private Timer? _detectionTimer;
    private IntPtr _targetWindow;
    private bool _isRunning;
    private bool _isProcessingCaptcha;

    public event EventHandler<CaptchaDetectionEvent>? CaptchaDetected;
    public event EventHandler<CaptchaSolveEvent>? CaptchaSolved;

    public CaptchaService(
        ILogger<CaptchaService> logger,
        ICaptchaSolver captchaSolver,
        ICaptchaDetector captchaDetector,
        IClickProvider clickProvider,
        IEventBus eventBus)
    {
        _logger = logger;
        _captchaSolver = captchaSolver;
        _captchaDetector = captchaDetector;
        _clickProvider = clickProvider;
        _eventBus = eventBus;
    }

    public async Task<bool> InitializeAsync(CaptchaConfig config, IntPtr targetWindow)
    {
        _config = config;
        _targetWindow = targetWindow;

        if (!_config.Enabled)
        {
            _logger.LogInformation("Captcha service disabled in configuration");
            return true;
        }

        try
        {
            var initialized = await _captchaSolver.InitializeAsync();
            if (!initialized)
            {
                _logger.LogError("Failed to initialize captcha solver");
                return false;
            }

            _logger.LogInformation("Captcha service initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing captcha service");
            return false;
        }
    }

    public void StartMonitoring()
    {
        if (!_config.Enabled || _isRunning)
            return;

        _isRunning = true;
        _detectionTimer = new Timer(CheckForCaptcha, null, TimeSpan.Zero, 
            TimeSpan.FromMilliseconds(_config.DetectionIntervalMs));
        
        _logger.LogInformation("Captcha monitoring started (interval: {IntervalMs}ms)", 
            _config.DetectionIntervalMs);
    }

    public void StopMonitoring()
    {
        _isRunning = false;
        _detectionTimer?.Dispose();
        _detectionTimer = null;
        
        _logger.LogInformation("Captcha monitoring stopped");
    }

    private async void CheckForCaptcha(object? state)
    {
        if (_isProcessingCaptcha || !_isRunning)
            return;

        try
        {
            using var screenshot = await CaptureScreenshotAsync();
            if (screenshot == null)
                return;

            var detected = await _captchaDetector.DetectCaptchaAsync(screenshot, _config.CaptchaArea);
            if (detected)
            {
                _logger.LogWarning("Captcha detected! Starting solve process...");
                await ProcessDetectedCaptcha(screenshot);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during captcha detection");
        }
    }

    private async Task ProcessDetectedCaptcha(Bitmap screenshot)
    {
        if (_isProcessingCaptcha)
            return;

        _isProcessingCaptcha = true;
        var startTime = DateTime.UtcNow;

        try
        {
            // Notify that captcha was detected
            var detectionEvent = new CaptchaDetectionEvent
            {
                DetectedArea = _config.CaptchaArea,
                Confidence = 0.9,
                DetectionMethod = _captchaDetector.Name
            };
            
            CaptchaDetected?.Invoke(this, detectionEvent);
            _eventBus.Publish(new CaptchaDetectedEvent
            {
                WindowId = _targetWindow.ToString(),
                Area = _config.CaptchaArea,
                Timestamp = DateTime.UtcNow
            });

            // Extract captcha area
            using var captchaImage = ExtractCaptchaArea(screenshot);
            if (captchaImage == null)
            {
                _logger.LogError("Failed to extract captcha area from screenshot");
                return;
            }

            // Solve captcha
            var solveResult = await _captchaSolver.SolveCaptchaAsync(captchaImage, _config.ProcessingOptions);
            
            var solveTime = DateTime.UtcNow - startTime;
            var solveEvent = new CaptchaSolveEvent
            {
                SolvedText = solveResult,
                Success = !string.IsNullOrEmpty(solveResult),
                SolveTime = solveTime
            };

            if (!string.IsNullOrEmpty(solveResult))
            {
                _logger.LogInformation("Captcha solved successfully: '{Text}' (took {Ms}ms)", 
                    solveResult, solveTime.TotalMilliseconds);
                
                await SubmitCaptchaSolution(solveResult);
                
                // Publish success event
                _eventBus.Publish(new CaptchaSolvedEvent
                {
                    WindowId = _targetWindow.ToString(),
                    SolvedText = solveResult,
                    Success = true,
                    SolveTime = solveTime,
                    Timestamp = DateTime.UtcNow
                });
            }
            else
            {
                _logger.LogWarning("Failed to solve captcha after {Ms}ms", solveTime.TotalMilliseconds);
                solveEvent = new CaptchaSolveEvent
                {
                    SolvedText = solveResult,
                    Success = false,
                    SolveTime = solveTime,
                    ErrorMessage = "OCR failed to extract text"
                };
            }

            CaptchaSolved?.Invoke(this, solveEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing detected captcha");
            
            var errorEvent = new CaptchaSolveEvent
            {
                Success = false,
                SolveTime = DateTime.UtcNow - startTime,
                ErrorMessage = ex.Message
            };
            
            CaptchaSolved?.Invoke(this, errorEvent);
        }
        finally
        {
            _isProcessingCaptcha = false;
        }
    }

    private async Task<Bitmap?> CaptureScreenshotAsync()
    {
        try
        {
            // This would use the existing capture backend
            // For now, return null as placeholder
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture screenshot");
            return null;
        }
    }

    private Bitmap? ExtractCaptchaArea(Bitmap screenshot)
    {
        try
        {
            var area = _config.CaptchaArea;
            if (area.Width <= 0 || area.Height <= 0)
                return null;

            if (area.Right > screenshot.Width || area.Bottom > screenshot.Height)
            {
                _logger.LogWarning("Captcha area extends beyond screenshot bounds");
                return null;
            }

            var captchaImage = new Bitmap(area.Width, area.Height);
            using var g = Graphics.FromImage(captchaImage);
            g.DrawImage(screenshot, 0, 0, area, GraphicsUnit.Pixel);
            
            return captchaImage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract captcha area");
            return null;
        }
    }

    private async Task SubmitCaptchaSolution(string solution)
    {
        try
        {
            // Click on text box
            await _clickProvider.ClickAsync(_targetWindow, _config.TextBoxLocation);
            await Task.Delay(100);

            // Clear any existing text and input solution
            // This would need to be implemented with proper text input
            _logger.LogDebug("Inputting captcha solution: '{Solution}'", solution);
            
            // Click submit button
            await Task.Delay(200);
            await _clickProvider.ClickAsync(_targetWindow, _config.SubmitButtonLocation);
            
            _logger.LogInformation("Captcha solution submitted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit captcha solution");
            throw;
        }
    }

    public void UpdateConfig(CaptchaConfig config)
    {
        _config = config;
        _logger.LogInformation("Captcha configuration updated");
        
        if (_isRunning && _detectionTimer != null)
        {
            _detectionTimer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(_config.DetectionIntervalMs));
        }
    }

    public CaptchaStats GetStats()
    {
        return new CaptchaStats
        {
            IsRunning = _isRunning,
            IsProcessing = _isProcessingCaptcha,
            SolverName = _captchaSolver.Name,
            DetectorName = _captchaDetector.Name,
            Config = _config
        };
    }

    public void Dispose()
    {
        StopMonitoring();
        _captchaSolver?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class CaptchaStats
{
    public bool IsRunning { get; init; }
    public bool IsProcessing { get; init; }
    public string SolverName { get; init; } = "";
    public string DetectorName { get; init; } = "";
    public CaptchaConfig Config { get; init; } = new();
}

// Event classes for the EventBus
public class CaptchaDetectedEvent
{
    public string WindowId { get; init; } = "";
    public Rectangle Area { get; init; }
    public DateTime Timestamp { get; init; }
}

public class CaptchaSolvedEvent
{
    public string WindowId { get; init; } = "";
    public string? SolvedText { get; init; }
    public bool Success { get; init; }
    public TimeSpan SolveTime { get; init; }
    public DateTime Timestamp { get; init; }
}