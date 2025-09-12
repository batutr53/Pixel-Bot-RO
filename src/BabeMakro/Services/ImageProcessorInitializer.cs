using System.Drawing;
using System.Diagnostics;
using PixelAutomation.Core.Interfaces;
using PixelAutomation.Tool.Overlay.WPF.Services;

namespace PixelAutomation.Tool.Overlay.WPF.Services;

public static class ImageProcessorInitializer
{
    public static void InitializeOptimizedProcessing()
    {
        // Set up optimized configuration
        ImageProcessor.Config = ParallelProcessingConfig.CreateOptimized();
        
        Console.WriteLine("[ImageProcessor] Parallel processing optimization enabled");
        Console.WriteLine($"[ImageProcessor] CPU Cores Available: {Environment.ProcessorCount}");
        Console.WriteLine($"[ImageProcessor] SIMD Support: {System.Numerics.Vector.IsHardwareAccelerated}");
        Console.WriteLine($"[ImageProcessor] Unsafe Optimizations: {ImageProcessor.Config.UseUnsafeOptimizations}");
        Console.WriteLine($"[ImageProcessor] Max Parallel Threads: {ImageProcessor.Config.MaxDegreeOfParallelism}");
        
        // Run a quick benchmark if desired
        if (ImageProcessor.Config.EnablePerformanceMonitoring)
        {
            RunInitializationBenchmark();
        }
    }
    
    public static void InitializeCompatibleMode()
    {
        // Set up compatible configuration for older systems
        ImageProcessor.Config = ParallelProcessingConfig.CreateCompatible();
        
        Console.WriteLine("[ImageProcessor] Compatible mode enabled (reduced parallelism)");
        Console.WriteLine($"[ImageProcessor] Max Parallel Threads: {ImageProcessor.Config.MaxDegreeOfParallelism}");
    }
    
    private static void RunInitializationBenchmark()
    {
        try
        {
            // Create a test image similar to CAPTCHA size (300x150)
            using var testImage = new Bitmap(300, 150);
            using var graphics = Graphics.FromImage(testImage);
            
            // Fill with some test pattern
            graphics.FillRectangle(System.Drawing.Brushes.White, 0, 0, 300, 150);
            graphics.DrawString("TEST", new Font("Arial", 12), System.Drawing.Brushes.Black, 10, 10);
            graphics.DrawString("12345", new Font("Arial", 16), System.Drawing.Brushes.DarkBlue, 10, 50);
            graphics.DrawRectangle(Pens.Red, 50, 80, 100, 40);
            
            var options = new CaptchaOptions
            {
                UseGrayscale = true,
                ContrastFactor = 2.5,
                BrightnessFactor = 1.2,
                UseHistogramEqualization = true,
                ScaleFactor = 2
            };
            
            Console.WriteLine("[ImageProcessor] Running performance benchmark...");
            var benchmark = ImageProcessor.RunPerformanceBenchmark(testImage, options, 5);
            Console.WriteLine(benchmark.ToString());
            
            // Recommend optimal settings based on performance
            if (benchmark.ParallelSpeedup < 1.5)
            {
                Console.WriteLine("[ImageProcessor] Warning: Limited parallel performance gain detected");
                Console.WriteLine("[ImageProcessor] Consider using compatible mode for this system");
            }
            else
            {
                Console.WriteLine($"[ImageProcessor] Excellent performance: {benchmark.ParallelSpeedup:F1}x speedup achieved");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ImageProcessor] Benchmark failed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Test method to verify all optimizations work correctly
    /// </summary>
    public static void TestAllOptimizations()
    {
        try
        {
            Console.WriteLine("[ImageProcessor] Testing all optimization methods...");
            
            // Create test image
            using var testImage = new Bitmap(100, 100);
            using var graphics = Graphics.FromImage(testImage);
            graphics.FillRectangle(System.Drawing.Brushes.LightGray, 0, 0, 100, 100);
            graphics.DrawString("ABC", new Font("Arial", 10), System.Drawing.Brushes.Black, 10, 10);
            
            var stopwatch = Stopwatch.StartNew();
            
            // Test HasDistinctiveColors
            var hasColors1 = ImageProcessor.HasDistinctiveColorsParallel(testImage, 10);
            Console.WriteLine($"[ImageProcessor] HasDistinctiveColorsParallel: {hasColors1}");
            
            // Test ToGrayscale methods
            using var gray1 = ImageProcessor.ToGrayscaleParallel(testImage);
            using var gray2 = ImageProcessor.ToGrayscaleUnsafe(testImage);
            Console.WriteLine($"[ImageProcessor] Grayscale conversion: Parallel={gray1.Width}x{gray1.Height}, Unsafe={gray2.Width}x{gray2.Height}");
            
            // Test contrast adjustment
            using var contrast1 = ImageProcessor.AdjustContrastParallel(testImage, 1.5);
            Console.WriteLine($"[ImageProcessor] Contrast adjustment: {contrast1.Width}x{contrast1.Height}");
            
            // Test brightness adjustment
            using var brightness1 = ImageProcessor.AdjustBrightnessParallel(testImage, 1.2);
            Console.WriteLine($"[ImageProcessor] Brightness adjustment: {brightness1.Width}x{brightness1.Height}");
            
            // Test histogram equalization
            using var hist1 = ImageProcessor.ApplyHistogramEqualizationParallel(testImage);
            Console.WriteLine($"[ImageProcessor] Histogram equalization: {hist1.Width}x{hist1.Height}");
            
            stopwatch.Stop();
            Console.WriteLine($"[ImageProcessor] All optimization tests completed in {stopwatch.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ImageProcessor] Optimization test failed: {ex.Message}");
            Console.WriteLine($"[ImageProcessor] Stack trace: {ex.StackTrace}");
        }
    }
}