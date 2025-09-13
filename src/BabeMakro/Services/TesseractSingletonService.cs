using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PixelAutomation.Core.Implementations;
using PixelAutomation.Core.Interfaces;
using PixelAutomation.Tool.Overlay.WPF.Services;

namespace PixelAutomation.Tool.Overlay.WPF.Services
{
    /// <summary>
    /// Singleton service for Tesseract OCR to ensure only one initialization occurs
    /// </summary>
    public class TesseractSingletonService
    {
        private static readonly Lazy<TesseractSingletonService> _instance = 
            new Lazy<TesseractSingletonService>(() => new TesseractSingletonService());
        
        private ICaptchaSolver? _captchaSolver;
        private readonly SemaphoreSlim _initSemaphore = new(1, 1);
        private bool _isInitialized = false;
        private Task<bool>? _initTask;
        
        private TesseractSingletonService() { }
        
        public static TesseractSingletonService Instance => _instance.Value;
        
        /// <summary>
        /// Gets the initialized Tesseract solver. Initializes on first call.
        /// Thread-safe and ensures only one initialization occurs.
        /// </summary>
        public async Task<ICaptchaSolver?> GetSolverAsync()
        {
            if (_isInitialized && _captchaSolver != null)
            {
                return _captchaSolver;
            }
            
            await _initSemaphore.WaitAsync();
            try
            {
                // Double-check after acquiring lock
                if (_isInitialized && _captchaSolver != null)
                {
                    return _captchaSolver;
                }
                
                // If initialization is already in progress, wait for it
                if (_initTask != null)
                {
                    await _initTask;
                    return _captchaSolver;
                }
                
                // Start initialization
                _initTask = InitializeInternalAsync();
                var success = await _initTask;
                
                if (success)
                {
                    _isInitialized = true;
                    return _captchaSolver;
                }
                
                return null;
            }
            finally
            {
                _initSemaphore.Release();
            }
        }
        
        private async Task<bool> InitializeInternalAsync()
        {
            try
            {
                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var logger = loggerFactory.CreateLogger<TesseractCaptchaSolver>();
                _captchaSolver = new TesseractCaptchaSolver(logger);
                
                Console.WriteLine("[TesseractSingleton] Initializing Tesseract OCR (single instance)...");
                bool initialized = await _captchaSolver.InitializeAsync();
                
                if (initialized)
                {
                    Console.WriteLine("[TesseractSingleton] ✅ Tesseract OCR initialized successfully (singleton)");
                }
                else
                {
                    Console.WriteLine("[TesseractSingleton] ⚠️ Tesseract OCR initialization failed");
                    _captchaSolver?.Dispose();
                    _captchaSolver = null;
                }
                
                return initialized;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TesseractSingleton] ❌ Failed to initialize Tesseract: {ex.Message}");
                _captchaSolver?.Dispose();
                _captchaSolver = null;
                return false;
            }
        }
        
        public void Dispose()
        {
            _captchaSolver?.Dispose();
            _captchaSolver = null;
            _isInitialized = false;
            _initTask = null;
            _initSemaphore?.Dispose();
        }
    }
}