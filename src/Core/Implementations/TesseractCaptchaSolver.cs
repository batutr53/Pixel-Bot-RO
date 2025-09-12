using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PixelAutomation.Core.Interfaces;

namespace PixelAutomation.Core.Implementations;

public class TesseractCaptchaSolver : ICaptchaSolver
{
    private readonly ILogger<TesseractCaptchaSolver> _logger;
    private string? _tesseractPath;
    private bool _isInitialized;

    public string Name => "Tesseract OCR";
    public bool IsAvailable => _isInitialized && !string.IsNullOrEmpty(_tesseractPath);

    public TesseractCaptchaSolver(ILogger<TesseractCaptchaSolver> logger)
    {
        _logger = logger;
    }

    public async Task<bool> InitializeAsync()
    {
        try
        {
            _tesseractPath = await FindTesseractExecutableAsync();
            
            if (string.IsNullOrEmpty(_tesseractPath))
            {
                _logger.LogWarning("Tesseract executable not found. Please install Tesseract OCR");
                return false;
            }

            // Test Tesseract
            var testResult = await RunTesseractAsync("--version", null);
            if (testResult.success)
            {
                _logger.LogInformation("Tesseract OCR initialized successfully at: {Path}", _tesseractPath);
                _logger.LogDebug("Tesseract version info: {Version}", testResult.output?.Split('\n')[0]);
                _isInitialized = true;
                return true;
            }

            _logger.LogError("Tesseract test failed");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Tesseract");
            return false;
        }
    }

    public async Task<string?> SolveCaptchaAsync(Bitmap captchaImage, CaptchaOptions? options = null)
    {
        if (!IsAvailable)
        {
            _logger.LogError("Tesseract solver is not available");
            return null;
        }

        options ??= new CaptchaOptions();
        
        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Process image based on options
            using var processedImage = options.ProcessingMode switch
            {
                CaptchaProcessingMode.Original => (Bitmap)captchaImage.Clone(),
                CaptchaProcessingMode.Enhanced => EnhanceImage(captchaImage, options),
                CaptchaProcessingMode.MultipleAttempts => await SolveWithMultipleAttempts(captchaImage, options),
                _ => (Bitmap)captchaImage.Clone()
            };

            if (processedImage == null)
                return null;

            var result = await ExtractTextFromImage(processedImage, options);
            
            var elapsed = stopwatch.Elapsed;
            _logger.LogDebug("OCR completed in {Ms}ms, result: '{Result}'", 
                elapsed.TotalMilliseconds, result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error solving captcha");
            return null;
        }
    }

    private async Task<Bitmap?> SolveWithMultipleAttempts(Bitmap image, CaptchaOptions options)
    {
        // Try different enhancement combinations
        var attempts = new[]
        {
            new CaptchaOptions { ProcessingMode = CaptchaProcessingMode.Original },
            new CaptchaOptions 
            { 
                ProcessingMode = CaptchaProcessingMode.Enhanced,
                ContrastFactor = 2.5,
                SharpnessFactor = 2.0,
                ScaleFactor = 3
            },
            new CaptchaOptions 
            { 
                ProcessingMode = CaptchaProcessingMode.Enhanced,
                ContrastFactor = 4.0,
                SharpnessFactor = 3.5,
                ScaleFactor = 4,
                UseHistogramEqualization = false
            }
        };

        foreach (var attemptOptions in attempts)
        {
            try
            {
                using var processedImage = attemptOptions.ProcessingMode == CaptchaProcessingMode.Original 
                    ? (Bitmap)image.Clone() 
                    : EnhanceImage(image, attemptOptions);
                    
                var result = await ExtractTextFromImage(processedImage, attemptOptions);
                if (!string.IsNullOrWhiteSpace(result))
                {
                    _logger.LogDebug("Multi-attempt success with contrast={Contrast}, sharpness={Sharpness}", 
                        attemptOptions.ContrastFactor, attemptOptions.SharpnessFactor);
                    return processedImage;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Multi-attempt failed for one configuration");
            }
        }

        return null;
    }

    private Bitmap EnhanceImage(Bitmap image, CaptchaOptions options)
    {
        try
        {
            var enhanced = new Bitmap(image.Width * options.ScaleFactor, 
                                    image.Height * options.ScaleFactor);

            using (var g = Graphics.FromImage(enhanced))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(image, 0, 0, enhanced.Width, enhanced.Height);
            }

            // Apply enhancements using System.Drawing
            enhanced = ApplyContrast(enhanced, options.ContrastFactor);
            enhanced = ApplySharpness(enhanced, options.SharpnessFactor);
            enhanced = ApplyBrightness(enhanced, options.BrightnessFactor);

            if (options.UseGrayscale)
            {
                enhanced = ConvertToGrayscale(enhanced);
            }

            if (options.UseHistogramEqualization)
            {
                enhanced = ApplyHistogramEqualization(enhanced);
            }

            return enhanced;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enhance image");
            return (Bitmap)image.Clone();
        }
    }

    private async Task<string?> ExtractTextFromImage(Bitmap image, CaptchaOptions options)
    {
        string? tempImagePath = null;
        string? tempOutputPath = null;

        try
        {
            // Save image to temporary file
            tempImagePath = Path.GetTempFileName() + ".png";
            tempOutputPath = Path.GetTempFileName();
            
            image.Save(tempImagePath, ImageFormat.Png);

            // Build Tesseract command
            var psmMode = (int)options.PsmMode;
            var args = $"\"{tempImagePath}\" \"{tempOutputPath}\" --psm {psmMode} -c tessedit_char_whitelist=0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

            var result = await RunTesseractAsync(args, null);
            
            if (!result.success)
            {
                _logger.LogWarning("Tesseract execution failed: {Error}", result.error);
                return null;
            }

            // Read output file
            var outputFile = tempOutputPath + ".txt";
            if (!File.Exists(outputFile))
            {
                _logger.LogWarning("Tesseract output file not found");
                return null;
            }

            var text = await File.ReadAllTextAsync(outputFile);
            text = CleanOcrText(text);

            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        finally
        {
            // Clean up temporary files
            try
            {
                if (!string.IsNullOrEmpty(tempImagePath) && File.Exists(tempImagePath))
                    File.Delete(tempImagePath);
                if (!string.IsNullOrEmpty(tempOutputPath) && File.Exists(tempOutputPath + ".txt"))
                    File.Delete(tempOutputPath + ".txt");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to clean up temporary files");
            }
        }
    }

    private string CleanOcrText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        // Remove whitespace and newlines
        text = text.Trim().Replace("\n", "").Replace("\r", "");
        
        // Remove common OCR errors/noise
        text = Regex.Replace(text, @"[^\w\d]", "");
        
        return text;
    }

    private async Task<(bool success, string? output, string? error)> RunTesseractAsync(string arguments, CancellationToken? cancellationToken)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = _tesseractPath!,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var timeout = cancellationToken ?? CancellationToken.None;
            await process.WaitForExitAsync(timeout);
            var completed = process.HasExited;

            if (!completed)
            {
                process.Kill();
                return (false, null, "Process timeout");
            }

            var output = outputBuilder.ToString();
            var error = errorBuilder.ToString();

            return (process.ExitCode == 0, output, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running Tesseract process");
            return (false, null, ex.Message);
        }
    }

    private async Task<string?> FindTesseractExecutableAsync()
    {
        // Common Tesseract installation paths
        var possiblePaths = new[]
        {
            @"C:\Program Files\Tesseract-OCR\tesseract.exe",
            @"C:\Program Files (x86)\Tesseract-OCR\tesseract.exe",
            @"C:\tools\tesseract\tesseract.exe",
            "tesseract", // Check if it's in PATH
        };

        foreach (var path in possiblePaths)
        {
            try
            {
                if (path == "tesseract")
                {
                    // Test if tesseract is in PATH
                    var result = await RunTesseractCommand("tesseract --version");
                    if (result.success)
                        return "tesseract";
                }
                else if (File.Exists(path))
                {
                    return path;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to test Tesseract path: {Path}", path);
            }
        }

        return null;
    }

    private async Task<(bool success, string output)> RunTesseractCommand(string command)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return (process.ExitCode == 0, output);
        }
        catch
        {
            return (false, "");
        }
    }

    #region Image Enhancement Methods
    
    private Bitmap ApplyContrast(Bitmap image, double factor)
    {
        var result = new Bitmap(image.Width, image.Height);
        
        for (int x = 0; x < image.Width; x++)
        {
            for (int y = 0; y < image.Height; y++)
            {
                var pixel = image.GetPixel(x, y);
                
                int r = Math.Max(0, Math.Min(255, (int)((pixel.R - 128) * factor + 128)));
                int g = Math.Max(0, Math.Min(255, (int)((pixel.G - 128) * factor + 128)));
                int b = Math.Max(0, Math.Min(255, (int)((pixel.B - 128) * factor + 128)));
                
                result.SetPixel(x, y, Color.FromArgb(r, g, b));
            }
        }
        
        return result;
    }

    private Bitmap ApplySharpness(Bitmap image, double factor)
    {
        // Simple sharpening filter
        var result = (Bitmap)image.Clone();
        
        // Sharpening kernel
        var kernel = new double[,] 
        {
            { 0, -1 * factor, 0 },
            { -1 * factor, 1 + 4 * factor, -1 * factor },
            { 0, -1 * factor, 0 }
        };
        
        return ApplyKernel(image, kernel);
    }

    private Bitmap ApplyBrightness(Bitmap image, double factor)
    {
        var result = new Bitmap(image.Width, image.Height);
        
        for (int x = 0; x < image.Width; x++)
        {
            for (int y = 0; y < image.Height; y++)
            {
                var pixel = image.GetPixel(x, y);
                
                int r = Math.Max(0, Math.Min(255, (int)(pixel.R * factor)));
                int g = Math.Max(0, Math.Min(255, (int)(pixel.G * factor)));
                int b = Math.Max(0, Math.Min(255, (int)(pixel.B * factor)));
                
                result.SetPixel(x, y, Color.FromArgb(r, g, b));
            }
        }
        
        return result;
    }

    private Bitmap ConvertToGrayscale(Bitmap image)
    {
        var result = new Bitmap(image.Width, image.Height);
        
        for (int x = 0; x < image.Width; x++)
        {
            for (int y = 0; y < image.Height; y++)
            {
                var pixel = image.GetPixel(x, y);
                int gray = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                result.SetPixel(x, y, Color.FromArgb(gray, gray, gray));
            }
        }
        
        return result;
    }

    private Bitmap ApplyHistogramEqualization(Bitmap image)
    {
        // Simplified histogram equalization
        var histogram = new int[256];
        var total = image.Width * image.Height;
        
        // Calculate histogram
        for (int x = 0; x < image.Width; x++)
        {
            for (int y = 0; y < image.Height; y++)
            {
                var pixel = image.GetPixel(x, y);
                int gray = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                histogram[gray]++;
            }
        }
        
        // Calculate cumulative distribution
        var cdf = new double[256];
        cdf[0] = histogram[0];
        for (int i = 1; i < 256; i++)
        {
            cdf[i] = cdf[i - 1] + histogram[i];
        }
        
        // Apply equalization
        var result = new Bitmap(image.Width, image.Height);
        for (int x = 0; x < image.Width; x++)
        {
            for (int y = 0; y < image.Height; y++)
            {
                var pixel = image.GetPixel(x, y);
                int gray = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                int newGray = (int)(cdf[gray] / total * 255);
                result.SetPixel(x, y, Color.FromArgb(newGray, newGray, newGray));
            }
        }
        
        return result;
    }

    private Bitmap ApplyKernel(Bitmap image, double[,] kernel)
    {
        var result = (Bitmap)image.Clone();
        int kernelSize = kernel.GetLength(0);
        int offset = kernelSize / 2;
        
        for (int x = offset; x < image.Width - offset; x++)
        {
            for (int y = offset; y < image.Height - offset; y++)
            {
                double r = 0, g = 0, b = 0;
                
                for (int kx = 0; kx < kernelSize; kx++)
                {
                    for (int ky = 0; ky < kernelSize; ky++)
                    {
                        var pixel = image.GetPixel(x + kx - offset, y + ky - offset);
                        r += pixel.R * kernel[kx, ky];
                        g += pixel.G * kernel[kx, ky];
                        b += pixel.B * kernel[kx, ky];
                    }
                }
                
                r = Math.Max(0, Math.Min(255, r));
                g = Math.Max(0, Math.Min(255, g));
                b = Math.Max(0, Math.Min(255, b));
                
                result.SetPixel(x, y, Color.FromArgb((int)r, (int)g, (int)b));
            }
        }
        
        return result;
    }

    #endregion

    public void Dispose()
    {
        _isInitialized = false;
        GC.SuppressFinalize(this);
    }
}

// Extension method for Process.WaitForExitAsync with CancellationToken
public static class ProcessExtensions
{
    public static async Task<bool> WaitForExitAsync(this Process process, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<bool>();
        
        void ProcessExited(object sender, EventArgs e) => tcs.TrySetResult(true);
        
        process.EnableRaisingEvents = true;
        process.Exited += ProcessExited;
        
        try
        {
            if (process.HasExited)
                return true;
                
            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                return await tcs.Task;
            }
        }
        finally
        {
            process.Exited -= ProcessExited;
        }
    }
}