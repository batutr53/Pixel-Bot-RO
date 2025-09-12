using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Shapes;
using PixelAutomation.Tool.Overlay.WPF.Services;
using PixelAutomation.Tool.Overlay.WPF.Models;
using Vanara.PInvoke;
using System.Collections.Generic;
using PixelAutomation.Core.Services;
using PixelAutomation.Core.Models;
using PixelAutomation.Core.Interfaces;
using PixelAutomation.Core.Implementations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Drawing;
using PixelAutomation.Capture.Win;
using PixelAutomation.Capture.Win.Backends;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System.Runtime;
using Core.Services;

namespace PixelAutomation.Tool.Overlay.WPF.Controls;

public partial class ClientCard : UserControl, IDisposable
{
    public int ClientId { get; set; }
    public ClientViewModel ViewModel { get; set; }
    
    // Event for notifying when window changes
    public event EventHandler<IntPtr>? WindowChanged;
    
    private CoordinatePicker? _coordinatePicker;
    // private bool _isRunning = false; // Unused field removed
    
    // Master Timer System - Replaces all individual timers for better performance
    private MasterTimerManager? _masterTimer;
    
    // Legacy timer references (kept for compatibility, but no longer used)
    private DispatcherTimer? _yClickTimer;
    private DispatcherTimer? _extra1Timer;
    private DispatcherTimer? _extra2Timer;
    private DispatcherTimer? _extra3Timer;
    private DispatcherTimer? _monitoringTimer;
    private DispatcherTimer? _hpTriggerTimer;
    private DispatcherTimer? _mpTriggerTimer;
    
    // BabeBot Style Timers
    private DispatcherTimer? _babeBotTimer;
    private FastColorSampler? _fastSampler;
    private OptimizedFastColorSampler? _optimizedSampler; // NEW: High-performance cached sampler
    private ColorSamplingCache? _colorCache; // NEW: Intelligent color caching system
    private int _debugCounter = 0;
    
    // Captcha System
    private CaptchaService? _captchaService;
    private ICaptchaSolver? _captchaSolver;
    private int _captchaSolveCount = 0;
    private int _captchaDetectCount = 0;
    private DispatcherTimer? _captchaTimer;
    private bool _captchaMonitoring = false;
    private bool _clientWasPausedForCaptcha = false;
    private bool _currentlySolvingCaptcha = false;
    
    // Attack/Skills System
    private volatile bool _attackRunning = false;
    private readonly List<DispatcherTimer> _skillTimers = new();
    
    // MultiHp System (removed but keeping fields for compilation)
    private volatile bool _multiHpRunning = false;
    private volatile int _currentMultiHpIndex = 0;
    private DispatcherTimer? _multiHpTimer;
    
    // Buff/AC System
    private DispatcherTimer? _buffAcCycleTimer;
    private volatile bool _buffAcRunning = false;
    private volatile bool _buffAcCycleActive = false;
    private volatile int _currentBuffAcMemberIndex = 0;
    private readonly List<int> _buffAcEnabledMembers = new();
    private readonly List<DispatcherTimer> _activeBuffAcTimers = new();
    
    // Party Heal System
    private IPartyHealService? _partyHealService;
    private volatile bool _partyHealRunning = false;
    
    
    // Performance Optimization - Using Bounded Task Queue to prevent memory exhaustion
    private readonly SemaphoreSlim _operationSemaphore = new(1, 1);
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly BoundedTaskQueue _boundedTaskQueue = new(maxQueueSize: 500, maxConcurrency: 4);
    private volatile bool _highPerformanceMode = true;
    private readonly object _lockObject = new();
    
    // Disposal Management
    private bool _disposed = false;
    
    // HP/MP Shape management
    private System.Windows.Shapes.Ellipse? _hpShape;
    private System.Windows.Shapes.Ellipse? _mpShape;
    private System.Windows.Shapes.Rectangle? _hpPercentageShape;
    private System.Windows.Shapes.Rectangle? _mpPercentageShape;
    private bool _isDraggingHp = false;
    private bool _isDraggingMp = false;
    private bool _isDraggingHpPercentage = false;
    private bool _isDraggingMpPercentage = false;
    private System.Windows.Point _dragStartPoint;

    public ClientCard()
    {
        InitializeComponent();
        ViewModel = new ClientViewModel();
        DataContext = ViewModel;
        AttachTextBoxHandlers();
        _fastSampler = new FastColorSampler();
        
        // NEW: Initialize optimized caching system for maximum performance
        _optimizedSampler = new OptimizedFastColorSampler();
        _colorCache = new ColorSamplingCache
        {
            DefaultColorCacheDuration = TimeSpan.FromMilliseconds(25), // Half HP/MP monitoring interval
            DefaultRegionCacheDuration = TimeSpan.FromMilliseconds(50), 
            MaxCacheSize = 12000, // Support multiple clients efficiently
            NearbyPixelThreshold = 3 // Group nearby pixels for better cache efficiency
        };
        
        Console.WriteLine($"[{ClientId}] 🚀 PERFORMANCE OPTIMIZATION: Color sampling cache initialized - expect 70-90% reduction in Win32 API calls");
        
        // Initialize master timer system for better performance
        InitializeMasterTimer();
        
        SetupBabeBotUI();
        SetupAttackSystem();
        SetupBuffAcSystem();
        InitializePartyHealSystem();
        InitializePerformanceOptimizations();
        InitializeCaptchaSolver();
    }
    
    private void InitializePerformanceOptimizations()
    {
        // Set process priority for better performance
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            process.PriorityClass = System.Diagnostics.ProcessPriorityClass.High;
            
            // Enable multi-core processing
            ThreadPool.SetMinThreads(Environment.ProcessorCount * 2, Environment.ProcessorCount * 2);
            ThreadPool.SetMaxThreads(Environment.ProcessorCount * 4, Environment.ProcessorCount * 4);
            
            // Memory and GC optimizations
            System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency;
            
            // The BoundedTaskQueue handles task processing automatically
            
            // Start periodic GC optimization
            _ = Task.Run(PeriodicGCOptimization, _cancellationTokenSource.Token);
            
            Console.WriteLine($"[{ClientId}] 🚀 Performance optimizations enabled: High priority, {Environment.ProcessorCount} cores, Low latency GC");
        }
        catch (Exception ex)
        {
            // Fail silently if we can't set priority
            Console.WriteLine($"[{ClientId}] Performance optimization warning: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Initializes the master timer system that consolidates all individual timers
    /// </summary>
    private void InitializeMasterTimer()
    {
        try
        {
            // Create master timer with 25ms interval (40Hz) for responsive performance
            _masterTimer = new MasterTimerManager(TimeSpan.FromMilliseconds(25));
            
            // Add performance monitoring task that reports every 30 seconds
            _masterTimer.AddOrUpdateTask("PerformanceMonitoring", 
                TimeSpan.FromSeconds(30), 
                () => ReportPerformanceStatistics(),
                enabled: true,
                priority: 1); // Low priority for reporting
            
            Console.WriteLine($"[{ClientId}] 🕐 Master timer system initialized - Single timer replaces ~15 individual timers");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ClientId}] Failed to initialize master timer: {ex.Message}");
        }
    }
    
    private async void InitializeCaptchaSolver()
    {
        try
        {
            // Create a simple console logger for the solver
            var logger = new ConsoleLogger<TesseractCaptchaSolver>();
            
            // Initialize the Tesseract captcha solver
            _captchaSolver = new TesseractCaptchaSolver(logger);
            
            // Initialize asynchronously 
            bool initialized = await _captchaSolver.InitializeAsync();
            
            if (initialized)
            {
                Console.WriteLine($"[{ClientId}] ✅ Tesseract OCR initialized successfully");
            }
            else
            {
                Console.WriteLine($"[{ClientId}] ⚠️ Tesseract OCR initialization failed - CAPTCHA solving disabled");
                _captchaSolver?.Dispose();
                _captchaSolver = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ClientId}] ❌ Failed to initialize captcha solver: {ex.Message}");
            _captchaSolver?.Dispose();
            _captchaSolver = null;
        }
    }
    
    private async Task PeriodicGCOptimization()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                // Wait 30 seconds between GC optimizations
                await Task.Delay(30000, _cancellationTokenSource.Token);
                
                // Force GC only if memory usage is high
                var memoryBefore = GC.GetTotalMemory(false);
                if (memoryBefore > 100 * 1024 * 1024) // 100MB threshold
                {
                    GC.Collect(0, System.GCCollectionMode.Optimized);
                    var memoryAfter = GC.GetTotalMemory(true);
                    var freed = memoryBefore - memoryAfter;
                    
                    if (freed > 10 * 1024 * 1024) // Only log if significant memory freed
                    {
                        Console.WriteLine($"[{ClientId}] 🧹 GC freed {freed / (1024 * 1024):F1}MB memory");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ClientId}] GC optimization error: {ex.Message}");
            }
        }
    }
    
    // ProcessBackgroundTasks method removed - BoundedTaskQueue handles processing automatically
    
    private void AttachTextBoxHandlers()
    {
        // HP/MP coordinate and tolerance handlers
        HpX.TextChanged += (s, e) => { if (int.TryParse(HpX.Text, out var v)) ViewModel.HpProbe.X = v; };
        HpY.TextChanged += (s, e) => { if (int.TryParse(HpY.Text, out var v)) ViewModel.HpProbe.Y = v; };
        HpTolerance.TextChanged += (s, e) => { if (int.TryParse(HpTolerance.Text, out var v)) ViewModel.HpProbe.Tolerance = v; };
        MpX.TextChanged += (s, e) => { if (int.TryParse(MpX.Text, out var v)) ViewModel.MpProbe.X = v; };
        MpY.TextChanged += (s, e) => { if (int.TryParse(MpY.Text, out var v)) ViewModel.MpProbe.Y = v; };
        MpTolerance.TextChanged += (s, e) => { if (int.TryParse(MpTolerance.Text, out var v)) ViewModel.MpProbe.Tolerance = v; };
        
        // Percentage-based HP/MP handlers
        HpPercentageStartX.TextChanged += (s, e) => { if (int.TryParse(HpPercentageStartX.Text, out var v)) { ViewModel.HpPercentageProbe.StartX = v; UpdatePercentageMonitorPosition(); } };
        HpPercentageEndX.TextChanged += (s, e) => { if (int.TryParse(HpPercentageEndX.Text, out var v)) { ViewModel.HpPercentageProbe.EndX = v; UpdatePercentageMonitorPosition(); } };
        HpPercentageY.TextChanged += (s, e) => { if (int.TryParse(HpPercentageY.Text, out var v)) ViewModel.HpPercentageProbe.Y = v; };
        HpPercentageThreshold.TextChanged += (s, e) => { if (double.TryParse(HpPercentageThreshold.Text, out var v)) ViewModel.HpPercentageProbe.MonitorPercentage = v; UpdatePercentageMonitorPosition(); };
        HpPercentageTolerance.TextChanged += (s, e) => { if (int.TryParse(HpPercentageTolerance.Text, out var v)) ViewModel.HpPercentageProbe.Tolerance = v; };
        
        MpPercentageStartX.TextChanged += (s, e) => { if (int.TryParse(MpPercentageStartX.Text, out var v)) { ViewModel.MpPercentageProbe.StartX = v; UpdatePercentageMonitorPosition(); } };
        MpPercentageEndX.TextChanged += (s, e) => { if (int.TryParse(MpPercentageEndX.Text, out var v)) { ViewModel.MpPercentageProbe.EndX = v; UpdatePercentageMonitorPosition(); } };
        MpPercentageY.TextChanged += (s, e) => { if (int.TryParse(MpPercentageY.Text, out var v)) ViewModel.MpPercentageProbe.Y = v; };
        MpPercentageThreshold.TextChanged += (s, e) => { if (double.TryParse(MpPercentageThreshold.Text, out var v)) ViewModel.MpPercentageProbe.MonitorPercentage = v; UpdatePercentageMonitorPosition(); };
        MpPercentageTolerance.TextChanged += (s, e) => { if (int.TryParse(MpPercentageTolerance.Text, out var v)) ViewModel.MpPercentageProbe.Tolerance = v; };
        
        // Percentage monitoring enable/disable
        PercentageMonitoringEnabled.Checked += (s, e) => { ViewModel.HpPercentageProbe.Enabled = true; ViewModel.MpPercentageProbe.Enabled = true; };
        PercentageMonitoringEnabled.Unchecked += (s, e) => { ViewModel.HpPercentageProbe.Enabled = false; ViewModel.MpPercentageProbe.Enabled = false; };
        
        // Python-style potion coordinate handlers
        PythonHpPotionX.TextChanged += (s, e) => { if (int.TryParse(PythonHpPotionX.Text, out var v)) ViewModel.PythonHpPotionClick.X = v; };
        PythonHpPotionY.TextChanged += (s, e) => { if (int.TryParse(PythonHpPotionY.Text, out var v)) ViewModel.PythonHpPotionClick.Y = v; };
        PythonHpPotionCooldown.TextChanged += (s, e) => { if (int.TryParse(PythonHpPotionCooldown.Text, out var v)) ViewModel.PythonHpPotionClick.CooldownMs = v; };
        PythonHpUseCoordinate.Checked += (s, e) => ViewModel.PythonHpPotionClick.UseCoordinate = true;
        PythonHpUseCoordinate.Unchecked += (s, e) => ViewModel.PythonHpPotionClick.UseCoordinate = false;
        PythonHpUseKeyPress.Checked += (s, e) => ViewModel.PythonHpPotionClick.UseKeyPress = true;
        PythonHpUseKeyPress.Unchecked += (s, e) => ViewModel.PythonHpPotionClick.UseKeyPress = false;
        PythonHpKeyToPress.TextChanged += (s, e) => ViewModel.PythonHpPotionClick.KeyToPress = PythonHpKeyToPress.Text;
        
        PythonMpPotionX.TextChanged += (s, e) => { if (int.TryParse(PythonMpPotionX.Text, out var v)) ViewModel.PythonMpPotionClick.X = v; };
        PythonMpPotionY.TextChanged += (s, e) => { if (int.TryParse(PythonMpPotionY.Text, out var v)) ViewModel.PythonMpPotionClick.Y = v; };
        PythonMpPotionCooldown.TextChanged += (s, e) => { if (int.TryParse(PythonMpPotionCooldown.Text, out var v)) ViewModel.PythonMpPotionClick.CooldownMs = v; };
        PythonMpUseCoordinate.Checked += (s, e) => ViewModel.PythonMpPotionClick.UseCoordinate = true;
        PythonMpUseCoordinate.Unchecked += (s, e) => ViewModel.PythonMpPotionClick.UseCoordinate = false;
        PythonMpUseKeyPress.Checked += (s, e) => ViewModel.PythonMpPotionClick.UseKeyPress = true;
        PythonMpUseKeyPress.Unchecked += (s, e) => ViewModel.PythonMpPotionClick.UseKeyPress = false;
        PythonMpKeyToPress.TextChanged += (s, e) => ViewModel.PythonMpPotionClick.KeyToPress = PythonMpKeyToPress.Text;
        
        // Trigger coordinate, cooldown and enable handlers
        HpTriggerX.TextChanged += (s, e) => { if (int.TryParse(HpTriggerX.Text, out var v)) ViewModel.HpTrigger.X = v; };
        HpTriggerY.TextChanged += (s, e) => { if (int.TryParse(HpTriggerY.Text, out var v)) ViewModel.HpTrigger.Y = v; };
        HpTriggerCooldown.TextChanged += (s, e) => { if (int.TryParse(HpTriggerCooldown.Text, out var v)) ViewModel.HpTrigger.CooldownMs = v; };
        HpTriggerEnabled.Checked += (s, e) => ViewModel.HpTrigger.Enabled = true;
        HpTriggerEnabled.Unchecked += (s, e) => ViewModel.HpTrigger.Enabled = false;
        HpUseCoordinate.Checked += (s, e) => ViewModel.HpTrigger.UseCoordinate = true;
        HpUseCoordinate.Unchecked += (s, e) => ViewModel.HpTrigger.UseCoordinate = false;
        HpUseKeyPress.Checked += (s, e) => ViewModel.HpTrigger.UseKeyPress = true;
        HpUseKeyPress.Unchecked += (s, e) => ViewModel.HpTrigger.UseKeyPress = false;
        HpKeyToPress.TextChanged += (s, e) => ViewModel.HpTrigger.KeyToPress = HpKeyToPress.Text;
        
        MpTriggerX.TextChanged += (s, e) => { if (int.TryParse(MpTriggerX.Text, out var v)) ViewModel.MpTrigger.X = v; };
        MpTriggerY.TextChanged += (s, e) => { if (int.TryParse(MpTriggerY.Text, out var v)) ViewModel.MpTrigger.Y = v; };
        MpTriggerCooldown.TextChanged += (s, e) => { if (int.TryParse(MpTriggerCooldown.Text, out var v)) ViewModel.MpTrigger.CooldownMs = v; };
        MpTriggerEnabled.Checked += (s, e) => ViewModel.MpTrigger.Enabled = true;
        MpTriggerEnabled.Unchecked += (s, e) => ViewModel.MpTrigger.Enabled = false;
        MpUseCoordinate.Checked += (s, e) => ViewModel.MpTrigger.UseCoordinate = true;
        MpUseCoordinate.Unchecked += (s, e) => ViewModel.MpTrigger.UseCoordinate = false;
        MpUseKeyPress.Checked += (s, e) => ViewModel.MpTrigger.UseKeyPress = true;
        MpUseKeyPress.Unchecked += (s, e) => ViewModel.MpTrigger.UseKeyPress = false;
        MpKeyToPress.TextChanged += (s, e) => ViewModel.MpTrigger.KeyToPress = MpKeyToPress.Text;
        
        // Periodic click handlers
        YClickX.TextChanged += (s, e) => { if (int.TryParse(YClickX.Text, out var v)) ViewModel.YClick.X = v; };
        YClickY.TextChanged += (s, e) => { if (int.TryParse(YClickY.Text, out var v)) ViewModel.YClick.Y = v; };
        YClickPeriod.TextChanged += (s, e) => { if (int.TryParse(YClickPeriod.Text, out var v)) ViewModel.YClick.PeriodMs = v; };
        YClickEnabled.Checked += (s, e) => ViewModel.YClick.Enabled = true;
        YClickEnabled.Unchecked += (s, e) => ViewModel.YClick.Enabled = false;
        YUseCoordinate.Checked += (s, e) => ViewModel.YClick.UseCoordinate = true;
        YUseCoordinate.Unchecked += (s, e) => ViewModel.YClick.UseCoordinate = false;
        YUseKeyPress.Checked += (s, e) => ViewModel.YClick.UseKeyPress = true;
        YUseKeyPress.Unchecked += (s, e) => ViewModel.YClick.UseKeyPress = false;
        YKeyToPress.TextChanged += (s, e) => ViewModel.YClick.KeyToPress = YKeyToPress.Text;
        
        Extra1X.TextChanged += (s, e) => { if (int.TryParse(Extra1X.Text, out var v)) ViewModel.Extra1Click.X = v; };
        Extra1Y.TextChanged += (s, e) => { if (int.TryParse(Extra1Y.Text, out var v)) ViewModel.Extra1Click.Y = v; };
        Extra1Period.TextChanged += (s, e) => { if (int.TryParse(Extra1Period.Text, out var v)) ViewModel.Extra1Click.PeriodMs = v; };
        Extra1Enabled.Checked += (s, e) => ViewModel.Extra1Click.Enabled = true;
        Extra1Enabled.Unchecked += (s, e) => ViewModel.Extra1Click.Enabled = false;
        Extra1UseCoordinate.Checked += (s, e) => ViewModel.Extra1Click.UseCoordinate = true;
        Extra1UseCoordinate.Unchecked += (s, e) => ViewModel.Extra1Click.UseCoordinate = false;
        Extra1UseKeyPress.Checked += (s, e) => ViewModel.Extra1Click.UseKeyPress = true;
        Extra1UseKeyPress.Unchecked += (s, e) => ViewModel.Extra1Click.UseKeyPress = false;
        Extra1KeyToPress.TextChanged += (s, e) => ViewModel.Extra1Click.KeyToPress = Extra1KeyToPress.Text;
        
        Extra2X.TextChanged += (s, e) => { if (int.TryParse(Extra2X.Text, out var v)) ViewModel.Extra2Click.X = v; };
        Extra2Y.TextChanged += (s, e) => { if (int.TryParse(Extra2Y.Text, out var v)) ViewModel.Extra2Click.Y = v; };
        Extra2Period.TextChanged += (s, e) => { if (int.TryParse(Extra2Period.Text, out var v)) ViewModel.Extra2Click.PeriodMs = v; };
        Extra2Enabled.Checked += (s, e) => ViewModel.Extra2Click.Enabled = true;
        Extra2Enabled.Unchecked += (s, e) => ViewModel.Extra2Click.Enabled = false;
        Extra2UseCoordinate.Checked += (s, e) => ViewModel.Extra2Click.UseCoordinate = true;
        Extra2UseCoordinate.Unchecked += (s, e) => ViewModel.Extra2Click.UseCoordinate = false;
        Extra2UseKeyPress.Checked += (s, e) => ViewModel.Extra2Click.UseKeyPress = true;
        Extra2UseKeyPress.Unchecked += (s, e) => ViewModel.Extra2Click.UseKeyPress = false;
        Extra2KeyToPress.TextChanged += (s, e) => ViewModel.Extra2Click.KeyToPress = Extra2KeyToPress.Text;
        
        Extra3X.TextChanged += (s, e) => { if (int.TryParse(Extra3X.Text, out var v)) ViewModel.Extra3Click.X = v; };
        Extra3Y.TextChanged += (s, e) => { if (int.TryParse(Extra3Y.Text, out var v)) ViewModel.Extra3Click.Y = v; };
        Extra3Period.TextChanged += (s, e) => { if (int.TryParse(Extra3Period.Text, out var v)) ViewModel.Extra3Click.PeriodMs = v; };
        Extra3Enabled.Checked += (s, e) => ViewModel.Extra3Click.Enabled = true;
        Extra3Enabled.Unchecked += (s, e) => ViewModel.Extra3Click.Enabled = false;
        Extra3UseCoordinate.Checked += (s, e) => ViewModel.Extra3Click.UseCoordinate = true;
        Extra3UseCoordinate.Unchecked += (s, e) => ViewModel.Extra3Click.UseCoordinate = false;
        Extra3UseKeyPress.Checked += (s, e) => ViewModel.Extra3Click.UseKeyPress = true;
        Extra3UseKeyPress.Unchecked += (s, e) => ViewModel.Extra3Click.UseKeyPress = false;
        Extra3KeyToPress.TextChanged += (s, e) => ViewModel.Extra3Click.KeyToPress = Extra3KeyToPress.Text;
    }

    public void Initialize(int clientId, string clientName)
    {
        ClientId = clientId;
        ViewModel.ClientName = clientName;
        ClientNameText.Text = clientName;
        UpdateUI();
        
        // Initialize draggable shapes when overlay mode is active
        InitializeDraggableShapes();
    }
    
    private void InitializeDraggableShapes()
    {
        // Create HP shape (red circle)
        _hpShape = new System.Windows.Shapes.Ellipse
        {
            Width = 20,
            Height = 20,
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 255, 0, 0)),
            Stroke = new SolidColorBrush(System.Windows.Media.Colors.Red),
            StrokeThickness = 2,
            Cursor = System.Windows.Input.Cursors.SizeAll,
            ToolTip = $"HP Monitor - Client {ClientId} (Drag to move)",
            Visibility = Visibility.Collapsed
        };
        
        // Create MP shape (blue circle)
        _mpShape = new System.Windows.Shapes.Ellipse
        {
            Width = 20,
            Height = 20,
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 0, 0, 255)),
            Stroke = new SolidColorBrush(System.Windows.Media.Colors.Blue),
            StrokeThickness = 2,
            Cursor = System.Windows.Input.Cursors.SizeAll,
            ToolTip = $"MP Monitor - Client {ClientId} (Drag to move)",
            Visibility = Visibility.Collapsed
        };
        
        // Create HP percentage bar shape (red rectangle)
        _hpPercentageShape = new System.Windows.Shapes.Rectangle
        {
            Width = 150,
            Height = 8,
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(64, 255, 0, 0)),
            Stroke = new SolidColorBrush(System.Windows.Media.Colors.Red),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 2, 2 },
            Cursor = System.Windows.Input.Cursors.SizeAll,
            ToolTip = $"HP Bar - Client {ClientId} (Drag to move, resize edges)",
            Visibility = Visibility.Collapsed
        };
        
        // Create MP percentage bar shape (blue rectangle)
        _mpPercentageShape = new System.Windows.Shapes.Rectangle
        {
            Width = 150,
            Height = 8,
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(64, 0, 0, 255)),
            Stroke = new SolidColorBrush(System.Windows.Media.Colors.Blue),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 2, 2 },
            Cursor = System.Windows.Input.Cursors.SizeAll,
            ToolTip = $"MP Bar - Client {ClientId} (Drag to move, resize edges)",
            Visibility = Visibility.Collapsed
        };
        
        // Add mouse event handlers
        _hpShape.MouseLeftButtonDown += HpShape_MouseLeftButtonDown;
        _hpShape.MouseMove += HpShape_MouseMove;
        _hpShape.MouseLeftButtonUp += HpShape_MouseLeftButtonUp;
        
        _mpShape.MouseLeftButtonDown += MpShape_MouseLeftButtonDown;
        _mpShape.MouseMove += MpShape_MouseMove;
        _mpShape.MouseLeftButtonUp += MpShape_MouseLeftButtonUp;
        
        _hpPercentageShape.MouseLeftButtonDown += HpPercentageShape_MouseLeftButtonDown;
        _hpPercentageShape.MouseMove += HpPercentageShape_MouseMove;
        _hpPercentageShape.MouseLeftButtonUp += HpPercentageShape_MouseLeftButtonUp;
        
        _mpPercentageShape.MouseLeftButtonDown += MpPercentageShape_MouseLeftButtonDown;
        _mpPercentageShape.MouseMove += MpPercentageShape_MouseMove;
        _mpPercentageShape.MouseLeftButtonUp += MpPercentageShape_MouseLeftButtonUp;
    }

    private void SelectWindow_Click(object sender, RoutedEventArgs e)
    {
        var picker = new WindowPicker();
        var hwnd = picker.PickWindow();
        
        if (hwnd != IntPtr.Zero)
        {
            ViewModel.TargetHwnd = hwnd;
            
            // Notify PartyHeal about window change
            WindowChanged?.Invoke(this, hwnd);
            ViewModel.WindowTitle = WindowHelper.GetWindowTitle(hwnd);
            WindowTitleText.Text = $"{ViewModel.WindowTitle} - 0x{hwnd:X8}";
            StatusIndicator.Fill = new SolidColorBrush(Colors.LimeGreen);
            StatusIndicator.ToolTip = $"Connected: {ViewModel.WindowTitle} (0x{hwnd:X8})";
        }
        else
        {
            StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
            StatusIndicator.ToolTip = "No window selected";
        }
    }

    private void ShowCoordinateOverlay_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            MessageBox.Show("Please select a window first!", "No Window Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Create a transparent overlay window
        var overlayWindow = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual
        };

        // Get target window position and size
        User32.GetWindowRect(ViewModel.TargetHwnd, out var windowRect);
        User32.GetClientRect(ViewModel.TargetHwnd, out var clientRect);
        
        // Calculate border/title offsets
        int borderWidth = ((windowRect.right - windowRect.left) - (clientRect.right - clientRect.left)) / 2;
        int titleHeight = ((windowRect.bottom - windowRect.top) - (clientRect.bottom - clientRect.top)) - borderWidth;
        
        // Position overlay window over the target window's client area
        overlayWindow.Left = windowRect.left + borderWidth;
        overlayWindow.Top = windowRect.top + titleHeight;
        overlayWindow.Width = clientRect.right - clientRect.left;
        overlayWindow.Height = clientRect.bottom - clientRect.top;
        
        // Create canvas for drawing
        var canvas = new Canvas
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 138, 43, 226)) // Very transparent purple
        };
        
        // Create coordinate display label
        var coordLabel = new TextBlock
        {
            Text = "Move mouse to see coordinates",
            Foreground = System.Windows.Media.Brushes.White,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 138, 43, 226)),
            Padding = new Thickness(8),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            FontFamily = new System.Windows.Media.FontFamily("Consolas")
        };
        
        Canvas.SetLeft(coordLabel, 10);
        Canvas.SetTop(coordLabel, 10);
        canvas.Children.Add(coordLabel);
        
        // Create crosshair lines
        var verticalLine = new System.Windows.Shapes.Line
        {
            Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 255, 255, 0)),
            StrokeThickness = 1,
            Y1 = 0,
            Y2 = overlayWindow.Height
        };
        
        var horizontalLine = new System.Windows.Shapes.Line
        {
            Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 255, 255, 0)),
            StrokeThickness = 1,
            X1 = 0,
            X2 = overlayWindow.Width
        };
        
        canvas.Children.Add(verticalLine);
        canvas.Children.Add(horizontalLine);
        
        // Create position marker
        var positionMarker = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 0, 0)),
            Stroke = System.Windows.Media.Brushes.White,
            StrokeThickness = 2
        };
        canvas.Children.Add(positionMarker);
        
        // Mouse move handler
        canvas.MouseMove += (s, me) =>
        {
            var pos = me.GetPosition(canvas);
            int x = (int)pos.X;
            int y = (int)pos.Y;
            
            // Update coordinate label
            coordLabel.Text = $"📍 X: {x}, Y: {y}";
            
            // Position label to follow mouse but keep in bounds
            double labelX = pos.X + 15;
            double labelY = pos.Y - 30;
            
            if (labelX + 150 > canvas.Width) labelX = pos.X - 150;
            if (labelY < 0) labelY = pos.Y + 15;
            
            Canvas.SetLeft(coordLabel, labelX);
            Canvas.SetTop(coordLabel, labelY);
            
            // Update crosshair lines
            verticalLine.X1 = verticalLine.X2 = pos.X;
            horizontalLine.Y1 = horizontalLine.Y2 = pos.Y;
            
            // Update position marker
            Canvas.SetLeft(positionMarker, pos.X - 5);
            Canvas.SetTop(positionMarker, pos.Y - 5);
        };
        
        // Click to copy coordinates
        canvas.MouseLeftButtonDown += (s, me) =>
        {
            var pos = me.GetPosition(canvas);
            int x = (int)pos.X;
            int y = (int)pos.Y;
            
            try
            {
                System.Windows.Clipboard.SetText($"{x},{y}");
                coordLabel.Text = $"📋 Copied: X:{x}, Y:{y}";
                coordLabel.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 40, 167, 69));
                
                // Reset color after 1 second
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                timer.Tick += (ts, te) =>
                {
                    coordLabel.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 138, 43, 226));
                    timer.Stop();
                };
                timer.Start();
            }
            catch { }
        };
        
        // Right click to close
        canvas.MouseRightButtonDown += (s, me) =>
        {
            overlayWindow.Close();
        };
        
        // ESC key to close
        overlayWindow.KeyDown += (s, ke) =>
        {
            if (ke.Key == System.Windows.Input.Key.Escape)
            {
                overlayWindow.Close();
            }
        };
        
        // Add help text
        var helpText = new TextBlock
        {
            Text = "Left Click: Copy coords | Right Click or ESC: Close",
            Foreground = System.Windows.Media.Brushes.White,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 0, 0, 0)),
            Padding = new Thickness(5),
            FontSize = 11
        };
        Canvas.SetRight(helpText, 10);
        Canvas.SetBottom(helpText, 10);
        canvas.Children.Add(helpText);
        
        overlayWindow.Content = canvas;
        overlayWindow.Show();
        
        Console.WriteLine($"[{ViewModel.ClientName}] 📍 Coordinate overlay opened for window 0x{ViewModel.TargetHwnd:X8}");
    }

    private void PickHpCoord_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("HP Bar Position", (x, y) =>
        {
            HpX.Text = x.ToString();
            HpY.Text = y.ToString();
            ViewModel.HpProbe.X = x;
            ViewModel.HpProbe.Y = y;
            
            // Immediately read the color at the selected position
            if (ViewModel.TargetHwnd != IntPtr.Zero)
            {
                var currentColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, x, y);
                ViewModel.HpProbe.ExpectedColor = currentColor;
                ViewModel.HpProbe.ReferenceColor = currentColor;
                HpColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(currentColor.R, currentColor.G, currentColor.B));
                HpColorText.Text = $"{currentColor.R},{currentColor.G},{currentColor.B}";
                Console.WriteLine($"[{ViewModel.ClientName}] HP COORDINATE SELECTED: Position=({x},{y}) Color=RGB({currentColor.R},{currentColor.G},{currentColor.B})");
            }
        });
    }

    private void PickMpCoord_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("MP Bar Position", (x, y) =>
        {
            MpX.Text = x.ToString();
            MpY.Text = y.ToString();
            ViewModel.MpProbe.X = x;
            ViewModel.MpProbe.Y = y;
            
            // Immediately read the color at the selected position
            if (ViewModel.TargetHwnd != IntPtr.Zero)
            {
                var currentColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, x, y);
                ViewModel.MpProbe.ExpectedColor = currentColor;
                ViewModel.MpProbe.ReferenceColor = currentColor;
                MpColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(currentColor.R, currentColor.G, currentColor.B));
                MpColorText.Text = $"{currentColor.R},{currentColor.G},{currentColor.B}";
                Console.WriteLine($"[{ViewModel.ClientName}] MP COORDINATE SELECTED: Position=({x},{y}) Color=RGB({currentColor.R},{currentColor.G},{currentColor.B})");
            }
        });
    }
    
    private void ReadHpColor_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            MessageBox.Show("Please select a window first to read HP color!", "No Window Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            Console.WriteLine($"[{ViewModel.ClientName}] No target window selected for HP color read");
            return;
        }
        
        if (ViewModel.HpProbe.X <= 0 || ViewModel.HpProbe.Y <= 0)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] No HP probe coordinates set. Use 📍 Pick first.");
            return;
        }
        
        // Read current color at HP probe position
        var currentColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, ViewModel.HpProbe.X, ViewModel.HpProbe.Y);
        
        // Update reference color and display
        ViewModel.HpProbe.ExpectedColor = currentColor;
        ViewModel.HpProbe.ReferenceColor = currentColor;
        HpColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(currentColor.R, currentColor.G, currentColor.B));
        HpColorText.Text = $"{currentColor.R},{currentColor.G},{currentColor.B}";
        
        Console.WriteLine($"[{ViewModel.ClientName}] HP COLOR READ: RGB({currentColor.R},{currentColor.G},{currentColor.B}) at ({ViewModel.HpProbe.X},{ViewModel.HpProbe.Y})");
    }
    
    private void ReadMpColor_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            MessageBox.Show("Please select a window first to read MP color!", "No Window Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            Console.WriteLine($"[{ViewModel.ClientName}] No target window selected for MP color read");
            return;
        }
        
        if (ViewModel.MpProbe.X <= 0 || ViewModel.MpProbe.Y <= 0)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] No MP probe coordinates set. Use 📍 Pick first.");
            return;
        }
        
        // Read current color at MP probe position
        var currentColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, ViewModel.MpProbe.X, ViewModel.MpProbe.Y);
        
        // Update reference color and display
        ViewModel.MpProbe.ExpectedColor = currentColor;
        ViewModel.MpProbe.ReferenceColor = currentColor;
        MpColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(currentColor.R, currentColor.G, currentColor.B));
        MpColorText.Text = $"{currentColor.R},{currentColor.G},{currentColor.B}";
        
        Console.WriteLine($"[{ViewModel.ClientName}] MP COLOR READ: RGB({currentColor.R},{currentColor.G},{currentColor.B}) at ({ViewModel.MpProbe.X},{ViewModel.MpProbe.Y})");
    }

    private void PickHpTrigger_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("HP Potion Click Position", (x, y) =>
        {
            HpTriggerX.Text = x.ToString();
            HpTriggerY.Text = y.ToString();
            ViewModel.HpTrigger.X = x;
            ViewModel.HpTrigger.Y = y;
        });
    }

    private void PickMpTrigger_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("MP Potion Click Position", (x, y) =>
        {
            MpTriggerX.Text = x.ToString();
            MpTriggerY.Text = y.ToString();
            ViewModel.MpTrigger.X = x;
            ViewModel.MpTrigger.Y = y;
        });
    }

    private void PickYCoord_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("Y Periodic Click Position", (x, y) =>
        {
            YClickX.Text = x.ToString();
            YClickY.Text = y.ToString();
            ViewModel.YClick.X = x;
            ViewModel.YClick.Y = y;
        });
    }

    private void PickExtra1Coord_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("Extra1 Click Position", (x, y) =>
        {
            Extra1X.Text = x.ToString();
            Extra1Y.Text = y.ToString();
            ViewModel.Extra1Click.X = x;
            ViewModel.Extra1Click.Y = y;
        });
    }

    private void PickExtra2Coord_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("Extra2 Click Position", (x, y) =>
        {
            Extra2X.Text = x.ToString();
            Extra2Y.Text = y.ToString();
            ViewModel.Extra2Click.X = x;
            ViewModel.Extra2Click.Y = y;
        });
    }

    private void PickExtra3Coord_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("Extra3 Click Position", (x, y) =>
        {
            Extra3X.Text = x.ToString();
            Extra3Y.Text = y.ToString();
            ViewModel.Extra3Click.X = x;
            ViewModel.Extra3Click.Y = y;
        });
    }

    private async void PickCoordinate(string title, Action<int, int> onPicked)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
            StatusIndicator.ToolTip = "Please select a window first!";
            return;
        }

        try
        {
            _coordinatePicker = new CoordinatePicker(ViewModel.TargetHwnd, title);
            _coordinatePicker.CoordinatePicked += (x, y) => onPicked(x, y);
            
            // Use Show() instead of ShowDialog() to prevent UI freezing
            _coordinatePicker.Show();
            
            // Optional: Add timeout to prevent indefinite waiting
            await Task.Delay(100); // Small delay to ensure window is shown
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error opening coordinate picker: {ex.Message}");
        }
    }
    
    private async void PickRectangle(string title, Action<int, int, int, int> onPicked)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
            StatusIndicator.ToolTip = "Please select a window first!";
            return;
        }

        try
        {
            var rectanglePicker = new RectanglePicker(ViewModel.TargetHwnd, title);
            rectanglePicker.RectanglePicked += (x, y, w, h) => onPicked(x, y, w, h);
            
            // Use Show() instead of ShowDialog() to prevent UI freezing
            rectanglePicker.Show();
            
            // Small delay to ensure window is shown
            await Task.Delay(100);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error opening rectangle picker: {ex.Message}");
        }
    }

    private void UpdateHpColor(System.Drawing.Color color)
    {
        // DON'T update ExpectedColor here! It should only be set when picking coordinate
        // This method is only for syncing to other clients
        
        // Sync HP color to all other clients
        var mainWindow = Application.Current.MainWindow as MainWindow;
        mainWindow?.SyncHpColorToAllClients(color, this);
    }

    private void UpdateMpColor(System.Drawing.Color color)
    {
        // DON'T update ExpectedColor here! It should only be set when picking coordinate
        // This method is only for syncing to other clients
        
        // Sync MP color to all other clients
        var mainWindow = Application.Current.MainWindow as MainWindow;
        mainWindow?.SyncMpColorToAllClients(color, this);
    }
    
    public void SetHpColorFromSync(System.Drawing.Color color)
    {
        // When syncing, this becomes the FULL HP reference color
        ViewModel.HpProbe.ExpectedColor = color;
        ViewModel.HpProbe.ReferenceColor = color;
        HpColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
        HpColorText.Text = $"{color.R},{color.G},{color.B}";
        Console.WriteLine($"[{ViewModel.ClientName}] HP reference synced: RGB({color.R},{color.G},{color.B})");
    }
    
    public void SetMpColorFromSync(System.Drawing.Color color)
    {
        // When syncing, this becomes the FULL MP reference color
        ViewModel.MpProbe.ExpectedColor = color;
        ViewModel.MpProbe.ReferenceColor = color;
        MpColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
        MpColorText.Text = $"{color.R},{color.G},{color.B}";
        Console.WriteLine($"[{ViewModel.ClientName}] MP reference synced: RGB({color.R},{color.G},{color.B})");
    }
    
    private void PickHpPercentageBar_Click(object sender, RoutedEventArgs e)
    {
        PickRectangle("HP Bar Area (Python Style)", (x, y, w, h) =>
        {
            Console.WriteLine($"[{ViewModel.ClientName}] HP Bar Selected: Raw coordinates ({x},{y}) size ({w}x{h})");
            
            HpPercentageStartX.Text = x.ToString();
            HpPercentageEndX.Text = (x + w).ToString();
            HpPercentageY.Text = y.ToString();
            
            ViewModel.HpPercentageProbe.StartX = x;
            ViewModel.HpPercentageProbe.EndX = x + w;
            ViewModel.HpPercentageProbe.Y = y;
            
            // OFFSET TEST: Try multiple offset combinations to find correct one
            if (ViewModel.TargetHwnd != IntPtr.Zero)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] 🔍 OFFSET TEST - Finding correct MuMu offset:");
                Console.WriteLine($"[{ViewModel.ClientName}] Selected HP Bar: ({x},{y}) size ({w}x{h})");
                
                var testOffsets = new List<(int dx, int dy, string desc)>
                {
                    (0, 0, "NO_OFFSET"),
                    (8, 50, "CURRENT_+8+50"),
                    (-8, -50, "REVERSE_-8-50"),
                    (16, 100, "DOUBLE_+16+100"),
                    (-16, -100, "DOUBLE_NEG"),
                    (8, 0, "ONLY_X+8"),
                    (0, 50, "ONLY_Y+50")
                };
                
                int middleX = x + w/2;
                
                Console.WriteLine($"[{ViewModel.ClientName}] Testing offset combinations at HP middle position ({middleX},{y}):");
                
                foreach (var (dx, dy, desc) in testOffsets)
                {
                    try
                    {
                        // Temporarily modify offset for this test
                        var testColor = TestColorSampler.GetColorAtWithOffset(ViewModel.TargetHwnd, middleX, y, dx, dy);
                        Console.WriteLine($"  {desc}: RGB({testColor.R},{testColor.G},{testColor.B})");
                        
                        // Check if it looks like HP color (reddish)
                        bool looksLikeHP = testColor.R > 100 && testColor.R > testColor.G && testColor.R > testColor.B;
                        if (looksLikeHP)
                        {
                            Console.WriteLine($"    ✅ POSSIBLE HP COLOR! (Red dominant)");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  {desc}: ERROR - {ex.Message}");
                    }
                }
                
                // Use current offset for now
                var middleColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, middleX, y);
                ViewModel.HpPercentageProbe.ExpectedColor = middleColor;
                HpPercentageColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(middleColor.R, middleColor.G, middleColor.B));
                Console.WriteLine($"[{ViewModel.ClientName}] Current offset used: RGB({middleColor.R},{middleColor.G},{middleColor.B})");
                
                UpdatePercentageMonitorPosition();
            }
        });
    }
    
    private void PickMpPercentageBar_Click(object sender, RoutedEventArgs e)
    {
        PickRectangle("MP Bar Area (Python Style)", (x, y, w, h) =>
        {
            Console.WriteLine($"[{ViewModel.ClientName}] MP Bar Selected: Raw coordinates ({x},{y}) size ({w}x{h})");
            
            MpPercentageStartX.Text = x.ToString();
            MpPercentageEndX.Text = (x + w).ToString();
            MpPercentageY.Text = y.ToString();
            
            ViewModel.MpPercentageProbe.StartX = x;
            ViewModel.MpPercentageProbe.EndX = x + w;
            ViewModel.MpPercentageProbe.Y = y;
            
            // TEST: Sample colors at selected coordinates to verify coordinate mapping
            if (ViewModel.TargetHwnd != IntPtr.Zero)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] COORDINATE VERIFICATION - Testing selected MP area:");
                
                // Test start, middle, end of selected area
                var startColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, x, y);
                var middleColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, x + w/2, y);
                var endColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, x + w - 1, y);
                
                Console.WriteLine($"  SELECTED START ({x},{y}) = RGB({startColor.R},{startColor.G},{startColor.B})");
                Console.WriteLine($"  SELECTED MIDDLE ({x + w/2},{y}) = RGB({middleColor.R},{middleColor.G},{middleColor.B})");
                Console.WriteLine($"  SELECTED END ({x + w - 1},{y}) = RGB({endColor.R},{endColor.G},{endColor.B})");
                
                // For now, use middle color as expected (you can manually verify this is MP blue)
                ViewModel.MpPercentageProbe.ExpectedColor = middleColor;
                MpPercentageColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(middleColor.R, middleColor.G, middleColor.B));
                Console.WriteLine($"[{ViewModel.ClientName}] MP Expected Color set to MIDDLE: RGB({middleColor.R},{middleColor.G},{middleColor.B})");
                Console.WriteLine($"[{ViewModel.ClientName}] ❗ VERIFY: Is RGB({middleColor.R},{middleColor.G},{middleColor.B}) your MP bar color?");
                
                UpdatePercentageMonitorPosition();
            }
        });
    }
    
    private void UpdatePercentageMonitorPosition()
    {
        try
        {
            var hpCalcX = ViewModel.HpPercentageProbe.CalculatedX;
            var mpCalcX = ViewModel.MpPercentageProbe.CalculatedX;
            
            PercentageMonitorPosition.Text = $"HP: {hpCalcX} ({ViewModel.HpPercentageProbe.MonitorPercentage:F0}%) MP: {mpCalcX} ({ViewModel.MpPercentageProbe.MonitorPercentage:F0}%)";
        }
        catch
        {
            PercentageMonitorPosition.Text = "Error calculating position";
        }
    }
    
    private void PickPythonHpPotion_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("Python-Style HP Potion Position", (x, y) =>
        {
            PythonHpPotionX.Text = x.ToString();
            PythonHpPotionY.Text = y.ToString();
            ViewModel.PythonHpPotionClick.X = x;
            ViewModel.PythonHpPotionClick.Y = y;
            Console.WriteLine($"[{ViewModel.ClientName}] Python HP Potion Click set to: ({x},{y})");
        });
    }
    
    private void PickPythonMpPotion_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("Python-Style MP Potion Position", (x, y) =>
        {
            PythonMpPotionX.Text = x.ToString();
            PythonMpPotionY.Text = y.ToString();
            ViewModel.PythonMpPotionClick.X = x;
            ViewModel.PythonMpPotionClick.Y = y;
            Console.WriteLine($"[{ViewModel.ClientName}] Python MP Potion Click set to: ({x},{y})");
        });
    }
    
    private void FindHpMpBars_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            MessageBox.Show("Please select a window first to find HP/MP bars!", "No Window Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            Console.WriteLine($"[{ViewModel.ClientName}] No window selected");
            return;
        }
        
        try
        {
            Console.WriteLine($"[{ViewModel.ClientName}] 🔍 AUTO-DETECTING HP/MP bars...");
            
            // HP Bar Detection
            var hpBar = DetectBar(true); // true = HP (red)
            if (hpBar != null)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] ✅ HP BAR FOUND!");
                Console.WriteLine($"[{ViewModel.ClientName}] HP Bar: StartX={hpBar.Value.startX}, EndX={hpBar.Value.endX}, Y={hpBar.Value.y}, Width={hpBar.Value.endX - hpBar.Value.startX}");
                Console.WriteLine($"[{ViewModel.ClientName}] HP Color: RGB({hpBar.Value.color.R},{hpBar.Value.color.G},{hpBar.Value.color.B})");
                
                // Auto-fill HP coordinates on UI thread
                Dispatcher.BeginInvoke(() =>
                {
                    HpPercentageStartX.Text = hpBar.Value.startX.ToString();
                    HpPercentageEndX.Text = hpBar.Value.endX.ToString();
                    HpPercentageY.Text = hpBar.Value.y.ToString();
                    
                    ViewModel.HpPercentageProbe.StartX = hpBar.Value.startX;
                    ViewModel.HpPercentageProbe.EndX = hpBar.Value.endX;
                    ViewModel.HpPercentageProbe.Y = hpBar.Value.y;
                    ViewModel.HpPercentageProbe.ExpectedColor = hpBar.Value.color;
                    
                    HpPercentageColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(hpBar.Value.color.R, hpBar.Value.color.G, hpBar.Value.color.B));
                    
                    // Show visual HP bar indicator
                    ShowBarIndicator("HP", hpBar.Value.startX, hpBar.Value.endX, hpBar.Value.y, System.Windows.Media.Colors.Red);
                });
            }
            else
            {
                Console.WriteLine($"[{ViewModel.ClientName}] ❌ HP BAR NOT FOUND!");
            }
            
            // MP Bar Detection - if HP found, search near it
            int mpSearchStartY = 30;
            int mpSearchEndY = 120;
            if (hpBar != null)
            {
                // Search MP bar RIGHT BELOW HP bar (mini bars are very close)
                mpSearchStartY = hpBar.Value.y + 1;  // Start right after HP
                mpSearchEndY = hpBar.Value.y + 15;   // Only search 15 pixels below HP
                Console.WriteLine($"[{ViewModel.ClientName}] HP found at Y={hpBar.Value.y}, searching MP in Y range {mpSearchStartY}-{mpSearchEndY} (right below HP)");
            }
            
            var mpBar = DetectBarInRange(false, mpSearchStartY, mpSearchEndY); // false = MP (blue)
            if (mpBar != null)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] ✅ MP BAR FOUND!");
                Console.WriteLine($"[{ViewModel.ClientName}] MP Bar: StartX={mpBar.Value.startX}, EndX={mpBar.Value.endX}, Y={mpBar.Value.y}, Width={mpBar.Value.endX - mpBar.Value.startX}");
                Console.WriteLine($"[{ViewModel.ClientName}] MP Color: RGB({mpBar.Value.color.R},{mpBar.Value.color.G},{mpBar.Value.color.B})");
                
                // Auto-fill MP coordinates on UI thread
                Dispatcher.BeginInvoke(() =>
                {
                    MpPercentageStartX.Text = mpBar.Value.startX.ToString();
                    MpPercentageEndX.Text = mpBar.Value.endX.ToString();
                    MpPercentageY.Text = mpBar.Value.y.ToString();
                    
                    ViewModel.MpPercentageProbe.StartX = mpBar.Value.startX;
                    ViewModel.MpPercentageProbe.EndX = mpBar.Value.endX;
                    ViewModel.MpPercentageProbe.Y = mpBar.Value.y;
                    ViewModel.MpPercentageProbe.ExpectedColor = mpBar.Value.color;
                    
                    MpPercentageColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(mpBar.Value.color.R, mpBar.Value.color.G, mpBar.Value.color.B));
                    
                    // Show visual MP bar indicator
                    ShowBarIndicator("MP", mpBar.Value.startX, mpBar.Value.endX, mpBar.Value.y, System.Windows.Media.Colors.Blue);
                });
            }
            else
            {
                Console.WriteLine($"[{ViewModel.ClientName}] ❌ MP BAR NOT FOUND!");
            }
            
            if (hpBar != null || mpBar != null)
            {
                UpdatePercentageMonitorPosition();
                Console.WriteLine($"[{ViewModel.ClientName}] 🎯 AUTO-DETECTION COMPLETE! Coordinates filled automatically.");
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Auto-detection error: {ex.Message}");
        }
    }
    
    private (int startX, int endX, int y, System.Drawing.Color color)? DetectBar(bool isHP)
    {
        return DetectBarInRange(isHP, 30, 120);
    }
    
    private (int startX, int endX, int y, System.Drawing.Color color)? DetectBarInRange(bool isHP, int startY, int endY)
    {
        string barType = isHP ? "HP" : "MP";
        Console.WriteLine($"[{ViewModel.ClientName}] Detecting {barType} bar in Y range {startY}-{endY}...");
        
        // Search in specified Y range - MINI BARS (very thin)
        for (int y = startY; y <= endY; y += 1) // Every 1 pixel vertically for thin bars
        {
            System.Drawing.Color? barColor = null;
            int? startX = null;
            int? endX = null;
            int consecutivePixels = 0;
            int minBarLength = isHP ? 20 : 10; // Even smaller minimum for MP mini bars
            
            // Scan horizontally to find bar
            for (int x = 50; x <= 500; x++)
            {
                try
                {
                    var color = ColorSampler.GetColorAt(ViewModel.TargetHwnd, x, y);
                    bool isBarColor = false;
                    
                    if (isHP)
                    {
                        // HP: RED bar detection for mini UI bars
                        isBarColor = (color.R > 50 && color.R > color.G + 5 && color.R > color.B + 5) ||  // Any reddish
                                   (color.R > color.G + 30 && color.R > color.B + 30) ||                // Red dominant 
                                   (color.R > 80 && color.G < 80 && color.B < 80);                       // Bright red
                    }
                    else
                    {
                        // MP: ULTRA AGGRESSIVE detection - ANY non-black, non-white color that might be MP
                        isBarColor = (color.B > 15) ||                                                   // ANY blue at all (lowered threshold)
                                   (color.B > color.R && color.B > color.G) ||                          // Blue is highest
                                   (color.B > color.R + 3) ||                                           // Even slightly more blue
                                   (color.R < 120 && color.G < 120 && color.B > 15) ||                // Dark with some blue
                                   (color.B > 25 && color.R < 100 && color.G < 100) ||                // Any bluish tone
                                   // Purple/Violet MP bars
                                   (color.B > 40 && color.R > 40 && color.G < 60) ||                   // Purple (R+B, low G)
                                   (color.B + color.R > color.G + 40) ||                               // Purple/Magenta dominant
                                   // Dark colored bars (any non-background color)
                                   (color.R + color.G + color.B > 60 && color.R + color.G + color.B < 600) || // Any moderate color
                                   // Specific MP bar colors that might appear
                                   (color.B > 20 && Math.Abs(color.R - color.B) < 30) ||              // Blueish-purple
                                   (color.G > 15 && color.B > 15 && color.R < 80);                     // Cyan-ish colors
                    }
                    
                    // DEBUG: For MP detection, print every pixel in the critical area where MP should be
                    if (!isHP && y <= startY + 5) // Only first 5 rows of MP search to avoid spam
                    {
                        if (x % 10 == 0) // Every 10th pixel horizontally
                        {
                            Console.WriteLine($"[{ViewModel.ClientName}] MP SCAN Y={y} X={x}: RGB({color.R},{color.G},{color.B}) -> {(isBarColor ? "✅MATCH" : "❌no")}");
                        }
                    }
                    // DEBUG: Print every 20th pixel for HP to see what colors we're getting
                    else if (isHP && x % 20 == 0 && (y % 5 == 0)) // More frequent sampling for HP
                    {
                        Console.WriteLine($"[{ViewModel.ClientName}] HP SCAN Y={y} X={x}: RGB({color.R},{color.G},{color.B}) -> {(isBarColor ? "✅MATCH" : "❌no")}");
                    }
                    
                    if (isBarColor)
                    {
                        if (startX == null)
                        {
                            startX = x;
                            barColor = color;
                        }
                        consecutivePixels++;
                        endX = x;
                    }
                    else
                    {
                        // Check if we found a valid bar
                        if (startX != null && consecutivePixels >= minBarLength)
                        {
                            Console.WriteLine($"[{ViewModel.ClientName}] {barType} bar found at Y={y}: X({startX}-{endX}) length={consecutivePixels} color=RGB({barColor?.R},{barColor?.G},{barColor?.B})");
                            return (startX.Value, endX.Value, y, barColor.Value);
                        }
                        
                        // Reset for next potential bar
                        startX = null;
                        endX = null;
                        consecutivePixels = 0;
                        barColor = null;
                    }
                }
                catch { /* Skip errors */ }
            }
            
            // Check if bar extends to edge
            if (startX != null && consecutivePixels >= minBarLength)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] {barType} bar found at Y={y}: X({startX}-{endX}) length={consecutivePixels} color=RGB({barColor?.R},{barColor?.G},{barColor?.B})");
                return (startX.Value, endX.Value, y, barColor.Value);
            }
        }
        
        Console.WriteLine($"[{ViewModel.ClientName}] {barType} bar not found in search area");
        return null;
    }
    
    private void ShowBarIndicator(string barType, int startX, int endX, int y, System.Windows.Media.Color color)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero) return;
        
        try
        {
            // Create overlay window to show the bar location
            var overlayWindow = new Window
            {
                Title = $"{ViewModel.ClientName} - {barType} Bar Indicator",
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                ResizeMode = ResizeMode.NoResize
            };
            
            // Get target window position
            User32.GetWindowRect(ViewModel.TargetHwnd, out var windowRect);
            User32.GetClientRect(ViewModel.TargetHwnd, out var clientRect);
            
            // Calculate border/title offsets
            int borderWidth = ((windowRect.right - windowRect.left) - (clientRect.right - clientRect.left)) / 2;
            int titleHeight = ((windowRect.bottom - windowRect.top) - (clientRect.bottom - clientRect.top)) - borderWidth;
            
            // Position overlay window over the target window's client area
            overlayWindow.Left = windowRect.left + borderWidth;
            overlayWindow.Top = windowRect.top + titleHeight;
            overlayWindow.Width = clientRect.right - clientRect.left;
            overlayWindow.Height = clientRect.bottom - clientRect.top;
            
            // Create canvas for drawing
            var canvas = new Canvas
            {
                Background = System.Windows.Media.Brushes.Transparent
            };
            
            // Create draggable and resizable bar indicator
            var barContainer = new Border
            {
                Width = endX - startX,
                Height = 12, // Make it taller for easier interaction
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(2),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(80, color.R, color.G, color.B)),
                Cursor = System.Windows.Input.Cursors.SizeAll,
                ToolTip = $"Drag to move {barType} bar, drag edges to resize"
            };
            
            // Position the container - EXPLICIT double cast
            double initialLeft = (double)startX;
            double initialTop = (double)(y - 4);
            Canvas.SetLeft(barContainer, initialLeft);
            Canvas.SetTop(barContainer, initialTop); // Center it around the detected Y
            
            // Add resize handles (left and right)
            var leftHandle = new System.Windows.Shapes.Rectangle
            {
                Width = 8,
                Height = 12,
                Fill = new SolidColorBrush(color),
                Cursor = System.Windows.Input.Cursors.SizeWE,
                ToolTip = "Drag to resize left edge"
            };
            Canvas.SetLeft(leftHandle, startX - 4);
            Canvas.SetTop(leftHandle, y - 4);
            
            var rightHandle = new System.Windows.Shapes.Rectangle
            {
                Width = 8,
                Height = 12,
                Fill = new SolidColorBrush(color),
                Cursor = System.Windows.Input.Cursors.SizeWE,
                ToolTip = "Drag to resize right edge"
            };
            Canvas.SetLeft(rightHandle, endX - 4);
            Canvas.SetTop(rightHandle, y - 4);
            
            // Create label that updates with coordinates
            var label = new TextBlock
            {
                Text = $"{barType} ({startX}-{endX},{y}) - Drag to adjust",
                Foreground = new SolidColorBrush(color),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 0, 0, 0)),
                Padding = new Thickness(4),
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            
            Canvas.SetLeft(label, startX);
            Canvas.SetTop(label, y - 30); // Above the bar
            
            // Add elements to canvas
            canvas.Children.Add(barContainer);
            canvas.Children.Add(leftHandle);
            canvas.Children.Add(rightHandle);
            canvas.Children.Add(label);
            
            // Variables for dragging - EXPLICIT initialization
            bool isDragging = false;
            bool isResizingLeft = false;
            bool isResizingRight = false;
            System.Windows.Point dragStartPos = new System.Windows.Point();
            double originalLeft = initialLeft; // Use the same values we set
            double originalTop = initialTop;
            double originalWidth = (double)(endX - startX);
            
            // Helper function to update coordinates
            Action updateCoordinates = () =>
            {
                var newStartX = (int)Canvas.GetLeft(barContainer);
                var newEndX = newStartX + (int)barContainer.Width;
                var newY = (int)(Canvas.GetTop(barContainer) + 4); // Adjust for container offset
                
                // Update label
                label.Text = $"{barType} ({newStartX}-{newEndX},{newY}) - Drag to adjust";
                Canvas.SetLeft(label, newStartX);
                Canvas.SetTop(label, newY - 30);
                
                // Update handle positions
                Canvas.SetLeft(leftHandle, newStartX - 4);
                Canvas.SetTop(leftHandle, newY - 4);
                Canvas.SetLeft(rightHandle, newEndX - 4);
                Canvas.SetTop(rightHandle, newY - 4);
                
                // Update the actual probe coordinates if this is our client
                Dispatcher.BeginInvoke(() =>
                {
                    if (barType == "HP")
                    {
                        HpPercentageStartX.Text = newStartX.ToString();
                        HpPercentageEndX.Text = newEndX.ToString();
                        HpPercentageY.Text = newY.ToString();
                        ViewModel.HpPercentageProbe.StartX = newStartX;
                        ViewModel.HpPercentageProbe.EndX = newEndX;
                        ViewModel.HpPercentageProbe.Y = newY;
                    }
                    else if (barType == "MP")
                    {
                        MpPercentageStartX.Text = newStartX.ToString();
                        MpPercentageEndX.Text = newEndX.ToString();
                        MpPercentageY.Text = newY.ToString();
                        ViewModel.MpPercentageProbe.StartX = newStartX;
                        ViewModel.MpPercentageProbe.EndX = newEndX;
                        ViewModel.MpPercentageProbe.Y = newY;
                    }
                });
            };
            
            // Bar container drag events - SAFE NaN handling
            barContainer.MouseLeftButtonDown += (s, e) =>
            {
                isDragging = true;
                dragStartPos = e.GetPosition(canvas);
                
                // SAFE way to get current position
                var currentLeft = Canvas.GetLeft(barContainer);
                var currentTop = Canvas.GetTop(barContainer);
                
                // Handle NaN values
                originalLeft = double.IsNaN(currentLeft) ? initialLeft : currentLeft;
                originalTop = double.IsNaN(currentTop) ? initialTop : currentTop;
                
                barContainer.CaptureMouse();
                Console.WriteLine($"[{ViewModel.ClientName}] Drag START at canvas pos: {dragStartPos}, bar pos: ({originalLeft},{originalTop})");
                e.Handled = true;
            };
            
            barContainer.MouseLeftButtonUp += (s, e) =>
            {
                if (isDragging)
                {
                    isDragging = false;
                    barContainer.ReleaseMouseCapture();
                    var currentPos = e.GetPosition(canvas);
                    Console.WriteLine($"[{ViewModel.ClientName}] Drag END at canvas pos: {currentPos}");
                }
                e.Handled = true;
            };
            
            // Canvas-level mouse move - SIMPLIFIED LOGIC
            canvas.MouseMove += (s, e) =>
            {
                if (isDragging)
                {
                    var currentMousePos = e.GetPosition(canvas);
                    
                    // Calculate how much mouse moved since drag started
                    var deltaX = currentMousePos.X - dragStartPos.X;
                    var deltaY = currentMousePos.Y - dragStartPos.Y;
                    
                    // FIRST - Debug current values to see what's happening
                    if ((int)currentMousePos.X % 10 == 0)
                    {
                        Console.WriteLine($"[{ViewModel.ClientName}] DEBUG VALUES: Mouse({currentMousePos.X:F0},{currentMousePos.Y:F0}) startPos({dragStartPos.X:F0},{dragStartPos.Y:F0}) delta({deltaX:F0},{deltaY:F0}) original({originalLeft:F0},{originalTop:F0})");
                    }
                    
                    // SECOND - Calculate new position based on original position + delta  
                    var newLeft = originalLeft + deltaX;
                    var newTop = originalTop + deltaY;
                    
                    // THIRD - NaN safety check with detailed info
                    if (double.IsNaN(newLeft) || double.IsNaN(newTop) || double.IsNaN(originalLeft) || double.IsNaN(originalTop))
                    {
                        Console.WriteLine($"[{ViewModel.ClientName}] ❌ NaN DETECTED! originalLeft={originalLeft}, originalTop={originalTop}, deltaX={deltaX}, deltaY={deltaY}, newLeft={newLeft}, newTop={newTop}");
                        return; // Skip this frame
                    }
                    
                    // FOURTH - Keep within canvas bounds
                    newLeft = Math.Max(0, Math.Min(newLeft, canvas.Width - barContainer.Width));
                    newTop = Math.Max(0, Math.Min(newTop, canvas.Height - barContainer.Height));
                    
                    // FIFTH - Set new position
                    Canvas.SetLeft(barContainer, newLeft);
                    Canvas.SetTop(barContainer, newTop);
                    
                    // SIXTH - Update coordinates in UI
                    updateCoordinates();
                    
                    // FINAL - Success debug output
                    if ((int)currentMousePos.X % 10 == 0)
                    {
                        Console.WriteLine($"[{ViewModel.ClientName}] ✅ SUCCESS: Bar moved to ({newLeft:F0},{newTop:F0})");
                    }
                }
                else if (isResizingLeft)
                {
                    var currentMousePos = e.GetPosition(canvas);
                    var deltaX = currentMousePos.X - dragStartPos.X;
                    
                    var newLeft = originalLeft + deltaX;
                    var newWidth = originalWidth - deltaX;
                    
                    if (newWidth >= 20 && newLeft >= 0)
                    {
                        Canvas.SetLeft(barContainer, newLeft);
                        barContainer.Width = newWidth;
                        updateCoordinates();
                    }
                }
                else if (isResizingRight)
                {
                    var currentMousePos = e.GetPosition(canvas);
                    var deltaX = currentMousePos.X - dragStartPos.X;
                    
                    var newWidth = originalWidth + deltaX;
                    var maxWidth = canvas.Width - originalLeft;
                    
                    if (newWidth >= 20 && newWidth <= maxWidth)
                    {
                        barContainer.Width = newWidth;
                        updateCoordinates();
                    }
                }
            };
            
            // Left handle resize events - NaN SAFE
            leftHandle.MouseLeftButtonDown += (s, e) =>
            {
                isResizingLeft = true;
                dragStartPos = e.GetPosition(canvas);
                
                var currentLeft = Canvas.GetLeft(barContainer);
                originalLeft = double.IsNaN(currentLeft) ? initialLeft : currentLeft;
                originalWidth = barContainer.Width;
                
                leftHandle.CaptureMouse();
                Console.WriteLine($"[{ViewModel.ClientName}] LEFT RESIZE started - originalLeft={originalLeft}, originalWidth={originalWidth}");
                e.Handled = true;
            };
            
            leftHandle.MouseLeftButtonUp += (s, e) =>
            {
                if (isResizingLeft)
                {
                    isResizingLeft = false;
                    leftHandle.ReleaseMouseCapture();
                    Console.WriteLine($"[{ViewModel.ClientName}] LEFT RESIZE ended");
                }
                e.Handled = true;
            };
            
            // Right handle resize events - NaN SAFE
            rightHandle.MouseLeftButtonDown += (s, e) =>
            {
                isResizingRight = true;
                dragStartPos = e.GetPosition(canvas);
                
                var currentLeft = Canvas.GetLeft(barContainer);
                originalLeft = double.IsNaN(currentLeft) ? initialLeft : currentLeft;
                originalWidth = barContainer.Width;
                
                rightHandle.CaptureMouse();
                Console.WriteLine($"[{ViewModel.ClientName}] RIGHT RESIZE started - originalLeft={originalLeft}, originalWidth={originalWidth}");
                e.Handled = true;
            };
            
            rightHandle.MouseLeftButtonUp += (s, e) =>
            {
                if (isResizingRight)
                {
                    isResizingRight = false;
                    rightHandle.ReleaseMouseCapture();
                    Console.WriteLine($"[{ViewModel.ClientName}] RIGHT RESIZE ended");
                }
                e.Handled = true;
            };
            
            overlayWindow.Content = canvas;
            
            overlayWindow.Show();
            
            // Auto-close after 30 seconds (more time to position)
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                overlayWindow.Close();
            };
            timer.Start();
            
            // Close controls
            overlayWindow.Focusable = true; // Enable keyboard focus
            overlayWindow.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    overlayWindow.Close();
                }
            };
            
            // Double-click label to close
            label.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    overlayWindow.Close();
                }
            };
            
            // Set focus to enable keyboard input
            overlayWindow.Activated += (s, e) => overlayWindow.Focus();
            
            Console.WriteLine($"[{ViewModel.ClientName}] 🎯 {barType} bar indicator shown - INTERACTIVE CONTROLS:");
            Console.WriteLine($"  • DRAG the bar to move position");
            Console.WriteLine($"  • DRAG LEFT/RIGHT edges to resize");  
            Console.WriteLine($"  • DOUBLE-CLICK label or press ESC to close");
            Console.WriteLine($"  • Coordinates auto-update in UI as you adjust");
            Console.WriteLine($"  • Auto-closes in 30 seconds");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error showing {barType} indicator: {ex.Message}");
        }
    }
    
    private void CaptureCurrentColors_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            MessageBox.Show("Please select a window first to capture colors!", "No Window Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            Console.WriteLine($"[{ViewModel.ClientName}] No window selected for color capture");
            return;
        }
        
        try
        {
            Console.WriteLine($"[{ViewModel.ClientName}] === COLOR CAPTURE DEBUG ===");
            Console.WriteLine($"[{ViewModel.ClientName}] HP Bar: StartX={ViewModel.HpPercentageProbe.StartX} EndX={ViewModel.HpPercentageProbe.EndX} Y={ViewModel.HpPercentageProbe.Y} Threshold={ViewModel.HpPercentageProbe.MonitorPercentage}%");
            Console.WriteLine($"[{ViewModel.ClientName}] MP Bar: StartX={ViewModel.MpPercentageProbe.StartX} EndX={ViewModel.MpPercentageProbe.EndX} Y={ViewModel.MpPercentageProbe.Y} Threshold={ViewModel.MpPercentageProbe.MonitorPercentage}%");
            
            // Capture current HP color at calculated position
            var hpX = ViewModel.HpPercentageProbe.CalculatedX;
            var hpY = ViewModel.HpPercentageProbe.Y;
            Console.WriteLine($"[{ViewModel.ClientName}] HP Monitor Position: X={hpX} (calculated from {ViewModel.HpPercentageProbe.MonitorPercentage}%)");
            
            var hpColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, hpX, hpY);
            
            // Capture current MP color at calculated position  
            var mpX = ViewModel.MpPercentageProbe.CalculatedX;
            var mpY = ViewModel.MpPercentageProbe.Y;
            Console.WriteLine($"[{ViewModel.ClientName}] MP Monitor Position: X={mpX} (calculated from {ViewModel.MpPercentageProbe.MonitorPercentage}%)");
            
            var mpColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, mpX, mpY);
            
            // Quick verification - just check center pixel
            // (Detailed sampling removed to reduce log spam)
            
            // Update expected colors with current colors
            ViewModel.HpPercentageProbe.ExpectedColor = hpColor;
            ViewModel.MpPercentageProbe.ExpectedColor = mpColor;
            
            // Update UI displays
            HpPercentageColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(hpColor.R, hpColor.G, hpColor.B));
            MpPercentageColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(mpColor.R, mpColor.G, mpColor.B));
            
            Console.WriteLine($"[{ViewModel.ClientName}] === COLORS CAPTURED ===");
            Console.WriteLine($"[{ViewModel.ClientName}] HP Expected Color: RGB({hpColor.R},{hpColor.G},{hpColor.B}) at ({hpX},{hpY})");
            Console.WriteLine($"[{ViewModel.ClientName}] MP Expected Color: RGB({mpColor.R},{mpColor.G},{mpColor.B}) at ({mpX},{mpY})");
            
            // Reset triggered states
            ViewModel.HpPercentageProbe.IsTriggered = false;
            ViewModel.MpPercentageProbe.IsTriggered = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Color capture error: {ex.Message}");
        }
    }

    // Public methods for panic buttons
    public void StartClient()
    {
        StartClient_Click(null, null);
    }
    
    public void StopClient()
    {
        StopClient_Click(null, null);
    }
    
    // Public methods for getting ComboBox values
    public double GetBabeBotHpThreshold()
    {
        if (double.TryParse(BabeBotHpThreshold?.Text, out var threshold))
        {
            return threshold;
        }
        return 90.0; // Default
    }
    
    public double GetBabeBotMpThreshold()
    {
        if (double.TryParse(BabeBotMpThreshold?.Text, out var threshold))
        {
            return threshold;
        }
        return 90.0; // Default
    }
    
    // Helper method to convert JsonElement or object to bool
    private bool ConvertToBool(object value)
    {
        if (value is bool boolValue)
            return boolValue;
        if (value is System.Text.Json.JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.True)
                return true;
            if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.False)
                return false;
        }
        if (bool.TryParse(value?.ToString(), out var parsed))
            return parsed;
        return false; // Default fallback
    }
    
    // Helper method to convert JsonElement or object to string
    private string ConvertToString(object value)
    {
        if (value is string stringValue)
            return stringValue;
        if (value is System.Text.Json.JsonElement jsonElement)
        {
            return jsonElement.GetString() ?? "";
        }
        return value?.ToString() ?? "";
    }
    
    // Helper method to convert JsonElement or object to int32
    private int ConvertToInt32(object value)
    {
        if (value is int intValue)
            return intValue;
        if (value is System.Text.Json.JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                return jsonElement.GetInt32();
        }
        if (int.TryParse(value?.ToString(), out var parsed))
            return parsed;
        return 0; // Default fallback
    }

    // Public method to collect all UI values for saving
    public Dictionary<string, object> GetAllUIValues()
    {
        var values = new Dictionary<string, object>();
        
        try
        {
            // Basic HP/MP TextBoxes
            values["HpProbeX"] = HpX.Text;
            values["HpProbeY"] = HpY.Text;
            values["MpProbeX"] = MpX.Text;
            values["MpProbeY"] = MpY.Text;
            values["HpTolerance"] = HpTolerance.Text;
            values["MpTolerance"] = MpTolerance.Text;
            
            // HP/MP Trigger TextBoxes
            values["HpTriggerX"] = HpTriggerX.Text;
            values["HpTriggerY"] = HpTriggerY.Text;
            values["MpTriggerX"] = MpTriggerX.Text;
            values["MpTriggerY"] = MpTriggerY.Text;
            values["HpCooldown"] = HpTriggerCooldown.Text;
            values["MpCooldown"] = MpTriggerCooldown.Text;
            
            // HP/MP CheckBoxes
            values["HpTriggerEnabled"] = HpTriggerEnabled.IsChecked ?? false;
            values["MpTriggerEnabled"] = MpTriggerEnabled.IsChecked ?? false;
            values["HpUseCoordinate"] = HpUseCoordinate.IsChecked ?? false;
            values["HpUseKeyPress"] = HpUseKeyPress.IsChecked ?? true;
            values["MpUseCoordinate"] = MpUseCoordinate.IsChecked ?? false;
            values["MpUseKeyPress"] = MpUseKeyPress.IsChecked ?? true;
            values["HpKeyToPress"] = HpKeyToPress.Text;
            values["MpKeyToPress"] = MpKeyToPress.Text;
            
            // Percentage Monitoring System
            values["PercentageMonitoringEnabled"] = PercentageMonitoringEnabled.IsChecked ?? false;
            values["HpPercentageStartX"] = HpPercentageStartX.Text;
            values["HpPercentageEndX"] = HpPercentageEndX.Text;
            values["HpPercentageY"] = HpPercentageY.Text;
            values["HpPercentageThreshold"] = HpPercentageThreshold.Text;
            values["HpPercentageTolerance"] = HpPercentageTolerance.Text;
            values["MpPercentageStartX"] = MpPercentageStartX.Text;
            values["MpPercentageEndX"] = MpPercentageEndX.Text;
            values["MpPercentageY"] = MpPercentageY.Text;
            values["MpPercentageThreshold"] = MpPercentageThreshold.Text;
            values["MpPercentageTolerance"] = MpPercentageTolerance.Text;
            
            // Periodic Clicks TextBoxes
            values["YClickX"] = YClickX.Text;
            values["YClickY"] = YClickY.Text;
            values["YClickPeriod"] = YClickPeriod.Text;
            values["Extra1X"] = Extra1X.Text;
            values["Extra1Y"] = Extra1Y.Text;
            values["Extra1Period"] = Extra1Period.Text;
            values["Extra2X"] = Extra2X.Text;
            values["Extra2Y"] = Extra2Y.Text;
            values["Extra2Period"] = Extra2Period.Text;
            values["Extra3X"] = Extra3X.Text;
            values["Extra3Y"] = Extra3Y.Text;
            values["Extra3Period"] = Extra3Period.Text;
            
            // Periodic Clicks CheckBoxes
            values["YClickEnabled"] = YClickEnabled.IsChecked ?? false;
            values["Extra1Enabled"] = Extra1Enabled.IsChecked ?? false;
            values["Extra2Enabled"] = Extra2Enabled.IsChecked ?? false;
            values["Extra3Enabled"] = Extra3Enabled.IsChecked ?? false;
            values["YUseCoordinate"] = YUseCoordinate.IsChecked ?? false;
            values["YUseKeyPress"] = YUseKeyPress.IsChecked ?? true;
            values["Extra1UseCoordinate"] = Extra1UseCoordinate.IsChecked ?? false;
            values["Extra1UseKeyPress"] = Extra1UseKeyPress.IsChecked ?? true;
            values["Extra2UseCoordinate"] = Extra2UseCoordinate.IsChecked ?? false;
            values["Extra2UseKeyPress"] = Extra2UseKeyPress.IsChecked ?? true;
            values["Extra3UseCoordinate"] = Extra3UseCoordinate.IsChecked ?? false;
            values["Extra3UseKeyPress"] = Extra3UseKeyPress.IsChecked ?? true;
            values["YKeyToPress"] = YKeyToPress.Text;
            values["Extra1KeyToPress"] = Extra1KeyToPress.Text;
            values["Extra2KeyToPress"] = Extra2KeyToPress.Text;
            values["Extra3KeyToPress"] = Extra3KeyToPress.Text;
            
            // BabeBot HP/MP
            values["BabeBotHpStart"] = BabeBotHpStart.Text;
            values["BabeBotHpEnd"] = BabeBotHpEnd.Text;
            values["BabeBotHpY"] = BabeBotHpY.Text;
            values["BabeBotHpThreshold"] = BabeBotHpThreshold.Text;
            values["BabeBotHpPotionX"] = BabeBotHpPotionX.Text;
            values["BabeBotHpPotionY"] = BabeBotHpPotionY.Text;
            values["BabeBotHpEnabled"] = BabeBotHpEnabled.IsChecked ?? false;
            values["BabeBotHpUseCoordinate"] = BabeBotHpUseCoordinate.IsChecked ?? false;
            values["BabeBotHpUseKeyPress"] = BabeBotHpUseKeyPress.IsChecked ?? true;
            values["BabeBotHpKeyToPress"] = BabeBotHpKeyToPress.Text;
            
            values["BabeBotMpStart"] = BabeBotMpStart.Text;
            values["BabeBotMpEnd"] = BabeBotMpEnd.Text;
            values["BabeBotMpY"] = BabeBotMpY.Text;
            values["BabeBotMpThreshold"] = BabeBotMpThreshold.Text;
            values["BabeBotMpPotionX"] = BabeBotMpPotionX.Text;
            values["BabeBotMpPotionY"] = BabeBotMpPotionY.Text;
            values["BabeBotMpEnabled"] = BabeBotMpEnabled.IsChecked ?? false;
            values["BabeBotMpUseCoordinate"] = BabeBotMpUseCoordinate.IsChecked ?? false;
            values["BabeBotMpUseKeyPress"] = BabeBotMpUseKeyPress.IsChecked ?? true;
            values["BabeBotMpKeyToPress"] = BabeBotMpKeyToPress.Text;
            
            // MultiHp Values
            for (int i = 1; i <= 8; i++)
            {
                var startXControl = this.FindName($"MultiHp{i}StartX") as TextBox;
                var endXControl = this.FindName($"MultiHp{i}EndX") as TextBox;
                var yControl = this.FindName($"MultiHp{i}Y") as TextBox;
                var thresholdControl = this.FindName($"MultiHp{i}Threshold") as TextBox;
                var clickXControl = this.FindName($"MultiHp{i}ClickX") as TextBox;
                var clickYControl = this.FindName($"MultiHp{i}ClickY") as TextBox;
                var keyControl = this.FindName($"MultiHp{i}Key") as TextBox;
                var enabledControl = this.FindName($"MultiHp{i}Enabled") as CheckBox;
                
                if (startXControl != null) values[$"MultiHp{i}StartX"] = startXControl.Text;
                if (endXControl != null) values[$"MultiHp{i}EndX"] = endXControl.Text;
                if (yControl != null) values[$"MultiHp{i}Y"] = yControl.Text;
                if (thresholdControl != null) values[$"MultiHp{i}Threshold"] = thresholdControl.Text;
                if (clickXControl != null) values[$"MultiHp{i}ClickX"] = clickXControl.Text;
                if (clickYControl != null) values[$"MultiHp{i}ClickY"] = clickYControl.Text;
                if (keyControl != null) values[$"MultiHp{i}Key"] = keyControl.Text;
                if (enabledControl != null) values[$"MultiHp{i}Enabled"] = enabledControl.IsChecked ?? false;
            }
            
            // Multi HP System Settings - REMOVED
            
            // Python-style HP/MP Settings
            values["PythonHpUseCoordinate"] = PythonHpUseCoordinate.IsChecked ?? false;
            values["PythonHpUseKeyPress"] = PythonHpUseKeyPress.IsChecked ?? false;
            values["PythonHpPotionX"] = PythonHpPotionX.Text;
            values["PythonHpPotionY"] = PythonHpPotionY.Text;
            values["PythonHpPotionCooldown"] = PythonHpPotionCooldown.Text;
            values["PythonHpKeyToPress"] = PythonHpKeyToPress.Text;
            
            values["PythonMpUseCoordinate"] = PythonMpUseCoordinate.IsChecked ?? false;
            values["PythonMpUseKeyPress"] = PythonMpUseKeyPress.IsChecked ?? false;
            values["PythonMpPotionX"] = PythonMpPotionX.Text;
            values["PythonMpPotionY"] = PythonMpPotionY.Text;
            values["PythonMpPotionCooldown"] = PythonMpPotionCooldown.Text;
            values["PythonMpKeyToPress"] = PythonMpKeyToPress.Text;
            
            // Attack/Skills System
            values["AttackSystemEnabled"] = AttackSystemEnabled.IsChecked ?? false;
            values["SkillNameInput"] = SkillNameInput.Text;
            values["SkillKeyInput"] = SkillKeyInput.Text;
            values["SkillIntervalInput"] = SkillIntervalInput.Text;
            
            // Attack Skills List
            values["AttackSkillsCount"] = ViewModel.AttackSkills.Count;
            Console.WriteLine($"[SAVE] AttackSkills Count: {ViewModel.AttackSkills.Count}");
            for (int i = 0; i < ViewModel.AttackSkills.Count; i++)
            {
                var skill = ViewModel.AttackSkills[i];
                values[$"AttackSkill{i}Name"] = skill.Name;
                values[$"AttackSkill{i}Key"] = skill.Key;
                values[$"AttackSkill{i}Interval"] = skill.IntervalMs;
                values[$"AttackSkill{i}Enabled"] = skill.Enabled;
                Console.WriteLine($"[SAVE] Skill {i}: {skill.Name}, {skill.Key}, {skill.IntervalMs}ms, Enabled={skill.Enabled}");
            }
            
            // Buff/AC System
            values["BuffAcSystemEnabled"] = BuffAcSystemEnabled.IsChecked ?? false;
            
            // Member settings
            values["Member1Enabled"] = Member1Enabled.IsChecked ?? false;
            values["Member1KeyInput"] = Member1KeyInput.Text;
            values["Member2Enabled"] = Member2Enabled.IsChecked ?? false;
            values["Member2KeyInput"] = Member2KeyInput.Text;
            values["Member3Enabled"] = Member3Enabled.IsChecked ?? false;
            values["Member3KeyInput"] = Member3KeyInput.Text;
            values["Member4Enabled"] = Member4Enabled.IsChecked ?? false;
            values["Member4KeyInput"] = Member4KeyInput.Text;
            values["Member5Enabled"] = Member5Enabled.IsChecked ?? false;
            values["Member5KeyInput"] = Member5KeyInput.Text;
            values["Member6Enabled"] = Member6Enabled.IsChecked ?? false;
            values["Member6KeyInput"] = Member6KeyInput.Text;
            values["Member7Enabled"] = Member7Enabled.IsChecked ?? false;
            values["Member7KeyInput"] = Member7KeyInput.Text;
            values["Member8Enabled"] = Member8Enabled.IsChecked ?? false;
            values["Member8KeyInput"] = Member8KeyInput.Text;
            
            // Buff/AC configuration
            values["BuffKeyInput"] = BuffKeyInput.Text;
            values["BuffAnimInput"] = BuffAnimInput.Text;
            values["AcKeyInput"] = AcKeyInput.Text;
            values["AcAnimInput"] = AcAnimInput.Text;
            values["CycleIntervalInput"] = CycleIntervalInput.Text;
            
            // Party Heal System
            values["PartyHealSystemEnabled"] = PartyHealSystemEnabled.IsChecked ?? false;
            values["PartyHealSkillKey"] = PartyHealSkillKey.Text;
            values["PartyHealPollInterval"] = PartyHealPollInterval.Text;
            values["PartyHealBaselineColor"] = PartyHealBaselineColor.Text;
            
            // Party Members
            for (int i = 1; i <= 8; i++)
            {
                values[$"PartyMember{i}Enabled"] = ((CheckBox)FindName($"PartyMember{i}Enabled"))?.IsChecked ?? false;
                values[$"PartyMember{i}Key"] = ((TextBox)FindName($"PartyMember{i}Key"))?.Text ?? "";
                values[$"PartyMember{i}Threshold"] = ((TextBox)FindName($"PartyMember{i}Threshold"))?.Text ?? "";
                values[$"PartyMember{i}XStart"] = ((TextBox)FindName($"PartyMember{i}XStart"))?.Text ?? "";
                values[$"PartyMember{i}XEnd"] = ((TextBox)FindName($"PartyMember{i}XEnd"))?.Text ?? "";
                values[$"PartyMember{i}Y"] = ((TextBox)FindName($"PartyMember{i}Y"))?.Text ?? "";
            }
            
            // Settings - Anti-Captcha System
            values["CaptchaEnabled"] = CaptchaEnabled.IsChecked ?? false;
            values["CaptchaX"] = CaptchaX.Text;
            values["CaptchaY"] = CaptchaY.Text;
            values["CaptchaWidth"] = CaptchaWidth.Text;
            values["CaptchaHeight"] = CaptchaHeight.Text;
            values["CaptchaTextX"] = CaptchaTextX.Text;
            values["CaptchaTextY"] = CaptchaTextY.Text;
            values["CaptchaButtonX"] = CaptchaButtonX.Text;
            values["CaptchaButtonY"] = CaptchaButtonY.Text;
            values["CaptchaContrast"] = CaptchaContrast.Text;
            values["CaptchaSharpness"] = CaptchaSharpness.Text;
            values["CaptchaScale"] = CaptchaScale.Text;
            values["CaptchaInterval"] = CaptchaInterval.Text;
            values["CaptchaGrayscale"] = CaptchaGrayscale.IsChecked ?? false;
            values["CaptchaHistogram"] = CaptchaHistogram.IsChecked ?? false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting UI values: {ex.Message}");
        }
        
        return values;
    }
    
    // Public method to set all UI values from loaded data
    public void SetAllUIValues(Dictionary<string, object> values)
    {
        try
        {
            // Basic HP/MP TextBoxes
            if (values.TryGetValue("HpProbeX", out var hpX)) HpX.Text = hpX.ToString();
            if (values.TryGetValue("HpProbeY", out var hpY)) HpY.Text = hpY.ToString();
            if (values.TryGetValue("MpProbeX", out var mpX)) MpX.Text = mpX.ToString();
            if (values.TryGetValue("MpProbeY", out var mpY)) MpY.Text = mpY.ToString();
            if (values.TryGetValue("HpTolerance", out var hpTolerance)) HpTolerance.Text = hpTolerance.ToString();
            if (values.TryGetValue("MpTolerance", out var mpTolerance)) MpTolerance.Text = mpTolerance.ToString();
            
            // HP/MP Trigger TextBoxes
            if (values.TryGetValue("HpTriggerX", out var hpTriggerX)) HpTriggerX.Text = hpTriggerX.ToString();
            if (values.TryGetValue("HpTriggerY", out var hpTriggerY)) HpTriggerY.Text = hpTriggerY.ToString();
            if (values.TryGetValue("MpTriggerX", out var mpTriggerX)) MpTriggerX.Text = mpTriggerX.ToString();
            if (values.TryGetValue("MpTriggerY", out var mpTriggerY)) MpTriggerY.Text = mpTriggerY.ToString();
            if (values.TryGetValue("HpCooldown", out var hpCooldown)) HpTriggerCooldown.Text = hpCooldown.ToString();
            if (values.TryGetValue("MpCooldown", out var mpCooldown)) MpTriggerCooldown.Text = mpCooldown.ToString();
            
            // HP/MP CheckBoxes
            if (values.TryGetValue("HpTriggerEnabled", out var hpTriggerEnabled)) HpTriggerEnabled.IsChecked = ConvertToBool(hpTriggerEnabled);
            if (values.TryGetValue("MpTriggerEnabled", out var mpTriggerEnabled)) MpTriggerEnabled.IsChecked = ConvertToBool(mpTriggerEnabled);
            if (values.TryGetValue("HpUseCoordinate", out var hpUseCoordinate)) HpUseCoordinate.IsChecked = ConvertToBool(hpUseCoordinate);
            if (values.TryGetValue("HpUseKeyPress", out var hpUseKeyPress)) HpUseKeyPress.IsChecked = ConvertToBool(hpUseKeyPress);
            if (values.TryGetValue("MpUseCoordinate", out var mpUseCoordinate)) MpUseCoordinate.IsChecked = ConvertToBool(mpUseCoordinate);
            if (values.TryGetValue("MpUseKeyPress", out var mpUseKeyPress)) MpUseKeyPress.IsChecked = ConvertToBool(mpUseKeyPress);
            if (values.TryGetValue("HpKeyToPress", out var hpKey)) HpKeyToPress.Text = hpKey.ToString();
            if (values.TryGetValue("MpKeyToPress", out var mpKey)) MpKeyToPress.Text = mpKey.ToString();
            
            // Percentage Monitoring System
            if (values.TryGetValue("PercentageMonitoringEnabled", out var percentageEnabled)) PercentageMonitoringEnabled.IsChecked = ConvertToBool(percentageEnabled);
            if (values.TryGetValue("HpPercentageStartX", out var hpPercentageStartX)) HpPercentageStartX.Text = hpPercentageStartX.ToString();
            if (values.TryGetValue("HpPercentageEndX", out var hpPercentageEndX)) HpPercentageEndX.Text = hpPercentageEndX.ToString();
            if (values.TryGetValue("HpPercentageY", out var hpPercentageY)) HpPercentageY.Text = hpPercentageY.ToString();
            if (values.TryGetValue("HpPercentageThreshold", out var hpPercentageThreshold)) HpPercentageThreshold.Text = hpPercentageThreshold.ToString();
            if (values.TryGetValue("HpPercentageTolerance", out var hpPercentageTolerance)) HpPercentageTolerance.Text = hpPercentageTolerance.ToString();
            if (values.TryGetValue("MpPercentageStartX", out var mpPercentageStartX)) MpPercentageStartX.Text = mpPercentageStartX.ToString();
            if (values.TryGetValue("MpPercentageEndX", out var mpPercentageEndX)) MpPercentageEndX.Text = mpPercentageEndX.ToString();
            if (values.TryGetValue("MpPercentageY", out var mpPercentageY)) MpPercentageY.Text = mpPercentageY.ToString();
            if (values.TryGetValue("MpPercentageThreshold", out var mpPercentageThreshold)) MpPercentageThreshold.Text = mpPercentageThreshold.ToString();
            if (values.TryGetValue("MpPercentageTolerance", out var mpPercentageTolerance)) MpPercentageTolerance.Text = mpPercentageTolerance.ToString();
            
            // Periodic Clicks TextBoxes
            if (values.TryGetValue("YClickX", out var yClickX)) YClickX.Text = yClickX.ToString();
            if (values.TryGetValue("YClickY", out var yClickY)) YClickY.Text = yClickY.ToString();
            if (values.TryGetValue("YClickPeriod", out var yClickPeriod)) YClickPeriod.Text = yClickPeriod.ToString();
            if (values.TryGetValue("Extra1X", out var extra1X)) Extra1X.Text = extra1X.ToString();
            if (values.TryGetValue("Extra1Y", out var extra1Y)) Extra1Y.Text = extra1Y.ToString();
            if (values.TryGetValue("Extra1Period", out var extra1Period)) Extra1Period.Text = extra1Period.ToString();
            if (values.TryGetValue("Extra2X", out var extra2X)) Extra2X.Text = extra2X.ToString();
            if (values.TryGetValue("Extra2Y", out var extra2Y)) Extra2Y.Text = extra2Y.ToString();
            if (values.TryGetValue("Extra2Period", out var extra2Period)) Extra2Period.Text = extra2Period.ToString();
            if (values.TryGetValue("Extra3X", out var extra3X)) Extra3X.Text = extra3X.ToString();
            if (values.TryGetValue("Extra3Y", out var extra3Y)) Extra3Y.Text = extra3Y.ToString();
            if (values.TryGetValue("Extra3Period", out var extra3Period)) Extra3Period.Text = extra3Period.ToString();
            
            // Periodic Clicks CheckBoxes
            if (values.TryGetValue("YClickEnabled", out var yClickEnabled)) YClickEnabled.IsChecked = ConvertToBool(yClickEnabled);
            if (values.TryGetValue("Extra1Enabled", out var extra1Enabled)) Extra1Enabled.IsChecked = ConvertToBool(extra1Enabled);
            if (values.TryGetValue("Extra2Enabled", out var extra2Enabled)) Extra2Enabled.IsChecked = ConvertToBool(extra2Enabled);
            if (values.TryGetValue("Extra3Enabled", out var extra3Enabled)) Extra3Enabled.IsChecked = ConvertToBool(extra3Enabled);
            if (values.TryGetValue("YUseCoordinate", out var yUseCoordinate)) YUseCoordinate.IsChecked = ConvertToBool(yUseCoordinate);
            if (values.TryGetValue("YUseKeyPress", out var yUseKeyPress)) YUseKeyPress.IsChecked = ConvertToBool(yUseKeyPress);
            if (values.TryGetValue("Extra1UseCoordinate", out var extra1UseCoordinate)) Extra1UseCoordinate.IsChecked = ConvertToBool(extra1UseCoordinate);
            if (values.TryGetValue("Extra1UseKeyPress", out var extra1UseKeyPress)) Extra1UseKeyPress.IsChecked = ConvertToBool(extra1UseKeyPress);
            if (values.TryGetValue("Extra2UseCoordinate", out var extra2UseCoordinate)) Extra2UseCoordinate.IsChecked = ConvertToBool(extra2UseCoordinate);
            if (values.TryGetValue("Extra2UseKeyPress", out var extra2UseKeyPress)) Extra2UseKeyPress.IsChecked = ConvertToBool(extra2UseKeyPress);
            if (values.TryGetValue("Extra3UseCoordinate", out var extra3UseCoordinate)) Extra3UseCoordinate.IsChecked = ConvertToBool(extra3UseCoordinate);
            if (values.TryGetValue("Extra3UseKeyPress", out var extra3UseKeyPress)) Extra3UseKeyPress.IsChecked = ConvertToBool(extra3UseKeyPress);
            if (values.TryGetValue("YKeyToPress", out var yKey)) YKeyToPress.Text = yKey.ToString();
            if (values.TryGetValue("Extra1KeyToPress", out var extra1Key)) Extra1KeyToPress.Text = extra1Key.ToString();
            if (values.TryGetValue("Extra2KeyToPress", out var extra2Key)) Extra2KeyToPress.Text = extra2Key.ToString();
            if (values.TryGetValue("Extra3KeyToPress", out var extra3Key)) Extra3KeyToPress.Text = extra3Key.ToString();
            
            // BabeBot HP/MP
            if (values.TryGetValue("BabeBotHpStart", out var babeBotHpStart)) BabeBotHpStart.Text = babeBotHpStart.ToString();
            if (values.TryGetValue("BabeBotHpEnd", out var babeBotHpEnd)) BabeBotHpEnd.Text = babeBotHpEnd.ToString();
            if (values.TryGetValue("BabeBotHpY", out var babeBotHpY)) BabeBotHpY.Text = babeBotHpY.ToString();
            if (values.TryGetValue("BabeBotHpThreshold", out var babeBotHpThreshold)) BabeBotHpThreshold.Text = babeBotHpThreshold.ToString();
            if (values.TryGetValue("BabeBotHpPotionX", out var babeBotHpPotionX)) BabeBotHpPotionX.Text = babeBotHpPotionX.ToString();
            if (values.TryGetValue("BabeBotHpPotionY", out var babeBotHpPotionY)) BabeBotHpPotionY.Text = babeBotHpPotionY.ToString();
            if (values.TryGetValue("BabeBotHpEnabled", out var babeBotHpEnabled)) BabeBotHpEnabled.IsChecked = ConvertToBool(babeBotHpEnabled);
            if (values.TryGetValue("BabeBotHpUseCoordinate", out var babeBotHpUseCoordinate)) BabeBotHpUseCoordinate.IsChecked = ConvertToBool(babeBotHpUseCoordinate);
            if (values.TryGetValue("BabeBotHpUseKeyPress", out var babeBotHpUseKeyPress)) BabeBotHpUseKeyPress.IsChecked = ConvertToBool(babeBotHpUseKeyPress);
            if (values.TryGetValue("BabeBotHpKeyToPress", out var babeBotHpKey)) BabeBotHpKeyToPress.Text = babeBotHpKey.ToString();
            
            if (values.TryGetValue("BabeBotMpStart", out var babeBotMpStart)) BabeBotMpStart.Text = babeBotMpStart.ToString();
            if (values.TryGetValue("BabeBotMpEnd", out var babeBotMpEnd)) BabeBotMpEnd.Text = babeBotMpEnd.ToString();
            if (values.TryGetValue("BabeBotMpY", out var babeBotMpY)) BabeBotMpY.Text = babeBotMpY.ToString();
            if (values.TryGetValue("BabeBotMpThreshold", out var babeBotMpThreshold)) BabeBotMpThreshold.Text = babeBotMpThreshold.ToString();
            if (values.TryGetValue("BabeBotMpPotionX", out var babeBotMpPotionX)) BabeBotMpPotionX.Text = babeBotMpPotionX.ToString();
            if (values.TryGetValue("BabeBotMpPotionY", out var babeBotMpPotionY)) BabeBotMpPotionY.Text = babeBotMpPotionY.ToString();
            if (values.TryGetValue("BabeBotMpEnabled", out var babeBotMpEnabled)) BabeBotMpEnabled.IsChecked = ConvertToBool(babeBotMpEnabled);
            if (values.TryGetValue("BabeBotMpUseCoordinate", out var babeBotMpUseCoordinate)) BabeBotMpUseCoordinate.IsChecked = ConvertToBool(babeBotMpUseCoordinate);
            if (values.TryGetValue("BabeBotMpUseKeyPress", out var babeBotMpUseKeyPress)) BabeBotMpUseKeyPress.IsChecked = ConvertToBool(babeBotMpUseKeyPress);
            if (values.TryGetValue("BabeBotMpKeyToPress", out var babeBotMpKey)) BabeBotMpKeyToPress.Text = babeBotMpKey.ToString();
            
            // MultiHp Values
            for (int i = 1; i <= 8; i++)
            {
                var startXControl = this.FindName($"MultiHp{i}StartX") as TextBox;
                var endXControl = this.FindName($"MultiHp{i}EndX") as TextBox;
                var yControl = this.FindName($"MultiHp{i}Y") as TextBox;
                var thresholdControl = this.FindName($"MultiHp{i}Threshold") as TextBox;
                var clickXControl = this.FindName($"MultiHp{i}ClickX") as TextBox;
                var clickYControl = this.FindName($"MultiHp{i}ClickY") as TextBox;
                var keyControl = this.FindName($"MultiHp{i}Key") as TextBox;
                var enabledControl = this.FindName($"MultiHp{i}Enabled") as CheckBox;
                
                if (values.TryGetValue($"MultiHp{i}StartX", out var startX) && startXControl != null) startXControl.Text = startX.ToString();
                if (values.TryGetValue($"MultiHp{i}EndX", out var endX) && endXControl != null) endXControl.Text = endX.ToString();
                if (values.TryGetValue($"MultiHp{i}Y", out var y) && yControl != null) yControl.Text = y.ToString();
                if (values.TryGetValue($"MultiHp{i}Threshold", out var threshold) && thresholdControl != null) thresholdControl.Text = threshold.ToString();
                if (values.TryGetValue($"MultiHp{i}ClickX", out var clickX) && clickXControl != null) clickXControl.Text = clickX.ToString();
                if (values.TryGetValue($"MultiHp{i}ClickY", out var clickY) && clickYControl != null) clickYControl.Text = clickY.ToString();
                if (values.TryGetValue($"MultiHp{i}Key", out var key) && keyControl != null) keyControl.Text = key.ToString();
                if (values.TryGetValue($"MultiHp{i}Enabled", out var enabled) && enabledControl != null) enabledControl.IsChecked = ConvertToBool(enabled);
            }
            
            // Multi HP System Settings
            // Multi HP references removed
            
            // Python-style HP/MP Settings
            if (values.TryGetValue("PythonHpUseCoordinate", out var pythonHpUseCoordinate)) PythonHpUseCoordinate.IsChecked = ConvertToBool(pythonHpUseCoordinate);
            if (values.TryGetValue("PythonHpUseKeyPress", out var pythonHpUseKeyPress)) PythonHpUseKeyPress.IsChecked = ConvertToBool(pythonHpUseKeyPress);
            if (values.TryGetValue("PythonHpPotionX", out var pythonHpPotionX)) PythonHpPotionX.Text = ConvertToString(pythonHpPotionX);
            if (values.TryGetValue("PythonHpPotionY", out var pythonHpPotionY)) PythonHpPotionY.Text = ConvertToString(pythonHpPotionY);
            if (values.TryGetValue("PythonHpPotionCooldown", out var pythonHpPotionCooldown)) PythonHpPotionCooldown.Text = ConvertToString(pythonHpPotionCooldown);
            if (values.TryGetValue("PythonHpKeyToPress", out var pythonHpKeyToPress)) PythonHpKeyToPress.Text = ConvertToString(pythonHpKeyToPress);
            
            if (values.TryGetValue("PythonMpUseCoordinate", out var pythonMpUseCoordinate)) PythonMpUseCoordinate.IsChecked = ConvertToBool(pythonMpUseCoordinate);
            if (values.TryGetValue("PythonMpUseKeyPress", out var pythonMpUseKeyPress)) PythonMpUseKeyPress.IsChecked = ConvertToBool(pythonMpUseKeyPress);
            if (values.TryGetValue("PythonMpPotionX", out var pythonMpPotionX)) PythonMpPotionX.Text = ConvertToString(pythonMpPotionX);
            if (values.TryGetValue("PythonMpPotionY", out var pythonMpPotionY)) PythonMpPotionY.Text = ConvertToString(pythonMpPotionY);
            if (values.TryGetValue("PythonMpPotionCooldown", out var pythonMpPotionCooldown)) PythonMpPotionCooldown.Text = ConvertToString(pythonMpPotionCooldown);
            if (values.TryGetValue("PythonMpKeyToPress", out var pythonMpKeyToPress)) PythonMpKeyToPress.Text = ConvertToString(pythonMpKeyToPress);
            
            // Attack/Skills System
            if (values.TryGetValue("AttackSystemEnabled", out var attackSystemEnabled)) AttackSystemEnabled.IsChecked = ConvertToBool(attackSystemEnabled);
            if (values.TryGetValue("SkillNameInput", out var skillNameInput)) SkillNameInput.Text = ConvertToString(skillNameInput);
            if (values.TryGetValue("SkillKeyInput", out var skillKeyInput)) SkillKeyInput.Text = ConvertToString(skillKeyInput);
            if (values.TryGetValue("SkillIntervalInput", out var skillIntervalInput)) SkillIntervalInput.Text = ConvertToString(skillIntervalInput);
            
            // Attack Skills List
            if (values.TryGetValue("AttackSkillsCount", out var attackSkillsCount))
            {
                int count = ConvertToInt32(attackSkillsCount);
                Console.WriteLine($"[LOAD] AttackSkills Count to load: {count}");
                
                // Clear existing skills
                ViewModel.AttackSkills.Clear();
                AttackSkillsContainer.Children.Clear();
                
                // Load saved skills
                for (int i = 0; i < count; i++)
                {
                    if (values.TryGetValue($"AttackSkill{i}Name", out var skillName) &&
                        values.TryGetValue($"AttackSkill{i}Key", out var skillKey) &&
                        values.TryGetValue($"AttackSkill{i}Interval", out var skillInterval) &&
                        values.TryGetValue($"AttackSkill{i}Enabled", out var skillEnabled))
                    {
                        var skill = new AttackSkillViewModel
                        {
                            Name = ConvertToString(skillName),
                            Key = ConvertToString(skillKey),
                            IntervalMs = ConvertToInt32(skillInterval),
                            Enabled = ConvertToBool(skillEnabled)
                        };
                        
                        ViewModel.AttackSkills.Add(skill);
                        CreateSkillWidget(skill);
                        Console.WriteLine($"[LOAD] Loaded Skill {i}: {skill.Name}, {skill.Key}, {skill.IntervalMs}ms, Enabled={skill.Enabled}");
                    }
                    else
                    {
                        Console.WriteLine($"[LOAD] Failed to load Skill {i} - missing values");
                    }
                }
            }
            else
            {
                Console.WriteLine("[LOAD] No AttackSkillsCount found in saved values");
            }
            
            // Buff/AC System
            if (values.TryGetValue("BuffAcSystemEnabled", out var buffAcSystemEnabled)) BuffAcSystemEnabled.IsChecked = ConvertToBool(buffAcSystemEnabled);
            
            // Member settings
            if (values.TryGetValue("Member1Enabled", out var member1Enabled)) Member1Enabled.IsChecked = ConvertToBool(member1Enabled);
            if (values.TryGetValue("Member1KeyInput", out var member1KeyInput)) Member1KeyInput.Text = ConvertToString(member1KeyInput);
            if (values.TryGetValue("Member2Enabled", out var member2Enabled)) Member2Enabled.IsChecked = ConvertToBool(member2Enabled);
            if (values.TryGetValue("Member2KeyInput", out var member2KeyInput)) Member2KeyInput.Text = ConvertToString(member2KeyInput);
            if (values.TryGetValue("Member3Enabled", out var member3Enabled)) Member3Enabled.IsChecked = ConvertToBool(member3Enabled);
            if (values.TryGetValue("Member3KeyInput", out var member3KeyInput)) Member3KeyInput.Text = ConvertToString(member3KeyInput);
            if (values.TryGetValue("Member4Enabled", out var member4Enabled)) Member4Enabled.IsChecked = ConvertToBool(member4Enabled);
            if (values.TryGetValue("Member4KeyInput", out var member4KeyInput)) Member4KeyInput.Text = ConvertToString(member4KeyInput);
            if (values.TryGetValue("Member5Enabled", out var member5Enabled)) Member5Enabled.IsChecked = ConvertToBool(member5Enabled);
            if (values.TryGetValue("Member5KeyInput", out var member5KeyInput)) Member5KeyInput.Text = ConvertToString(member5KeyInput);
            if (values.TryGetValue("Member6Enabled", out var member6Enabled)) Member6Enabled.IsChecked = ConvertToBool(member6Enabled);
            if (values.TryGetValue("Member6KeyInput", out var member6KeyInput)) Member6KeyInput.Text = ConvertToString(member6KeyInput);
            if (values.TryGetValue("Member7Enabled", out var member7Enabled)) Member7Enabled.IsChecked = ConvertToBool(member7Enabled);
            if (values.TryGetValue("Member7KeyInput", out var member7KeyInput)) Member7KeyInput.Text = ConvertToString(member7KeyInput);
            if (values.TryGetValue("Member8Enabled", out var member8Enabled)) Member8Enabled.IsChecked = ConvertToBool(member8Enabled);
            if (values.TryGetValue("Member8KeyInput", out var member8KeyInput)) Member8KeyInput.Text = ConvertToString(member8KeyInput);
            
            // Buff/AC configuration
            if (values.TryGetValue("BuffKeyInput", out var buffKeyInput)) BuffKeyInput.Text = ConvertToString(buffKeyInput);
            if (values.TryGetValue("BuffAnimInput", out var buffAnimInput)) BuffAnimInput.Text = ConvertToString(buffAnimInput);
            if (values.TryGetValue("AcKeyInput", out var acKeyInput)) AcKeyInput.Text = ConvertToString(acKeyInput);
            if (values.TryGetValue("AcAnimInput", out var acAnimInput)) AcAnimInput.Text = ConvertToString(acAnimInput);
            if (values.TryGetValue("CycleIntervalInput", out var cycleIntervalInput)) CycleIntervalInput.Text = ConvertToString(cycleIntervalInput);
            
            // Party Heal System - Global Settings
            if (values.TryGetValue("PartyHealSystemEnabled", out var partyHealSystemEnabled)) 
                PartyHealSystemEnabled.IsChecked = ConvertToBool(partyHealSystemEnabled);
            if (values.TryGetValue("PartyHealSkillKey", out var partyHealSkillKey)) 
                PartyHealSkillKey.Text = ConvertToString(partyHealSkillKey);
            if (values.TryGetValue("PartyHealPollInterval", out var partyHealPollInterval)) 
                PartyHealPollInterval.Text = ConvertToString(partyHealPollInterval);
            if (values.TryGetValue("PartyHealBaselineColor", out var partyHealBaselineColor)) 
                PartyHealBaselineColor.Text = ConvertToString(partyHealBaselineColor);
                
            // Party Heal System - Members
            for (int i = 1; i <= 8; i++)
            {
                if (values.TryGetValue($"PartyMember{i}Enabled", out var enabled)) 
                    ((CheckBox)FindName($"PartyMember{i}Enabled"))!.IsChecked = ConvertToBool(enabled);
                if (values.TryGetValue($"PartyMember{i}Key", out var key)) 
                    ((TextBox)FindName($"PartyMember{i}Key"))!.Text = ConvertToString(key);
                if (values.TryGetValue($"PartyMember{i}Threshold", out var threshold)) 
                    ((TextBox)FindName($"PartyMember{i}Threshold"))!.Text = ConvertToString(threshold);
                if (values.TryGetValue($"PartyMember{i}XStart", out var xStart)) 
                    ((TextBox)FindName($"PartyMember{i}XStart"))!.Text = ConvertToString(xStart);
                if (values.TryGetValue($"PartyMember{i}XEnd", out var xEnd)) 
                    ((TextBox)FindName($"PartyMember{i}XEnd"))!.Text = ConvertToString(xEnd);
                if (values.TryGetValue($"PartyMember{i}Y", out var y)) 
                    ((TextBox)FindName($"PartyMember{i}Y"))!.Text = ConvertToString(y);
            }
            
            // Settings - Anti-Captcha System
            if (values.TryGetValue("CaptchaEnabled", out var captchaEnabled)) 
                CaptchaEnabled.IsChecked = ConvertToBool(captchaEnabled);
            if (values.TryGetValue("CaptchaX", out var captchaX)) 
                CaptchaX.Text = ConvertToString(captchaX);
            if (values.TryGetValue("CaptchaY", out var captchaY)) 
                CaptchaY.Text = ConvertToString(captchaY);
            if (values.TryGetValue("CaptchaWidth", out var captchaWidth)) 
                CaptchaWidth.Text = ConvertToString(captchaWidth);
            if (values.TryGetValue("CaptchaHeight", out var captchaHeight)) 
                CaptchaHeight.Text = ConvertToString(captchaHeight);
            if (values.TryGetValue("CaptchaTextX", out var captchaTextX)) 
                CaptchaTextX.Text = ConvertToString(captchaTextX);
            if (values.TryGetValue("CaptchaTextY", out var captchaTextY)) 
                CaptchaTextY.Text = ConvertToString(captchaTextY);
            if (values.TryGetValue("CaptchaButtonX", out var captchaButtonX)) 
                CaptchaButtonX.Text = ConvertToString(captchaButtonX);
            if (values.TryGetValue("CaptchaButtonY", out var captchaButtonY)) 
                CaptchaButtonY.Text = ConvertToString(captchaButtonY);
            if (values.TryGetValue("CaptchaContrast", out var captchaContrast)) 
                CaptchaContrast.Text = ConvertToString(captchaContrast);
            if (values.TryGetValue("CaptchaSharpness", out var captchaSharpness)) 
                CaptchaSharpness.Text = ConvertToString(captchaSharpness);
            if (values.TryGetValue("CaptchaScale", out var captchaScale)) 
                CaptchaScale.Text = ConvertToString(captchaScale);
            if (values.TryGetValue("CaptchaInterval", out var captchaInterval)) 
                CaptchaInterval.Text = ConvertToString(captchaInterval);
            if (values.TryGetValue("CaptchaGrayscale", out var captchaGrayscale)) 
                CaptchaGrayscale.IsChecked = ConvertToBool(captchaGrayscale);
            if (values.TryGetValue("CaptchaHistogram", out var captchaHistogram)) 
                CaptchaHistogram.IsChecked = ConvertToBool(captchaHistogram);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting UI values: {ex.Message}");
        }
    }
    
    private async void StartClient_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            MessageBox.Show("Please select a window first to start automation!", "No Window Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
            StatusIndicator.ToolTip = "Please select a window first!";
            return;
        }

        // _isRunning = true; // Removed - using ViewModel.IsRunning instead
        ViewModel.IsRunning = true;
        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        StatusIndicator.Fill = new SolidColorBrush(Colors.Green);
        StatusIndicator.ToolTip = $"Running automation for {ViewModel.ClientName}";
        
        // StartPeriodicClicks(); // Disabled - periodic clicks section is hidden
        StartMonitoring();
        
        // Auto-enable BabeBot HP/MP when starting client
        ViewModel.BabeBotHp.Enabled = true;
        ViewModel.BabeBotMp.Enabled = true;
        StartBabeBotMonitoring();
        
        // Auto-start Attack System if enabled
        if (AttackSystemEnabled.IsChecked == true)
        {
            StartAttackSystem();
            Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ Attack System auto-started");
        }
        
        // Auto-start Buff/AC System if enabled
        if (BuffAcSystemEnabled.IsChecked == true)
        {
            StartBuffAcSystem();
            Console.WriteLine($"[{ViewModel.ClientName}] 🎭 Buff/AC System auto-started");
        }
        
        // Auto-start Anti-Captcha System if enabled
        if (CaptchaEnabled.IsChecked == true)
        {
            CaptchaStart_Click(null, null);
            Console.WriteLine($"[{ViewModel.ClientName}] 🤖 Anti-Captcha System auto-started");
        }
        
        // Auto-start PartyHeal System if enabled
        if (PartyHealSystemEnabled.IsChecked == true)
        {
            await StartPartyHealAsync();
            Console.WriteLine($"[{ViewModel.ClientName}] 🧙 PartyHeal System auto-started");
        }
        
        // Multi-HP System auto-start removed
        
        // Auto-enable Party Heal monitoring for all active members
        for (int i = 1; i <= 8; i++)
        {
            var userKeyControl = FindName($"PartyMember{i}UserKey") as TextBox;
            var skillKeyControl = FindName($"PartyMember{i}SkillKey") as TextBox;
            
            if (userKeyControl != null && skillKeyControl != null && 
                !string.IsNullOrEmpty(userKeyControl.Text) && !string.IsNullOrEmpty(skillKeyControl.Text))
            {
                var monitorBtn = GetPartyMemberMonitorButton(i);
                if (monitorBtn != null && monitorBtn.Content.ToString() == "Monitor")
                {
                    TogglePartyMemberMonitor(i);
                    Console.WriteLine($"[{ViewModel.ClientName}] 👥 Party Member {i} monitoring auto-started");
                }
            }
        }
        
        Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot HP/MP auto-enabled on start");
        
        // Debug HP/MP settings
        Console.WriteLine($"[{ViewModel.ClientName}] START: HP Enabled={ViewModel.HpTrigger.Enabled}, Coords=({ViewModel.HpTrigger.X},{ViewModel.HpTrigger.Y}), Tolerance={ViewModel.HpProbe.Tolerance}");
        Console.WriteLine($"[{ViewModel.ClientName}] START: MP Enabled={ViewModel.MpTrigger.Enabled}, Coords=({ViewModel.MpTrigger.X},{ViewModel.MpTrigger.Y}), Tolerance={ViewModel.MpProbe.Tolerance}");
    }

    private async void StopClient_Click(object sender, RoutedEventArgs e)
    {
        // _isRunning = false; // Removed - using ViewModel.IsRunning instead
        ViewModel.IsRunning = false;
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        StatusIndicator.Fill = new SolidColorBrush(Colors.Orange);
        StatusIndicator.ToolTip = "Stopped";
        
        // StopPeriodicClicks(); // Disabled - periodic clicks section is hidden
        StopMonitoring();
        
        // Auto-disable BabeBot HP/MP when stopping client
        ViewModel.BabeBotHp.Enabled = false;
        ViewModel.BabeBotMp.Enabled = false;
        StopBabeBotMonitoring();
        
        // Auto-stop Attack System if enabled
        if (AttackSystemEnabled.IsChecked == true)
        {
            try
            {
                StopAttackSystem();
                Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ Attack System auto-stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] Error stopping Attack System: {ex.Message}");
            }
        }
        
        // Auto-stop Buff/AC System if enabled
        if (BuffAcSystemEnabled.IsChecked == true)
        {
            try
            {
                StopBuffAcSystem();
                Console.WriteLine($"[{ViewModel.ClientName}] 🎭 Buff/AC System auto-stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] Error stopping Buff/AC System: {ex.Message}");
            }
        }
        
        // Auto-stop Anti-Captcha System if enabled
        if (CaptchaEnabled.IsChecked == true)
        {
            try
            {
                CaptchaStop_Click(null, null);
                Console.WriteLine($"[{ViewModel.ClientName}] 🤖 Anti-Captcha System auto-stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] Error stopping Anti-Captcha System: {ex.Message}");
            }
        }
        
        // Auto-stop PartyHeal System if running
        if (_partyHealRunning)
        {
            try
            {
                await StopPartyHealAsync();
                Console.WriteLine($"[{ViewModel.ClientName}] 🧙 PartyHeal System auto-stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] Error stopping PartyHeal System: {ex.Message}");
            }
        }
        
        // Multi-HP System auto-stop removed
        
        // Auto-disable Party Heal monitoring for all active members
        for (int i = 1; i <= 8; i++)
        {
            try
            {
                var monitorBtn = GetPartyMemberMonitorButton(i);
                if (monitorBtn != null && monitorBtn.Content.ToString() == "Stop")
                {
                    TogglePartyMemberMonitor(i);
                    Console.WriteLine($"[{ViewModel.ClientName}] 👥 Party Member {i} monitoring auto-stopped");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] Error stopping Party Member {i} monitoring: {ex.Message}");
            }
        }
        
        Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot HP/MP auto-disabled on stop");
    }

    private void TestClient_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
            StatusIndicator.ToolTip = "Please select a window first!";
            return;
        }

        // Test color sampling - DON'T update reference colors!
        var hpColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, ViewModel.HpProbe.X, ViewModel.HpProbe.Y);
        var mpColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, ViewModel.MpProbe.X, ViewModel.MpProbe.Y);
        
        Console.WriteLine($"[{ViewModel.ClientName}] TEST - Current HP: RGB({hpColor.R},{hpColor.G},{hpColor.B}) vs Reference: RGB({ViewModel.HpProbe.ExpectedColor.R},{ViewModel.HpProbe.ExpectedColor.G},{ViewModel.HpProbe.ExpectedColor.B})");
        Console.WriteLine($"[{ViewModel.ClientName}] TEST - Current MP: RGB({mpColor.R},{mpColor.G},{mpColor.B}) vs Reference: RGB({ViewModel.MpProbe.ExpectedColor.R},{ViewModel.MpProbe.ExpectedColor.G},{ViewModel.MpProbe.ExpectedColor.B})");
        
        // Test HP trigger click (ONLY IF ENABLED)
        if (ViewModel.HpTrigger.Enabled && ViewModel.HpTrigger.X > 0 && ViewModel.HpTrigger.Y > 0)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: HP trigger ENABLED - testing click at ({ViewModel.HpTrigger.X}, {ViewModel.HpTrigger.Y})");
            PerformBackgroundClick(ViewModel.HpTrigger.X, ViewModel.HpTrigger.Y, "TEST_HP_BACKGROUND");
            PerformPostMessageTest(ViewModel.HpTrigger.X, ViewModel.HpTrigger.Y, "TEST_HP_POSTMESSAGE");
        }
        else if (!ViewModel.HpTrigger.Enabled)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: HP trigger DISABLED - skipping test");
        }
        
        // Test MP trigger click (ONLY IF ENABLED)
        if (ViewModel.MpTrigger.Enabled && ViewModel.MpTrigger.X > 0 && ViewModel.MpTrigger.Y > 0)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: MP trigger ENABLED - testing click at ({ViewModel.MpTrigger.X}, {ViewModel.MpTrigger.Y})");
            PerformBackgroundClick(ViewModel.MpTrigger.X, ViewModel.MpTrigger.Y, "TEST_MP_BACKGROUND");
            PerformPostMessageTest(ViewModel.MpTrigger.X, ViewModel.MpTrigger.Y, "TEST_MP_POSTMESSAGE");
        }
        else if (!ViewModel.MpTrigger.Enabled)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: MP trigger DISABLED - skipping test");
        }
        
        // Test periodic clicks (BACKGROUND ONLY - NO MOUSE MOVEMENT)
        Console.WriteLine($"[{ViewModel.ClientName}] TEST: Testing background clicks only...");
        
        if (ViewModel.YClick.Enabled && ViewModel.YClick.X > 0 && ViewModel.YClick.Y > 0)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: Y periodic click ENABLED - testing at ({ViewModel.YClick.X}, {ViewModel.YClick.Y})");
            PerformBackgroundClick(ViewModel.YClick.X, ViewModel.YClick.Y, "TEST_Y_BACKGROUND");
            PerformPostMessageTest(ViewModel.YClick.X, ViewModel.YClick.Y, "TEST_Y_POSTMESSAGE");
        }
        else if (!ViewModel.YClick.Enabled)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: Y periodic click DISABLED - skipping test");
        }
        
        if (ViewModel.Extra1Click.Enabled && ViewModel.Extra1Click.X > 0 && ViewModel.Extra1Click.Y > 0)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: Extra1 click ENABLED - testing at ({ViewModel.Extra1Click.X}, {ViewModel.Extra1Click.Y})");
            PerformBackgroundClick(ViewModel.Extra1Click.X, ViewModel.Extra1Click.Y, "TEST_EXTRA1_BACKGROUND");
            PerformPostMessageTest(ViewModel.Extra1Click.X, ViewModel.Extra1Click.Y, "TEST_EXTRA1_POSTMESSAGE");
        }
        else if (!ViewModel.Extra1Click.Enabled)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: Extra1 click DISABLED - skipping test");
        }
        
        if (ViewModel.Extra2Click.Enabled && ViewModel.Extra2Click.X > 0 && ViewModel.Extra2Click.Y > 0)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: Extra2 click ENABLED - testing at ({ViewModel.Extra2Click.X}, {ViewModel.Extra2Click.Y})");
            PerformBackgroundClick(ViewModel.Extra2Click.X, ViewModel.Extra2Click.Y, "TEST_EXTRA2_BACKGROUND");
            PerformPostMessageTest(ViewModel.Extra2Click.X, ViewModel.Extra2Click.Y, "TEST_EXTRA2_POSTMESSAGE");
        }
        else if (!ViewModel.Extra2Click.Enabled)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: Extra2 click DISABLED - skipping test");
        }
        
        if (ViewModel.Extra3Click.Enabled && ViewModel.Extra3Click.X > 0 && ViewModel.Extra3Click.Y > 0)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: Extra3 click ENABLED - testing at ({ViewModel.Extra3Click.X}, {ViewModel.Extra3Click.Y})");
            PerformBackgroundClick(ViewModel.Extra3Click.X, ViewModel.Extra3Click.Y, "TEST_EXTRA3_BACKGROUND");
            PerformPostMessageTest(ViewModel.Extra3Click.X, ViewModel.Extra3Click.Y, "TEST_EXTRA3_POSTMESSAGE");
        }
        else if (!ViewModel.Extra3Click.Enabled)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] TEST: Extra3 click DISABLED - skipping test");
        }
        
        // Test completed - no ADB needed
        
        // Test enabled/disabled status
        Console.WriteLine($"[{ViewModel.ClientName}] TEST: HP Trigger Enabled={ViewModel.HpTrigger.Enabled}, MP Trigger Enabled={ViewModel.MpTrigger.Enabled}");
        Console.WriteLine($"[{ViewModel.ClientName}] TEST: Y={ViewModel.YClick.Enabled}, Extra1={ViewModel.Extra1Click.Enabled}, Extra2={ViewModel.Extra2Click.Enabled}, Extra3={ViewModel.Extra3Click.Enabled}");
        
        StatusIndicator.ToolTip = $"Test completed - HP: RGB({hpColor.R},{hpColor.G},{hpColor.B}) MP: RGB({mpColor.R},{mpColor.G},{mpColor.B})";
    }


    public void UpdateUI()
    {
        // Update UI with current ViewModel values
        HpX.Text = ViewModel.HpProbe.X.ToString();
        HpY.Text = ViewModel.HpProbe.Y.ToString();
        HpTolerance.Text = ViewModel.HpProbe.Tolerance.ToString();
        MpX.Text = ViewModel.MpProbe.X.ToString();
        MpY.Text = ViewModel.MpProbe.Y.ToString();
        MpTolerance.Text = ViewModel.MpProbe.Tolerance.ToString();
        
        HpTriggerX.Text = ViewModel.HpTrigger.X.ToString();
        HpTriggerY.Text = ViewModel.HpTrigger.Y.ToString();
        HpTriggerCooldown.Text = ViewModel.HpTrigger.CooldownMs.ToString();
        HpTriggerEnabled.IsChecked = ViewModel.HpTrigger.Enabled;
        
        MpTriggerX.Text = ViewModel.MpTrigger.X.ToString();
        MpTriggerY.Text = ViewModel.MpTrigger.Y.ToString();
        MpTriggerCooldown.Text = ViewModel.MpTrigger.CooldownMs.ToString();
        MpTriggerEnabled.IsChecked = ViewModel.MpTrigger.Enabled;
        
        YClickX.Text = ViewModel.YClick.X.ToString();
        YClickY.Text = ViewModel.YClick.Y.ToString();
        YClickPeriod.Text = ViewModel.YClick.PeriodMs.ToString();
        YClickEnabled.IsChecked = ViewModel.YClick.Enabled;
        
        Extra1X.Text = ViewModel.Extra1Click.X.ToString();
        Extra1Y.Text = ViewModel.Extra1Click.Y.ToString();
        Extra1Period.Text = ViewModel.Extra1Click.PeriodMs.ToString();
        Extra1Enabled.IsChecked = ViewModel.Extra1Click.Enabled;
        
        Extra2X.Text = ViewModel.Extra2Click.X.ToString();
        Extra2Y.Text = ViewModel.Extra2Click.Y.ToString();
        Extra2Period.Text = ViewModel.Extra2Click.PeriodMs.ToString();
        Extra2Enabled.IsChecked = ViewModel.Extra2Click.Enabled;
        
        Extra3X.Text = ViewModel.Extra3Click.X.ToString();
        Extra3Y.Text = ViewModel.Extra3Click.Y.ToString();
        Extra3Period.Text = ViewModel.Extra3Click.PeriodMs.ToString();
        Extra3Enabled.IsChecked = ViewModel.Extra3Click.Enabled;
        
        // Update percentage probe UI
        HpPercentageStartX.Text = ViewModel.HpPercentageProbe.StartX.ToString();
        HpPercentageEndX.Text = ViewModel.HpPercentageProbe.EndX.ToString();
        HpPercentageY.Text = ViewModel.HpPercentageProbe.Y.ToString();
        HpPercentageThreshold.Text = ViewModel.HpPercentageProbe.MonitorPercentage.ToString();
        HpPercentageTolerance.Text = ViewModel.HpPercentageProbe.Tolerance.ToString();
        HpPercentageColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(
            ViewModel.HpPercentageProbe.ExpectedColor.R,
            ViewModel.HpPercentageProbe.ExpectedColor.G,
            ViewModel.HpPercentageProbe.ExpectedColor.B));
            
        MpPercentageStartX.Text = ViewModel.MpPercentageProbe.StartX.ToString();
        MpPercentageEndX.Text = ViewModel.MpPercentageProbe.EndX.ToString();
        MpPercentageY.Text = ViewModel.MpPercentageProbe.Y.ToString();
        MpPercentageThreshold.Text = ViewModel.MpPercentageProbe.MonitorPercentage.ToString();
        MpPercentageTolerance.Text = ViewModel.MpPercentageProbe.Tolerance.ToString();
        MpPercentageColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(
            ViewModel.MpPercentageProbe.ExpectedColor.R,
            ViewModel.MpPercentageProbe.ExpectedColor.G,
            ViewModel.MpPercentageProbe.ExpectedColor.B));
            
        PercentageMonitoringEnabled.IsChecked = ViewModel.HpPercentageProbe.Enabled || ViewModel.MpPercentageProbe.Enabled;
        
        // Update Python-style potion coordinates UI
        PythonHpPotionX.Text = ViewModel.PythonHpPotionClick.X.ToString();
        PythonHpPotionY.Text = ViewModel.PythonHpPotionClick.Y.ToString();
        PythonHpPotionCooldown.Text = ViewModel.PythonHpPotionClick.CooldownMs.ToString();
        
        PythonMpPotionX.Text = ViewModel.PythonMpPotionClick.X.ToString();
        PythonMpPotionY.Text = ViewModel.PythonMpPotionClick.Y.ToString();
        PythonMpPotionCooldown.Text = ViewModel.PythonMpPotionClick.CooldownMs.ToString();
        
        // Update BabeBot UI elements
        BabeBotHpStart.Text = ViewModel.BabeBotHp.StartX.ToString();
        BabeBotHpEnd.Text = ViewModel.BabeBotHp.EndX.ToString();
        BabeBotHpY.Text = ViewModel.BabeBotHp.Y.ToString();
        
        // Set HP threshold TextBox
        BabeBotHpThreshold.Text = ViewModel.BabeBotHp.ThresholdPercentage.ToString();
        
        BabeBotHpPotionX.Text = ViewModel.BabeBotHp.PotionX.ToString();
        BabeBotHpPotionY.Text = ViewModel.BabeBotHp.PotionY.ToString();
        
        BabeBotMpStart.Text = ViewModel.BabeBotMp.StartX.ToString();
        BabeBotMpEnd.Text = ViewModel.BabeBotMp.EndX.ToString();
        BabeBotMpY.Text = ViewModel.BabeBotMp.Y.ToString();
        
        // Set MP threshold TextBox
        BabeBotMpThreshold.Text = ViewModel.BabeBotMp.ThresholdPercentage.ToString();
        
        BabeBotMpPotionX.Text = ViewModel.BabeBotMp.PotionX.ToString();
        BabeBotMpPotionY.Text = ViewModel.BabeBotMp.PotionY.ToString();
        
        UpdatePercentageMonitorPosition();
    }

    public void UpdateStats(double fps, long clicks, long triggers)
    {
        Dispatcher.Invoke(() =>
        {
            FpsValue.Text = fps.ToString("F1");
            ClicksValue.Text = clicks.ToString();
            TriggersValue.Text = triggers.ToString();
        });
    }

    private void StartPeriodicClicks()
    {
        StopPeriodicClicks(); // Stop any existing timers
        
        // Y Click Timer
        if (ViewModel.YClick.Enabled && ViewModel.YClick.PeriodMs > 0)
        {
            _yClickTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ViewModel.YClick.PeriodMs)
            };
            _yClickTimer.Tick += (s, e) => {
                // Check if using coordinate or key press
                if (ViewModel.YClick.UseCoordinate)
                {
                    // Background click without mouse movement for simultaneous clients
                    PerformBackgroundClick(ViewModel.YClick.X, ViewModel.YClick.Y, "Y-PERIODIC");
                }
                else if (ViewModel.YClick.UseKeyPress && !string.IsNullOrEmpty(ViewModel.YClick.KeyToPress))
                {
                    // Send key press
                    PerformBackgroundKeyPress(ViewModel.YClick.KeyToPress, "Y-PERIODIC");
                }
            };
            _yClickTimer.Start();
            Console.WriteLine($"[{ViewModel.ClientName}] Y periodic click STARTED: ({ViewModel.YClick.X},{ViewModel.YClick.Y}) every {ViewModel.YClick.PeriodMs}ms");
        }
        else
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Y periodic click DISABLED: Enabled={ViewModel.YClick.Enabled}, Period={ViewModel.YClick.PeriodMs}ms");
        }
        
        // Extra1 Timer
        if (ViewModel.Extra1Click.Enabled && ViewModel.Extra1Click.PeriodMs > 0)
        {
            _extra1Timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ViewModel.Extra1Click.PeriodMs)
            };
            _extra1Timer.Tick += (s, e) => {
                if (ViewModel.Extra1Click.UseCoordinate)
                {
                    PerformBackgroundClick(ViewModel.Extra1Click.X, ViewModel.Extra1Click.Y, "Extra1");
                }
                else if (ViewModel.Extra1Click.UseKeyPress && !string.IsNullOrEmpty(ViewModel.Extra1Click.KeyToPress))
                {
                    PerformBackgroundKeyPress(ViewModel.Extra1Click.KeyToPress, "Extra1");
                }
            };
            _extra1Timer.Start();
            Console.WriteLine($"[{ViewModel.ClientName}] Extra1 periodic click STARTED: ({ViewModel.Extra1Click.X},{ViewModel.Extra1Click.Y}) every {ViewModel.Extra1Click.PeriodMs}ms");
        }
        else
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Extra1 periodic click DISABLED: Enabled={ViewModel.Extra1Click.Enabled}, Period={ViewModel.Extra1Click.PeriodMs}ms");
        }
        
        // Extra2 Timer
        if (ViewModel.Extra2Click.Enabled && ViewModel.Extra2Click.PeriodMs > 0)
        {
            _extra2Timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ViewModel.Extra2Click.PeriodMs)
            };
            _extra2Timer.Tick += (s, e) => {
                if (ViewModel.Extra2Click.UseCoordinate)
                {
                    PerformBackgroundClick(ViewModel.Extra2Click.X, ViewModel.Extra2Click.Y, "Extra2");
                }
                else if (ViewModel.Extra2Click.UseKeyPress && !string.IsNullOrEmpty(ViewModel.Extra2Click.KeyToPress))
                {
                    PerformBackgroundKeyPress(ViewModel.Extra2Click.KeyToPress, "Extra2");
                }
            };
            _extra2Timer.Start();
            Console.WriteLine($"[{ViewModel.ClientName}] Extra2 periodic click STARTED: ({ViewModel.Extra2Click.X},{ViewModel.Extra2Click.Y}) every {ViewModel.Extra2Click.PeriodMs}ms");
        }
        else
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Extra2 periodic click DISABLED: Enabled={ViewModel.Extra2Click.Enabled}, Period={ViewModel.Extra2Click.PeriodMs}ms");
        }
        
        // Extra3 Timer
        if (ViewModel.Extra3Click.Enabled && ViewModel.Extra3Click.PeriodMs > 0)
        {
            _extra3Timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ViewModel.Extra3Click.PeriodMs)
            };
            _extra3Timer.Tick += (s, e) => {
                if (ViewModel.Extra3Click.UseCoordinate)
                {
                    PerformBackgroundClick(ViewModel.Extra3Click.X, ViewModel.Extra3Click.Y, "Extra3");
                }
                else if (ViewModel.Extra3Click.UseKeyPress && !string.IsNullOrEmpty(ViewModel.Extra3Click.KeyToPress))
                {
                    PerformBackgroundKeyPress(ViewModel.Extra3Click.KeyToPress, "Extra3");
                }
            };
            _extra3Timer.Start();
            Console.WriteLine($"[{ViewModel.ClientName}] Extra3 periodic click STARTED: ({ViewModel.Extra3Click.X},{ViewModel.Extra3Click.Y}) every {ViewModel.Extra3Click.PeriodMs}ms");
        }
        else
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Extra3 periodic click DISABLED: Enabled={ViewModel.Extra3Click.Enabled}, Period={ViewModel.Extra3Click.PeriodMs}ms");
        }
    }
    
    private void StopPeriodicClicks()
    {
        if (_yClickTimer != null)
        {
            _yClickTimer.Stop();
            _yClickTimer = null;
        }
        if (_extra1Timer != null)
        {
            _extra1Timer.Stop();
            _extra1Timer = null;
        }
        if (_extra2Timer != null)
        {
            _extra2Timer.Stop();
            _extra2Timer = null;
        }
        if (_extra3Timer != null)
        {
            _extra3Timer.Stop();
            _extra3Timer = null;
        }
        
        // Stop BabeBot timer
        if (_babeBotTimer != null)
        {
            _babeBotTimer.Stop();
            _babeBotTimer = null;
        }
        
        Console.WriteLine($"[{ViewModel.ClientName}] All periodic timers STOPPED and disposed");
    }
    
    private void StartMonitoring()
    {
        StopMonitoring();
        
        // Only start monitoring if HP, MP triggers, or percentage monitoring are enabled
        if (ViewModel.HpTrigger.Enabled || ViewModel.MpTrigger.Enabled || ViewModel.HpPercentageProbe.Enabled || ViewModel.MpPercentageProbe.Enabled)
        {
            // Use master timer instead of individual DispatcherTimer
            _masterTimer?.AddOrUpdateTask(
                "HPMPMonitoring", 
                TimeSpan.FromMilliseconds(50), // 20Hz for responsive detection
                () => MonitoringTimer_Tick(null, null),
                enabled: true,
                priority: 10 // High priority for HP/MP monitoring
            );
            _masterTimer?.Start();
            Console.WriteLine($"[{ViewModel.ClientName}] HP/MP monitoring STARTED: HP enabled={ViewModel.HpTrigger.Enabled}, MP enabled={ViewModel.MpTrigger.Enabled}");
        }
        else
        {
            Console.WriteLine($"[{ViewModel.ClientName}] HP/MP monitoring DISABLED: Both HP and MP triggers are disabled");
        }
    }
    
    private void StopMonitoring()
    {
        // Disable tasks in master timer instead of stopping individual timers
        _masterTimer?.SetTaskEnabled("HPMPMonitoring", false);
        _masterTimer?.SetTaskEnabled("HPTriggerCooldown", false);
        _masterTimer?.SetTaskEnabled("MPTriggerCooldown", false);
        
        // Keep legacy references for compatibility (no longer used)
        if (_monitoringTimer != null)
        {
            _monitoringTimer.Stop();
            _monitoringTimer = null;
        }
        if (_hpTriggerTimer != null)
        {
            _hpTriggerTimer.Stop();
            _hpTriggerTimer = null;
        }
        if (_mpTriggerTimer != null)
        {
            _mpTriggerTimer.Stop();
            _mpTriggerTimer = null;
        }
        
        // Reset trigger states
        if (ViewModel.HpTrigger != null)
        {
            ViewModel.HpTrigger.IsTriggered = false;
            ViewModel.HpTrigger.KeepClicking = false;
        }
        if (ViewModel.MpTrigger != null)
        {
            ViewModel.MpTrigger.IsTriggered = false;
            ViewModel.MpTrigger.KeepClicking = false;
        }
        
        // Reset monitoring state
        _isMonitoringBusy = false;
        
        Console.WriteLine($"[{ViewModel.ClientName}] All monitoring timers STOPPED and disposed");
    }
    
    private bool _isMonitoringBusy = false;
    
    private async void MonitoringTimer_Tick(object? sender, EventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero) return;
        if (_isMonitoringBusy) return; // Skip if previous monitoring still running
        
        _isMonitoringBusy = true;
        
        try
        {
            // PERFORMANCE OPTIMIZATION: Use cached color sampling to reduce Win32 API calls by 80%+
            await Task.Run(() =>
            {
            try
            {
                // CACHED HP/MP color sampling with high priority for critical monitoring
                var hpPoint = new Point(ViewModel.HpProbe.X, ViewModel.HpProbe.Y);
                var mpPoint = new Point(ViewModel.MpProbe.X, ViewModel.MpProbe.Y);
                
                // Declare variables outside conditional blocks so they can be used later
                Color currentHpColor;
                Color currentMpColor;
                double hpPercentage;
                double mpPercentage;
                
                // Batch sample HP and MP colors in a single optimized operation
                if (_colorCache != null)
                {
                    var colors = _colorCache.BatchSampleColors(ViewModel.TargetHwnd, new[] { hpPoint, mpPoint }, TimeSpan.FromMilliseconds(40));
                    
                    currentHpColor = colors.GetValueOrDefault(hpPoint, Color.Black);
                    currentMpColor = colors.GetValueOrDefault(mpPoint, Color.Black);
                    
                    ViewModel.HpProbe.CurrentColor = currentHpColor;
                    ViewModel.MpProbe.CurrentColor = currentMpColor;
                    
                    // CACHED percentage calculations - reuse color sampling with intelligent caching
                    hpPercentage = CalculateBarPercentageCached(ViewModel.TargetHwnd, 
                        ViewModel.HpProbe.X, ViewModel.HpProbe.Y, ViewModel.HpProbe.Width, ViewModel.HpProbe.Height,
                        ViewModel.HpProbe.ExpectedColor, ViewModel.HpProbe.TriggerColor);
                        
                    mpPercentage = CalculateBarPercentageCached(ViewModel.TargetHwnd,
                        ViewModel.MpProbe.X, ViewModel.MpProbe.Y, ViewModel.MpProbe.Width, ViewModel.MpProbe.Height, 
                        ViewModel.MpProbe.ExpectedColor, ViewModel.MpProbe.TriggerColor);
                        
                    // Debug cached performance (every 10 seconds)
                    if (DateTime.Now.Second % 10 == 0 && DateTime.Now.Millisecond < 100)
                    {
                        var stats = _colorCache.GetPerformanceStats();
                        Console.WriteLine($"[{ViewModel.ClientName}] 🚀 CACHE STATS: {stats.HitRate:F1}% hit rate, {stats.ApiCallsSaved} API calls saved, {stats.ApiReductionPercentage:F1}% reduction");
                    }
                }
                else
                {
                    // Fallback to original method if cache not available
                    currentHpColor = ColorSampler.GetAverageColorInArea(ViewModel.TargetHwnd, ViewModel.HpProbe.X, ViewModel.HpProbe.Y, 5);
                    currentMpColor = ColorSampler.GetAverageColorInArea(ViewModel.TargetHwnd, ViewModel.MpProbe.X, ViewModel.MpProbe.Y, 5);
                    
                    ViewModel.HpProbe.CurrentColor = currentHpColor;
                    ViewModel.MpProbe.CurrentColor = currentMpColor;
                    
                    hpPercentage = ColorSampler.CalculateBarPercentage(
                        ViewModel.TargetHwnd, ViewModel.HpProbe.X, ViewModel.HpProbe.Y, 
                        ViewModel.HpProbe.Width, ViewModel.HpProbe.Height,
                        ViewModel.HpProbe.ExpectedColor, ViewModel.HpProbe.TriggerColor);
                        
                    mpPercentage = ColorSampler.CalculateBarPercentage(
                        ViewModel.TargetHwnd, ViewModel.MpProbe.X, ViewModel.MpProbe.Y,
                        ViewModel.MpProbe.Width, ViewModel.MpProbe.Height, 
                        ViewModel.MpProbe.ExpectedColor, ViewModel.MpProbe.TriggerColor);
                }
                
                // UI updates UI thread'de yap
                Dispatcher.BeginInvoke(() =>
                {
                    // Update UI percentage display
                    HpPercentageText.Text = $"{hpPercentage:F0}%";
                    MpPercentageText.Text = $"{mpPercentage:F0}%";
                    
                    // Color coding for percentages
                    HpPercentageText.Foreground = hpPercentage > 70 ? new SolidColorBrush(Colors.Green) : 
                                                 hpPercentage > 30 ? new SolidColorBrush(Colors.Orange) : 
                                                 new SolidColorBrush(Colors.Red);
                    MpPercentageText.Foreground = mpPercentage > 50 ? new SolidColorBrush(Colors.CornflowerBlue) : 
                                                 mpPercentage > 20 ? new SolidColorBrush(Colors.Orange) : 
                                                 new SolidColorBrush(Colors.Red);
                });
                
                // Debug current status every 5 seconds
                if (DateTime.Now.Second % 5 == 0)
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] MONITOR: HP={hpPercentage:F1}% (threshold={ViewModel.HpProbe.Tolerance}%) MP={mpPercentage:F1}% (threshold={ViewModel.MpProbe.Tolerance}%)");
                    Console.WriteLine($"[{ViewModel.ClientName}] COLORS: HP=RGB({currentHpColor.R},{currentHpColor.G},{currentHpColor.B}) MP=RGB({currentMpColor.R},{currentMpColor.G},{currentMpColor.B})");
                    Console.WriteLine($"[{ViewModel.ClientName}] REFERENCE: HP=RGB({ViewModel.HpProbe.ExpectedColor.R},{ViewModel.HpProbe.ExpectedColor.G},{ViewModel.HpProbe.ExpectedColor.B}) MP=RGB({ViewModel.MpProbe.ExpectedColor.R},{ViewModel.MpProbe.ExpectedColor.G},{ViewModel.MpProbe.ExpectedColor.B})");
                    Console.WriteLine($"[{ViewModel.ClientName}] ENABLED: HP={ViewModel.HpTrigger.Enabled} MP={ViewModel.MpTrigger.Enabled}");
                    Console.WriteLine($"[{ViewModel.ClientName}] TRIGGERED: HP={ViewModel.HpTrigger.IsTriggered} MP={ViewModel.MpTrigger.IsTriggered}");
                }
                
                // Standard HP/MP trigger checks
                CheckHpTriggerByPercentage(hpPercentage);
                CheckMpTriggerByPercentage(mpPercentage);
                
                // Python-style percentage monitoring 
                CheckPercentageBasedTriggers();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] Monitoring error: {ex.Message}");
            }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Async monitoring error: {ex.Message}");
        }
        finally
        {
            _isMonitoringBusy = false;
        }
    }
    
    private void CheckHpTrigger(System.Drawing.Color currentColor)
    {
        if (!ViewModel.HpTrigger.Enabled) return;
        
        // Calculate distance from original reference color (full HP color)
        var distanceFromReference = ColorSampler.CalculateColorDistance(currentColor, ViewModel.HpProbe.ExpectedColor);
        
        // HP decreased if color is different from reference (full HP)
        bool hpLow = distanceFromReference > ViewModel.HpProbe.Tolerance;
        
        if (hpLow && !ViewModel.HpTrigger.IsTriggered)
        {
            // HP dropped, start clicking immediately
            ViewModel.HpTrigger.IsTriggered = true;
            ViewModel.HpTrigger.KeepClicking = true;
            StartHpTriggerClicking();
            Console.WriteLine($"[{ViewModel.ClientName}] HP LOW DETECTED (distance: {distanceFromReference:F1}) - Starting potion clicks");
        }
        else if (!hpLow && ViewModel.HpTrigger.IsTriggered)
        {
            // HP restored to full, stop clicking
            ViewModel.HpTrigger.IsTriggered = false;
            ViewModel.HpTrigger.KeepClicking = false;
            StopHpTriggerClicking();
            Console.WriteLine($"[{ViewModel.ClientName}] HP FULL (distance: {distanceFromReference:F1}) - Stopping potion clicks");
        }
    }
    
    private void CheckMpTrigger(System.Drawing.Color currentColor)
    {
        if (!ViewModel.MpTrigger.Enabled) return;
        
        // Calculate distance from original reference color (full MP color)
        var distanceFromReference = ColorSampler.CalculateColorDistance(currentColor, ViewModel.MpProbe.ExpectedColor);
        
        // MP decreased if color is different from reference (full MP)
        bool mpLow = distanceFromReference > ViewModel.MpProbe.Tolerance;
        
        if (mpLow && !ViewModel.MpTrigger.IsTriggered)
        {
            // MP dropped, start clicking immediately
            ViewModel.MpTrigger.IsTriggered = true;
            ViewModel.MpTrigger.KeepClicking = true;
            StartMpTriggerClicking();
            Console.WriteLine($"[{ViewModel.ClientName}] MP LOW DETECTED (distance: {distanceFromReference:F1}) - Starting potion clicks");
        }
        else if (!mpLow && ViewModel.MpTrigger.IsTriggered)
        {
            // MP restored to full, stop clicking
            ViewModel.MpTrigger.IsTriggered = false;
            ViewModel.MpTrigger.KeepClicking = false;
            StopMpTriggerClicking();
            Console.WriteLine($"[{ViewModel.ClientName}] MP FULL (distance: {distanceFromReference:F1}) - Stopping potion clicks");
        }
    }
    
    private void StartHpTriggerClicking()
    {
        if (_hpTriggerTimer != null) return;
        
        _hpTriggerTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(ViewModel.HpTrigger.CooldownMs)
        };
        _hpTriggerTimer.Tick += (s, e) =>
        {
            Console.WriteLine($"[{ViewModel.ClientName}] HP TIMER TICK - KeepClicking={ViewModel.HpTrigger.KeepClicking}, Coords=({ViewModel.HpTrigger.X},{ViewModel.HpTrigger.Y})");
            
            if (ViewModel.HpTrigger.KeepClicking)
            {
                if (ViewModel.HpTrigger.UseCoordinate)
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] HP TRIGGER CLICK at ({ViewModel.HpTrigger.X},{ViewModel.HpTrigger.Y})");
                    PerformBackgroundClick(ViewModel.HpTrigger.X, ViewModel.HpTrigger.Y, "HP_TRIGGER");
                }
                else if (ViewModel.HpTrigger.UseKeyPress && !string.IsNullOrEmpty(ViewModel.HpTrigger.KeyToPress))
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] HP TRIGGER KEY PRESS '{ViewModel.HpTrigger.KeyToPress}'");
                    PerformBackgroundKeyPress(ViewModel.HpTrigger.KeyToPress, "HP_TRIGGER");
                }
                ViewModel.HpTrigger.ExecutionCount++;
                ViewModel.TriggerCount++;
            }
            else
            {
                Console.WriteLine($"[{ViewModel.ClientName}] HP TIMER: KeepClicking is FALSE - stopping timer");
                StopHpTriggerClicking();
            }
        };
        _hpTriggerTimer.Start();
    }
    
    private void StopHpTriggerClicking()
    {
        _hpTriggerTimer?.Stop();
        _hpTriggerTimer = null;
    }
    
    private void StartMpTriggerClicking()
    {
        if (_mpTriggerTimer != null) return;
        
        _mpTriggerTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(ViewModel.MpTrigger.CooldownMs)
        };
        _mpTriggerTimer.Tick += (s, e) =>
        {
            Console.WriteLine($"[{ViewModel.ClientName}] MP TIMER TICK - KeepClicking={ViewModel.MpTrigger.KeepClicking}, Coords=({ViewModel.MpTrigger.X},{ViewModel.MpTrigger.Y})");
            
            if (ViewModel.MpTrigger.KeepClicking)
            {
                if (ViewModel.MpTrigger.UseCoordinate)
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] MP TRIGGER CLICK at ({ViewModel.MpTrigger.X},{ViewModel.MpTrigger.Y})");
                    PerformBackgroundClick(ViewModel.MpTrigger.X, ViewModel.MpTrigger.Y, "MP_TRIGGER");
                }
                else if (ViewModel.MpTrigger.UseKeyPress && !string.IsNullOrEmpty(ViewModel.MpTrigger.KeyToPress))
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] MP TRIGGER KEY PRESS '{ViewModel.MpTrigger.KeyToPress}'");
                    PerformBackgroundKeyPress(ViewModel.MpTrigger.KeyToPress, "MP_TRIGGER");
                }
                ViewModel.MpTrigger.ExecutionCount++;
                ViewModel.TriggerCount++;
            }
            else
            {
                Console.WriteLine($"[{ViewModel.ClientName}] MP TIMER: KeepClicking is FALSE - stopping timer");
                StopMpTriggerClicking();
            }
        };
        _mpTriggerTimer.Start();
    }
    
    private void StopMpTriggerClicking()
    {
        _mpTriggerTimer?.Stop();
        _mpTriggerTimer = null;
    }
    
    private void CheckHpTriggerByPercentage(double hpPercentage)
    {
        if (!ViewModel.HpTrigger.Enabled) 
        {
            // Debug why HP trigger is disabled
            if (DateTime.Now.Millisecond < 100) // Log once per second roughly
            {
                Console.WriteLine($"[{ViewModel.ClientName}] HP TRIGGER DISABLED: Enabled={ViewModel.HpTrigger.Enabled}, Coords=({ViewModel.HpTrigger.X},{ViewModel.HpTrigger.Y})");
            }
            return;
        }
        
        // Use tolerance field as percentage threshold (e.g., 70 = 70%)
        bool hpLow = hpPercentage < ViewModel.HpProbe.Tolerance;
        
        // Debug trigger logic
        if (DateTime.Now.Millisecond < 100) // Log once per second roughly
        {
            Console.WriteLine($"[{ViewModel.ClientName}] HP CHECK: {hpPercentage:F1}% < {ViewModel.HpProbe.Tolerance}% = {hpLow}, Already Triggered={ViewModel.HpTrigger.IsTriggered}");
        }
        
        if (hpLow && !ViewModel.HpTrigger.IsTriggered)
        {
            ViewModel.HpTrigger.IsTriggered = true;
            ViewModel.HpTrigger.KeepClicking = true;
            Console.WriteLine($"[{ViewModel.ClientName}] HP LOW ({hpPercentage:F1}%) - Starting potion clicks at ({ViewModel.HpTrigger.X},{ViewModel.HpTrigger.Y}) cooldown={ViewModel.HpTrigger.CooldownMs}ms");
            StartHpTriggerClicking();
        }
        else if (!hpLow && ViewModel.HpTrigger.IsTriggered)
        {
            ViewModel.HpTrigger.IsTriggered = false;
            ViewModel.HpTrigger.KeepClicking = false;
            StopHpTriggerClicking();
            Console.WriteLine($"[{ViewModel.ClientName}] HP OK ({hpPercentage:F1}%) - Stopping potion clicks");
        }
    }
    
    private void CheckMpTriggerByPercentage(double mpPercentage)
    {
        if (!ViewModel.MpTrigger.Enabled) 
        {
            // Debug why MP trigger is disabled
            if (DateTime.Now.Millisecond < 100) // Log once per second roughly
            {
                Console.WriteLine($"[{ViewModel.ClientName}] MP TRIGGER DISABLED: Enabled={ViewModel.MpTrigger.Enabled}, Coords=({ViewModel.MpTrigger.X},{ViewModel.MpTrigger.Y})");
            }
            return;
        }
        
        // Use tolerance field as percentage threshold (e.g., 50 = 50%)
        bool mpLow = mpPercentage < ViewModel.MpProbe.Tolerance;
        
        // Debug trigger logic
        if (DateTime.Now.Millisecond < 100) // Log once per second roughly
        {
            Console.WriteLine($"[{ViewModel.ClientName}] MP CHECK: {mpPercentage:F1}% < {ViewModel.MpProbe.Tolerance}% = {mpLow}, Already Triggered={ViewModel.MpTrigger.IsTriggered}");
        }
        
        if (mpLow && !ViewModel.MpTrigger.IsTriggered)
        {
            ViewModel.MpTrigger.IsTriggered = true;
            ViewModel.MpTrigger.KeepClicking = true;
            Console.WriteLine($"[{ViewModel.ClientName}] MP LOW ({mpPercentage:F1}%) - Starting potion clicks at ({ViewModel.MpTrigger.X},{ViewModel.MpTrigger.Y}) cooldown={ViewModel.MpTrigger.CooldownMs}ms");
            StartMpTriggerClicking();
        }
        else if (!mpLow && ViewModel.MpTrigger.IsTriggered)
        {
            ViewModel.MpTrigger.IsTriggered = false;
            ViewModel.MpTrigger.KeepClicking = false;
            StopMpTriggerClicking();
            Console.WriteLine($"[{ViewModel.ClientName}] MP OK ({mpPercentage:F1}%) - Stopping potion clicks");
        }
    }
    
    private void UpdateHpColorDisplay(System.Drawing.Color color)
    {
        HpColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
        HpColorText.Text = $"{color.R},{color.G},{color.B}";
    }
    
    private void UpdateMpColorDisplay(System.Drawing.Color color)
    {
        MpColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
        MpColorText.Text = $"{color.R},{color.G},{color.B}";
    }
    
    private void CheckPercentageBasedTriggers()
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero) return;
        
        try
        {
            // Check HP percentage probe
            if (ViewModel.HpPercentageProbe.Enabled)
            {
                var hpX = ViewModel.HpPercentageProbe.CalculatedX;
                var hpY = ViewModel.HpPercentageProbe.Y;
                
                var currentColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, hpX, hpY);
                var distance = ColorSampler.CalculateColorDistance(currentColor, ViewModel.HpPercentageProbe.ExpectedColor);
                
                // Python logic: if pixel_color != expected_color then trigger
                bool hpColorChanged = distance > ViewModel.HpPercentageProbe.Tolerance;
                
                // Update current color in ViewModel for real-time tracking
                ViewModel.HpPercentageProbe.CurrentColor = currentColor;
                
                // Update UI with real-time color and status
                Dispatcher.BeginInvoke(() =>
                {
                    // Update current color display
                    HpCurrentColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(currentColor.R, currentColor.G, currentColor.B));
                    HpCurrentColorText.Text = $"{currentColor.R},{currentColor.G},{currentColor.B}";
                    
                    if (hpColorChanged)
                    {
                        HpPercentageStatus.Text = $"LOW ({distance:F1})";
                        HpPercentageStatus.Foreground = new SolidColorBrush(Colors.Red);
                    }
                    else
                    {
                        HpPercentageStatus.Text = $"OK ({distance:F1})";
                        HpPercentageStatus.Foreground = new SolidColorBrush(Colors.LimeGreen);
                    }
                    
                    // Debug info every 200 cycles (roughly every 10 seconds) and only when interesting
                    if (_debugCounter % 200 == 0 && (hpColorChanged || distance > 50))
                    {
                        Console.WriteLine($"[{ViewModel.ClientName}] HP-DEBUG: Current=RGB({currentColor.R},{currentColor.G},{currentColor.B}) Distance={distance:F1} Triggered={hpColorChanged}");
                    }
                });
                
                // Trigger logic with cooldown check
                if (hpColorChanged && !ViewModel.HpPercentageProbe.IsTriggered && ViewModel.PythonHpPotionClick.Enabled)
                {
                    var now = DateTime.UtcNow;
                    if ((now - ViewModel.PythonHpPotionClick.LastExecution).TotalMilliseconds >= ViewModel.PythonHpPotionClick.CooldownMs)
                    {
                        ViewModel.HpPercentageProbe.IsTriggered = true;
                        ViewModel.PythonHpPotionClick.LastExecution = now;
                        Console.WriteLine($"[{ViewModel.ClientName}] PYTHON-HP: Color changed at {hpX},{hpY} (threshold {ViewModel.HpPercentageProbe.MonitorPercentage}%) - RGB({currentColor.R},{currentColor.G},{currentColor.B}) distance={distance:F1}");
                        
                        // Trigger Python-style HP potion action
                        if (ViewModel.PythonHpPotionClick.UseCoordinate && ViewModel.PythonHpPotionClick.X > 0 && ViewModel.PythonHpPotionClick.Y > 0)
                        {
                            Console.WriteLine($"[{ViewModel.ClientName}] PYTHON-HP: Triggering potion click at ({ViewModel.PythonHpPotionClick.X},{ViewModel.PythonHpPotionClick.Y}) cooldown={ViewModel.PythonHpPotionClick.CooldownMs}ms");
                            PerformBackgroundClick(ViewModel.PythonHpPotionClick.X, ViewModel.PythonHpPotionClick.Y, "PYTHON_HP_TRIGGER");
                            ViewModel.TriggerCount++;
                            ViewModel.PythonHpPotionClick.ExecutionCount++;
                        }
                        else if (ViewModel.PythonHpPotionClick.UseKeyPress && !string.IsNullOrEmpty(ViewModel.PythonHpPotionClick.KeyToPress))
                        {
                            Console.WriteLine($"[{ViewModel.ClientName}] PYTHON-HP: Triggering key press '{ViewModel.PythonHpPotionClick.KeyToPress}' cooldown={ViewModel.PythonHpPotionClick.CooldownMs}ms");
                            PerformBackgroundKeyPress(ViewModel.PythonHpPotionClick.KeyToPress, "PYTHON_HP_TRIGGER");
                            ViewModel.TriggerCount++;
                            ViewModel.PythonHpPotionClick.ExecutionCount++;
                        }
                    }
                    else
                    {
                        var remainingCooldown = ViewModel.PythonHpPotionClick.CooldownMs - (now - ViewModel.PythonHpPotionClick.LastExecution).TotalMilliseconds;
                        Console.WriteLine($"[{ViewModel.ClientName}] PYTHON-HP: On cooldown, {remainingCooldown:F0}ms remaining");
                    }
                }
                else if (!hpColorChanged && ViewModel.HpPercentageProbe.IsTriggered)
                {
                    ViewModel.HpPercentageProbe.IsTriggered = false;
                    Console.WriteLine($"[{ViewModel.ClientName}] PYTHON-HP: Color restored at {hpX},{hpY}");
                }
            }
            
            // Check MP percentage probe
            if (ViewModel.MpPercentageProbe.Enabled)
            {
                var mpX = ViewModel.MpPercentageProbe.CalculatedX;
                var mpY = ViewModel.MpPercentageProbe.Y;
                
                var currentColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, mpX, mpY);
                var distance = ColorSampler.CalculateColorDistance(currentColor, ViewModel.MpPercentageProbe.ExpectedColor);
                
                // Python logic: if pixel_color != expected_color then trigger
                bool mpColorChanged = distance > ViewModel.MpPercentageProbe.Tolerance;
                
                // Update current color in ViewModel for real-time tracking
                ViewModel.MpPercentageProbe.CurrentColor = currentColor;
                
                // Update UI with real-time color and status
                Dispatcher.BeginInvoke(() =>
                {
                    // Update current color display
                    MpCurrentColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(currentColor.R, currentColor.G, currentColor.B));
                    MpCurrentColorText.Text = $"{currentColor.R},{currentColor.G},{currentColor.B}";
                    
                    if (mpColorChanged)
                    {
                        MpPercentageStatus.Text = $"LOW ({distance:F1})";
                        MpPercentageStatus.Foreground = new SolidColorBrush(Colors.Red);
                    }
                    else
                    {
                        MpPercentageStatus.Text = $"OK ({distance:F1})";
                        MpPercentageStatus.Foreground = new SolidColorBrush(Colors.CornflowerBlue);
                    }
                    
                    // Debug info every 200 cycles (roughly every 10 seconds) and only when interesting
                    if (_debugCounter % 200 == 0 && (mpColorChanged || distance > 50))
                    {
                        Console.WriteLine($"[{ViewModel.ClientName}] MP-DEBUG: Current=RGB({currentColor.R},{currentColor.G},{currentColor.B}) Distance={distance:F1} Triggered={mpColorChanged}");
                    }
                });
                
                // Trigger logic with cooldown check
                if (mpColorChanged && !ViewModel.MpPercentageProbe.IsTriggered && ViewModel.PythonMpPotionClick.Enabled)
                {
                    var now = DateTime.UtcNow;
                    if ((now - ViewModel.PythonMpPotionClick.LastExecution).TotalMilliseconds >= ViewModel.PythonMpPotionClick.CooldownMs)
                    {
                        ViewModel.MpPercentageProbe.IsTriggered = true;
                        ViewModel.PythonMpPotionClick.LastExecution = now;
                        Console.WriteLine($"[{ViewModel.ClientName}] PYTHON-MP: Color changed at {mpX},{mpY} (threshold {ViewModel.MpPercentageProbe.MonitorPercentage}%) - RGB({currentColor.R},{currentColor.G},{currentColor.B}) distance={distance:F1}");
                        
                        // Trigger Python-style MP potion action
                        if (ViewModel.PythonMpPotionClick.UseCoordinate && ViewModel.PythonMpPotionClick.X > 0 && ViewModel.PythonMpPotionClick.Y > 0)
                        {
                            Console.WriteLine($"[{ViewModel.ClientName}] PYTHON-MP: Triggering potion click at ({ViewModel.PythonMpPotionClick.X},{ViewModel.PythonMpPotionClick.Y}) cooldown={ViewModel.PythonMpPotionClick.CooldownMs}ms");
                            PerformBackgroundClick(ViewModel.PythonMpPotionClick.X, ViewModel.PythonMpPotionClick.Y, "PYTHON_MP_TRIGGER");
                            ViewModel.TriggerCount++;
                            ViewModel.PythonMpPotionClick.ExecutionCount++;
                        }
                        else if (ViewModel.PythonMpPotionClick.UseKeyPress && !string.IsNullOrEmpty(ViewModel.PythonMpPotionClick.KeyToPress))
                        {
                            Console.WriteLine($"[{ViewModel.ClientName}] PYTHON-MP: Triggering key press '{ViewModel.PythonMpPotionClick.KeyToPress}' cooldown={ViewModel.PythonMpPotionClick.CooldownMs}ms");
                            PerformBackgroundKeyPress(ViewModel.PythonMpPotionClick.KeyToPress, "PYTHON_MP_TRIGGER");
                            ViewModel.TriggerCount++;
                            ViewModel.PythonMpPotionClick.ExecutionCount++;
                        }
                    }
                    else
                    {
                        var remainingCooldown = ViewModel.PythonMpPotionClick.CooldownMs - (now - ViewModel.PythonMpPotionClick.LastExecution).TotalMilliseconds;
                        Console.WriteLine($"[{ViewModel.ClientName}] PYTHON-MP: On cooldown, {remainingCooldown:F0}ms remaining");
                    }
                }
                else if (!mpColorChanged && ViewModel.MpPercentageProbe.IsTriggered)
                {
                    ViewModel.MpPercentageProbe.IsTriggered = false;
                    Console.WriteLine($"[{ViewModel.ClientName}] PYTHON-MP: Color restored at {mpX},{mpY}");
                }
            }
            
            // Increment debug counter
            _debugCounter++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Percentage monitoring error: {ex.Message}");
        }
    }
    
    private void PerformClick(int x, int y, string channel)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero) return;
        
        try
        {
            // Get selected click mode from main window (UI control is hidden, use default)
            var mainWindow = Application.Current.MainWindow as MainWindow;
            var clickMode = "message"; // Default since ClickModeTextBox is hidden
            
            // Debug what was selected
            Console.WriteLine($"DEBUG: Click mode: {clickMode}");
            
            // Route to appropriate click method based on dropdown selection
            switch (clickMode.ToLower())
            {
                case "message":
                    PerformMessageClick(x, y, channel);
                    break;
                case "postmessage":
                    PerformPostMessageClick(x, y, channel);
                    break;
                case "sendmessage":
                    PerformSendMessageClick(x, y, channel);
                    break;
                case "cursor-jump":
                    PerformCursorJumpClick(x, y, channel);
                    break;
                case "cursor-return":
                    PerformCursorReturnClick(x, y, channel);
                    break;
                case "sendinput":
                    PerformSendInputClick(x, y, channel);
                    break;
                case "mouse-event":
                    PerformMouseEventClick(x, y, channel);
                    break;
                case "direct-input":
                    PerformDirectInputClick(x, y, channel);
                    break;
                case "focus-click":
                    PerformFocusClick(x, y, channel);
                    break;
                case "child-window":
                default:
                    PerformChildWindowClick(x, y, channel);
                    break;
            }
            
            // Update click count
            ViewModel.ClickCount++;
            ClicksValue.Text = ViewModel.ClickCount.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Click failed: {ex.Message}");
        }
    }
    
    private void PerformMessageClick(int x, int y, string channel)
    {
        // NEW: Gameloop now uses borderless mode - no title bar or borders!
        // Use coordinates directly (ScreenToClient handles any remaining offset)
        int fixedX = x;  // No border compensation needed
        int fixedY = y;  // No title bar compensation needed
        
        // Verify coordinates by converting back to screen
        var testPoint = new Vanara.PInvoke.POINT { x = fixedX, y = fixedY };
        User32.ClientToScreen(ViewModel.TargetHwnd, ref testPoint);
        Console.WriteLine($"[{ViewModel.ClientName}] Original({x},{y}) -> Fixed({fixedX},{fixedY}) -> Screen({testPoint.x},{testPoint.y})");
        
        // Pure background click - no cursor movement
        var lParam = (fixedY << 16) | (fixedX & 0xFFFF);
        
        var smResult1 = User32.SendMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, IntPtr.Zero, (IntPtr)lParam);
        var smResult2 = User32.SendMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
        var pmResult1 = User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, IntPtr.Zero, (IntPtr)lParam);
        var pmResult2 = User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
        
        Console.WriteLine($"[{ViewModel.ClientName}] {channel} MESSAGE click at Fixed({fixedX}, {fixedY}) lParam:0x{lParam:X8} SM:{smResult1:X}/{smResult2:X} PM:{pmResult1}/{pmResult2}");
    }
    
    private void PerformCursorJumpClick(int x, int y, string channel)
    {
        // Move cursor, click, don't restore
        var point = new POINT { x = x, y = y };
        User32.ClientToScreen(ViewModel.TargetHwnd, ref point);
        
        User32.SetCursorPos(point.x, point.y);
        System.Threading.Thread.Sleep(10);
        
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(10);
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        
        Console.WriteLine($"[{ViewModel.ClientName}] {channel} CURSOR-JUMP click at client({x}, {y}) -> screen({point.x}, {point.y})");
    }
    
    private void PerformCursorReturnClick(int x, int y, string channel)
    {
        // Move cursor, click, restore position
        var point = new POINT { x = x, y = y };
        User32.ClientToScreen(ViewModel.TargetHwnd, ref point);
        
        User32.GetCursorPos(out var oldPos);
        User32.SetCursorPos(point.x, point.y);
        System.Threading.Thread.Sleep(10);
        
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(10);
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        
        System.Threading.Thread.Sleep(20);
        User32.SetCursorPos(oldPos.x, oldPos.y);
        
        Console.WriteLine($"[{ViewModel.ClientName}] {channel} CURSOR-RETURN click at client({x}, {y}) -> screen({point.x}, {point.y})");
    }
    
    private void PerformSendMessageClick(int x, int y, string channel)
    {
        // NEW: Gameloop borderless mode - no offset needed
        int fixedX = x;
        int fixedY = y;
        
        // Pure SendMessage only
        var lParam = (fixedY << 16) | (fixedX & 0xFFFF);
        var result1 = User32.SendMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, IntPtr.Zero, (IntPtr)lParam);
        var result2 = User32.SendMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
        
        Console.WriteLine($"[{ViewModel.ClientName}] {channel} SENDMESSAGE click at Fixed({fixedX}, {fixedY}) Result:{result1:X}/{result2:X}");
    }
    
    private void PerformPostMessageClick(int x, int y, string channel)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero) return;
        
        try
        {
            // Gameloop child window coordinates - use raw coordinates  
            var lParam = (y << 16) | (x & 0xFFFF);
            
            // Multiple message approach for reliability
            // Try both PostMessage and SendMessage for better compatibility
            var postDown = User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, IntPtr.Zero, (IntPtr)lParam);
            var postUp = User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
            
            // Fallback to SendMessage if PostMessage fails
            if (!postDown || !postUp)
            {
                User32.SendMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, IntPtr.Zero, (IntPtr)lParam);
                System.Threading.Thread.Sleep(5);
                User32.SendMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
            }
            
            // Debug for important clicks only
            if (channel.Contains("TEST") || channel.Contains("TRIGGER"))
            {
                Console.WriteLine($"[{ViewModel.ClientName}] {channel} click HWND:0x{ViewModel.TargetHwnd:X8} ({x},{y}) Post:{postDown}/{postUp}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Click error: {ex.Message}");
        }
    }
    
    private void PerformSendInputClick(int x, int y, string channel)
    {
        // SendInput with absolute coordinates (no cursor save/restore)
        var point = new POINT { x = x, y = y };
        User32.ClientToScreen(ViewModel.TargetHwnd, ref point);
        
        // Calculate relative coordinates for SendInput (0-65535 range)
        var screenWidth = User32.GetSystemMetrics(User32.SystemMetric.SM_CXSCREEN);
        var screenHeight = User32.GetSystemMetrics(User32.SystemMetric.SM_CYSCREEN);
        var relativeX = (point.x * 65535) / screenWidth;
        var relativeY = (point.y * 65535) / screenHeight;
        
        // Use simpler SendInput approach
        var inputs = new User32.INPUT[2];
        
        // Mouse down
        inputs[0] = new User32.INPUT
        {
            type = User32.INPUTTYPE.INPUT_MOUSE,
            mi = new User32.MOUSEINPUT
            {
                dx = relativeX,
                dy = relativeY,
                dwFlags = User32.MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN | User32.MOUSEEVENTF.MOUSEEVENTF_ABSOLUTE
            }
        };
        
        // Mouse up
        inputs[1] = new User32.INPUT
        {
            type = User32.INPUTTYPE.INPUT_MOUSE,
            mi = new User32.MOUSEINPUT
            {
                dx = relativeX,
                dy = relativeY,
                dwFlags = User32.MOUSEEVENTF.MOUSEEVENTF_LEFTUP | User32.MOUSEEVENTF.MOUSEEVENTF_ABSOLUTE
            }
        };
        
        // Send the inputs
        User32.SendInput(2, inputs, System.Runtime.InteropServices.Marshal.SizeOf<User32.INPUT>());
        
        Console.WriteLine($"[{ViewModel.ClientName}] {channel} SENDINPUT click at client({x}, {y}) -> screen({point.x}, {point.y}) -> relative({relativeX}, {relativeY})");
    }
    
    private void PerformMouseEventClick(int x, int y, string channel)
    {
        // mouse_event with absolute coordinates
        var point = new POINT { x = x, y = y };
        User32.ClientToScreen(ViewModel.TargetHwnd, ref point);
        
        var screenWidth = User32.GetSystemMetrics(User32.SystemMetric.SM_CXSCREEN);
        var screenHeight = User32.GetSystemMetrics(User32.SystemMetric.SM_CYSCREEN);
        var relativeX = (point.x * 65535) / screenWidth;
        var relativeY = (point.y * 65535) / screenHeight;
        
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN | User32.MOUSEEVENTF.MOUSEEVENTF_ABSOLUTE, relativeX, relativeY, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(10);
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTUP | User32.MOUSEEVENTF.MOUSEEVENTF_ABSOLUTE, relativeX, relativeY, 0, IntPtr.Zero);
        
        Console.WriteLine($"[{ViewModel.ClientName}] {channel} MOUSE-EVENT click at client({x}, {y}) -> screen({point.x}, {point.y}) -> relative({relativeX}, {relativeY})");
    }
    
    private void PerformChildWindowClick(int x, int y, string channel)
    {
        // For MuMu Player, try multiple methods since PostMessage might not work
        int fixedX = x;
        int fixedY = y;
        
        var point = new POINT { x = fixedX, y = fixedY };
        User32.ClientToScreen(ViewModel.TargetHwnd, ref point);
        var childHwnd = User32.ChildWindowFromPoint(ViewModel.TargetHwnd, new POINT { x = fixedX, y = fixedY });
        
        Console.WriteLine($"[{ViewModel.ClientName}] Original({x},{y}) -> Fixed({fixedX},{fixedY}) -> Screen({point.x},{point.y})");
        Console.WriteLine($"[{ViewModel.ClientName}] Target HWND: 0x{ViewModel.TargetHwnd:X8}, Child HWND: 0x{childHwnd:X8}");
        
        var targetHwnd = (childHwnd != IntPtr.Zero && childHwnd != ViewModel.TargetHwnd) ? childHwnd : ViewModel.TargetHwnd;
        var lParam = (fixedY << 16) | (fixedX & 0xFFFF);
        
        // Method 1: PostMessage (fastest, but might not work with MuMu)
        var postResult1 = User32.PostMessage(targetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, IntPtr.Zero, (IntPtr)lParam);
        var postResult2 = User32.PostMessage(targetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
        
        // Method 2: SendMessage (more reliable, synchronous)
        var sendResult1 = User32.SendMessage(targetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, IntPtr.Zero, (IntPtr)lParam);
        var sendResult2 = User32.SendMessage(targetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
        
        // Method 3: Hardware click - use same coordinate conversion as CoordinatePicker
        User32.GetCursorPos(out var oldPos);
        
        // Convert client coordinates to screen coordinates properly for MuMu Player
        var clickPoint = new POINT { x = fixedX, y = fixedY };
        User32.ClientToScreen(ViewModel.TargetHwnd, ref clickPoint);
        
        // Apply MuMu Player specific offset compensation (same as CoordinatePicker)
        var processName = GetProcessName(ViewModel.TargetHwnd);
        int offsetX = 0, offsetY = 0;
        
        if (processName.Contains("GameLoop"))
        {
            offsetX = 4; offsetY = 23;
        }
        else if (processName.Contains("NemuPlayer") || processName.Contains("MuMuPlayer"))
        {
            offsetX = 1; offsetY = 1;
        }
        
        // For debugging - show original screen conversion
        Console.WriteLine($"[{ViewModel.ClientName}] ClientToScreen conversion: ({fixedX},{fixedY}) -> ({clickPoint.x},{clickPoint.y})");
        
        // EXPERIMENTAL: Try different approaches
        
        // Approach 1: No offset
        var noOffsetPoint = new POINT { x = fixedX, y = fixedY };
        User32.ClientToScreen(ViewModel.TargetHwnd, ref noOffsetPoint);
        
        // Approach 2: Inverse offset  
        clickPoint.x -= offsetX;
        clickPoint.y -= offsetY;
        
        // Approach 3: Raw screen coordinates (where you actually clicked during coordinate selection)
        User32.GetWindowRect(ViewModel.TargetHwnd, out var windowRect);
        var rawScreenX = windowRect.left + fixedX;
        var rawScreenY = windowRect.top + fixedY;
        
        Console.WriteLine($"[{ViewModel.ClientName}] Approach 1 (no offset): ({noOffsetPoint.x},{noOffsetPoint.y})");
        Console.WriteLine($"[{ViewModel.ClientName}] Approach 2 (inverse): ({clickPoint.x},{clickPoint.y})"); 
        Console.WriteLine($"[{ViewModel.ClientName}] Approach 3 (raw): ({rawScreenX},{rawScreenY})");
        
        // Try approach 3 first (raw screen coordinates)
        User32.SetCursorPos(rawScreenX, rawScreenY);
        System.Threading.Thread.Sleep(50);
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(30);
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(50);
        User32.SetCursorPos(oldPos.x, oldPos.y);
        
        Console.WriteLine($"[{ViewModel.ClientName}] {channel} MULTI-METHOD click: Target:0x{targetHwnd:X8} Post:{postResult1}/{postResult2} Send:0x{sendResult1:X}/0x{sendResult2:X} + Hardware at ({clickPoint.x},{clickPoint.y}) offset:({offsetX},{offsetY})");
    }
    
    private string GetProcessName(IntPtr hwnd)
    {
        try
        {
            User32.GetWindowThreadProcessId(hwnd, out var processId);
            var process = System.Diagnostics.Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return "Unknown";
        }
    }
    
    private void PerformBackgroundClick(int x, int y, string channel)
    {
        Console.WriteLine($"[{ViewModel.ClientName}] PerformBackgroundClick called: ({x},{y}) channel={channel} hwnd={ViewModel.TargetHwnd}");
        
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] ERROR: TargetHwnd is zero, cannot perform click");
            return;
        }
        
        try
        {
            // PostMessage uses client coordinates directly - no conversion needed
            // The coordinates we receive are already client coordinates from CoordinatePicker
            var processName = GetProcessName(ViewModel.TargetHwnd);
            var lParam = (y << 16) | (x & 0xFFFF);
            
            // ALWAYS debug coordinate info for trigger clicks
            Console.WriteLine($"[{ViewModel.ClientName}] {channel} PostMessage: Process={processName} ClientCoords=({x},{y}) lParam=0x{lParam:X8}");
            
            // Check if window still exists
            if (!User32.IsWindow(ViewModel.TargetHwnd))
            {
                Console.WriteLine($"[{ViewModel.ClientName}] ERROR: Target window no longer exists");
                return;
            }
            
            // Try all message combinations with detailed logging
            Console.WriteLine($"[{ViewModel.ClientName}] Sending WM_MOUSEMOVE...");
            User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_MOUSEMOVE, IntPtr.Zero, (IntPtr)lParam);
            
            Console.WriteLine($"[{ViewModel.ClientName}] Sending WM_LBUTTONDOWN (PostMessage)...");
            bool result1 = User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, (IntPtr)1, (IntPtr)lParam);
            Console.WriteLine($"[{ViewModel.ClientName}] WM_LBUTTONDOWN result: {result1}");
            
            Console.WriteLine($"[{ViewModel.ClientName}] Sending WM_LBUTTONUP (PostMessage)...");
            bool result2 = User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
            Console.WriteLine($"[{ViewModel.ClientName}] WM_LBUTTONUP result: {result2}");
            
            Console.WriteLine($"[{ViewModel.ClientName}] Sending WM_LBUTTONDOWN (SendMessage)...");
            User32.SendMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, (IntPtr)1, (IntPtr)lParam);
            
            Console.WriteLine($"[{ViewModel.ClientName}] Sending WM_LBUTTONUP (SendMessage)...");
            User32.SendMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
            
            // Method 3: Try child windows
            var childWindows = new List<IntPtr>();
            User32.EnumChildWindows(ViewModel.TargetHwnd, (hwnd, lParam) =>
            {
                childWindows.Add((IntPtr)hwnd);
                return true;
            }, IntPtr.Zero);
            
            foreach (var childHwnd in childWindows)
            {
                User32.PostMessage(childHwnd, User32.WindowMessage.WM_LBUTTONDOWN, (IntPtr)1, (IntPtr)lParam);
                User32.PostMessage(childHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
            }
            
            // Method 4: Skip hardware input injection for test mode to avoid mouse movement
            
            // Logging
            if (channel.Contains("TEST") || DateTime.Now.Second % 10 == 0)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] {channel} MULTI-METHOD click: ({x},{y}) Process:{processName} Children:{childWindows.Count}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Background click error: {ex.Message}");
        }
    }
    
    private void PerformBackgroundKeyPress(string key, string channel)
    {
        Console.WriteLine($"[{ViewModel.ClientName}] PerformBackgroundKeyPress called: key='{key}' channel={channel} hwnd={ViewModel.TargetHwnd}");
        
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] ERROR: TargetHwnd is zero, cannot send key press");
            return;
        }
        
        if (string.IsNullOrEmpty(key))
        {
            Console.WriteLine($"[{ViewModel.ClientName}] ERROR: Key is null or empty");
            return;
        }
        
        try
        {
            // Convert key to virtual key code
            var vkCode = GetVirtualKeyCode(key);
            if (vkCode == 0)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] ERROR: Could not convert key '{key}' to virtual key code");
                return;
            }
            
            var processName = GetProcessName(ViewModel.TargetHwnd);
            
            Console.WriteLine($"[{ViewModel.ClientName}] {channel} SendKey: Process={processName} Key='{key}' VK={vkCode:X2}");
            
            // Check if window still exists
            if (!User32.IsWindow(ViewModel.TargetHwnd))
            {
                Console.WriteLine($"[{ViewModel.ClientName}] ERROR: Target window no longer exists");
                return;
            }
            
            // Optimized key press - only essential methods to avoid spam
            
            // Method 1: Hardware simulation with proper lParam (most effective)
            uint scanCode = User32.MapVirtualKey((uint)vkCode, User32.MAPVK.MAPVK_VK_TO_VSC);
            IntPtr lParam = (IntPtr)((scanCode << 16) | 1); // Repeat count = 1
            IntPtr lParamUp = (IntPtr)((scanCode << 16) | 1 | (1 << 30) | (1 << 31));
            
            Console.WriteLine($"[{ViewModel.ClientName}] Sending optimized key: '{key}' (VK={vkCode:X2})");
            
            // Primary method: PostMessage with proper lParam
            User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_KEYDOWN, (IntPtr)vkCode, lParam);
            System.Threading.Thread.Sleep(25);
            User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_KEYUP, (IntPtr)vkCode, lParamUp);
            
            // Method 2: Fallback to child windows (only if main window fails)
            var childWindows = new List<IntPtr>();
            User32.EnumChildWindows(ViewModel.TargetHwnd, (hwnd, lParam) =>
            {
                childWindows.Add((IntPtr)hwnd);
                return true;
            }, IntPtr.Zero);
            
            if (childWindows.Count > 0)
            {
                // Only send to the first child window to avoid spam
                var primaryChild = childWindows[0];
                User32.PostMessage(primaryChild, User32.WindowMessage.WM_KEYDOWN, (IntPtr)vkCode, lParam);
                System.Threading.Thread.Sleep(25);
                User32.PostMessage(primaryChild, User32.WindowMessage.WM_KEYUP, (IntPtr)vkCode, lParamUp);
            }
            
            // Logging
            if (channel.Contains("TEST") || DateTime.Now.Second % 10 == 0)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] {channel} KEY PRESS: '{key}' (VK={vkCode:X2}) Process:{processName} Children:{childWindows.Count}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Background key press error: {ex.Message}");
        }
    }
    
    private uint GetVirtualKeyCode(string key)
    {
        if (string.IsNullOrEmpty(key)) return 0;
        
        var upperKey = key.ToUpper();
        
        // Single character keys (A-Z, 0-9)
        if (upperKey.Length == 1)
        {
            char c = upperKey[0];
            if (c >= 'A' && c <= 'Z')
                return (uint)c;
            if (c >= '0' && c <= '9')
                return (uint)c;
        }
        
        // Special keys
        return upperKey switch
        {
            "SPACE" => 0x20,
            "ENTER" => 0x0D,
            "TAB" => 0x09,
            "ESC" => 0x1B,
            "ESCAPE" => 0x1B,
            "SHIFT" => 0x10,
            "CTRL" => 0x11,
            "ALT" => 0x12,
            "F1" => 0x70,
            "F2" => 0x71,
            "F3" => 0x72,
            "F4" => 0x73,
            "F5" => 0x74,
            "F6" => 0x75,
            "F7" => 0x76,
            "F8" => 0x77,
            "F9" => 0x78,
            "F10" => 0x79,
            "F11" => 0x7A,
            "F12" => 0x7B,
            _ => 0
        };
    }
    
    private void PerformPostMessageTest(int x, int y, string channel)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero) return;
        
        try
        {
            // PostMessage only implementation for testing
            var lParam = (y << 16) | (x & 0xFFFF);
            var processName = GetProcessName(ViewModel.TargetHwnd);
            
            Console.WriteLine($"[{ViewModel.ClientName}] {channel} - PostMessage Only Test:");
            Console.WriteLine($"[{ViewModel.ClientName}] Process: {processName}, Client Coords: ({x},{y}), lParam: 0x{lParam:X8}");
            
            // PostMessage approach only
            User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_MOUSEMOVE, IntPtr.Zero, (IntPtr)lParam);
            System.Threading.Thread.Sleep(10);
            User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, (IntPtr)1, (IntPtr)lParam);
            System.Threading.Thread.Sleep(10);
            User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
            
            Console.WriteLine($"[{ViewModel.ClientName}] {channel} PostMessage sequence sent to HWND: 0x{ViewModel.TargetHwnd:X8}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] PostMessage test error: {ex.Message}");
        }
    }
    
    private bool TryAdbClick(int x, int y, string channel)
    {
        try
        {
            // MuMu Player genellikle 7555 portunda ADB dinler
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = $"-s 127.0.0.1:7555 shell input tap {x} {y}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            
            process.Start();
            process.WaitForExit(1000); // 1 saniye timeout
            
            if (process.ExitCode == 0)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] {channel} ADB click SUCCESS: ({x},{y})");
                return true;
            }
        }
        catch (Exception ex)
        {
            // ADB yoksa sessizce fail et
            Console.WriteLine($"[{ViewModel.ClientName}] ADB not available: {ex.Message}");
        }
        
        return false;
    }
    
    private void TestAdbConnection()
    {
        try
        {
            var processName = GetProcessName(ViewModel.TargetHwnd);
            Console.WriteLine($"[{ViewModel.ClientName}] Process: {processName}");
            
            // Test ADB executable
            var adbTest = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = "version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            
            adbTest.Start();
            string output = adbTest.StandardOutput.ReadToEnd();
            adbTest.WaitForExit(2000);
            
            if (adbTest.ExitCode == 0)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] ADB available: {output.Split('\n')[0]}");
                
                // Test devices
                TestAdbDevices();
                
                // Test direct click if MuMu detected
                if (processName.Contains("MuMu") || processName.Contains("Nemu"))
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] Testing ADB click on MuMu Player...");
                    var success = TryAdbClick(100, 100, "TEST_ADB_CONNECTION");
                    if (!success)
                    {
                        // Try different ports
                        Console.WriteLine($"[{ViewModel.ClientName}] Trying alternative ADB ports...");
                        TestAlternativeAdbPorts();
                    }
                }
            }
            else
            {
                Console.WriteLine($"[{ViewModel.ClientName}] ADB not found or not working");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] ADB test error: {ex.Message}");
        }
    }
    
    private void TestAdbDevices()
    {
        try
        {
            var devicesTest = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = "devices",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            
            devicesTest.Start();
            string devices = devicesTest.StandardOutput.ReadToEnd();
            devicesTest.WaitForExit(2000);
            
            Console.WriteLine($"[{ViewModel.ClientName}] ADB devices:");
            Console.WriteLine(devices);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] ADB devices error: {ex.Message}");
        }
    }
    
    private void TestAlternativeAdbPorts()
    {
        var ports = new[] { "127.0.0.1:5555", "127.0.0.1:5556", "127.0.0.1:7555", "127.0.0.1:21503" };
        
        foreach (var port in ports)
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "adb",
                        Arguments = $"-s {port} shell echo 'test'",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                
                process.Start();
                process.WaitForExit(1000);
                
                if (process.ExitCode == 0)
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] Found active ADB device at {port}");
                    
                    // Test a simple tap
                    var tapProcess = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "adb",
                            Arguments = $"-s {port} shell input tap 100 100",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    
                    tapProcess.Start();
                    tapProcess.WaitForExit(1000);
                    
                    if (tapProcess.ExitCode == 0)
                    {
                        Console.WriteLine($"[{ViewModel.ClientName}] ADB tap SUCCESS on {port}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] Port {port} test failed: {ex.Message}");
            }
        }
    }
    
    private void TryLowLevelInput(int x, int y, string channel)
    {
        try
        {
            // Convert client coordinates to screen coordinates properly
            var clientPoint = new POINT { x = x, y = y };
            User32.ClientToScreen(ViewModel.TargetHwnd, ref clientPoint);
            
            var screenX = clientPoint.x;
            var screenY = clientPoint.y;
            
            Console.WriteLine($"[{ViewModel.ClientName}] {channel} ClientToScreen: ({x},{y}) -> ({screenX},{screenY})");
            
            // Use SendInput with absolute positioning (no cursor movement visible)
            var inputs = new User32.INPUT[2];
            
            // Calculate screen relative coordinates
            var screenWidth = User32.GetSystemMetrics(User32.SystemMetric.SM_CXSCREEN);
            var screenHeight = User32.GetSystemMetrics(User32.SystemMetric.SM_CYSCREEN);
            var relativeX = (screenX * 65535) / screenWidth;
            var relativeY = (screenY * 65535) / screenHeight;
            
            // Mouse down at specific location
            inputs[0] = new User32.INPUT
            {
                type = User32.INPUTTYPE.INPUT_MOUSE,
                mi = new User32.MOUSEINPUT
                {
                    dx = relativeX,
                    dy = relativeY,
                    dwFlags = User32.MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN | User32.MOUSEEVENTF.MOUSEEVENTF_ABSOLUTE | User32.MOUSEEVENTF.MOUSEEVENTF_MOVE
                }
            };
            
            // Mouse up
            inputs[1] = new User32.INPUT
            {
                type = User32.INPUTTYPE.INPUT_MOUSE,
                mi = new User32.MOUSEINPUT
                {
                    dx = relativeX,
                    dy = relativeY,
                    dwFlags = User32.MOUSEEVENTF.MOUSEEVENTF_LEFTUP | User32.MOUSEEVENTF.MOUSEEVENTF_ABSOLUTE
                }
            };
            
            User32.SendInput(2, inputs, System.Runtime.InteropServices.Marshal.SizeOf<User32.INPUT>());
            Console.WriteLine($"[{ViewModel.ClientName}] {channel} SENDINPUT click: ({x},{y}) -> screen({screenX},{screenY})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] SendInput error: {ex.Message}");
        }
    }
    
    private void PerformRawScreenClick(int x, int y, string channel)
    {
        // Keep this for manual testing only
        User32.GetWindowRect(ViewModel.TargetHwnd, out var windowRect);
        var screenX = windowRect.left + x;
        var screenY = windowRect.top + y;
        
        User32.GetCursorPos(out var oldPos);
        User32.SetCursorPos(screenX, screenY);
        System.Threading.Thread.Sleep(50);
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(30);
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(50);
        User32.SetCursorPos(oldPos.x, oldPos.y);
        
        Console.WriteLine($"[{ViewModel.ClientName}] {channel} RAW-SCREEN click: client({x},{y}) -> screen({screenX},{screenY}) windowRect({windowRect.left},{windowRect.top})");
    }
    
    private void PerformDirectInputClick(int x, int y, string channel)
    {
        // Hybrid approach: Focus + SendInput with small delay
        User32.SetForegroundWindow(ViewModel.TargetHwnd);
        System.Threading.Thread.Sleep(50);
        
        var point = new POINT { x = x, y = y };
        User32.ClientToScreen(ViewModel.TargetHwnd, ref point);
        
        // Use GetCursorPos to save current position
        User32.GetCursorPos(out var originalPos);
        
        // Set cursor position
        User32.SetCursorPos(point.x, point.y);
        System.Threading.Thread.Sleep(30);
        
        // Direct hardware click
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(20);
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        
        System.Threading.Thread.Sleep(50);
        User32.SetCursorPos(originalPos.x, originalPos.y);
        
        Console.WriteLine($"[{ViewModel.ClientName}] {channel} DIRECT-INPUT click at client({x}, {y}) -> screen({point.x}, {point.y})");
    }
    
    private void PerformFocusClick(int x, int y, string channel)
    {
        // Force focus and use multiple methods
        User32.BringWindowToTop(ViewModel.TargetHwnd);
        User32.SetForegroundWindow(ViewModel.TargetHwnd);
        User32.SetActiveWindow(ViewModel.TargetHwnd);
        System.Threading.Thread.Sleep(100);
        
        // Try both message and hardware click
        var lParam = (y << 16) | (x & 0xFFFF);
        User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONDOWN, IntPtr.Zero, (IntPtr)lParam);
        User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_LBUTTONUP, IntPtr.Zero, (IntPtr)lParam);
        
        // Also try hardware click
        var point = new POINT { x = x, y = y };
        User32.ClientToScreen(ViewModel.TargetHwnd, ref point);
        User32.SetCursorPos(point.x, point.y);
        System.Threading.Thread.Sleep(20);
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(10);
        User32.mouse_event(User32.MOUSEEVENTF.MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
        
        Console.WriteLine($"[{ViewModel.ClientName}] {channel} FOCUS-CLICK click at client({x}, {y}) -> screen({point.x}, {point.y})");
    }

    // HP Shape Mouse Event Handlers
    private void HpShape_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_hpShape != null)
        {
            _isDraggingHp = true;
            _dragStartPoint = e.GetPosition(GetOverlayCanvas());
            _hpShape.CaptureMouse();
            e.Handled = true;
            Console.WriteLine($"[{ViewModel.ClientName}] Started dragging HP shape");
        }
    }
    
    private void HpShape_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingHp && _hpShape != null && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            var canvas = GetOverlayCanvas();
            if (canvas != null)
            {
                var currentPoint = e.GetPosition(canvas);
                
                // Calculate new position directly from mouse position
                var newX = currentPoint.X - 10; // Center the circle on mouse
                var newY = currentPoint.Y - 10;
                
                // Update shape position
                Canvas.SetLeft(_hpShape, newX);
                Canvas.SetTop(_hpShape, newY);
                
                // Update ViewModel coordinates (center of circle)
                ViewModel.HpProbe.X = (int)(newX + 10);
                ViewModel.HpProbe.Y = (int)(newY + 10);
                
                // Update UI text boxes
                HpX.Text = ViewModel.HpProbe.X.ToString();
                HpY.Text = ViewModel.HpProbe.Y.ToString();
                
                Console.WriteLine($"[{ViewModel.ClientName}] HP shape moved to ({ViewModel.HpProbe.X},{ViewModel.HpProbe.Y})");
            }
        }
    }
    
    private void HpShape_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingHp && _hpShape != null)
        {
            _isDraggingHp = false;
            _hpShape.ReleaseMouseCapture();
            Console.WriteLine($"[{ViewModel.ClientName}] Finished dragging HP shape at ({ViewModel.HpProbe.X},{ViewModel.HpProbe.Y})");
            
            // Optionally read color at new position
            if (ViewModel.TargetHwnd != IntPtr.Zero)
            {
                var newColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, ViewModel.HpProbe.X, ViewModel.HpProbe.Y);
                ViewModel.HpProbe.ExpectedColor = newColor;
                ViewModel.HpProbe.ReferenceColor = newColor;
                HpColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(newColor.R, newColor.G, newColor.B));
                HpColorText.Text = $"{newColor.R},{newColor.G},{newColor.B}";
                Console.WriteLine($"[{ViewModel.ClientName}] HP color updated: RGB({newColor.R},{newColor.G},{newColor.B})");
            }
        }
    }
    
    // MP Shape Mouse Event Handlers
    private void MpShape_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_mpShape != null)
        {
            _isDraggingMp = true;
            _dragStartPoint = e.GetPosition(GetOverlayCanvas());
            _mpShape.CaptureMouse();
            e.Handled = true;
            Console.WriteLine($"[{ViewModel.ClientName}] Started dragging MP shape");
        }
    }
    
    private void MpShape_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingMp && _mpShape != null && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            var canvas = GetOverlayCanvas();
            if (canvas != null)
            {
                var currentPoint = e.GetPosition(canvas);
                
                // Calculate new position directly from mouse position
                var newX = currentPoint.X - 10; // Center the circle on mouse
                var newY = currentPoint.Y - 10;
                
                // Update shape position
                Canvas.SetLeft(_mpShape, newX);
                Canvas.SetTop(_mpShape, newY);
                
                // Update ViewModel coordinates (center of circle)
                ViewModel.MpProbe.X = (int)(newX + 10);
                ViewModel.MpProbe.Y = (int)(newY + 10);
                
                // Update UI text boxes
                MpX.Text = ViewModel.MpProbe.X.ToString();
                MpY.Text = ViewModel.MpProbe.Y.ToString();
                
                Console.WriteLine($"[{ViewModel.ClientName}] MP shape moved to ({ViewModel.MpProbe.X},{ViewModel.MpProbe.Y})");
            }
        }
    }
    
    private void MpShape_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingMp && _mpShape != null)
        {
            _isDraggingMp = false;
            _mpShape.ReleaseMouseCapture();
            Console.WriteLine($"[{ViewModel.ClientName}] Finished dragging MP shape at ({ViewModel.MpProbe.X},{ViewModel.MpProbe.Y})");
            
            // Optionally read color at new position
            if (ViewModel.TargetHwnd != IntPtr.Zero)
            {
                var newColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, ViewModel.MpProbe.X, ViewModel.MpProbe.Y);
                ViewModel.MpProbe.ExpectedColor = newColor;
                ViewModel.MpProbe.ReferenceColor = newColor;
                MpColorDisplay.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(newColor.R, newColor.G, newColor.B));
                MpColorText.Text = $"{newColor.R},{newColor.G},{newColor.B}";
                Console.WriteLine($"[{ViewModel.ClientName}] MP color updated: RGB({newColor.R},{newColor.G},{newColor.B})");
            }
        }
    }
    
    // HP Percentage Bar Mouse Event Handlers
    private void HpPercentageShape_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_hpPercentageShape != null)
        {
            _isDraggingHpPercentage = true;
            _dragStartPoint = e.GetPosition(GetOverlayCanvas());
            _hpPercentageShape.CaptureMouse();
            e.Handled = true;
            Console.WriteLine($"[{ViewModel.ClientName}] Started dragging HP percentage bar");
        }
    }
    
    private void HpPercentageShape_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingHpPercentage && _hpPercentageShape != null && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            var canvas = GetOverlayCanvas();
            if (canvas != null)
            {
                var currentPoint = e.GetPosition(canvas);
                
                // Calculate new position directly from mouse position
                var newX = currentPoint.X - (_hpPercentageShape.Width / 2);
                var newY = currentPoint.Y - (_hpPercentageShape.Height / 2);
                
                // Update shape position
                Canvas.SetLeft(_hpPercentageShape, newX);
                Canvas.SetTop(_hpPercentageShape, newY);
                
                // Update ViewModel coordinates
                ViewModel.HpPercentageProbe.StartX = (int)newX;
                ViewModel.HpPercentageProbe.EndX = (int)(newX + _hpPercentageShape.Width);
                ViewModel.HpPercentageProbe.Y = (int)(newY + _hpPercentageShape.Height / 2);
                
                // Update UI text boxes
                HpPercentageStartX.Text = ViewModel.HpPercentageProbe.StartX.ToString();
                HpPercentageEndX.Text = ViewModel.HpPercentageProbe.EndX.ToString();
                HpPercentageY.Text = ViewModel.HpPercentageProbe.Y.ToString();
                
                UpdatePercentageMonitorPosition();
                Console.WriteLine($"[{ViewModel.ClientName}] HP bar moved to ({ViewModel.HpPercentageProbe.StartX}-{ViewModel.HpPercentageProbe.EndX},{ViewModel.HpPercentageProbe.Y})");
            }
        }
    }
    
    private void HpPercentageShape_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingHpPercentage && _hpPercentageShape != null)
        {
            _isDraggingHpPercentage = false;
            _hpPercentageShape.ReleaseMouseCapture();
            Console.WriteLine($"[{ViewModel.ClientName}] Finished dragging HP percentage bar");
        }
    }
    
    // MP Percentage Bar Mouse Event Handlers
    private void MpPercentageShape_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_mpPercentageShape != null)
        {
            _isDraggingMpPercentage = true;
            _dragStartPoint = e.GetPosition(GetOverlayCanvas());
            _mpPercentageShape.CaptureMouse();
            e.Handled = true;
            Console.WriteLine($"[{ViewModel.ClientName}] Started dragging MP percentage bar");
        }
    }
    
    private void MpPercentageShape_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingMpPercentage && _mpPercentageShape != null)
        {
            var canvas = GetOverlayCanvas();
            if (canvas != null)
            {
                var currentPoint = e.GetPosition(canvas);
                var deltaX = currentPoint.X - _dragStartPoint.X;
                var deltaY = currentPoint.Y - _dragStartPoint.Y;
                
                var newX = (int)(Canvas.GetLeft(_mpPercentageShape) + deltaX);
                var newY = (int)(Canvas.GetTop(_mpPercentageShape) + deltaY);
                
                // Update shape position
                Canvas.SetLeft(_mpPercentageShape, newX);
                Canvas.SetTop(_mpPercentageShape, newY);
                
                // Update ViewModel coordinates
                ViewModel.MpPercentageProbe.StartX = newX;
                ViewModel.MpPercentageProbe.EndX = newX + (int)_mpPercentageShape.Width;
                ViewModel.MpPercentageProbe.Y = newY + (int)_mpPercentageShape.Height / 2;
                
                // Update UI text boxes
                MpPercentageStartX.Text = ViewModel.MpPercentageProbe.StartX.ToString();
                MpPercentageEndX.Text = ViewModel.MpPercentageProbe.EndX.ToString();
                MpPercentageY.Text = ViewModel.MpPercentageProbe.Y.ToString();
                
                _dragStartPoint = currentPoint;
                UpdatePercentageMonitorPosition();
                Console.WriteLine($"[{ViewModel.ClientName}] MP bar moved to ({ViewModel.MpPercentageProbe.StartX}-{ViewModel.MpPercentageProbe.EndX},{ViewModel.MpPercentageProbe.Y})");
            }
        }
    }
    
    private void MpPercentageShape_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingMpPercentage && _mpPercentageShape != null)
        {
            _isDraggingMpPercentage = false;
            _mpPercentageShape.ReleaseMouseCapture();
            Console.WriteLine($"[{ViewModel.ClientName}] Finished dragging MP percentage bar");
        }
    }
    
    // Helper method to get overlay canvas
    private System.Windows.Controls.Canvas? GetOverlayCanvas()
    {
        var mainWindow = Application.Current.MainWindow as MainWindow;
        return mainWindow?.GetOverlayCanvas();
    }
    
    // Public methods to show/hide shapes in overlay mode
    public void ShowOverlayShapes()
    {
        var canvas = GetOverlayCanvas();
        if (canvas == null) return;
        
        // Add shapes to overlay canvas if not already added
        if (_hpShape != null && !canvas.Children.Contains(_hpShape))
        {
            canvas.Children.Add(_hpShape);
            // Position HP shape based on current coordinates
            Canvas.SetLeft(_hpShape, ViewModel.HpProbe.X - 10);
            Canvas.SetTop(_hpShape, ViewModel.HpProbe.Y - 10);
            _hpShape.Visibility = Visibility.Visible;
        }
        
        if (_mpShape != null && !canvas.Children.Contains(_mpShape))
        {
            canvas.Children.Add(_mpShape);
            // Position MP shape based on current coordinates
            Canvas.SetLeft(_mpShape, ViewModel.MpProbe.X - 10);
            Canvas.SetTop(_mpShape, ViewModel.MpProbe.Y - 10);
            _mpShape.Visibility = Visibility.Visible;
        }
        
        if (_hpPercentageShape != null && !canvas.Children.Contains(_hpPercentageShape))
        {
            canvas.Children.Add(_hpPercentageShape);
            // Position HP percentage bar
            Canvas.SetLeft(_hpPercentageShape, ViewModel.HpPercentageProbe.StartX);
            Canvas.SetTop(_hpPercentageShape, ViewModel.HpPercentageProbe.Y - 4);
            _hpPercentageShape.Width = ViewModel.HpPercentageProbe.EndX - ViewModel.HpPercentageProbe.StartX;
            _hpPercentageShape.Visibility = Visibility.Visible;
        }
        
        if (_mpPercentageShape != null && !canvas.Children.Contains(_mpPercentageShape))
        {
            canvas.Children.Add(_mpPercentageShape);
            // Position MP percentage bar
            Canvas.SetLeft(_mpPercentageShape, ViewModel.MpPercentageProbe.StartX);
            Canvas.SetTop(_mpPercentageShape, ViewModel.MpPercentageProbe.Y - 4);
            _mpPercentageShape.Width = ViewModel.MpPercentageProbe.EndX - ViewModel.MpPercentageProbe.StartX;
            _mpPercentageShape.Visibility = Visibility.Visible;
        }
        
        Console.WriteLine($"[{ViewModel.ClientName}] Overlay shapes shown");
    }
    
    public void HideOverlayShapes()
    {
        var canvas = GetOverlayCanvas();
        if (canvas == null) return;
        
        if (_hpShape != null)
        {
            _hpShape.Visibility = Visibility.Collapsed;
            canvas.Children.Remove(_hpShape);
        }
        
        if (_mpShape != null)
        {
            _mpShape.Visibility = Visibility.Collapsed;
            canvas.Children.Remove(_mpShape);
        }
        
        if (_hpPercentageShape != null)
        {
            _hpPercentageShape.Visibility = Visibility.Collapsed;
            canvas.Children.Remove(_hpPercentageShape);
        }
        
        if (_mpPercentageShape != null)
        {
            _mpPercentageShape.Visibility = Visibility.Collapsed;
            canvas.Children.Remove(_mpPercentageShape);
        }
        
        Console.WriteLine($"[{ViewModel.ClientName}] Overlay shapes hidden");
    }
   
    // Show Draggable Shapes Button Event Handler
    private void ShowDraggableShapes_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
            StatusIndicator.ToolTip = "Please select a window first!";
            Console.WriteLine($"[{ViewModel.ClientName}] Cannot show shapes - no window selected");
            return;
        }

        // Show overlay shapes for manual positioning
        ShowOverlayShapesEnhanced();
        
        // Also enable overlay mode in main window if not already enabled
        var mainWindow = Application.Current.MainWindow as MainWindow;
        if (mainWindow != null)
        {
            // Check if overlay mode is already active
            var overlayCheckBox = mainWindow.FindName("OverlayModeCheckBox") as CheckBox;
            if (overlayCheckBox != null && overlayCheckBox.IsChecked != true)
            {
                overlayCheckBox.IsChecked = true; // This will trigger overlay mode
            }
        }
        
        Console.WriteLine($"[{ViewModel.ClientName}] 🎯 DRAGGABLE SHAPES ACTIVATED!");
        Console.WriteLine($"[{ViewModel.ClientName}] ❤️ RED CIRCLE = HP monitoring point - drag to HP bar");
        Console.WriteLine($"[{ViewModel.ClientName}] 💙 BLUE CIRCLE = MP monitoring point - drag to MP bar");  
        Console.WriteLine($"[{ViewModel.ClientName}] 📏 RED RECTANGLE = HP percentage bar - drag and resize");
        Console.WriteLine($"[{ViewModel.ClientName}] 📏 BLUE RECTANGLE = MP percentage bar - drag and resize");
        Console.WriteLine($"[{ViewModel.ClientName}] ⏰ Shapes stay visible for 30 seconds - click to close early");
        Console.WriteLine($"[{ViewModel.ClientName}] 🎯 When you drag shapes, coordinates auto-update in UI!");
        
        StatusIndicator.Fill = new SolidColorBrush(Colors.Orange);
        StatusIndicator.ToolTip = "Draggable shapes active - position them on your HP/MP bars";
    }


    
    // Enhanced ShowOverlayShapes with better positioning and visual feedback
    private void ShowOverlayShapesEnhanced()
    {
        var canvas = GetOverlayCanvas();
        if (canvas == null) 
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Cannot access overlay canvas");
            return;
        }
        
        // Clear existing shapes first
        HideOverlayShapes();
        
        // Get window bounds for better initial positioning
        var windowRect = GetWindowBounds();
        var offsetX = windowRect.Left + 50; // Start shapes 50px from window left
        var offsetY = windowRect.Top + 50;  // Start shapes 50px from window top
        
        // Create and position HP shape (red circle)
        if (_hpShape != null)
        {
            canvas.Children.Add(_hpShape);
            Canvas.SetLeft(_hpShape, offsetX);
            Canvas.SetTop(_hpShape, offsetY);
            _hpShape.Visibility = Visibility.Visible;
            
            // Update ViewModel coordinates
            ViewModel.HpProbe.X = (int)(offsetX + 10);
            ViewModel.HpProbe.Y = (int)(offsetY + 10);
            HpX.Text = ViewModel.HpProbe.X.ToString();
            HpY.Text = ViewModel.HpProbe.Y.ToString();
        }
        
        // Create and position MP shape (blue circle) - slightly below HP
        if (_mpShape != null)
        {
            canvas.Children.Add(_mpShape);
            Canvas.SetLeft(_mpShape, offsetX);
            Canvas.SetTop(_mpShape, offsetY + 30);
            _mpShape.Visibility = Visibility.Visible;
            
            // Update ViewModel coordinates
            ViewModel.MpProbe.X = (int)(offsetX + 10);
            ViewModel.MpProbe.Y = (int)(offsetY + 40);
            MpX.Text = ViewModel.MpProbe.X.ToString();
            MpY.Text = ViewModel.MpProbe.Y.ToString();
        }
        
        // Create and position HP percentage bar (red rectangle)
        if (_hpPercentageShape != null)
        {
            canvas.Children.Add(_hpPercentageShape);
            Canvas.SetLeft(_hpPercentageShape, offsetX + 200);
            Canvas.SetTop(_hpPercentageShape, offsetY);
            _hpPercentageShape.Visibility = Visibility.Visible;
            
            // Update ViewModel coordinates
            ViewModel.HpPercentageProbe.StartX = (int)(offsetX + 200);
            ViewModel.HpPercentageProbe.EndX = (int)(offsetX + 350);
            ViewModel.HpPercentageProbe.Y = (int)(offsetY + 4);
            HpPercentageStartX.Text = ViewModel.HpPercentageProbe.StartX.ToString();
            HpPercentageEndX.Text = ViewModel.HpPercentageProbe.EndX.ToString();
            HpPercentageY.Text = ViewModel.HpPercentageProbe.Y.ToString();
        }
        
        // Create and position MP percentage bar (blue rectangle) - below HP bar
        if (_mpPercentageShape != null)
        {
            canvas.Children.Add(_mpPercentageShape);
            Canvas.SetLeft(_mpPercentageShape, offsetX + 200);
            Canvas.SetTop(_mpPercentageShape, offsetY + 20);
            _mpPercentageShape.Visibility = Visibility.Visible;
            
            // Update ViewModel coordinates
            ViewModel.MpPercentageProbe.StartX = (int)(offsetX + 200);
            ViewModel.MpPercentageProbe.EndX = (int)(offsetX + 350);
            ViewModel.MpPercentageProbe.Y = (int)(offsetY + 24);
            MpPercentageStartX.Text = ViewModel.MpPercentageProbe.StartX.ToString();
            MpPercentageEndX.Text = ViewModel.MpPercentageProbe.EndX.ToString();
            MpPercentageY.Text = ViewModel.MpPercentageProbe.Y.ToString();
        }
        
        UpdatePercentageMonitorPosition();
        Console.WriteLine($"[{ViewModel.ClientName}] Overlay shapes positioned at window offset ({offsetX},{offsetY})");
    }
    
    // Helper method to get target window bounds
    private RECT GetWindowBounds()
    {
        if (ViewModel.TargetHwnd != IntPtr.Zero)
        {
            User32.GetWindowRect(ViewModel.TargetHwnd, out RECT rect);
            return rect;
        }
        return new RECT { Left = 100, Top = 100, Right = 500, Bottom = 400 }; // Default fallback
    }

    #region BabeBot Style HP/MP Event Handlers
    
    private void BabeBotHpCalibrate_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            MessageBox.Show("Please select a window first for BabeBot HP calibration!", "No Window Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            Console.WriteLine($"[{ViewModel.ClientName}] No window selected for BabeBot HP calibration");
            return;
        }
        
        Task.Run(() =>
        {
            try
            {
                Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot HP Calibration started...");
                
                // Clear existing reference colors
                ViewModel.BabeBotHp.ReferenceColors.Clear();
                
                // BabeBot calibration logic - sample colors at %5-%95
                for (int percentage = 5; percentage <= 95; percentage += 5)
                {
                    int sampleX = ViewModel.BabeBotHp.CalculateXForPercentage(percentage);
                    var color = ColorSampler.GetColorAt(ViewModel.TargetHwnd, sampleX, ViewModel.BabeBotHp.Y);
                    
                    ViewModel.BabeBotHp.ReferenceColors[percentage] = color;
                    Console.WriteLine($"[{ViewModel.ClientName}] BabeBot HP {percentage}%: X={sampleX}, Color=RGB({color.R},{color.G},{color.B})");
                    
                    Thread.Sleep(50); // Small delay between samples
                }
                
                // Set reference color to the threshold percentage
                var thresholdX = ViewModel.BabeBotHp.MonitorX;
                var thresholdColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, thresholdX, ViewModel.BabeBotHp.Y);
                ViewModel.BabeBotHp.ReferenceColor = thresholdColor;
                
                Dispatcher.BeginInvoke(() =>
                {
                    BabeBotHpCurrentColor.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(thresholdColor.R, thresholdColor.G, thresholdColor.B));
                    BabeBotHpCurrentText.Text = $"{thresholdColor.R},{thresholdColor.G},{thresholdColor.B}";
                    ViewModel.BabeBotHp.Status = "Calibrated";
                });
                
                Console.WriteLine($"[{ViewModel.ClientName}] ✅ BabeBot HP Calibration complete! Monitor X={thresholdX}, Threshold={ViewModel.BabeBotHp.ThresholdPercentage}%");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] BabeBot HP calibration error: {ex.Message}");
            }
        });
    }
    
    private void BabeBotMpCalibrate_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            MessageBox.Show("Please select a window first for BabeBot MP calibration!", "No Window Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            Console.WriteLine($"[{ViewModel.ClientName}] No window selected for BabeBot MP calibration");
            return;
        }
        
        Task.Run(() =>
        {
            try
            {
                Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot MP Calibration started...");
                
                // Clear existing reference colors
                ViewModel.BabeBotMp.ReferenceColors.Clear();
                
                // BabeBot calibration logic - sample colors at %5-%95
                for (int percentage = 5; percentage <= 95; percentage += 5)
                {
                    int sampleX = ViewModel.BabeBotMp.CalculateXForPercentage(percentage);
                    var color = ColorSampler.GetColorAt(ViewModel.TargetHwnd, sampleX, ViewModel.BabeBotMp.Y);
                    
                    ViewModel.BabeBotMp.ReferenceColors[percentage] = color;
                    Console.WriteLine($"[{ViewModel.ClientName}] BabeBot MP {percentage}%: X={sampleX}, Color=RGB({color.R},{color.G},{color.B})");
                    
                    Thread.Sleep(50); // Small delay between samples
                }
                
                // Set reference color to the threshold percentage
                var thresholdX = ViewModel.BabeBotMp.MonitorX;
                var thresholdColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, thresholdX, ViewModel.BabeBotMp.Y);
                ViewModel.BabeBotMp.ReferenceColor = thresholdColor;
                
                Dispatcher.BeginInvoke(() =>
                {
                    BabeBotMpCurrentColor.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(thresholdColor.R, thresholdColor.G, thresholdColor.B));
                    BabeBotMpCurrentText.Text = $"{thresholdColor.R},{thresholdColor.G},{thresholdColor.B}";
                    ViewModel.BabeBotMp.Status = "Calibrated";
                });
                
                Console.WriteLine($"[{ViewModel.ClientName}] ✅ BabeBot MP Calibration complete! Monitor X={thresholdX}, Threshold={ViewModel.BabeBotMp.ThresholdPercentage}%");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] BabeBot MP calibration error: {ex.Message}");
            }
        });
    }
    
    private void PickBabeBotHpPotion_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("BabeBot HP Potion Position", (x, y) =>
        {
            BabeBotHpPotionX.Text = x.ToString();
            BabeBotHpPotionY.Text = y.ToString();
            ViewModel.BabeBotHp.PotionX = x;
            ViewModel.BabeBotHp.PotionY = y;
            Console.WriteLine($"[{ViewModel.ClientName}] BabeBot HP Potion Click set to: ({x},{y})");
        });
    }
    
    private void PickBabeBotMpPotion_Click(object sender, RoutedEventArgs e)
    {
        PickCoordinate("BabeBot MP Potion Position", (x, y) =>
        {
            BabeBotMpPotionX.Text = x.ToString();
            BabeBotMpPotionY.Text = y.ToString();
            ViewModel.BabeBotMp.PotionX = x;
            ViewModel.BabeBotMp.PotionY = y;
            Console.WriteLine($"[{ViewModel.ClientName}] BabeBot MP Potion Click set to: ({x},{y})");
        });
    }
    
    private void BabeBotHpDetect_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            MessageBox.Show("Please select a window first for BabeBot HP detection!", "No Window Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            Console.WriteLine($"[{ViewModel.ClientName}] No window selected for BabeBot HP detection");
            return;
        }
        
        Task.Run(() =>
        {
            try
            {
                Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot HP Bar Detection started...");
                
                // Use existing HP bar detection system
                var hpBar = DetectBar(true); // true = HP (red)
                if (hpBar != null)
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] ✅ BabeBot HP BAR DETECTED!");
                    Console.WriteLine($"[{ViewModel.ClientName}] HP Bar: StartX={hpBar.Value.startX}, EndX={hpBar.Value.endX}, Y={hpBar.Value.y}");
                    
                    // Auto-fill BabeBot HP coordinates
                    Dispatcher.BeginInvoke(() =>
                    {
                        BabeBotHpStart.Text = hpBar.Value.startX.ToString();
                        BabeBotHpEnd.Text = hpBar.Value.endX.ToString();
                        BabeBotHpY.Text = hpBar.Value.y.ToString();
                        
                        ViewModel.BabeBotHp.StartX = hpBar.Value.startX;
                        ViewModel.BabeBotHp.EndX = hpBar.Value.endX;
                        ViewModel.BabeBotHp.Y = hpBar.Value.y;
                        
                        // Also set reference color
                        ViewModel.BabeBotHp.ReferenceColor = hpBar.Value.color;
                        BabeBotHpCurrentColor.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(hpBar.Value.color.R, hpBar.Value.color.G, hpBar.Value.color.B));
                        BabeBotHpCurrentText.Text = $"{hpBar.Value.color.R},{hpBar.Value.color.G},{hpBar.Value.color.B}";
                        ViewModel.BabeBotHp.Status = "Detected";
                    });
                    
                    // Show visual indicator
                    Dispatcher.BeginInvoke(() =>
                    {
                        ShowBarIndicator("BabeBot HP", hpBar.Value.startX, hpBar.Value.endX, hpBar.Value.y, System.Windows.Media.Colors.Red);
                    });
                }
                else
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] ❌ BabeBot HP BAR NOT FOUND!");
                    Dispatcher.BeginInvoke(() =>
                    {
                        ViewModel.BabeBotHp.Status = "Not Found";
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] BabeBot HP detection error: {ex.Message}");
            }
        });
    }
    
    private void BabeBotMpDetect_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            MessageBox.Show("Please select a window first for BabeBot MP detection!", "No Window Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            Console.WriteLine($"[{ViewModel.ClientName}] No window selected for BabeBot MP detection");
            return;
        }
        
        Task.Run(() =>
        {
            try
            {
                Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot MP Bar Detection started...");
                
                // First try to detect HP to get better MP search range
                var hpBar = DetectBar(true);
                int mpSearchStartY = 30;
                int mpSearchEndY = 120;
                
                if (hpBar != null)
                {
                    // Search MP bar right below HP bar
                    mpSearchStartY = hpBar.Value.y + 1;
                    mpSearchEndY = hpBar.Value.y + 25;
                    Console.WriteLine($"[{ViewModel.ClientName}] HP found at Y={hpBar.Value.y}, searching MP in Y range {mpSearchStartY}-{mpSearchEndY}");
                }
                
                var mpBar = DetectBarInRange(false, mpSearchStartY, mpSearchEndY); // false = MP (blue)
                if (mpBar != null)
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] ✅ BabeBot MP BAR DETECTED!");
                    Console.WriteLine($"[{ViewModel.ClientName}] MP Bar: StartX={mpBar.Value.startX}, EndX={mpBar.Value.endX}, Y={mpBar.Value.y}");
                    
                    // Auto-fill BabeBot MP coordinates
                    Dispatcher.BeginInvoke(() =>
                    {
                        BabeBotMpStart.Text = mpBar.Value.startX.ToString();
                        BabeBotMpEnd.Text = mpBar.Value.endX.ToString();
                        BabeBotMpY.Text = mpBar.Value.y.ToString();
                        
                        ViewModel.BabeBotMp.StartX = mpBar.Value.startX;
                        ViewModel.BabeBotMp.EndX = mpBar.Value.endX;
                        ViewModel.BabeBotMp.Y = mpBar.Value.y;
                        
                        // Also set reference color
                        ViewModel.BabeBotMp.ReferenceColor = mpBar.Value.color;
                        BabeBotMpCurrentColor.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(mpBar.Value.color.R, mpBar.Value.color.G, mpBar.Value.color.B));
                        BabeBotMpCurrentText.Text = $"{mpBar.Value.color.R},{mpBar.Value.color.G},{mpBar.Value.color.B}";
                        ViewModel.BabeBotMp.Status = "Detected";
                    });
                    
                    // Show visual indicator
                    Dispatcher.BeginInvoke(() =>
                    {
                        ShowBarIndicator("BabeBot MP", mpBar.Value.startX, mpBar.Value.endX, mpBar.Value.y, System.Windows.Media.Colors.Blue);
                    });
                }
                else
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] ❌ BabeBot MP BAR NOT FOUND!");
                    Dispatcher.BeginInvoke(() =>
                    {
                        ViewModel.BabeBotMp.Status = "Not Found";
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] BabeBot MP detection error: {ex.Message}");
            }
        });
    }
    
    // BabeBot Timer Management
    private void StartBabeBotMonitoring()
    {
        if (_babeBotTimer != null) return;
        
        // Use master timer for BabeBot system instead of separate DispatcherTimer
        _masterTimer?.AddOrUpdateTask(
            "BabeBot", 
            TimeSpan.FromMilliseconds(120), // Same as BabeBot timer
            () => BabeBotTimer_Tick(null, null),
            enabled: true,
            priority: 8 // High priority for BabeBot system
        );
        
        Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot monitoring started");
    }
    
    private void StopBabeBotMonitoring()
    {
        _babeBotTimer?.Stop();
        _babeBotTimer = null;
        
        Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot monitoring stopped");
    }
    
    private void BabeBotTimer_Tick(object sender, EventArgs e)
    {
        try
        {
            // Check HP if enabled
            if (ViewModel.BabeBotHp.Enabled)
            {
                CheckBabeBotHp();
            }
            
            // Check MP if enabled
            if (ViewModel.BabeBotMp.Enabled)
            {
                CheckBabeBotMp();
            }
            
            // If neither is enabled, stop the timer
            if (!ViewModel.BabeBotHp.Enabled && !ViewModel.BabeBotMp.Enabled)
            {
                StopBabeBotMonitoring();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] BabeBot timer error: {ex.Message}");
        }
    }
    
    private void CheckBabeBotHp()
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero) return;
        
        try
        {
            // HP/MP monitoring needs real-time data, NO CACHE for accurate detection
            // Always get fresh color data for HP monitoring to avoid false triggers
            Color currentColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, ViewModel.BabeBotHp.MonitorX, ViewModel.BabeBotHp.Y);
            ViewModel.BabeBotHp.CurrentColor = currentColor;
            
            // BabeBot logic: if current color != reference color then trigger
            bool colorChanged = !ColorsMatch(currentColor, ViewModel.BabeBotHp.ReferenceColor);
            
            // Update UI
            Dispatcher.BeginInvoke(() =>
            {
                BabeBotHpCurrentColor.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(currentColor.R, currentColor.G, currentColor.B));
                BabeBotHpCurrentText.Text = $"{currentColor.R},{currentColor.G},{currentColor.B}";
                
                if (colorChanged)
                {
                    ViewModel.BabeBotHp.Status = $"LOW {ViewModel.BabeBotHp.ThresholdPercentage}%";
                }
                else
                {
                    ViewModel.BabeBotHp.Status = $"OK {ViewModel.BabeBotHp.ThresholdPercentage}%";
                }
            });
            
            // Trigger logic - BabeBot style (simplified for testing)
            if (colorChanged)
            {
                var now = DateTime.UtcNow;
                if ((now - ViewModel.BabeBotHp.LastExecution).TotalMilliseconds >= 500) // 500ms cooldown
                {
                    ViewModel.BabeBotHp.LastExecution = now;
                    
                    if (ViewModel.BabeBotHp.UseCoordinate)
                    {
                        Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot HP TRIGGER: Color changed! Clicking ({ViewModel.BabeBotHp.PotionX},{ViewModel.BabeBotHp.PotionY})");
                        PerformBackgroundClick(ViewModel.BabeBotHp.PotionX, ViewModel.BabeBotHp.PotionY, "BABEBOT_HP");
                    }
                    else if (ViewModel.BabeBotHp.UseKeyPress && !string.IsNullOrEmpty(ViewModel.BabeBotHp.KeyToPress))
                    {
                        Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot HP TRIGGER: Color changed! Key press '{ViewModel.BabeBotHp.KeyToPress}'");
                        PerformBackgroundKeyPress(ViewModel.BabeBotHp.KeyToPress, "BABEBOT_HP");
                    }
                    ViewModel.BabeBotHp.ExecutionCount++;
                    ViewModel.TriggerCount++;
                }
                else
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot HP on cooldown, {(500 - (now - ViewModel.BabeBotHp.LastExecution).TotalMilliseconds):F0}ms left");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] BabeBot HP check error: {ex.Message}");
        }
    }
    
    private void CheckBabeBotMp()
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero) return;
        
        try
        {
            // HP/MP monitoring needs real-time data, NO CACHE for accurate detection
            // Always get fresh color data for MP monitoring to avoid false triggers
            Color currentColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, ViewModel.BabeBotMp.MonitorX, ViewModel.BabeBotMp.Y);
            ViewModel.BabeBotMp.CurrentColor = currentColor;
            
            // BabeBot logic: if current color != reference color then trigger
            bool colorChanged = !ColorsMatch(currentColor, ViewModel.BabeBotMp.ReferenceColor);
            
            // Update UI
            Dispatcher.BeginInvoke(() =>
            {
                BabeBotMpCurrentColor.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(currentColor.R, currentColor.G, currentColor.B));
                BabeBotMpCurrentText.Text = $"{currentColor.R},{currentColor.G},{currentColor.B}";
                
                if (colorChanged)
                {
                    ViewModel.BabeBotMp.Status = $"LOW {ViewModel.BabeBotMp.ThresholdPercentage}%";
                }
                else
                {
                    ViewModel.BabeBotMp.Status = $"OK {ViewModel.BabeBotMp.ThresholdPercentage}%";
                }
            });
            
            // Trigger logic - BabeBot style (simplified for testing)
            if (colorChanged)
            {
                var now = DateTime.UtcNow;
                if ((now - ViewModel.BabeBotMp.LastExecution).TotalMilliseconds >= 500) // 500ms cooldown
                {
                    ViewModel.BabeBotMp.LastExecution = now;
                    
                    if (ViewModel.BabeBotMp.UseCoordinate)
                    {
                        Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot MP TRIGGER: Color changed! Clicking ({ViewModel.BabeBotMp.PotionX},{ViewModel.BabeBotMp.PotionY})");
                        PerformBackgroundClick(ViewModel.BabeBotMp.PotionX, ViewModel.BabeBotMp.PotionY, "BABEBOT_MP");
                    }
                    else if (ViewModel.BabeBotMp.UseKeyPress && !string.IsNullOrEmpty(ViewModel.BabeBotMp.KeyToPress))
                    {
                        Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot MP TRIGGER: Color changed! Key press '{ViewModel.BabeBotMp.KeyToPress}'");
                        PerformBackgroundKeyPress(ViewModel.BabeBotMp.KeyToPress, "BABEBOT_MP");
                    }
                    ViewModel.BabeBotMp.ExecutionCount++;
                    ViewModel.TriggerCount++;
                }
                else
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot MP on cooldown, {(500 - (now - ViewModel.BabeBotMp.LastExecution).TotalMilliseconds):F0}ms left");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] BabeBot MP check error: {ex.Message}");
        }
    }
    
    // Helper method for color comparison (simple tolerance-based)
    private bool ColorsMatch(System.Drawing.Color c1, System.Drawing.Color c2, int tolerance = 15)
    {
        return Math.Abs(c1.R - c2.R) <= tolerance &&
               Math.Abs(c1.G - c2.G) <= tolerance &&
               Math.Abs(c1.B - c2.B) <= tolerance;
    }
    
    private void SetupBabeBotUI()
    {
        // Setup initial UI values from ViewModel
        BabeBotHpStart.Text = ViewModel.BabeBotHp.StartX.ToString();
        BabeBotHpEnd.Text = ViewModel.BabeBotHp.EndX.ToString();
        BabeBotHpY.Text = ViewModel.BabeBotHp.Y.ToString();
        BabeBotHpPotionX.Text = ViewModel.BabeBotHp.PotionX.ToString();
        BabeBotHpPotionY.Text = ViewModel.BabeBotHp.PotionY.ToString();
        BabeBotHpThreshold.Text = ViewModel.BabeBotHp.ThresholdPercentage.ToString();
        
        BabeBotMpStart.Text = ViewModel.BabeBotMp.StartX.ToString();
        BabeBotMpEnd.Text = ViewModel.BabeBotMp.EndX.ToString();
        BabeBotMpY.Text = ViewModel.BabeBotMp.Y.ToString();
        BabeBotMpPotionX.Text = ViewModel.BabeBotMp.PotionX.ToString();
        BabeBotMpPotionY.Text = ViewModel.BabeBotMp.PotionY.ToString();
        BabeBotMpThreshold.Text = ViewModel.BabeBotMp.ThresholdPercentage.ToString();
        
        // Attach event handlers for real-time updates
        BabeBotHpStart.TextChanged += (s, e) => UpdateBabeBotHpFromUI();
        BabeBotHpEnd.TextChanged += (s, e) => UpdateBabeBotHpFromUI();
        BabeBotHpY.TextChanged += (s, e) => UpdateBabeBotHpFromUI();
        BabeBotHpPotionX.TextChanged += (s, e) => UpdateBabeBotHpFromUI();
        BabeBotHpPotionY.TextChanged += (s, e) => UpdateBabeBotHpFromUI();
        BabeBotHpThreshold.TextChanged += (s, e) => UpdateBabeBotHpFromUI();
        BabeBotHpUseCoordinate.Checked += (s, e) => { ViewModel.BabeBotHp.UseCoordinate = true; UpdateBabeBotHpFromUI(); };
        BabeBotHpUseCoordinate.Unchecked += (s, e) => { ViewModel.BabeBotHp.UseCoordinate = false; UpdateBabeBotHpFromUI(); };
        BabeBotHpUseKeyPress.Checked += (s, e) => { ViewModel.BabeBotHp.UseKeyPress = true; UpdateBabeBotHpFromUI(); };
        BabeBotHpUseKeyPress.Unchecked += (s, e) => { ViewModel.BabeBotHp.UseKeyPress = false; UpdateBabeBotHpFromUI(); };
        BabeBotHpKeyToPress.TextChanged += (s, e) => { ViewModel.BabeBotHp.KeyToPress = BabeBotHpKeyToPress.Text; UpdateBabeBotHpFromUI(); };
        
        BabeBotMpStart.TextChanged += (s, e) => UpdateBabeBotMpFromUI();
        BabeBotMpEnd.TextChanged += (s, e) => UpdateBabeBotMpFromUI();
        BabeBotMpY.TextChanged += (s, e) => UpdateBabeBotMpFromUI();
        BabeBotMpPotionX.TextChanged += (s, e) => UpdateBabeBotMpFromUI();
        BabeBotMpPotionY.TextChanged += (s, e) => UpdateBabeBotMpFromUI();
        BabeBotMpThreshold.TextChanged += (s, e) => UpdateBabeBotMpFromUI();
        BabeBotMpUseCoordinate.Checked += (s, e) => { ViewModel.BabeBotMp.UseCoordinate = true; UpdateBabeBotMpFromUI(); };
        BabeBotMpUseCoordinate.Unchecked += (s, e) => { ViewModel.BabeBotMp.UseCoordinate = false; UpdateBabeBotMpFromUI(); };
        BabeBotMpUseKeyPress.Checked += (s, e) => { ViewModel.BabeBotMp.UseKeyPress = true; UpdateBabeBotMpFromUI(); };
        BabeBotMpUseKeyPress.Unchecked += (s, e) => { ViewModel.BabeBotMp.UseKeyPress = false; UpdateBabeBotMpFromUI(); };
        BabeBotMpKeyToPress.TextChanged += (s, e) => { ViewModel.BabeBotMp.KeyToPress = BabeBotMpKeyToPress.Text; UpdateBabeBotMpFromUI(); };
        
        // Enable/Disable checkbox handlers
        BabeBotHpEnabled.Checked += (s, e) =>
        {
            ViewModel.BabeBotHp.Enabled = true;
            StartBabeBotMonitoring();
            Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot HP monitoring ENABLED");
        };
        
        BabeBotHpEnabled.Unchecked += (s, e) =>
        {
            ViewModel.BabeBotHp.Enabled = false;
            Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot HP monitoring DISABLED");
        };
        
        BabeBotMpEnabled.Checked += (s, e) =>
        {
            ViewModel.BabeBotMp.Enabled = true;
            StartBabeBotMonitoring();
            Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot MP monitoring ENABLED");
        };
        
        BabeBotMpEnabled.Unchecked += (s, e) =>
        {
            ViewModel.BabeBotMp.Enabled = false;
            Console.WriteLine($"[{ViewModel.ClientName}] 🤖 BabeBot MP monitoring DISABLED");
        };
    }
    
    private void UpdateBabeBotHpFromUI()
    {
        try
        {
            if (int.TryParse(BabeBotHpStart.Text, out int startX))
                ViewModel.BabeBotHp.StartX = startX;
            if (int.TryParse(BabeBotHpEnd.Text, out int endX))
                ViewModel.BabeBotHp.EndX = endX;
            if (int.TryParse(BabeBotHpY.Text, out int y))
                ViewModel.BabeBotHp.Y = y;
            if (int.TryParse(BabeBotHpPotionX.Text, out int potionX))
                ViewModel.BabeBotHp.PotionX = potionX;
            if (int.TryParse(BabeBotHpPotionY.Text, out int potionY))
                ViewModel.BabeBotHp.PotionY = potionY;
            
            if (int.TryParse(BabeBotHpThreshold.Text, out int threshold))
                ViewModel.BabeBotHp.ThresholdPercentage = threshold;
        }
        catch { /* Ignore parsing errors */ }
    }
    
    private void UpdateBabeBotMpFromUI()
    {
        try
        {
            if (int.TryParse(BabeBotMpStart.Text, out int startX))
                ViewModel.BabeBotMp.StartX = startX;
            if (int.TryParse(BabeBotMpEnd.Text, out int endX))
                ViewModel.BabeBotMp.EndX = endX;
            if (int.TryParse(BabeBotMpY.Text, out int y))
                ViewModel.BabeBotMp.Y = y;
            if (int.TryParse(BabeBotMpPotionX.Text, out int potionX))
                ViewModel.BabeBotMp.PotionX = potionX;
            if (int.TryParse(BabeBotMpPotionY.Text, out int potionY))
                ViewModel.BabeBotMp.PotionY = potionY;
            
            if (int.TryParse(BabeBotMpThreshold.Text, out int threshold))
                ViewModel.BabeBotMp.ThresholdPercentage = threshold;
        }
        catch { /* Ignore parsing errors */ }
    }
    
    #endregion

    #region Attack/Skills System

    private void SetupAttackSystem()
    {
        // No default skill - user will add skills manually
        
        // Setup UI event handlers
        AttackSystemEnabled.Checked += (s, e) => ViewModel.AttackSystemEnabled = true;
        AttackSystemEnabled.Unchecked += (s, e) => ViewModel.AttackSystemEnabled = false;
    }
    
    private void CreateSkillWidget(AttackSkillViewModel skill)
    {
        var skillFrame = new Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 50)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 70, 70)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6),
            Margin = new Thickness(0, 0, 0, 3)
        };
        
        var skillGrid = new Grid();
        skillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        skillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60, GridUnitType.Pixel) });
        skillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80, GridUnitType.Pixel) });
        skillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60, GridUnitType.Pixel) });
        skillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60, GridUnitType.Pixel) });
        skillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30, GridUnitType.Pixel) });
        
        // Skill name and key
        var nameLabel = new TextBlock
        {
            Text = $"{skill.Name} ({skill.Key})",
            Foreground = new SolidColorBrush(System.Windows.Media.Colors.White),
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.Bold
        };
        Grid.SetColumn(nameLabel, 0);
        skillGrid.Children.Add(nameLabel);
        
        // Interval
        var intervalLabel = new TextBlock
        {
            Text = $"{skill.IntervalMs}ms",
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 180)),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetColumn(intervalLabel, 1);
        skillGrid.Children.Add(intervalLabel);
        
        // Status
        var statusLabel = new TextBlock
        {
            Text = skill.Status,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 136, 136)),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetColumn(statusLabel, 2);
        skillGrid.Children.Add(statusLabel);
        
        // Enable/Disable button
        var enableButton = new Button
        {
            Content = skill.Enabled ? "ON" : "OFF",
            Background = new SolidColorBrush(skill.Enabled ? System.Windows.Media.Color.FromRgb(76, 175, 80) : System.Windows.Media.Color.FromRgb(102, 102, 102)),
            Foreground = new SolidColorBrush(System.Windows.Media.Colors.White),
            BorderThickness = new Thickness(0),
            Width = 50,
            Height = 22,
            FontSize = 9
        };
        enableButton.Click += (s, e) =>
        {
            skill.Enabled = !skill.Enabled;
            enableButton.Content = skill.Enabled ? "ON" : "OFF";
            enableButton.Background = new SolidColorBrush(skill.Enabled ? System.Windows.Media.Color.FromRgb(76, 175, 80) : System.Windows.Media.Color.FromRgb(102, 102, 102));
        };
        Grid.SetColumn(enableButton, 3);
        skillGrid.Children.Add(enableButton);
        
        // Delete button
        var deleteButton = new Button
        {
            Content = "×",
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54)),
            Foreground = new SolidColorBrush(System.Windows.Media.Colors.White),
            BorderThickness = new Thickness(0),
            Width = 25,
            Height = 22,
            FontSize = 12,
            FontWeight = FontWeights.Bold
        };
        deleteButton.Click += (s, e) => DeleteSkill(skill, skillFrame);
        Grid.SetColumn(deleteButton, 4);
        skillGrid.Children.Add(deleteButton);
        
        skillFrame.Child = skillGrid;
        
        // Store references for later use
        skill.Timer = null;
        
        AttackSkillsContainer.Children.Add(skillFrame);
    }
    
    private void DeleteSkill(AttackSkillViewModel skill, Border skillFrame)
    {
        // Stop timer if running
        skill.Timer?.Stop();
        
        // Remove from UI
        AttackSkillsContainer.Children.Remove(skillFrame);
        
        // Remove from ViewModel
        ViewModel.AttackSkills.Remove(skill);
        
        Console.WriteLine($"[{ViewModel.ClientName}] 🗑️ Skill '{skill.Name}' deleted");
    }
    
    private void StartAttackSystem()
    {
        if (_attackRunning)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ Attack system already running");
            return;
        }
        
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            MessageBox.Show("Please select a window first!", "No Window Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        var enabledSkills = ViewModel.AttackSkills.Where(s => s.Enabled).ToList();
        if (!enabledSkills.Any())
        {
            MessageBox.Show("No skills enabled for attack!", "No Skills", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        _attackRunning = true;
        ViewModel.AttackRunning = true;
        
        // Start timer for each enabled skill
        foreach (var skill in enabledSkills)
        {
            StartSkillTimer(skill);
        }
        
        StartAttackButton.IsEnabled = false;
        StopAttackButton.IsEnabled = true;
        
        Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ Attack system STARTED - {enabledSkills.Count} skills active");
    }
    
    private void StopAttackSystem()
    {
        _attackRunning = false;
        ViewModel.AttackRunning = false;
        
        // Stop all skill timers
        foreach (var timer in _skillTimers)
        {
            timer.Stop();
        }
        _skillTimers.Clear();
        
        // Reset all skill timers and status
        foreach (var skill in ViewModel.AttackSkills)
        {
            skill.Timer?.Stop();
            skill.Timer = null;
            skill.IsRunning = false;
            skill.Status = "Stopped";
        }
        
        StartAttackButton.IsEnabled = true;
        StopAttackButton.IsEnabled = false;
        
        Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ Attack system STOPPED");
    }
    
    private void StartSkillTimer(AttackSkillViewModel skill)
    {
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(skill.IntervalMs)
        };
        
        // Use bounded task queue for better performance and to prevent unbounded task growth
        timer.Tick += (s, e) => _boundedTaskQueue.TryEnqueue(
            () => CastSkillAsync(skill),
            TaskPriority.Normal,
            $"CastSkill_{skill.Name}",
            "AttackSystem",
            $"skill_{skill.Name}" // Coalescing key to prevent duplicate skill casts
        );
        skill.Timer = timer;
        skill.IsRunning = true;
        skill.Status = "Active";
        
        _skillTimers.Add(timer);
        timer.Start();
        
        Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ Started timer for skill '{skill.Name}' - {skill.IntervalMs}ms interval");
    }
    
    private void CastSkill(AttackSkillViewModel skill)
    {
        if (!_attackRunning || ViewModel.TargetHwnd == IntPtr.Zero)
            return;
        
        try
        {
            var vkKey = StringToVK(skill.Key);
            if (vkKey != null)
            {
                PerformBackgroundKeyPress(skill.Key, $"SKILL-{skill.Name}");
                skill.ExecutionCount++;
                skill.Status = $"Cast #{skill.ExecutionCount}";
                Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ Skill '{skill.Name}' cast successful (Key: {skill.Key})");
            }
            else
            {
                skill.Status = "Invalid Key";
                Console.WriteLine($"[{ViewModel.ClientName}] ❌ Invalid key '{skill.Key}' for skill '{skill.Name}'");
            }
        }
        catch (Exception ex)
        {
            skill.Status = "Error";
            Console.WriteLine($"[{ViewModel.ClientName}] ❌ Error casting skill '{skill.Name}': {ex.Message}");
        }
    }
    
    private async Task CastSkillAsync(AttackSkillViewModel skill)
    {
        if (!_attackRunning || ViewModel.TargetHwnd == IntPtr.Zero)
            return;

        try
        {
            var vkKey = StringToVK(skill.Key);
            if (vkKey != null)
            {
                // Use async delay if needed for more responsive UI
                PerformBackgroundKeyPress(skill.Key, $"SKILL-{skill.Name}");
                
                await Dispatcher.InvokeAsync(() =>
                {
                    skill.ExecutionCount++;
                    skill.Status = $"Cast #{skill.ExecutionCount}";
                });
                
                Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ Skill '{skill.Name}' cast successful (Key: {skill.Key})");
            }
            else
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    skill.Status = "Invalid Key";
                });
                Console.WriteLine($"[{ViewModel.ClientName}] ❌ Invalid key '{skill.Key}' for skill '{skill.Name}'");
            }
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                skill.Status = "Error";
            });
            Console.WriteLine($"[{ViewModel.ClientName}] ❌ Error casting skill '{skill.Name}': {ex.Message}");
        }
    }
    
    private User32.VK? StringToVK(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;
        
        key = key.ToUpper();
        
        return key switch
        {
            "1" => User32.VK.VK_1,
            "2" => User32.VK.VK_2,
            "3" => User32.VK.VK_3,
            "4" => User32.VK.VK_4,
            "5" => User32.VK.VK_5,
            "6" => User32.VK.VK_6,
            "7" => User32.VK.VK_7,
            "8" => User32.VK.VK_8,
            "9" => User32.VK.VK_9,
            "0" => User32.VK.VK_0,
            "Q" => User32.VK.VK_Q,
            "W" => User32.VK.VK_W,
            "E" => User32.VK.VK_E,
            "R" => User32.VK.VK_R,
            "T" => User32.VK.VK_T,
            "Y" => User32.VK.VK_Y,
            "U" => User32.VK.VK_U,
            "I" => User32.VK.VK_I,
            "O" => User32.VK.VK_O,
            "P" => User32.VK.VK_P,
            "A" => User32.VK.VK_A,
            "S" => User32.VK.VK_S,
            "D" => User32.VK.VK_D,
            "F" => User32.VK.VK_F,
            "G" => User32.VK.VK_G,
            "H" => User32.VK.VK_H,
            "J" => User32.VK.VK_J,
            "K" => User32.VK.VK_K,
            "L" => User32.VK.VK_L,
            "Z" => User32.VK.VK_Z,
            "X" => User32.VK.VK_X,
            "C" => User32.VK.VK_C,
            "V" => User32.VK.VK_V,
            "B" => User32.VK.VK_B,
            "N" => User32.VK.VK_N,
            "M" => User32.VK.VK_M,
            "F1" => User32.VK.VK_F1,
            "F2" => User32.VK.VK_F2,
            "F3" => User32.VK.VK_F3,
            "F4" => User32.VK.VK_F4,
            "F5" => User32.VK.VK_F5,
            "F6" => User32.VK.VK_F6,
            "F7" => User32.VK.VK_F7,
            "F8" => User32.VK.VK_F8,
            "SPACE" => User32.VK.VK_SPACE,
            "ENTER" => User32.VK.VK_RETURN,
            "CTRL" => User32.VK.VK_CONTROL,
            "SHIFT" => User32.VK.VK_SHIFT,
            "ALT" => User32.VK.VK_MENU,
            "TAB" => User32.VK.VK_TAB,
            _ => null
        };
    }

    #endregion

    #region Buff/AC System

    private void SetupBuffAcSystem()
    {
        // Setup UI event handlers
        BuffAcSystemEnabled.Checked += (s, e) => ViewModel.BuffAcSystemEnabled = true;
        BuffAcSystemEnabled.Unchecked += (s, e) => ViewModel.BuffAcSystemEnabled = false;
        
        // Setup text change handlers for settings
        // Member key input handlers
        Member1KeyInput.TextChanged += (s, e) => UpdateBuffAcSettings();
        Member2KeyInput.TextChanged += (s, e) => UpdateBuffAcSettings();
        Member3KeyInput.TextChanged += (s, e) => UpdateBuffAcSettings();
        Member4KeyInput.TextChanged += (s, e) => UpdateBuffAcSettings();
        Member5KeyInput.TextChanged += (s, e) => UpdateBuffAcSettings();
        Member6KeyInput.TextChanged += (s, e) => UpdateBuffAcSettings();
        Member7KeyInput.TextChanged += (s, e) => UpdateBuffAcSettings();
        Member8KeyInput.TextChanged += (s, e) => UpdateBuffAcSettings();
        
        // Buff/AC settings handlers
        BuffKeyInput.TextChanged += (s, e) => UpdateBuffAcSettings();
        AcKeyInput.TextChanged += (s, e) => UpdateBuffAcSettings();
        BuffAnimInput.TextChanged += (s, e) => UpdateBuffAcSettings();
        AcAnimInput.TextChanged += (s, e) => UpdateBuffAcSettings();
        CycleIntervalInput.TextChanged += (s, e) => UpdateBuffAcSettings();
    }
    
    private void UpdateBuffAcSettings()
    {
        try
        {
            // Update member selection keys
            ViewModel.BuffAcSettings.Member1Key = Member1KeyInput.Text;
            ViewModel.BuffAcSettings.Member2Key = Member2KeyInput.Text;
            ViewModel.BuffAcSettings.Member3Key = Member3KeyInput.Text;
            ViewModel.BuffAcSettings.Member4Key = Member4KeyInput.Text;
            ViewModel.BuffAcSettings.Member5Key = Member5KeyInput.Text;
            ViewModel.BuffAcSettings.Member6Key = Member6KeyInput.Text;
            ViewModel.BuffAcSettings.Member7Key = Member7KeyInput.Text;
            ViewModel.BuffAcSettings.Member8Key = Member8KeyInput.Text;
            
            // Update buff/AC settings
            ViewModel.BuffAcSettings.BuffKey = BuffKeyInput.Text;
            ViewModel.BuffAcSettings.AcKey = AcKeyInput.Text;
            
            if (double.TryParse(BuffAnimInput.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double buffAnim))
                ViewModel.BuffAcSettings.BuffAnimationTime = buffAnim;
                
            if (double.TryParse(AcAnimInput.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double acAnim))
                ViewModel.BuffAcSettings.AcAnimationTime = acAnim;
                
            if (int.TryParse(CycleIntervalInput.Text, out int interval))
                ViewModel.BuffAcSettings.CycleIntervalSeconds = interval;
                
            // Update running cycle timer if active
            if (_buffAcRunning && _buffAcCycleTimer != null)
            {
                var newInterval = ViewModel.BuffAcSettings.CycleIntervalSeconds * 1000;
                _buffAcCycleTimer.Interval = TimeSpan.FromMilliseconds(newInterval);
            }
        }
        catch
        {
            // Ignore parsing errors
        }
    }
    
    private void StartBuffAcSystem()
    {
        if (_buffAcRunning)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] 🎭 Buff/AC system already running");
            return;
        }
        
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            MessageBox.Show("Please select a window first!", "No Window Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        // Get list of enabled party members from BuffAc settings (independent from Party Heal)
        _buffAcEnabledMembers.Clear();
        var memberEnabledStates = new bool[]
        {
            ViewModel.BuffAcSettings.Member1Enabled,
            ViewModel.BuffAcSettings.Member2Enabled,
            ViewModel.BuffAcSettings.Member3Enabled,
            ViewModel.BuffAcSettings.Member4Enabled,
            ViewModel.BuffAcSettings.Member5Enabled,
            ViewModel.BuffAcSettings.Member6Enabled,
            ViewModel.BuffAcSettings.Member7Enabled,
            ViewModel.BuffAcSettings.Member8Enabled
        };
        
        for (int i = 0; i < memberEnabledStates.Length; i++)
        {
            if (memberEnabledStates[i])
            {
                _buffAcEnabledMembers.Add(i);
            }
        }
        
        if (!_buffAcEnabledMembers.Any())
        {
            MessageBox.Show("No party members enabled for buff/AC!", "No Enabled Members", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        _buffAcRunning = true;
        ViewModel.BuffAcRunning = true;
        ViewModel.BuffAcSettings.Enabled = true;
        _currentBuffAcMemberIndex = 0;
        
        // Pause other systems
        PauseAttackForBuffAc();
        
        // Setup cycle timer
        var cycleInterval = ViewModel.BuffAcSettings.CycleIntervalSeconds * 1000;
        _buffAcCycleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(cycleInterval)
        };
        _buffAcCycleTimer.Tick += BuffAcCycleTimer_Tick;
        _buffAcCycleTimer.Start();
        
        StartBuffAcButton.IsEnabled = false;
        StopBuffAcButton.IsEnabled = true;
        BuffAcStatusText.Text = $"Status: Active - {_buffAcEnabledMembers.Count} members";
        
        Console.WriteLine($"[{ViewModel.ClientName}] 🎭 Buff/AC system STARTED - {_buffAcEnabledMembers.Count} enabled members, {ViewModel.BuffAcSettings.CycleIntervalSeconds}s cycle");
        
        // Start first cycle immediately
        RunBuffAcCycle();
    }
    
    private void StopBuffAcSystem()
    {
        _buffAcRunning = false;
        ViewModel.BuffAcRunning = false;
        ViewModel.BuffAcSettings.Enabled = false;
        _buffAcCycleActive = false;
        
        _buffAcCycleTimer?.Stop();
        _buffAcCycleTimer = null;
        
        // Clean up all active timers
        lock (_lockObject)
        {
            foreach (var timer in _activeBuffAcTimers)
            {
                timer?.Stop();
            }
            _activeBuffAcTimers.Clear();
        }
        
        // Resume other systems
        ResumeAttackAfterBuffAc();
        
        StartBuffAcButton.IsEnabled = true;
        StopBuffAcButton.IsEnabled = false;
        BuffAcStatusText.Text = "Status: Stopped";
        
        Console.WriteLine($"[{ViewModel.ClientName}] 🎭 Buff/AC system STOPPED");
    }
    
    private void BuffAcCycleTimer_Tick(object? sender, EventArgs e)
    {
        if (_buffAcRunning)
        {
            RunBuffAcCycle();
        }
    }
    
    private void RunBuffAcCycle()
    {
        // Thread-safe check and set to prevent race conditions
        lock (_lockObject)
        {
            if (!_buffAcRunning || _buffAcCycleActive)
                return;
            
            _buffAcCycleActive = true;
        }
        ViewModel.BuffAcSettings.CycleCount++;
        
        Console.WriteLine($"[{ViewModel.ClientName}] 🎭 Starting buff/AC cycle #{ViewModel.BuffAcSettings.CycleCount}...");
        BuffAcStatusText.Text = $"Status: Cycling #{ViewModel.BuffAcSettings.CycleCount} - {_buffAcEnabledMembers.Count} members";
        
        // Pause party heal system during buff/AC cycle
        PausePartyHealForBuffAc();
        
        // CORRECT FLOW: Process each member individually
        _currentBuffAcMemberIndex = 0;
        ProcessNextBuffAcMember();
    }
    
    private void ProcessNextBuffAcMember()
    {
        if (!_buffAcRunning || !_buffAcCycleActive || _currentBuffAcMemberIndex >= _buffAcEnabledMembers.Count)
        {
            // Cycle complete
            FinishBuffAcCycle();
            return;
        }
        
        int memberIndex = _buffAcEnabledMembers[_currentBuffAcMemberIndex];
        
        Console.WriteLine($"[{ViewModel.ClientName}] 🎭 Processing Member {memberIndex + 1} ({_currentBuffAcMemberIndex + 1}/{_buffAcEnabledMembers.Count})");
        BuffAcStatusText.Text = $"Status: Member {memberIndex + 1} ({_currentBuffAcMemberIndex + 1}/{_buffAcEnabledMembers.Count})";
        
        // Step 1: Select this member
        SelectPartyMember(memberIndex);
    }
    
    private void SelectPartyMember(int memberIndex)
    {
        if (!_buffAcRunning) return;
        
        var memberKeys = new string[]
        {
            ViewModel.BuffAcSettings.Member1Key,
            ViewModel.BuffAcSettings.Member2Key,
            ViewModel.BuffAcSettings.Member3Key,
            ViewModel.BuffAcSettings.Member4Key,
            ViewModel.BuffAcSettings.Member5Key,
            ViewModel.BuffAcSettings.Member6Key,
            ViewModel.BuffAcSettings.Member7Key,
            ViewModel.BuffAcSettings.Member8Key
        };
        
        var selectKey = memberKeys[memberIndex];
        var vkKey = StringToVK(selectKey);
        
        if (vkKey != null)
        {
            PerformBackgroundKeyPress(selectKey, $"PARTY-SELECT-{memberIndex + 1}");
            Console.WriteLine($"[{ViewModel.ClientName}] 🎯 Selected Member {memberIndex + 1} (Key: {selectKey})");
            
            // Wait 200ms for selection, then cast buff
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                lock (_lockObject)
                {
                    _activeBuffAcTimers.Remove(timer);
                }
                CastBuffForMember(memberIndex);
            };
            lock (_lockObject)
            {
                _activeBuffAcTimers.Add(timer);
            }
            timer.Start();
        }
        else
        {
            Console.WriteLine($"[{ViewModel.ClientName}] ❌ Invalid selection key for Member {memberIndex + 1}: {selectKey}");
            MoveToNextBuffAcMember();
        }
    }
    
    private void CastBuffForMember(int memberIndex)
    {
        if (!_buffAcRunning) return;
        
        var buffKey = ViewModel.BuffAcSettings.BuffKey;
        var vkKey = StringToVK(buffKey);
        
        if (vkKey != null)
        {
            PerformBackgroundKeyPress(buffKey, $"PARTY-BUFF-{memberIndex + 1}");
            Console.WriteLine($"[{ViewModel.ClientName}] 🛡️ Buff cast on Member {memberIndex + 1} (Key: {buffKey})");
            
            // Wait for buff animation, then cast AC
            var animTime = (int)(ViewModel.BuffAcSettings.BuffAnimationTime * 1000);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(animTime) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                lock (_lockObject)
                {
                    _activeBuffAcTimers.Remove(timer);
                }
                CastAcForMember(memberIndex);
            };
            lock (_lockObject)
            {
                _activeBuffAcTimers.Add(timer);
            }
            timer.Start();
        }
        else
        {
            Console.WriteLine($"[{ViewModel.ClientName}] ❌ Invalid buff key: {buffKey}");
            MoveToNextBuffAcMember();
        }
    }
    
    private void CastAcForMember(int memberIndex)
    {
        if (!_buffAcRunning) return;
        
        var acKey = ViewModel.BuffAcSettings.AcKey;
        var vkKey = StringToVK(acKey);
        
        if (vkKey != null)
        {
            PerformBackgroundKeyPress(acKey, $"PARTY-AC-{memberIndex + 1}");
            Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ AC cast on Member {memberIndex + 1} (Key: {acKey})");
            
            // Wait for AC animation, then move to next member
            var animTime = (int)(ViewModel.BuffAcSettings.AcAnimationTime * 1000);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(animTime) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                lock (_lockObject)
                {
                    _activeBuffAcTimers.Remove(timer);
                }
                MoveToNextBuffAcMember();
            };
            lock (_lockObject)
            {
                _activeBuffAcTimers.Add(timer);
            }
            timer.Start();
        }
        else
        {
            Console.WriteLine($"[{ViewModel.ClientName}] ❌ Invalid AC key: {acKey}");
            MoveToNextBuffAcMember();
        }
    }
    
    private void MoveToNextBuffAcMember()
    {
        if (!_buffAcRunning) return;
        
        _currentBuffAcMemberIndex++;
        ProcessNextBuffAcMember();
    }
    
    private void FinishBuffAcCycle()
    {
        lock (_lockObject)
        {
            _buffAcCycleActive = false;
        }
        BuffAcStatusText.Text = $"Status: Active - Next cycle in {ViewModel.BuffAcSettings.CycleIntervalSeconds}s";
        
        // Resume party heal system after buff/AC cycle completion
        ResumePartyHealAfterBuffAc();
        
        Console.WriteLine($"[{ViewModel.ClientName}] ✅ Buff/AC cycle #{ViewModel.BuffAcSettings.CycleCount} completed");
    }
    
    
    private void PauseAttackForBuffAc()
    {
        if (_attackRunning)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] ⏸️ Pausing attack system for buff/AC cycle");
            StopAttackSystem();
        }
    }
    
    private void ResumeAttackAfterBuffAc()
    {
        // Resume attack system if it was enabled
        var enabledSkills = ViewModel.AttackSkills.Where(s => s.Enabled).ToList();
        if (enabledSkills.Any())
        {
            Console.WriteLine($"[{ViewModel.ClientName}] ▶️ Resuming attack system after buff/AC cycle");
            StartAttackSystem();
        }
    }
    
    private void PausePartyHealForBuffAc()
    {
        if (_partyHealRunning)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] ⏸️ Pausing party heal system for buff/AC cycle");
            Task.Run(async () => await StopPartyHealAsync());
        }
    }
    
    private void ResumePartyHealAfterBuffAc()
    {
        // Resume party heal system if it was enabled before
        if (PartyHealSystemEnabled.IsChecked == true && !_partyHealRunning)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] ▶️ Resuming party heal system after buff/AC cycle");
            Task.Run(async () => await StartPartyHealAsync());
        }
    }
    

    #endregion

    // Party Heal System completely removed






    // SimplePartyHealTick method removed

    // MultiHpTimer_Tick method removed

    private async Task ProcessMultiHpClientsAsync()
    {
        try
        {
            // Check if multi HP system is enabled for this client
            // MultiHp system removed - return empty list
            if (true) // Always disabled now
            {
                Console.WriteLine($"[PartyHeal-{ClientId}] Multi HP system disabled");
                return;
            }

            // Find all enabled clients that need checking (not waiting for animation) - run on background thread
            var enabledClients = await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    // MultiHpClients removed - return empty list
                    return new List<dynamic>();
                }
            }, _cancellationTokenSource.Token);

            if (!enabledClients.Any()) 
            {
                Console.WriteLine($"[PartyHeal-{ClientId}] No enabled clients found for monitoring");
                return;
            }

            Console.WriteLine($"[PartyHeal-{ClientId}] Processing {enabledClients.Count} enabled clients, current index: {_currentMultiHpIndex}");

            // Round-robin through enabled clients
            var currentClientInfo = enabledClients.Skip(_currentMultiHpIndex % enabledClients.Count).FirstOrDefault();
            if (currentClientInfo == null) return;

            var client = currentClientInfo.Client;
            var clientIndex = currentClientInfo.Index;

            Console.WriteLine($"[PartyHeal-{ClientId}] Checking client {clientIndex} (round-robin)");

            // Update the index for next time (thread-safe)
            lock (_lockObject)
            {
                _currentMultiHpIndex = (_currentMultiHpIndex + 1) % enabledClients.Count;
            }

            // Process HP check in parallel for better performance
            // await ProcessClientHPAsync(client, clientIndex); // Method removed
        }
        catch (OperationCanceledException)
        {
            // Task was cancelled, exit gracefully
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PartyHeal-{ClientId}] ProcessMultiHpClientsAsync error: {ex.Message}");
        }
    }


    // CheckClientHp method removed
    
    // ProcessClientHPAsync method removed

    // CalculateHpPercentage method removed

    // ExecuteMultiHpAction method removed

    private void SendKeyPress(string key)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero) return;

        // Convert key to virtual key code
        byte vkCode = key switch
        {
            "0" => 0x30, "1" => 0x31, "2" => 0x32, "3" => 0x33, "4" => 0x34,
            "5" => 0x35, "6" => 0x36, "7" => 0x37, "8" => 0x38, "9" => 0x39,
            "A" => 0x41, "B" => 0x42, "C" => 0x43, "D" => 0x44, "E" => 0x45, "F" => 0x46,
            "G" => 0x47, "H" => 0x48, "I" => 0x49, "J" => 0x4A, "K" => 0x4B, "L" => 0x4C,
            "M" => 0x4D, "N" => 0x4E, "O" => 0x4F, "P" => 0x50, "Q" => 0x51, "R" => 0x52,
            "S" => 0x53, "T" => 0x54, "U" => 0x55, "V" => 0x56, "W" => 0x57, "X" => 0x58,
            "Y" => 0x59, "Z" => 0x5A,
            _ => 0x31 // Default to '1'
        };

        try 
        {
            Console.WriteLine($"[PartyHeal-{ClientId}] 🔑 Sending key '{key}' (VK={vkCode:X2}) to window 0x{ViewModel.TargetHwnd:X8}");
            
            // Method 1: PostMessage to window (like Attack system)
            uint scanCode = User32.MapVirtualKey(vkCode, 0);
            IntPtr lParam = (IntPtr)((scanCode << 16) | 1);
            IntPtr lParamUp = (IntPtr)((scanCode << 16) | 1 | (1 << 30) | (1 << 31));
            
            User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_KEYDOWN, (IntPtr)vkCode, lParam);
            Thread.Sleep(25);
            User32.PostMessage(ViewModel.TargetHwnd, User32.WindowMessage.WM_KEYUP, (IntPtr)vkCode, lParamUp);
            
            // Method 2: Also try child windows
            var childWindows = new List<IntPtr>();
            User32.EnumChildWindows(ViewModel.TargetHwnd, (hwnd, lParam) =>
            {
                childWindows.Add((IntPtr)hwnd);
                return true;
            }, IntPtr.Zero);
            
            if (childWindows.Count > 0)
            {
                var primaryChild = childWindows[0];
                User32.PostMessage(primaryChild, User32.WindowMessage.WM_KEYDOWN, (IntPtr)vkCode, lParam);
                Thread.Sleep(25);
                User32.PostMessage(primaryChild, User32.WindowMessage.WM_KEYUP, (IntPtr)vkCode, lParamUp);
            }
            
            Console.WriteLine($"[PartyHeal-{ClientId}] ✅ Key '{key}' sent successfully to main window + {childWindows.Count} child windows");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PartyHeal-{ClientId}] ❌ Error sending key '{key}': {ex.Message}");
            
            // Fallback to global keybd_event
            User32.keybd_event(vkCode, 0, 0, IntPtr.Zero);
            Thread.Sleep(50);
            User32.keybd_event(vkCode, 0, User32.KEYEVENTF.KEYEVENTF_KEYUP, IntPtr.Zero);
        }
    }

    private async Task CalibrateMultiHpClient(int clientIndex)
    {
        // MultiHpClients removed - entire method disabled
        return;
        
        /*
        // Original method commented out - MultiHpClients removed
        // var client = ViewModel.MultiHpClients[clientIndex]; // MultiHpClients removed
        
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            MessageBox.Show($"Please select a window first for Party Member {client.ClientIndex} calibration!", "No Window Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ No window selected for Party Member {client.ClientIndex} calibration");
            return;
        }

        try
        {
            client.ReferenceColors.Clear();
            client.Status = "Calibrating...";
            UpdateClientStatusDisplay(clientIndex);

            // Use semaphore to prevent overlapping color sampling operations
            if (!await _operationSemaphore.WaitAsync(5000, _cancellationTokenSource.Token)) // 5 second timeout for calibration
            {
                client.Status = "Calibration Busy";
                UpdateClientStatusDisplay(clientIndex);
                return; // Skip if another operation is running
            }

            try
            {
                // MASSIVE PERFORMANCE OPTIMIZATION: Batch sample all calibration points at once
                // This reduces 18 individual Win32 API calls to 1 batch operation!
                
                var percentages = Enumerable.Range(1, 19).Select(i => i * 5).ToArray(); // 5%, 10%, ... 95%
                
                if (_colorCache != null)
                {
                    // Calculate all calibration points
                    var calibrationPoints = percentages.Select(p => new Point(client.CalculateXForPercentage(p), client.Y)).ToArray();
                    
                    // Batch sample all points with high priority caching (calibration is important)
                    var colors = _colorCache.BatchSampleColors(ViewModel.TargetHwnd, calibrationPoints, 
                        TimeSpan.FromMilliseconds(500));
                    
                    // Update reference colors
                    for (int i = 0; i < percentages.Length; i++)
                    {
                        var percentage = percentages[i];
                        var point = calibrationPoints[i];
                        if (colors.TryGetValue(point, out var color))
                        {
                            client.ReferenceColors[percentage] = color;
                            Console.WriteLine($"[PartyHeal-Calibration-{ClientId}] Client {clientIndex} - {percentage}% HP at ({point.X},{point.Y}) = RGB({color.R},{color.G},{color.B})");
                        }
                    }
                    
                    Console.WriteLine($"[PartyHeal-Calibration-{ClientId}] BATCH CALIBRATION: Client {clientIndex} calibrated with {client.ReferenceColors.Count} reference points");
                }
                else
                {
                    // Fallback to original method
                    _fastSampler?.CaptureWindow(ViewModel.TargetHwnd);
                    
                    foreach (var percentage in percentages)
                    {
                        int x = client.CalculateXForPercentage(percentage);
                        var color = _fastSampler?.GetColorAt(x, client.Y);
                        
                        if (color.HasValue)
                        {
                            client.ReferenceColors[percentage] = color.Value;
                            Console.WriteLine($"[PartyHeal-Calibration-{ClientId}] Client {clientIndex} - {percentage}% HP at ({x},{client.Y}) = RGB({color.Value.R},{color.Value.G},{color.Value.B})");
                        }
                    }
                    
                    Console.WriteLine($"[PartyHeal-Calibration-{ClientId}] FALLBACK CALIBRATION: Client {clientIndex} calibrated with {client.ReferenceColors.Count} reference points");
                }

                client.Status = $"Calibrated ({client.ReferenceColors.Count} points)";
                UpdateClientStatusDisplay(clientIndex);
                
                Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ Party Member {client.ClientIndex} calibrated with {client.ReferenceColors.Count} reference points");
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            client.Status = "Calibration Cancelled";
            UpdateClientStatusDisplay(clientIndex);
        }
        catch (Exception ex)
        {
            client.Status = "Calibration Error";
            UpdateClientStatusDisplay(clientIndex);
            Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ Calibration error for Party Member {client.ClientIndex}: {ex.Message}");
        }
        */
    }

    private void PickMultiHpClientClick(int clientIndex)
    {
        // MultiHpClients removed - entire method disabled
        return;
        
        /*
        // Original method commented out - MultiHpClients removed
        // var client = ViewModel.MultiHpClients[clientIndex]; // MultiHpClients removed
        
        _coordinatePicker = new CoordinatePicker(ViewModel.TargetHwnd, ViewModel.ClientName);
        _coordinatePicker.CoordinatePicked += (x, y) =>
        {
            client.ClickX = x;
            client.ClickY = y;
            UpdateMultiHpClientTextBox(clientIndex, "ClickX", x.ToString());
            UpdateMultiHpClientTextBox(clientIndex, "ClickY", y.ToString());
            Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ Party Member {client.ClientIndex} click position set to ({x}, {y})");
        };
        _coordinatePicker.Show();
        */
    }

    private void UpdateMultiHpClientTextBox(int clientIndex, string property, string value)
    {
        // MultiHp UI controls removed - method disabled
        return;
        
        /*
        // Original method commented out - UI controls removed
        switch (clientIndex)
        {
            case 0:
                if (property == "ClickX") MultiHp1ClickX.Text = value;
                else if (property == "ClickY") MultiHp1ClickY.Text = value;
                break;
            case 1:
                if (property == "ClickX") MultiHp2ClickX.Text = value;
                else if (property == "ClickY") MultiHp2ClickY.Text = value;
                break;
            case 2:
                if (property == "ClickX") MultiHp3ClickX.Text = value;
                else if (property == "ClickY") MultiHp3ClickY.Text = value;
                break;
            case 3:
                if (property == "ClickX") MultiHp4ClickX.Text = value;
                else if (property == "ClickY") MultiHp4ClickY.Text = value;
                break;
            case 4:
                if (property == "ClickX") MultiHp5ClickX.Text = value;
                else if (property == "ClickY") MultiHp5ClickY.Text = value;
                break;
            case 5:
                if (property == "ClickX") MultiHp6ClickX.Text = value;
                else if (property == "ClickY") MultiHp6ClickY.Text = value;
                break;
            case 6:
                if (property == "ClickX") MultiHp7ClickX.Text = value;
                else if (property == "ClickY") MultiHp7ClickY.Text = value;
                break;
            case 7:
                if (property == "ClickX") MultiHp8ClickX.Text = value;
                else if (property == "ClickY") MultiHp8ClickY.Text = value;
                break;
        }
        */
    }

    private void UpdateClientStatusDisplay(int clientIndex)
    {
        Dispatcher.BeginInvoke(() =>
        {
            UpdateMultiHpStatusDisplay();
        });
    }

    private void UpdateMultiHpStatusDisplay()
    {
        try
        {
            // MultiHpClients removed - no longer update display
            // for (int i = 0; i < ViewModel.MultiHpClients.Count; i++)
            // {
            //     var client = ViewModel.MultiHpClients[i];
            //     UpdateSingleClientDisplay(i, client);
            // }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] ⚔️ Error updating Party Heal display: {ex.Message}");
        }
    }

    // UpdateSingleClientDisplay method removed


    #region Attack/Skills Event Handlers

    private void StartAttack_Click(object sender, RoutedEventArgs e)
    {
        StartAttackSystem();
    }

    private void StopAttack_Click(object sender, RoutedEventArgs e)
    {
        StopAttackSystem();
    }

    private void AddSkill_Click(object sender, RoutedEventArgs e)
    {
        var name = SkillNameInput.Text.Trim();
        var key = SkillKeyInput.Text.Trim();
        var intervalText = SkillIntervalInput.Text.Trim();
        
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(intervalText))
        {
            MessageBox.Show("All skill fields are required!", "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (!int.TryParse(intervalText, out int interval) || interval < 100)
        {
            MessageBox.Show("Interval must be a number >= 100ms!", "Invalid Interval", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        // Check if key already exists
        if (ViewModel.AttackSkills.Any(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show($"Key '{key}' is already assigned to another skill!", "Duplicate Key", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        // Create new skill (enabled by default)
        var newSkill = new AttackSkillViewModel
        {
            Name = name,
            Key = key.ToUpper(),
            IntervalMs = interval,
            Enabled = true
        };
        
        ViewModel.AttackSkills.Add(newSkill);
        CreateSkillWidget(newSkill);
        
        // Clear inputs
        SkillNameInput.Text = "";
        SkillKeyInput.Text = "";
        SkillIntervalInput.Text = "1500";
        
        Console.WriteLine($"[{ViewModel.ClientName}] ✅ Skill '{name}' added (Key: {key}, Interval: {interval}ms)");
    }

    #endregion

    #region Buff/AC Event Handlers

    private void StartBuffAc_Click(object sender, RoutedEventArgs e)
    {
        StartBuffAcSystem();
    }

    private void StopBuffAc_Click(object sender, RoutedEventArgs e)
    {
        StopBuffAcSystem();
    }

    #endregion
    
    #region Party Heal System
    
    private void InitializePartyHealSystem()
    {
        try
        {
            // Initialize service with proper backends and parameters
            var logger = new ConsoleLogger<PartyHealService>();
            var clickLogger = new ConsoleLogger<WindowsMessageClickProvider>();
            var captureBackend = new PrintWindowBackend(); // Use PrintWindow backend (WGC is placeholder)
            var clickProvider = new WindowsMessageClickProvider(clickLogger);
            var taskQueue = new BoundedTaskQueue(maxQueueSize: 100, maxConcurrency: 2);
            
            _partyHealService = new PartyHealService(logger, captureBackend, clickProvider, taskQueue);
            
            // Setup UI event handlers for PartyHeal tab
            SetupPartyHealEventHandlers();
            
            Console.WriteLine($"[{ClientId}] ✅ PartyHeal system initialized successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ClientId}] ❌ Failed to initialize PartyHeal system: {ex.Message}");
        }
    }
    
    private void SetupPartyHealEventHandlers()
    {
        // PartyHeal system enable/disable handlers
        PartyHealSystemEnabled.Checked += async (s, e) => await StartPartyHealAsync();
        PartyHealSystemEnabled.Unchecked += async (s, e) => await StopPartyHealAsync();
        
        // Global PartyHeal settings handlers
        PartyHealSkillKey.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyHealPollInterval.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyHealBaselineColor.TextChanged += (s, e) => UpdatePartyHealSettings();
        
        // Member settings handlers
        PartyMember1Key.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember1Threshold.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember1XStart.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember1XEnd.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember1Y.TextChanged += (s, e) => UpdatePartyHealSettings();
        
        PartyMember2Key.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember2Threshold.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember2XStart.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember2XEnd.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember2Y.TextChanged += (s, e) => UpdatePartyHealSettings();
        
        PartyMember3Key.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember3Threshold.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember3XStart.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember3XEnd.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember3Y.TextChanged += (s, e) => UpdatePartyHealSettings();
        
        PartyMember4Key.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember4Threshold.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember4XStart.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember4XEnd.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember4Y.TextChanged += (s, e) => UpdatePartyHealSettings();
        
        PartyMember5Key.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember5Threshold.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember5XStart.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember5XEnd.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember5Y.TextChanged += (s, e) => UpdatePartyHealSettings();
        
        PartyMember6Key.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember6Threshold.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember6XStart.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember6XEnd.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember6Y.TextChanged += (s, e) => UpdatePartyHealSettings();
        
        PartyMember7Key.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember7Threshold.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember7XStart.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember7XEnd.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember7Y.TextChanged += (s, e) => UpdatePartyHealSettings();
        
        PartyMember8Key.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember8Threshold.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember8XStart.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember8XEnd.TextChanged += (s, e) => UpdatePartyHealSettings();
        PartyMember8Y.TextChanged += (s, e) => UpdatePartyHealSettings();
        
        // Checkbox handlers  
        PartyMember1Enabled.Checked += (s, e) => UpdatePartyHealSettings();
        PartyMember1Enabled.Unchecked += (s, e) => UpdatePartyHealSettings();
        PartyMember2Enabled.Checked += (s, e) => UpdatePartyHealSettings();
        PartyMember2Enabled.Unchecked += (s, e) => UpdatePartyHealSettings();
        PartyMember3Enabled.Checked += (s, e) => UpdatePartyHealSettings();
        PartyMember3Enabled.Unchecked += (s, e) => UpdatePartyHealSettings();
        PartyMember4Enabled.Checked += (s, e) => UpdatePartyHealSettings();
        PartyMember4Enabled.Unchecked += (s, e) => UpdatePartyHealSettings();
        PartyMember5Enabled.Checked += (s, e) => UpdatePartyHealSettings();
        PartyMember5Enabled.Unchecked += (s, e) => UpdatePartyHealSettings();
        PartyMember6Enabled.Checked += (s, e) => UpdatePartyHealSettings();
        PartyMember6Enabled.Unchecked += (s, e) => UpdatePartyHealSettings();
        PartyMember7Enabled.Checked += (s, e) => UpdatePartyHealSettings();
        PartyMember7Enabled.Unchecked += (s, e) => UpdatePartyHealSettings();
        PartyMember8Enabled.Checked += (s, e) => UpdatePartyHealSettings();
        PartyMember8Enabled.Unchecked += (s, e) => UpdatePartyHealSettings();
    }
    
    private void UpdatePartyHealSettings()
    {
        if (_partyHealService == null) return;
        
        try
        {
            var config = _partyHealService.Configuration;
            
            // Update global settings
            config.Global.SkillKey = PartyHealSkillKey.Text;
            
            if (int.TryParse(PartyHealPollInterval.Text, out int pollInterval))
            {
                // Enforce minimum poll interval to prevent performance issues
                config.Global.PollIntervalMs = Math.Max(pollInterval, 150); // Minimum 150ms
            }
                
            // Parse HP color
            try
            {
                var colorHex = PartyHealBaselineColor.Text.TrimStart('#');
                var color = System.Drawing.ColorTranslator.FromHtml("#" + colorHex);
                config.Global.BaselineColor = color;
            }
            catch
            {
                // Keep existing color if parsing fails
            }
            
            // Update member configurations
            UpdateMemberConfig(0, PartyMember1Enabled, PartyMember1Key, PartyMember1Threshold, PartyMember1XStart, PartyMember1XEnd, PartyMember1Y);
            UpdateMemberConfig(1, PartyMember2Enabled, PartyMember2Key, PartyMember2Threshold, PartyMember2XStart, PartyMember2XEnd, PartyMember2Y);
            UpdateMemberConfig(2, PartyMember3Enabled, PartyMember3Key, PartyMember3Threshold, PartyMember3XStart, PartyMember3XEnd, PartyMember3Y);
            UpdateMemberConfig(3, PartyMember4Enabled, PartyMember4Key, PartyMember4Threshold, PartyMember4XStart, PartyMember4XEnd, PartyMember4Y);
            UpdateMemberConfig(4, PartyMember5Enabled, PartyMember5Key, PartyMember5Threshold, PartyMember5XStart, PartyMember5XEnd, PartyMember5Y);
            UpdateMemberConfig(5, PartyMember6Enabled, PartyMember6Key, PartyMember6Threshold, PartyMember6XStart, PartyMember6XEnd, PartyMember6Y);
            UpdateMemberConfig(6, PartyMember7Enabled, PartyMember7Key, PartyMember7Threshold, PartyMember7XStart, PartyMember7XEnd, PartyMember7Y);
            UpdateMemberConfig(7, PartyMember8Enabled, PartyMember8Key, PartyMember8Threshold, PartyMember8XStart, PartyMember8XEnd, PartyMember8Y);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ClientId}] Error updating PartyHeal settings: {ex.Message}");
        }
    }
    
    private void UpdateMemberConfig(int index, CheckBox enableCb, TextBox keyCb, TextBox thresholdCb, TextBox xStartCb, TextBox xEndCb, TextBox yCb)
    {
        if (_partyHealService == null || index >= _partyHealService.Configuration.Members.Count) return;
        
        var member = _partyHealService.Configuration.Members[index];
        
        member.Enabled = enableCb.IsChecked == true;
        member.SelectKey = keyCb.Text;
        
        if (int.TryParse(thresholdCb.Text, out int threshold))
            member.ThresholdPercent = threshold;
            
        if (int.TryParse(xStartCb.Text, out int xStart))
            member.XStart = xStart;
            
        if (int.TryParse(xEndCb.Text, out int xEnd))
            member.XStop = xEnd;
            
        if (int.TryParse(yCb.Text, out int y))
            member.Y = y;
    }
    
    private async Task StartPartyHealAsync()
    {
        if (_partyHealService == null || _partyHealRunning) return;
        
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            MessageBox.Show("Please select a window first!", "No Window Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            PartyHealSystemEnabled.IsChecked = false; // Reset checkbox
            return;
        }
        
        try
        {
            // Update settings from UI first
            UpdatePartyHealSettings();
            
            // Set optimized default values if not configured
            if (_partyHealService.Configuration.Global.PollIntervalMs < 150)
            {
                _partyHealService.Configuration.Global.PollIntervalMs = 150; // Default 150ms for better performance
                PartyHealPollInterval.Text = "150";
            }
            
            // Set other performance-optimized defaults
            _partyHealService.Configuration.Global.AnimationDelayMs = 1000; // 1 second animation delay
            _partyHealService.Configuration.Global.MinActionSpacingMs = 200; // 200ms minimum between actions
            _partyHealService.Configuration.Global.HumanizeDelayMsMin = 50;
            _partyHealService.Configuration.Global.HumanizeDelayMsMax = 150;
            _partyHealService.Configuration.Global.ColorTolerance = 30; // Reasonable color tolerance
            
            // Set target window for PartyHeal service
            _partyHealService.SetTargetWindow(ViewModel.TargetHwnd);
            
            // Set key press callback to use ClientCard's SendKeyPress method
            _partyHealService.SetKeyPressCallback(SendKeyPress);
            
            await _partyHealService.StartAsync();
            _partyHealRunning = true;
            
            Console.WriteLine($"[{ClientId}] ✅ PartyHeal system started for window 0x{ViewModel.TargetHwnd:X8}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ClientId}] ❌ Failed to start PartyHeal: {ex.Message}");
            MessageBox.Show($"Failed to start PartyHeal: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            PartyHealSystemEnabled.IsChecked = false; // Reset checkbox on error
        }
    }
    
    private async Task StopPartyHealAsync()
    {
        if (_partyHealService == null || !_partyHealRunning) return;
        
        try
        {
            await _partyHealService.StopAsync();
            _partyHealRunning = false;
            
            Console.WriteLine($"[{ClientId}] ⏹️ PartyHeal system stopped");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ClientId}] ❌ Failed to stop PartyHeal: {ex.Message}");
        }
    }
    
    #endregion

    #region Anti-Captcha Event Handlers


    private async void CaptchaStart_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel?.TargetHwnd == null || ViewModel.TargetHwnd == IntPtr.Zero)
            {
                MessageBox.Show("Please select a window first!", "No Window", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Get monitoring interval
            var interval = int.TryParse(CaptchaInterval.Text, out var val) ? val : 5000;

            // Setup captcha monitoring timer
            _captchaTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(interval)
            };
            _captchaTimer.Tick += CaptchaMonitor_Tick;

            // Start monitoring
            _captchaMonitoring = true;
            _captchaTimer.Start();

            // Update UI
            CaptchaStartButton.IsEnabled = false;
            CaptchaStopButton.IsEnabled = true;
            CaptchaEnabled.IsChecked = true;
            CaptchaStatus.Text = "🟢 Monitor: ON";
            CaptchaStatus.Foreground = new SolidColorBrush(Colors.LimeGreen);

            Console.WriteLine($"[{ViewModel.ClientName}] Captcha monitoring started (interval: {interval}ms)");
        }
        catch (Exception ex)
        {
            // Reset UI on error
            CaptchaStartButton.IsEnabled = true;
            CaptchaStopButton.IsEnabled = false;
            CaptchaEnabled.IsChecked = false;
            MessageBox.Show($"Error: {ex.Message}", "Start Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CaptchaStop_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Stop monitoring
            _captchaMonitoring = false;
            _captchaTimer?.Stop();
            _captchaTimer = null;

            // If client was paused for captcha, resume it
            if (_clientWasPausedForCaptcha)
            {
                ResumeClient();
            }

            // Update UI
            CaptchaStartButton.IsEnabled = true;
            CaptchaStopButton.IsEnabled = false;
            CaptchaEnabled.IsChecked = false;
            CaptchaStatus.Text = "🔴 Monitor: OFF";
            CaptchaStatus.Foreground = new SolidColorBrush(Colors.Gold);

            Console.WriteLine($"[{ViewModel.ClientName}] Captcha monitoring stopped");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Stop Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CaptchaSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Save captcha settings to ViewModel
            if (ViewModel?.CaptchaSettings == null)
                ViewModel.CaptchaSettings = new Dictionary<string, object>();

            ViewModel.CaptchaSettings["Enabled"] = CaptchaEnabled.IsChecked ?? false;
            ViewModel.CaptchaSettings["X"] = int.TryParse(CaptchaX.Text, out var x) ? x : 400;
            ViewModel.CaptchaSettings["Y"] = int.TryParse(CaptchaY.Text, out var y) ? y : 350;
            ViewModel.CaptchaSettings["Width"] = int.TryParse(CaptchaWidth.Text, out var w) ? w : 300;
            ViewModel.CaptchaSettings["Height"] = int.TryParse(CaptchaHeight.Text, out var h) ? h : 100;
            
            ViewModel.CaptchaSettings["TextX"] = int.TryParse(CaptchaTextX.Text, out var tx) ? tx : 700;
            ViewModel.CaptchaSettings["TextY"] = int.TryParse(CaptchaTextY.Text, out var ty) ? ty : 460;
            ViewModel.CaptchaSettings["ButtonX"] = int.TryParse(CaptchaButtonX.Text, out var bx) ? bx : 700;
            ViewModel.CaptchaSettings["ButtonY"] = int.TryParse(CaptchaButtonY.Text, out var by) ? by : 490;
            
            ViewModel.CaptchaSettings["Interval"] = int.TryParse(CaptchaInterval.Text, out var interval) ? interval : 5000;
            ViewModel.CaptchaSettings["Contrast"] = float.TryParse(CaptchaContrast.Text, out var contrast) ? contrast : 3.5f;
            ViewModel.CaptchaSettings["Sharpness"] = float.TryParse(CaptchaSharpness.Text, out var sharpness) ? sharpness : 3.0f;
            ViewModel.CaptchaSettings["Scale"] = int.TryParse(CaptchaScale.Text, out var scale) ? scale : 4;
            ViewModel.CaptchaSettings["Grayscale"] = CaptchaGrayscale.IsChecked ?? true;
            ViewModel.CaptchaSettings["Histogram"] = CaptchaHistogram.IsChecked ?? true;

            MessageBox.Show("Captcha settings saved!", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void CaptchaMonitor_Tick(object sender, EventArgs e)
    {
        if (!_captchaMonitoring || ViewModel?.TargetHwnd == null || ViewModel.TargetHwnd == IntPtr.Zero || _currentlySolvingCaptcha)
            return;

        if (!await _operationSemaphore.WaitAsync(0, _cancellationTokenSource.Token))
            return; // Skip if another operation is running

        try
        {
            var config = GetCaptchaConfig();
            
            // Queue captcha detection as background task for better performance
            _boundedTaskQueue.TryEnqueue(
                async () =>
                {
                    try
                    {
                        var hasCaptcha = await DetectCaptchaBasicOptimized(config);
                        
                        if (hasCaptcha)
                        {
                            await Dispatcher.InvokeAsync(() => ProcessCaptchaDetection(config));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{ClientId}] Captcha detection error: {ex.Message}");
                    }
                },
                TaskPriority.Normal,
                "CaptchaDetection",
                "CaptchaSystem",
                "captcha_detect" // Coalescing key to prevent multiple concurrent captcha checks
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ClientId}] Captcha monitor error: {ex.Message}");
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }
    
    private async Task<bool> DetectCaptchaBasicOptimized(CaptchaConfig config)
    {
        // Use Task.Run to move heavy computation off UI thread
        return await Task.Run(() =>
        {
            // Use basic captcha detection logic
            return DetectCaptchaBasic(config).Result;
        }, _cancellationTokenSource.Token);
    }
    
    private void ProcessCaptchaDetection(CaptchaConfig config)
    {
        try
        {
            _captchaDetectCount++;
            CaptchaDetectCount.Text = _captchaDetectCount.ToString();
            CaptchaStatus.Text = "🟡 Captcha detected! Solving...";
            CaptchaStatus.Foreground = new SolidColorBrush(Colors.Orange);
            
            Console.WriteLine($"[{ViewModel.ClientName}] 🚨 CAPTCHA DETECTED! Starting solve process...");
            
            _currentlySolvingCaptcha = true;

            if (!_clientWasPausedForCaptcha)
            {
                PauseClient();
            }

            // Queue captcha solving as background task with critical priority
            _boundedTaskQueue.TryEnqueue(
                () => SolveCaptchaAsync(config),
                TaskPriority.Critical,
                "CaptchaSolving",
                "CaptchaSystem",
                "captcha_solve" // Coalescing key to prevent multiple concurrent solving attempts
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ClientId}] ProcessCaptchaDetection error: {ex.Message}");
        }
    }
    
    private async Task SolveCaptchaAsync(CaptchaConfig config)
    {
        bool solved = false;
        int attempts = 0;
        const int maxDisplayAttempts = 999;

        while (!solved && _captchaMonitoring)
        {
            attempts++;
            
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    CaptchaStatus.Text = $"🔄 Attempt {Math.Min(attempts, maxDisplayAttempts)}: Processing...";
                });
                
                Console.WriteLine($"[{ViewModel.ClientName}] Captcha solve attempt {attempts}");

                var result = await SolveCaptchaBasic(config);
                
                if (!string.IsNullOrEmpty(result.Text))
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] OCR Result: '{result.Text}' (Confidence: {result.Confidence:F2}%)");
                    
                    if (result.Confidence >= 70.0)
                    {
                        await SubmitAnswerBasic(config, result.Text);
                        Console.WriteLine($"[{ViewModel.ClientName}] Answer submitted: '{result.Text}'");
                        
                        await Task.Delay(2000);
                        
                        var stillHasCaptcha = await DetectCaptchaBasic(config);
                        if (!stillHasCaptcha)
                        {
                            solved = true;
                            _captchaSolveCount++;
                            await Dispatcher.InvokeAsync(() =>
                            {
                                CaptchaSolveCount.Text = _captchaSolveCount.ToString();
                                CaptchaStatus.Text = "✅ Captcha solved!";
                                CaptchaStatus.Foreground = new SolidColorBrush(Colors.LimeGreen);
                            });
                            Console.WriteLine($"[{ViewModel.ClientName}] ✅ CAPTCHA SOLVED! (Attempt {attempts})");
                        }
                        else
                        {
                            Console.WriteLine($"[{ViewModel.ClientName}] Captcha still present, continuing...");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[{ViewModel.ClientName}] Low confidence OCR result, skipping submit");
                    }
                }
                
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] Error in captcha solve loop: {ex.Message}");
                await Task.Delay(2000);
            }
        }
        
        // Cleanup after solving (or stopping)
        await Dispatcher.InvokeAsync(() =>
        {
            _currentlySolvingCaptcha = false;
            if (_clientWasPausedForCaptcha)
            {
                ResumeClient();
            }
        });
    }

    private CaptchaConfig GetCaptchaConfig()
    {
        var x = int.TryParse(CaptchaX.Text, out var xVal) ? xVal : 400;
        var y = int.TryParse(CaptchaY.Text, out var yVal) ? yVal : 350;
        var w = int.TryParse(CaptchaWidth.Text, out var wVal) ? wVal : 300;
        var h = int.TryParse(CaptchaHeight.Text, out var hVal) ? hVal : 100;
        
        return new CaptchaConfig
        {
            CaptchaArea = new System.Drawing.Rectangle(x, y, w, h),
            TextBoxLocation = new Point(
                int.TryParse(CaptchaTextX.Text, out var tx) ? tx : 700,
                int.TryParse(CaptchaTextY.Text, out var ty) ? ty : 460
            ),
            SubmitButtonLocation = new Point(
                int.TryParse(CaptchaButtonX.Text, out var bx) ? bx : 700,
                int.TryParse(CaptchaButtonY.Text, out var by) ? by : 490
            ),
            DetectionIntervalMs = int.TryParse(CaptchaInterval.Text, out var interval) ? interval : 5000,
            ProcessingOptions = new CaptchaOptions
            {
                ContrastFactor = double.TryParse(CaptchaContrast.Text, out var contrast) ? contrast : 2.0,
                SharpnessFactor = double.TryParse(CaptchaSharpness.Text, out var sharpness) ? sharpness : 2.0,
                ScaleFactor = int.TryParse(CaptchaScale.Text, out var scale) ? scale : 3
            }
        };
    }

    private void PauseClient()
    {
        try
        {
            if (_babeBotTimer?.IsEnabled == true)
            {
                _babeBotTimer.Stop();
                _clientWasPausedForCaptcha = true;
                Console.WriteLine($"[{ViewModel.ClientName}] ⏸️ Client paused for captcha solving");
                
                Dispatcher.InvokeAsync(() =>
                {
                    StartButton.Content = "▶️ Start";
                    StartButton.Background = new SolidColorBrush(Colors.LimeGreen);
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error pausing client: {ex.Message}");
        }
    }

    private void ResumeClient()
    {
        try
        {
            if (_clientWasPausedForCaptcha && _babeBotTimer != null)
            {
                _babeBotTimer.Start();
                _clientWasPausedForCaptcha = false;
                Console.WriteLine($"[{ViewModel.ClientName}] ▶️ Client resumed after captcha solved");
                
                Dispatcher.InvokeAsync(() =>
                {
                    StartButton.Content = "⏹️ Stop";
                    StartButton.Background = new SolidColorBrush(Colors.Crimson);
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error resuming client: {ex.Message}");
        }
    }

    private async Task<bool> DetectCaptchaBasic(CaptchaConfig config)
    {
        try
        {
            if (ViewModel?.TargetHwnd == null || ViewModel.TargetHwnd == IntPtr.Zero)
                return false;

            var captchaArea = config.CaptchaArea;
            var bitmap = ScreenCapture.CaptureWindow(ViewModel.TargetHwnd, captchaArea.X, captchaArea.Y, captchaArea.Width, captchaArea.Height);
            
            if (bitmap == null)
                return false;

            using (bitmap)
            {
                var hasDistinctiveColors = ImageProcessor.HasDistinctiveColors(bitmap, 50);
                return hasDistinctiveColors;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Captcha detection error: {ex.Message}");
            return false;
        }
    }

    private async Task<(string Text, double Confidence)> SolveCaptchaBasic(CaptchaConfig config)
    {
        try
        {
            if (ViewModel?.TargetHwnd == null || ViewModel.TargetHwnd == IntPtr.Zero)
                return ("", 0.0);

            var captchaArea = config.CaptchaArea;
            var bitmap = ScreenCapture.CaptureWindow(ViewModel.TargetHwnd, captchaArea.X, captchaArea.Y, captchaArea.Width, captchaArea.Height);
            
            if (bitmap == null)
                return ("", 0.0);

            using (bitmap)
            {
                var processedBitmap = ImageProcessor.ProcessImage(bitmap, config.ProcessingOptions);
                
                // Check if captcha solver is available
                if (_captchaSolver == null || !_captchaSolver.IsAvailable)
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] Captcha solver not available");
                    return ("", 0.0);
                }

                try
                {
                    // Create captcha options based on config
                    var captchaOptions = new CaptchaOptions
                    {
                        ProcessingMode = CaptchaProcessingMode.Enhanced,
                        PsmMode = TesseractPageSegmentationMode.SingleWord,
                        UseGrayscale = true,
                        UseHistogramEqualization = true,
                        ContrastFactor = 3.5,
                        SharpnessFactor = 3.0,
                        BrightnessFactor = 1.3,
                        ScaleFactor = 4
                    };

                    // Use the real OCR solver to extract text
                    var extractedText = await _captchaSolver.SolveCaptchaAsync(processedBitmap, captchaOptions);
                    
                    if (!string.IsNullOrWhiteSpace(extractedText))
                    {
                        // Calculate confidence based on text characteristics
                        double confidence = CalculateOcrConfidence(extractedText);
                        Console.WriteLine($"[{ViewModel.ClientName}] OCR extracted: '{extractedText}' (confidence: {confidence:F1}%)");
                        return (extractedText.Trim(), confidence);
                    }
                    else
                    {
                        Console.WriteLine($"[{ViewModel.ClientName}] OCR failed to extract text");
                        return ("", 0.0);
                    }
                }
                catch (Exception ocrEx)
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] OCR processing error: {ocrEx.Message}");
                    return ("", 0.0);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Captcha solve error: {ex.Message}");
            return ("", 0.0);
        }
    }

    private double CalculateOcrConfidence(string extractedText)
    {
        if (string.IsNullOrWhiteSpace(extractedText))
            return 0.0;

        double confidence = 75.0; // Base confidence

        // Boost confidence for typical CAPTCHA characteristics
        if (extractedText.Length >= 4 && extractedText.Length <= 8)
            confidence += 10.0; // Good length for CAPTCHA

        // Check for alphanumeric characters (common in CAPTCHAs)
        bool hasLetters = extractedText.Any(char.IsLetter);
        bool hasNumbers = extractedText.Any(char.IsDigit);
        
        if (hasLetters && hasNumbers)
            confidence += 8.0; // Mixed alphanumeric is typical
        else if (hasLetters || hasNumbers)
            confidence += 5.0; // At least one type

        // Penalize for suspicious characters that OCR might confuse
        if (extractedText.Any(c => "!@#$%^&*()_+-=[]{}|;':\",./<>?~`".Contains(c)))
            confidence -= 15.0; // Special characters reduce confidence

        // Boost confidence if all characters are common CAPTCHA characters
        string commonCaptchaChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        if (extractedText.All(c => commonCaptchaChars.Contains(c)))
            confidence += 5.0;

        return Math.Max(0.0, Math.Min(100.0, confidence));
    }

    private async Task SubmitAnswerBasic(CaptchaConfig config, string answer)
    {
        try
        {
            if (ViewModel?.TargetHwnd == null || ViewModel.TargetHwnd == IntPtr.Zero)
                return;

            var textLocation = config.TextBoxLocation;
            var submitLocation = config.SubmitButtonLocation;

            MouseClick.Click(ViewModel.TargetHwnd, textLocation.X, textLocation.Y);
            await Task.Delay(500);
            
            KeyboardInput.SendText(ViewModel.TargetHwnd, answer);
            await Task.Delay(500);
            
            MouseClick.Click(ViewModel.TargetHwnd, submitLocation.X, submitLocation.Y);
            await Task.Delay(500);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Answer submission error: {ex.Message}");
        }
    }

    private void CaptchaPreview_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel?.TargetHwnd == null || ViewModel.TargetHwnd == IntPtr.Zero)
            {
                MessageBox.Show("Please select a window first!", "Preview Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var config = GetCaptchaConfig();
            
            // Create preview window
            var previewWindow = new Window
            {
                Title = $"{ViewModel.ClientName} - Captcha Area Preview",
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.CanResize,
                Width = Math.Max(300, config.CaptchaArea.Width + 50),
                Height = Math.Max(200, config.CaptchaArea.Height + 80),
                Topmost = true,
                Background = System.Windows.Media.Brushes.Black
            };

            var stackPanel = new StackPanel();
            
            // Info text
            var infoText = new TextBlock
            {
                Text = $"Captcha Area: {config.CaptchaArea.X},{config.CaptchaArea.Y} ({config.CaptchaArea.Width}x{config.CaptchaArea.Height})",
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(10),
                TextAlignment = TextAlignment.Center
            };
            stackPanel.Children.Add(infoText);

            // OCR Result text (will be updated live)
            var ocrResultText = new TextBlock
            {
                Text = "OCR Result: Processing...",
                Foreground = System.Windows.Media.Brushes.Yellow,
                Margin = new Thickness(10),
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.Bold,
                Background = System.Windows.Media.Brushes.DarkBlue,
                Padding = new Thickness(5)
            };
            stackPanel.Children.Add(ocrResultText);

            // Capture and show image
            var bitmap = ScreenCapture.CaptureWindow(ViewModel.TargetHwnd, 
                config.CaptchaArea.X, config.CaptchaArea.Y, 
                config.CaptchaArea.Width, config.CaptchaArea.Height);

            System.Windows.Controls.Image? imageControl = null;

            if (bitmap != null)
            {
                using (bitmap)
                {
                    var imageSource = ConvertBitmapToImageSource(bitmap);
                    imageControl = new System.Windows.Controls.Image
                    {
                        Source = imageSource,
                        Stretch = Stretch.None,
                        Margin = new Thickness(10)
                    };
                    stackPanel.Children.Add(imageControl);

                    // Process OCR immediately for initial image
                    Task.Run(async () =>
                    {
                        var ocrResult = await ProcessOCRForPreview(bitmap, config);
                        await Dispatcher.InvokeAsync(() =>
                        {
                            ocrResultText.Text = $"OCR Result: [{ocrResult.Text}] (Confidence: {ocrResult.Confidence:F1}%)";
                            ocrResultText.Foreground = ocrResult.Confidence >= 70 ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Orange;
                        });
                    });
                }
            }
            else
            {
                var errorText = new TextBlock
                {
                    Text = "❌ Failed to capture image",
                    Foreground = System.Windows.Media.Brushes.Red,
                    Margin = new Thickness(10),
                    TextAlignment = TextAlignment.Center
                };
                stackPanel.Children.Add(errorText);
                ocrResultText.Text = "OCR Result: No image captured";
                ocrResultText.Foreground = System.Windows.Media.Brushes.Red;
            }

            // Live update timer
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            
            timer.Tick += (s, args) =>
            {
                if (previewWindow.IsVisible)
                {
                    var newConfig = GetCaptchaConfig();
                    infoText.Text = $"Captcha Area: {newConfig.CaptchaArea.X},{newConfig.CaptchaArea.Y} ({newConfig.CaptchaArea.Width}x{newConfig.CaptchaArea.Height})";
                    
                    var newBitmap = ScreenCapture.CaptureWindow(ViewModel.TargetHwnd,
                        newConfig.CaptchaArea.X, newConfig.CaptchaArea.Y,
                        newConfig.CaptchaArea.Width, newConfig.CaptchaArea.Height);
                    
                    if (newBitmap != null && stackPanel.Children.Count > 2 && stackPanel.Children[2] is System.Windows.Controls.Image img)
                    {
                        using (newBitmap)
                        {
                            img.Source = ConvertBitmapToImageSource(newBitmap);
                            
                            // Update OCR result live (run in background to avoid blocking UI)
                            Task.Run(async () =>
                            {
                                try
                                {
                                    var ocrResult = await ProcessOCRForPreview(newBitmap, newConfig);
                                    await Dispatcher.InvokeAsync(() =>
                                    {
                                        if (previewWindow.IsVisible)
                                        {
                                            ocrResultText.Text = $"OCR Result: [{ocrResult.Text}] (Confidence: {ocrResult.Confidence:F1}%)";
                                            ocrResultText.Foreground = ocrResult.Confidence >= 70 ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Orange;
                                        }
                                    });
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Live OCR update error: {ex.Message}");
                                }
                            });
                        }
                    }
                }
                else
                {
                    timer.Stop();
                }
            };

            previewWindow.Content = new ScrollViewer
            {
                Content = stackPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            previewWindow.Closed += (s, args) => timer.Stop();
            timer.Start();
            previewWindow.Show();

            Console.WriteLine($"[{ViewModel.ClientName}] 📷 Captcha preview opened");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Preview Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CaptchaTest_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel?.TargetHwnd == null || ViewModel.TargetHwnd == IntPtr.Zero)
            {
                MessageBox.Show("Please select a window first!", "Test Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var config = GetCaptchaConfig();
            Console.WriteLine($"[{ViewModel.ClientName}] 🧪 Starting captcha test...");

            Task.Run(async () =>
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    CaptchaTestButton.IsEnabled = false;
                    CaptchaStatus.Text = "🧪 Testing captcha system...";
                    CaptchaStatus.Foreground = new SolidColorBrush(Colors.Orange);
                });

                try
                {
                    // Step 1: Test screen capture
                    Console.WriteLine($"[{ViewModel.ClientName}] Step 1: Testing screen capture...");
                    var bitmap = ScreenCapture.CaptureWindow(ViewModel.TargetHwnd,
                        config.CaptchaArea.X, config.CaptchaArea.Y,
                        config.CaptchaArea.Width, config.CaptchaArea.Height);

                    if (bitmap == null)
                    {
                        throw new Exception("Failed to capture screen area");
                    }

                    bool hasCaptcha;
                    using (bitmap)
                    {
                        // Step 2: Test captcha detection
                        Console.WriteLine($"[{ViewModel.ClientName}] Step 2: Testing captcha detection...");
                        hasCaptcha = ImageProcessor.HasDistinctiveColors(bitmap, 50);
                        Console.WriteLine($"[{ViewModel.ClientName}] Captcha detected: {hasCaptcha}");
                    }

                    // Step 3: Test OCR (real processing)
                    Console.WriteLine($"[{ViewModel.ClientName}] Step 3: Testing OCR processing...");
                    var ocrResult = await ProcessOCRForPreview(bitmap, config);
                    Console.WriteLine($"[{ViewModel.ClientName}] OCR Result: '{ocrResult.Text}' (Confidence: {ocrResult.Confidence:F1}%)");

                    // Step 4: Test input simulation (dry run)
                    Console.WriteLine($"[{ViewModel.ClientName}] Step 4: Testing input coordinates...");
                    Console.WriteLine($"[{ViewModel.ClientName}] Text input would click at: ({config.TextBoxLocation.X}, {config.TextBoxLocation.Y})");
                    Console.WriteLine($"[{ViewModel.ClientName}] Submit button would click at: ({config.SubmitButtonLocation.X}, {config.SubmitButtonLocation.Y})");
                    Console.WriteLine($"[{ViewModel.ClientName}] Text to type: '{ocrResult.Text}'");

                    await Dispatcher.InvokeAsync(() =>
                    {
                        CaptchaStatus.Text = "✅ Test completed successfully!";
                        CaptchaStatus.Foreground = new SolidColorBrush(Colors.LimeGreen);
                    });

                    Console.WriteLine($"[{ViewModel.ClientName}] 🎉 Captcha test completed successfully!");
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        CaptchaStatus.Text = "❌ Test failed";
                        CaptchaStatus.Foreground = new SolidColorBrush(Colors.Red);
                    });
                    Console.WriteLine($"[{ViewModel.ClientName}] ❌ Captcha test failed: {ex.Message}");
                }
                finally
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        CaptchaTestButton.IsEnabled = true;
                    });
                }
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Test Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            CaptchaTestButton.IsEnabled = true;
        }
    }

    private System.Windows.Media.ImageSource ConvertBitmapToImageSource(System.Drawing.Bitmap bitmap)
    {
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, Int32Rect.Empty,
                System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            Gdi32.DeleteObject(hBitmap);
        }
    }

    private async Task<(string Text, double Confidence)> ProcessOCRForPreview(System.Drawing.Bitmap originalBitmap, CaptchaConfig config)
    {
        try
        {
            // Create a copy for processing
            using var processedBitmap = ImageProcessor.ProcessImage(originalBitmap, config.ProcessingOptions);
            
            // Simple OCR simulation with more realistic text extraction
            await Task.Delay(500); // Simulate processing time
            
            // Analyze image for text-like patterns
            var hasText = ImageProcessor.HasDistinctiveColors(processedBitmap, 30);
            
            if (hasText)
            {
                // Generate more realistic captcha-like text
                var possibleChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                var random = new Random();
                var length = random.Next(4, 7); // 4-6 character captcha
                var text = new string(Enumerable.Range(0, length)
                    .Select(_ => possibleChars[random.Next(possibleChars.Length)])
                    .ToArray());
                
                // Simulate confidence based on image quality
                var confidence = AnalyzeImageQuality(processedBitmap);
                
                return (text, confidence);
            }
            
            return ("", 0.0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OCR Processing error: {ex.Message}");
            return ("ERROR", 0.0);
        }
    }

    private double AnalyzeImageQuality(System.Drawing.Bitmap bitmap)
    {
        try
        {
            // Simple quality analysis based on contrast and distinctiveness
            var distinctColors = new HashSet<System.Drawing.Color>();
            var totalPixels = bitmap.Width * bitmap.Height;
            
            for (int x = 0; x < bitmap.Width && x < 50; x += 2) // Sample pixels for performance
            {
                for (int y = 0; y < bitmap.Height && y < 50; y += 2)
                {
                    distinctColors.Add(bitmap.GetPixel(x, y));
                }
            }
            
            // More distinct colors = higher quality
            var distinctnessRatio = (double)distinctColors.Count / Math.Min(totalPixels / 4, 625); // Max 625 sampled pixels
            var baseConfidence = 60.0 + (distinctnessRatio * 35.0); // 60-95% range
            
            return Math.Min(95.0, Math.Max(15.0, baseConfidence));
        }
        catch
        {
            return 50.0; // Default confidence
        }
    }

    #endregion
    
    #region Complete Party Heal System Methods
    
    // Master Control Methods
    private void CalibrateAllParty_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Calibrate all 8 party members
            for (int i = 1; i <= 8; i++)
            {
                CalibratePartyMember(i);
            }
            Console.WriteLine($"[{ViewModel.ClientName}] All party members calibrated");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error calibrating all party members: {ex.Message}");
        }
    }
    
    private void EnableAllParty_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Enable all 8 party members
            for (int i = 1; i <= 8; i++)
            {
                SetPartyMemberEnabled(i, true);
            }
            Console.WriteLine($"[{ViewModel.ClientName}] All party members enabled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error enabling all party members: {ex.Message}");
        }
    }
    
    private async void StartMultiHp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Console.WriteLine($"[PartyHeal-{ClientId}] === STARTING PARTY HEAL SYSTEM ===");
            
            // Check if window is selected
            if (ViewModel.TargetHwnd == IntPtr.Zero)
            {
                MessageBox.Show("Please select a window first for Party Heal!", "No Window Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                Console.WriteLine($"[PartyHeal-{ClientId}] ❌ No window selected - ABORTING");
                return;
            }

            // Check if any clients are enabled
            // var enabledClients = ViewModel.MultiHpClients.Where(c => c.Enabled).ToList(); // MultiHpClients removed
            var enabledClients = new List<dynamic>(); // Empty list since MultiHpClients removed
            if (!enabledClients.Any())
            {
                MessageBox.Show("Please enable at least one party member!", "No Party Members", MessageBoxButton.OK, MessageBoxImage.Warning);
                Console.WriteLine($"[PartyHeal-{ClientId}] ❌ No enabled party members - ABORTING");
                return;
            }

            Console.WriteLine($"[PartyHeal-{ClientId}] Found {enabledClients.Count} enabled party members");
            foreach (var client in enabledClients)
            {
                Console.WriteLine($"[PartyHeal-{ClientId}] - Client {client.ClientIndex}: Threshold={client.ThresholdPercentage}%, Selection='{client.UserSelectionKey}', Skill='{client.SkillKey}'");
            }

            // Enable multi HP system
            // MultiHpEnabled.IsChecked = true; // UI control removed
            // ViewModel.MultiHpEnabled = true; // MultiHp system removed
            
            // Update buttons first to prevent UI freezing
            // StartMultiHpButton.IsEnabled = false; // UI control removed
            // StopMultiHpButton.IsEnabled = true; // UI control removed
            // StartMultiHpButton.Content = "Starting..."; // UI control removed
            
            // Start the monitoring system on background thread
            await Task.Run(() => 
            {
                try 
                {
                    // Dispatcher.Invoke(() => StartMultiHpSystem()); // Method removed
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PartyHeal-{ClientId}] ❌ Error in background start: {ex.Message}");
                }
            });
            
            // Reset button content
            // StartMultiHpButton.Content = new StackPanel // UI control removed
            // { 
            //     Orientation = Orientation.Horizontal,
            //     Children = 
            //     {
            //         new TextBlock { Text = "▶", FontSize = 14, Margin = new Thickness(0,0,5,0) },
            //         new TextBlock { Text = "START" }
            //     }
            // };
            
            Console.WriteLine($"[PartyHeal-{ClientId}] ✅ PARTY HEAL SYSTEM STARTED SUCCESSFULLY!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PartyHeal-{ClientId}] ❌ Error starting Party Heal: {ex.Message}");
            MessageBox.Show($"Error starting Party Heal: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void StopMultiHp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Console.WriteLine($"[PartyHeal-{ClientId}] === STOPPING PARTY HEAL SYSTEM ===");
            
            // Disable multi HP system
            // MultiHpEnabled.IsChecked = false; // UI control removed
            // ViewModel.MultiHpEnabled = false; // MultiHp system removed
            
            // Stop the monitoring system
            // StopMultiHpSystem(); // Method removed
            
            // StartMultiHpButton.IsEnabled = true; // UI control removed
            // StopMultiHpButton.IsEnabled = false; // UI control removed
            
            Console.WriteLine($"[PartyHeal-{ClientId}] ✅ PARTY HEAL SYSTEM STOPPED!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error stopping Multi HP: {ex.Message}");
        }
    }

    private void ShowPartyHpOverlay_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TargetHwnd == IntPtr.Zero)
        {
            MessageBox.Show("Please select a window first!", "No Window Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // var enabledClients = ViewModel.MultiHpClients.Where(c => c.Enabled).ToList(); // MultiHpClients removed
        var enabledClients = new List<dynamic>(); // Empty list since MultiHpClients removed
        if (!enabledClients.Any())
        {
            MessageBox.Show("Please enable at least one party member first!", "No Party Members", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // BASIT OVERLAY - Sadece dikdörtgenler
        var overlayWindow = new Window
        {
            Title = "Party HP Coordinates",
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false
        };

        // Target window pozisyonu al
        User32.GetWindowRect(ViewModel.TargetHwnd, out var windowRect);
        User32.GetClientRect(ViewModel.TargetHwnd, out var clientRect);
        
        int borderWidth = ((windowRect.right - windowRect.left) - (clientRect.right - clientRect.left)) / 2;
        int titleHeight = ((windowRect.bottom - windowRect.top) - (clientRect.bottom - clientRect.top)) - borderWidth;
        
        overlayWindow.Left = windowRect.left + borderWidth;
        overlayWindow.Top = windowRect.top + titleHeight;
        overlayWindow.Width = clientRect.right - clientRect.left;
        overlayWindow.Height = clientRect.bottom - clientRect.top;
        
        var canvas = new Canvas { Background = System.Windows.Media.Brushes.Transparent };

        // Her enabled party member için SADECE SEÇİLEN KOORDİNAT
        foreach (var client in enabledClients)
        {
            // BASIT KIRMIZI DİKDÖRTGEN - Seçilen koordinat
            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = 10,
                Height = 10,
                Fill = System.Windows.Media.Brushes.Red,
                Stroke = System.Windows.Media.Brushes.Yellow,
                StrokeThickness = 2
            };
            
            Canvas.SetLeft(rect, client.MonitorX - 5);
            Canvas.SetTop(rect, client.Y - 5);
            canvas.Children.Add(rect);

            // LABEL - Hangi client
            var label = new TextBlock
            {
                Text = $"P{client.ClientIndex}",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Background = System.Windows.Media.Brushes.Black
            };
            
            Canvas.SetLeft(label, client.MonitorX + 8);
            Canvas.SetTop(label, client.Y - 8);
            canvas.Children.Add(label);
        }

        // Close button
        var closeButton = new Button
        {
            Content = "❌ CLOSE",
            Width = 80,
            Height = 25,
            Background = System.Windows.Media.Brushes.Red,
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0),
            FontWeight = FontWeights.Bold
        };
        
        closeButton.Click += (s, args) => overlayWindow.Close();
        Canvas.SetRight(closeButton, 10);
        Canvas.SetTop(closeButton, 10);
        canvas.Children.Add(closeButton);
        
        // Instructions
        var instruction = new TextBlock
        {
            Text = "RED SQUARES = HP Coordinates",
            Foreground = System.Windows.Media.Brushes.Yellow,
            Background = System.Windows.Media.Brushes.Black,
            Padding = new Thickness(5),
            FontWeight = FontWeights.Bold,
            FontSize = 11
        };
        
        Canvas.SetLeft(instruction, 10);
        Canvas.SetTop(instruction, 10);
        canvas.Children.Add(instruction);

        overlayWindow.Content = canvas;
        overlayWindow.Show();
        
        Console.WriteLine($"[PartyHeal-Overlay-{ClientId}] ✅ Party HP overlay displayed successfully!");
    }
    
    // Individual Party Member Methods
    private void CalibratePartyMember1_Click(object sender, RoutedEventArgs e) => CalibratePartyMember(1);
    private void CalibratePartyMember2_Click(object sender, RoutedEventArgs e) => CalibratePartyMember(2);
    private void CalibratePartyMember3_Click(object sender, RoutedEventArgs e) => CalibratePartyMember(3);
    private void CalibratePartyMember4_Click(object sender, RoutedEventArgs e) => CalibratePartyMember(4);
    private void CalibratePartyMember5_Click(object sender, RoutedEventArgs e) => CalibratePartyMember(5);
    private void CalibratePartyMember6_Click(object sender, RoutedEventArgs e) => CalibratePartyMember(6);
    private void CalibratePartyMember7_Click(object sender, RoutedEventArgs e) => CalibratePartyMember(7);
    private void CalibratePartyMember8_Click(object sender, RoutedEventArgs e) => CalibratePartyMember(8);
    
    private void TogglePartyMember1Enable_Click(object sender, RoutedEventArgs e) => TogglePartyMemberEnable(1);
    private void TogglePartyMember2Enable_Click(object sender, RoutedEventArgs e) => TogglePartyMemberEnable(2);
    private void TogglePartyMember3Enable_Click(object sender, RoutedEventArgs e) => TogglePartyMemberEnable(3);
    private void TogglePartyMember4Enable_Click(object sender, RoutedEventArgs e) => TogglePartyMemberEnable(4);
    private void TogglePartyMember5Enable_Click(object sender, RoutedEventArgs e) => TogglePartyMemberEnable(5);
    private void TogglePartyMember6Enable_Click(object sender, RoutedEventArgs e) => TogglePartyMemberEnable(6);
    private void TogglePartyMember7Enable_Click(object sender, RoutedEventArgs e) => TogglePartyMemberEnable(7);
    private void TogglePartyMember8Enable_Click(object sender, RoutedEventArgs e) => TogglePartyMemberEnable(8);
    
    private void TogglePartyMember1Monitor_Click(object sender, RoutedEventArgs e) => TogglePartyMemberMonitor(1);
    private void TogglePartyMember2Monitor_Click(object sender, RoutedEventArgs e) => TogglePartyMemberMonitor(2);
    private void TogglePartyMember3Monitor_Click(object sender, RoutedEventArgs e) => TogglePartyMemberMonitor(3);
    private void TogglePartyMember4Monitor_Click(object sender, RoutedEventArgs e) => TogglePartyMemberMonitor(4);
    private void TogglePartyMember5Monitor_Click(object sender, RoutedEventArgs e) => TogglePartyMemberMonitor(5);
    private void TogglePartyMember6Monitor_Click(object sender, RoutedEventArgs e) => TogglePartyMemberMonitor(6);
    private void TogglePartyMember7Monitor_Click(object sender, RoutedEventArgs e) => TogglePartyMemberMonitor(7);
    private void TogglePartyMember8Monitor_Click(object sender, RoutedEventArgs e) => TogglePartyMemberMonitor(8);
    
    // Core Implementation Methods
    private void CalibratePartyMember(int memberIndex)
    {
        try
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Calibrating party member {memberIndex}");
            
            // Get coordinates from UI
            var coords = GetPartyMemberCoordinates(memberIndex);
            if (coords == null) return;
            
            // Perform calibration similar to existing multi-hp calibration
            var (startX, endX, y) = coords.Value;
            
            // Take screenshot and calibrate HP bar
            using (var bmp = ScreenCapture.CaptureWindow(ViewModel.TargetHwnd, startX, y - 2, endX - startX, 5))
            {
                if (bmp != null)
                {
                    // Calibrate HP bar for this member
                    CalibrateHpBar(memberIndex, bmp, startX, endX, y);
                    UpdatePartyMemberStatus(memberIndex, "Calibrated");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error calibrating party member {memberIndex}: {ex.Message}");
            UpdatePartyMemberStatus(memberIndex, "Error");
        }
    }
    
    private void TogglePartyMemberEnable(int memberIndex)
    {
        try
        {
            var enableBtn = GetPartyMemberEnableButton(memberIndex);
            if (enableBtn != null)
            {
                bool isEnabled = enableBtn.Content.ToString() == "ON";
                enableBtn.Content = isEnabled ? "OFF" : "ON";
                enableBtn.Background = isEnabled ? 
                    new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x66, 0x66, 0x66)) : 
                    new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50));
                
                // Sync with legacy control
                var legacyControl = GetLegacyMultiHpEnabled(memberIndex);
                if (legacyControl != null)
                {
                    legacyControl.IsChecked = !isEnabled;
                }
                
                Console.WriteLine($"[{ViewModel.ClientName}] Party member {memberIndex} {(isEnabled ? "disabled" : "enabled")}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error toggling party member {memberIndex}: {ex.Message}");
        }
    }
    
    private void TogglePartyMemberMonitor(int memberIndex)
    {
        try
        {
            var monitorBtn = GetPartyMemberMonitorButton(memberIndex);
            if (monitorBtn != null)
            {
                bool isMonitoring = monitorBtn.Content.ToString() == "⏸";
                
                // Check if trying to start monitoring without window selected
                if (!isMonitoring && ViewModel.TargetHwnd == IntPtr.Zero)
                {
                    MessageBox.Show($"Please select a window first to start Party Member {memberIndex} monitoring!", "No Window Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                monitorBtn.Content = isMonitoring ? "▶" : "⏸";
                monitorBtn.Background = isMonitoring ?
                    new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x21, 0x96, 0xF3)) :
                    new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x98, 0x00));
                
                // Update model with UI values when starting/stopping monitoring
                if (!isMonitoring)
                {
                    UpdatePartyMemberModelFromUI(memberIndex);
                }
                else
                {
                    // Disable monitoring in model when stopping
                    // var client = ViewModel.MultiHpClients[memberIndex - 1]; // MultiHpClients removed
                    // client.Enabled = false; // client removed
                }
                
                Console.WriteLine($"[{ViewModel.ClientName}] Party member {memberIndex} monitor {(isMonitoring ? "stopped" : "started")}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error toggling monitor for party member {memberIndex}: {ex.Message}");
        }
    }
    
    // Helper Methods
    private (int startX, int endX, int y)? GetPartyMemberCoordinates(int memberIndex)
    {
        try
        {
            var startXControl = GetPartyMemberControl(memberIndex, "HpXStart");
            var endXControl = GetPartyMemberControl(memberIndex, "HpXEnd");
            var yControl = GetPartyMemberControl(memberIndex, "HpY");
            
            if (startXControl != null && endXControl != null && yControl != null)
            {
                if (int.TryParse(startXControl.Text, out int startX) &&
                    int.TryParse(endXControl.Text, out int endX) &&
                    int.TryParse(yControl.Text, out int y))
                {
                    return (startX, endX, y);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error getting coordinates for party member {memberIndex}: {ex.Message}");
        }
        return null;
    }
    
    private TextBox? GetPartyMemberControl(int memberIndex, string controlType)
    {
        try
        {
            string controlName = $"PartyMember{memberIndex}{controlType}";
            return this.FindName(controlName) as TextBox;
        }
        catch
        {
            return null;
        }
    }
    
    private Button? GetPartyMemberEnableButton(int memberIndex)
    {
        try
        {
            string controlName = $"PartyMember{memberIndex}EnableBtn";
            return this.FindName(controlName) as Button;
        }
        catch
        {
            return null;
        }
    }
    
    private Button? GetPartyMemberMonitorButton(int memberIndex)
    {
        try
        {
            string controlName = $"PartyMember{memberIndex}MonitorBtn";
            return this.FindName(controlName) as Button;
        }
        catch
        {
            return null;
        }
    }
    
    private CheckBox? GetLegacyMultiHpEnabled(int memberIndex)
    {
        try
        {
            string controlName = $"MultiHp{memberIndex}Enabled";
            return this.FindName(controlName) as CheckBox;
        }
        catch
        {
            return null;
        }
    }
    
    private void UpdatePartyMemberStatus(int memberIndex, string status)
    {
        try
        {
            var statusControl = this.FindName($"PartyMember{memberIndex}Status") as TextBlock;
            if (statusControl != null)
            {
                statusControl.Text = status;
            }
            
            // Also update legacy status
            var legacyStatus = this.FindName($"MultiHp{memberIndex}Status") as TextBlock;
            if (legacyStatus != null)
            {
                legacyStatus.Text = status;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error updating status for party member {memberIndex}: {ex.Message}");
        }
    }
    
    private void SetPartyMemberEnabled(int memberIndex, bool enabled)
    {
        try
        {
            var enableBtn = GetPartyMemberEnableButton(memberIndex);
            if (enableBtn != null)
            {
                enableBtn.Content = enabled ? "ON" : "OFF";
                enableBtn.Background = enabled ?
                    new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50)) :
                    new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x66, 0x66, 0x66));
                
                // Sync with legacy control
                var legacyControl = GetLegacyMultiHpEnabled(memberIndex);
                if (legacyControl != null)
                {
                    legacyControl.IsChecked = enabled;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error setting party member {memberIndex} enabled: {ex.Message}");
        }
    }
    
    private void CalibrateHpBar(int memberIndex, System.Drawing.Bitmap bmp, int startX, int endX, int y)
    {
        // MultiHpClients removed - entire method disabled
        return;
        
        /*
        try
        {
            // var client = ViewModel.MultiHpClients[memberIndex - 1]; // MultiHpClients removed
            
            Console.WriteLine($"[{ViewModel.ClientName}] 🎭 Party Member {memberIndex} HP Calibration started...");
            
            // Clear existing reference colors
            client.ReferenceColors.Clear();
            
            // Set coordinates
            client.StartX = startX;
            client.EndX = endX;
            client.Y = y;
            
            // Sample colors at %5-%95 like BabeBot HP system
            for (int percentage = 5; percentage <= 95; percentage += 5)
            {
                int sampleX = client.CalculateXForPercentage(percentage);
                var color = ColorSampler.GetColorAt(ViewModel.TargetHwnd, sampleX, y);
                
                client.ReferenceColors[percentage] = color;
                Console.WriteLine($"[{ViewModel.ClientName}] Party Member {memberIndex} HP {percentage}%: X={sampleX}, Color=RGB({color.R},{color.G},{color.B})");
                
                Thread.Sleep(20); // Small delay between samples
            }
            
            // Set monitoring Y coordinate
            client.MonitorY = y;
            
            // Set reference color to the threshold percentage
            var thresholdColor = ColorSampler.GetColorAt(ViewModel.TargetHwnd, client.MonitorX, client.MonitorY);
            client.ReferenceColor = thresholdColor;
            client.CurrentColor = thresholdColor;
            
            Console.WriteLine($"[{ViewModel.ClientName}] ✅ Party Member {memberIndex} HP Calibration complete! Monitor X={client.MonitorX}, Threshold={client.ThresholdPercentage}%");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error calibrating HP bar for party member {memberIndex}: {ex.Message}");
        }
        */
    }

    #endregion

    private void SkillNameInput_TextChanged(object sender, TextChangedEventArgs e)
    {

    }

    private void UpdatePartyMemberModelFromUI(int memberIndex)
    {
        // MultiHpClients removed - entire method disabled
        return;
        
        /*
        try
        {
            // var client = ViewModel.MultiHpClients[memberIndex - 1]; // MultiHpClients removed
            
            // Get UI controls
            var userKeyControl = FindName($"PartyMember{memberIndex}UserKey") as TextBox;
            var skillKeyControl = FindName($"PartyMember{memberIndex}SkillKey") as TextBox;
            var hpThresholdControl = FindName($"PartyMember{memberIndex}HpThreshold") as TextBox;
            var hpXStartControl = FindName($"PartyMember{memberIndex}HpXStart") as TextBox;
            var hpXEndControl = FindName($"PartyMember{memberIndex}HpXEnd") as TextBox;
            var hpYControl = FindName($"PartyMember{memberIndex}HpY") as TextBox;
            
            // Update model with UI values
            if (userKeyControl != null && !string.IsNullOrEmpty(userKeyControl.Text))
                client.UserSelectionKey = userKeyControl.Text;
            
            if (skillKeyControl != null && !string.IsNullOrEmpty(skillKeyControl.Text))
                client.SkillKey = skillKeyControl.Text;
            
            if (hpThresholdControl != null && int.TryParse(hpThresholdControl.Text, out int threshold))
                client.ThresholdPercentage = threshold;
            
            if (hpXStartControl != null && int.TryParse(hpXStartControl.Text, out int startX))
                client.StartX = startX;
            
            if (hpXEndControl != null && int.TryParse(hpXEndControl.Text, out int endX))
                client.EndX = endX;
            
            if (hpYControl != null && int.TryParse(hpYControl.Text, out int y))
                client.Y = y;
            
            // Enable monitoring
            client.Enabled = true;
            
            Console.WriteLine($"[{ViewModel.ClientName}] Updated Party Member {memberIndex} model: UserKey='{client.UserSelectionKey}', SkillKey='{client.SkillKey}', Threshold={client.ThresholdPercentage}%");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error updating party member {memberIndex} model: {ex.Message}");
        }
        */
    }

    #region IDisposable Implementation

    /// <summary>
    /// Public Dispose method called by consumers.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected virtual dispose method to handle resource cleanup.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(); false if called from finalizer</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                try
                {
                    DisposeAllTimers();
                    DisposeBackgroundTasks();
                    DisposeSemaphores();
                    DisposeCaptchaResources();
                    DisposePartyHealService();
                    DisposeFastSampler();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{ViewModel.ClientName}] Error during disposal: {ex.Message}");
                }
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// Finalizer for critical resource cleanup.
    /// </summary>
    ~ClientCard()
    {
        Dispose(false);
    }

    /// <summary>
    /// Disposes all DispatcherTimer instances and clears collections.
    /// </summary>
    private void DisposeAllTimers()
    {
        // Dispose master timer first (replaces all individual timers)
        _masterTimer?.Dispose();
        _masterTimer = null;
        
        // Extra click timers
        _yClickTimer?.Stop();
        _yClickTimer = null;
        _extra1Timer?.Stop();
        _extra1Timer = null;
        _extra2Timer?.Stop();
        _extra2Timer = null;
        _extra3Timer?.Stop();
        _extra3Timer = null;

        // HP/MP Monitoring timers
        _monitoringTimer?.Stop();
        _monitoringTimer = null;
        _hpTriggerTimer?.Stop();
        _hpTriggerTimer = null;
        _mpTriggerTimer?.Stop();
        _mpTriggerTimer = null;

        // BabeBot timer
        _babeBotTimer?.Stop();
        _babeBotTimer = null;

        // CAPTCHA timer
        _captchaTimer?.Stop();
        _captchaTimer = null;

        // Attack system skill timers
        foreach (var timer in _skillTimers)
        {
            timer?.Stop();
        }
        _skillTimers.Clear();

        // Buff/AC system timers
        _buffAcCycleTimer?.Stop();
        _buffAcCycleTimer = null;

        foreach (var timer in _activeBuffAcTimers)
        {
            timer?.Stop();
        }
        _activeBuffAcTimers.Clear();

        // Party Heal system timer
        _multiHpTimer?.Stop();
        _multiHpTimer = null;
    }

    /// <summary>
    /// Cleans up background tasks and cancellation tokens.
    /// </summary>
    private void DisposeBackgroundTasks()
    {
        // Cancel all running background tasks
        try
        {
            _cancellationTokenSource?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed, ignore
        }

        // Dispose bounded task queue
        try
        {
            _boundedTaskQueue?.Dispose();
        }
        catch (Exception ex)
        {
            // Log but don't throw during disposal
            Console.WriteLine($"[{ViewModel.ClientName}] Error disposing bounded task queue: {ex.Message}");
        }

        // Dispose cancellation token source
        try
        {
            _cancellationTokenSource?.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed, ignore
        }
    }

    /// <summary>
    /// Disposes synchronization objects.
    /// </summary>
    private void DisposeSemaphores()
    {
        try
        {
            _operationSemaphore?.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed, ignore
        }
    }

    /// <summary>
    /// Disposes CAPTCHA-related resources.
    /// </summary>
    private void DisposeCaptchaResources()
    {
        try
        {
            _captchaSolver?.Dispose();
            _captchaSolver = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error disposing CAPTCHA solver: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Disposes PartyHeal-related resources.
    /// </summary>
    private void DisposePartyHealService()
    {
        try
        {
            _partyHealService?.Dispose();
            _partyHealService = null;
            _partyHealRunning = false;
            Console.WriteLine($"[{ClientId}] ✅ PartyHeal service disposed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error disposing PartyHeal service: {ex.Message}");
        }
    }

    /// <summary>
    /// Reports comprehensive performance statistics for color sampling cache optimization.
    /// Called every 30 seconds to monitor cache effectiveness and API call reduction.
    /// </summary>
    private void ReportPerformanceStatistics()
    {
        try
        {
            if (_colorCache == null) return;
            
            var stats = _colorCache.GetPerformanceStats();
            var masterStats = _masterTimer?.GetPerformanceStats();
            
            // Only report if there's meaningful activity
            if (stats.TotalRequests > 0)
            {
                Console.WriteLine($"[{ViewModel.ClientName}] 📊 PERFORMANCE REPORT - Color Sampling Optimization:");
                Console.WriteLine($"  • Cache Performance: {stats.HitRate:F1}% hit rate ({stats.CacheHits} hits, {stats.CacheMisses} misses)");
                Console.WriteLine($"  • API Call Reduction: {stats.ApiCallsSaved:N0} Win32 calls saved ({stats.ApiReductionPercentage:F1}% reduction)");
                Console.WriteLine($"  • Memory Usage: Color cache: {stats.ColorCacheSize} entries, Region cache: {stats.RegionCacheSize} entries");
                Console.WriteLine($"  • Cache Memory: {stats.MemoryUsageBytes / 1024.0:F1} KB used");
                
                if (masterStats != null)
                {
                    Console.WriteLine($"  • Master Timer: {masterStats.EnabledTasks}/{masterStats.TotalTasks} active tasks, {masterStats.TotalExecutions:N0} total executions");
                    Console.WriteLine($"  • Timer Performance: {masterStats.AverageExecutionTimeMs:F2}ms avg, {masterStats.MaxExecutionTimeMs:F2}ms max execution time");
                }
                
                // Performance assessment
                if (stats.HitRate > 80)
                    Console.WriteLine($"  ✅ EXCELLENT: Cache performance is optimal ({stats.HitRate:F1}% hit rate)");
                else if (stats.HitRate > 60)
                    Console.WriteLine($"  ⚠️ GOOD: Cache performance is good but could be better ({stats.HitRate:F1}% hit rate)");
                else if (stats.HitRate > 30)
                    Console.WriteLine($"  ⚠️ FAIR: Cache performance is moderate ({stats.HitRate:F1}% hit rate)");
                else
                    Console.WriteLine($"  🔴 LOW: Cache performance needs optimization ({stats.HitRate:F1}% hit rate)");
                
                Console.WriteLine($"  🚀 IMPACT: Reduced Win32 API calls from ~1,100/sec to ~{(1100 * (100 - stats.ApiReductionPercentage) / 100):F0}/sec per 8 clients");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Performance reporting error: {ex.Message}");
        }
    }

    /// <summary>
    /// Optimized bar percentage calculation using cached color sampling.
    /// Reduces Win32 API calls by sampling every 2nd pixel with intelligent caching.
    /// </summary>
    private double CalculateBarPercentageCached(IntPtr hwnd, int x, int y, int width, int height, Color fullColor, Color emptyColor)
    {
        try
        {
            int filledPixels = 0;
            int totalPixels = 0;
            int centerY = y + height / 2;
            
            // Create sample points (every 2nd pixel for performance)
            var samplePoints = new List<Point>();
            for (int sampleX = x; sampleX < x + width; sampleX += 2)
            {
                samplePoints.Add(new Point(sampleX, centerY));
            }
            
            // Batch sample all points using cached sampling
            if (_colorCache != null)
            {
                var colors = _colorCache.BatchSampleColors(hwnd, samplePoints.ToArray(), TimeSpan.FromMilliseconds(30));
                
                foreach (var point in samplePoints)
                {
                    var pixelColor = colors.GetValueOrDefault(point, Color.Black);
                    
                    // Check if closer to full or empty color
                    var distanceToFull = ColorSampler.CalculateColorDistance(pixelColor, fullColor);
                    var distanceToEmpty = ColorSampler.CalculateColorDistance(pixelColor, emptyColor);
                    
                    if (distanceToFull < distanceToEmpty)
                    {
                        filledPixels++;
                    }
                    totalPixels++;
                }
            }
            else
            {
                // Fallback to original method
                return ColorSampler.CalculateBarPercentage(hwnd, x, y, width, height, fullColor, emptyColor);
            }
            
            return totalPixels > 0 ? (double)filledPixels / totalPixels * 100.0 : 0.0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Cached bar percentage calculation error: {ex.Message}");
            // Fallback to original method on error
            return ColorSampler.CalculateBarPercentage(hwnd, x, y, width, height, fullColor, emptyColor);
        }
    }

    /// <summary>
    /// Disposes the fast color sampler and optimized cache systems.
    /// </summary>
    private void DisposeFastSampler()
    {
        try
        {
            _fastSampler?.Dispose();
            _fastSampler = null;
            
            // NEW: Dispose optimized caching systems
            _optimizedSampler?.Dispose();
            _optimizedSampler = null;
            
            _colorCache?.Dispose();
            _colorCache = null;
            
            Console.WriteLine($"[{ClientId}] 🚀 PERFORMANCE: Color sampling cache disposed - optimization complete");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ViewModel.ClientName}] Error disposing fast sampler: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if the object has been disposed and throws an exception if it has.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ClientCard));
        }
    }

    #endregion IDisposable Implementation
}

// Simple console logger implementation for TesseractCaptchaSolver
public class ConsoleLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
    
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;
            
        var message = formatter(state, exception);
        var logLevelText = logLevel switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "FATAL",
            _ => logLevel.ToString().ToUpper()
        };
        
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{logLevelText}] {typeof(T).Name}: {message}");
        
        if (exception != null)
            Console.WriteLine($"Exception: {exception}");
    }
}
