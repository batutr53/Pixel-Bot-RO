using System;
using System.Drawing;
using PixelAutomation.Core.Interfaces;

namespace PixelAutomation.Tool.Overlay.WPF.Services
{
    /// <summary>
    /// Singleton wrapper for ImageProcessor initialization to ensure it only runs once
    /// </summary>
    public static class ImageProcessorSingleton
    {
        private static readonly object _lock = new object();
        private static bool _isInitialized = false;
        
        /// <summary>
        /// Initializes the ImageProcessor with optimized settings (runs only once)
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
                return;
                
            lock (_lock)
            {
                if (_isInitialized)
                    return;
                    
                try
                {
                    Console.WriteLine("🚀 Initializing image processing optimizations (singleton)...");
                    
                    // Set up optimized configuration
                    ImageProcessor.Config = ParallelProcessingConfig.CreateOptimized();
                    
                    // Disable verbose logging to reduce console spam
                    ImageProcessor.Config.EnablePerformanceMonitoring = false;
                    
                    Console.WriteLine($"[ImageProcessor] CPU Cores: {Environment.ProcessorCount}, SIMD: {System.Numerics.Vector.IsHardwareAccelerated}");
                    
                    // Run a single quick test to verify it works
                    TestBasicFunctionality();
                    
                    _isInitialized = true;
                    Console.WriteLine("✅ Image processing optimizations initialized successfully!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Failed to initialize image processing: {ex.Message}");
                    // Fall back to compatible mode
                    ImageProcessor.Config = ParallelProcessingConfig.CreateCompatible();
                    ImageProcessor.Config.EnablePerformanceMonitoring = false;
                    _isInitialized = true;
                }
            }
        }
        
        private static void TestBasicFunctionality()
        {
            // Quick test to ensure basic functionality works
            using var testImage = new Bitmap(50, 50);
            using var graphics = Graphics.FromImage(testImage);
            graphics.FillRectangle(System.Drawing.Brushes.White, 0, 0, 50, 50);
            
            var options = new CaptchaOptions
            {
                UseGrayscale = true,
                ContrastFactor = 1.5,
                ScaleFactor = 1
            };
            
            using var processed = ImageProcessor.ProcessImage(testImage, options);
            // Test passed if no exception thrown
        }
        
        public static bool IsInitialized => _isInitialized;
    }
}