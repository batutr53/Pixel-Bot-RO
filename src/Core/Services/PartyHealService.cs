using System.Collections.Concurrent;
using System.Drawing;
using Microsoft.Extensions.Logging;
using PixelAutomation.Core.Interfaces;
using PixelAutomation.Core.Models;
using Core.Services;

namespace PixelAutomation.Core.Services;

public class PartyHealService : IPartyHealService
{
    private readonly ILogger<PartyHealService> _logger;
    private readonly ICaptureBackend _captureBackend;
    private readonly IClickProvider _clickProvider;
    private readonly BoundedTaskQueue _taskQueue;
    private readonly ConcurrentDictionary<int, PartyMemberState> _memberStates = new();
    
    private Timer? _monitoringTimer;
    private IntPtr _targetWindow = IntPtr.Zero;
    private volatile bool _isRunning = false;
    private volatile bool _disposed = false;
    private DateTime _lastHealTime = DateTime.MinValue;
    private int _currentHealingMember = -1;
    private DateTime _healAnimationEndTime = DateTime.MinValue;
    private Action<string>? _keyPressCallback;

    public event EventHandler<PartyMemberHealedEventArgs>? MemberHealed;
    public event EventHandler<PartyHealStatusChangedEventArgs>? StatusChanged;

    public bool IsRunning => _isRunning;
    public PartyHealConfig Configuration { get; set; } = new();

    public PartyHealService(
        ILogger<PartyHealService> logger,
        ICaptureBackend captureBackend,
        IClickProvider clickProvider,
        BoundedTaskQueue taskQueue)
    {
        _logger = logger;
        _captureBackend = captureBackend;
        _clickProvider = clickProvider;
        _taskQueue = taskQueue;
        
        // Initialize member states
        for (int i = 0; i < 8; i++)
        {
            _memberStates[i] = new PartyMemberState { Index = i };
        }
    }

    public async Task StartAsync()
    {
        if (_isRunning) return;
        
        _logger.LogInformation("Starting PartyHeal monitoring");
        
        // Initialize capture backend with target window if available
        if (_targetWindow != IntPtr.Zero)
        {
            var initialized = await _captureBackend.InitializeAsync(_targetWindow);
            if (!initialized)
            {
                _logger.LogWarning("Failed to initialize capture backend with target window");
            }
        }
        
        _isRunning = true;
        
        // Use higher default interval to reduce CPU usage
        var pollInterval = Math.Max(Configuration.Global.PollIntervalMs, 100); // Minimum 100ms
        _monitoringTimer = new Timer(MonitorPartyMembers, null, 
            TimeSpan.Zero, TimeSpan.FromMilliseconds(pollInterval));
        
        StatusChanged?.Invoke(this, new PartyHealStatusChangedEventArgs 
        { 
            IsRunning = true, 
            StatusMessage = "Party healing started" 
        });
    }

    public async Task StopAsync()
    {
        if (!_isRunning) return;
        
        _logger.LogInformation("Stopping PartyHeal monitoring");
        _isRunning = false;
        
        _monitoringTimer?.Dispose();
        _monitoringTimer = null;
        
        _currentHealingMember = -1;
        _healAnimationEndTime = DateTime.MinValue;
        
        StatusChanged?.Invoke(this, new PartyHealStatusChangedEventArgs 
        { 
            IsRunning = false, 
            StatusMessage = "Party healing stopped" 
        });
    }
    
    public void SetTargetWindow(IntPtr hwnd)
    {
        _targetWindow = hwnd;
        _logger.LogInformation("PartyHeal target window set to 0x{Window:X8}", hwnd.ToInt64());
    }
    
    public void SetKeyPressCallback(Action<string> keyPressCallback)
    {
        _keyPressCallback = keyPressCallback;
        _logger.LogInformation("PartyHeal key press callback set");
    }

    public async Task<Color> CalibrateBaselineColorAsync(int memberIndex, IntPtr targetWindow)
    {
        if (memberIndex < 0 || memberIndex >= 8)
            throw new ArgumentOutOfRangeException(nameof(memberIndex));

        var member = Configuration.Members[memberIndex];
        if (!member.IsConfigured)
            throw new InvalidOperationException($"Member {memberIndex} is not configured");

        _targetWindow = targetWindow;
        var initialized = await _captureBackend.InitializeAsync(targetWindow);
        if (!initialized)
            throw new InvalidOperationException("Failed to initialize capture backend");

        var thresholdPixel = member.ThresholdPixel;
        var roi = new Rectangle(thresholdPixel.X - 2, thresholdPixel.Y, 5, 1);
        
        using var bitmap = await _captureBackend.CaptureAsync(roi);
        if (bitmap == null)
            throw new InvalidOperationException("Failed to capture screen for calibration");

        // Sample 5x1 area and get average color for anti-aliasing
        var colors = new List<Color>();
        for (int x = 0; x < Math.Min(5, bitmap.Width); x++)
        {
            colors.Add(bitmap.GetPixel(x, 0));
        }

        var avgR = (int)colors.Average(c => c.R);
        var avgG = (int)colors.Average(c => c.G);
        var avgB = (int)colors.Average(c => c.B);
        
        var baselineColor = Color.FromArgb(avgR, avgG, avgB);
        
        _logger.LogInformation("Calibrated baseline color for member {MemberIndex}: {Color}", 
            memberIndex, baselineColor);
        
        return baselineColor;
    }

    public PartyMemberStatus GetMemberStatus(int memberIndex)
    {
        if (!_memberStates.TryGetValue(memberIndex, out var state))
            return new PartyMemberStatus { Index = memberIndex };

        var config = Configuration.Members[memberIndex];
        return new PartyMemberStatus
        {
            Index = memberIndex,
            IsEnabled = config.Enabled,
            LastDetectedColor = state.LastDetectedColor,
            LastColorDistance = state.LastColorDistance,
            LastCheck = state.LastCheck,
            LastHealed = state.LastHealed,
            IsOnCooldown = DateTime.Now < state.NextAvailableTime,
            TotalHeals = state.TotalHeals
        };
    }

    private async void MonitorPartyMembers(object? state)
    {
        if (!_isRunning || _disposed || _targetWindow == IntPtr.Zero)
        {
            _logger.LogDebug("Monitor skipped: running={Running} disposed={Disposed} window={Window:X8}", 
                _isRunning, _disposed, _targetWindow.ToInt64());
            return;
        }

        // Run monitoring on background thread to prevent UI blocking
        await Task.Run(async () =>
        {
            try
            {
                var now = DateTime.Now;
                var enabledMembers = Configuration.Members.Where(m => m.Enabled && m.IsConfigured).ToList();
                
                // Skip this cycle if no members are enabled
                if (!enabledMembers.Any()) return;
                
                // Check if we're still in heal animation delay
                bool inHealAnimation = now < _healAnimationEndTime;
                if (inHealAnimation) return; // Skip check during animation
                
                var membersNeedingHeal = new List<(int index, double distance, DateTime detectedAt)>();

                // Batch capture all member pixels in one operation for efficiency
                var captureResults = new Dictionary<int, Color?>();
                
                // Parallel check for better performance
                await Parallel.ForEachAsync(enabledMembers, new ParallelOptions { MaxDegreeOfParallelism = 2 }, async (member, ct) =>
                {
                    var memberState = _memberStates[member.Index];
                    
                    // Skip if member is on cooldown
                    if (now < memberState.NextAvailableTime)
                        return;

                    var color = await GetPixelColorSafeAsync(member.ThresholdPixel);
                    if (color != null)
                    {
                        lock (captureResults)
                        {
                            captureResults[member.Index] = color;
                        }
                    }
                });
                
                // Process results
                foreach (var kvp in captureResults)
                {
                    var member = enabledMembers.First(m => m.Index == kvp.Key);
                    var memberState = _memberStates[member.Index];
                    var color = kvp.Value;
                    
                    if (color == null) continue;
                    
                    var distance = CalculateColorDistance(color.Value, Configuration.Global.BaselineColor);
                    
                    memberState.LastDetectedColor = color.Value;
                    memberState.LastColorDistance = distance;
                    memberState.LastCheck = now;
                    
                    // Check if HP is below threshold (color distance > tolerance)
                    if (distance > Configuration.Global.ColorTolerance)
                    {
                        membersNeedingHeal.Add((member.Index, distance, now));
                    }
                }

                // Process healing logic
                if (membersNeedingHeal.Count > 0)
                {
                    await ProcessHealingQueue(membersNeedingHeal, inHealAnimation);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in party heal monitoring cycle");
            }
        });
    }

    private async Task ProcessHealingQueue(List<(int index, double distance, DateTime detectedAt)> needsHealing, bool inHealAnimation)
    {
        var now = DateTime.Now;
        
        // Sort by priority: higher distance (lower HP) = higher priority
        var prioritized = needsHealing.OrderByDescending(x => x.distance).ToList();
        
        // Determine who to heal
        int targetMember;
        
        if (Configuration.Global.PreemptEnabled && _currentHealingMember >= 0)
        {
            // Check if any member has significantly lower HP than current target
            var currentTarget = needsHealing.FirstOrDefault(x => x.index == _currentHealingMember);
            var highestPriority = prioritized.First();
            
            // Preempt if new target has >10 more color distance (significantly lower HP)
            if (highestPriority.distance > currentTarget.distance + 10)
            {
                targetMember = highestPriority.index;
                _logger.LogDebug("Preempting heal: member {Old} -> {New} (distance: {OldDist} -> {NewDist})",
                    _currentHealingMember, targetMember, currentTarget.distance, highestPriority.distance);
            }
            else
            {
                targetMember = _currentHealingMember;
            }
        }
        else
        {
            targetMember = prioritized.First().index;
        }

        // Check action spacing
        if (now - _lastHealTime < TimeSpan.FromMilliseconds(Configuration.Global.MinActionSpacingMs))
            return;

        // Execute heal
        await ExecuteHealSequence(targetMember);
    }

    private async Task ExecuteHealSequence(int memberIndex)
    {
        var member = Configuration.Members[memberIndex];
        var memberState = _memberStates[memberIndex];
        var now = DateTime.Now;

        try
        {
            _currentHealingMember = memberIndex;
            _lastHealTime = now;

            Console.WriteLine($"[PartyHeal] 🎯 HEALING Member {memberIndex}: SelectKey='{member.SelectKey}' HealKey='{Configuration.Global.SkillKey}'");
            
            if (_keyPressCallback == null)
            {
                Console.WriteLine($"[PartyHeal] ❌ Key press callback not set! Cannot send keys.");
                return;
            }
            
            // 1. Press select key using ClientCard's SendKeyPress method
            Console.WriteLine($"[PartyHeal] 🔹 Pressing SELECT key '{member.SelectKey}' for member {memberIndex}");
            _keyPressCallback.Invoke(member.SelectKey);

            // 2. Humanize delay
            var humanizeDelay = Random.Shared.Next(
                Configuration.Global.HumanizeDelayMsMin,
                Configuration.Global.HumanizeDelayMsMax);
            
            Console.WriteLine($"[PartyHeal] ⏳ Humanize delay: {humanizeDelay}ms");
            await Task.Delay(humanizeDelay);

            // 3. Press heal skill key using ClientCard's SendKeyPress method
            Console.WriteLine($"[PartyHeal] 🔹 Pressing HEAL key '{Configuration.Global.SkillKey}' for member {memberIndex}");
            _keyPressCallback.Invoke(Configuration.Global.SkillKey);

            // 4. Set cooldowns and animation delay
            memberState.LastHealed = now;
            memberState.NextAvailableTime = now.AddMilliseconds(member.RearmMs);
            memberState.TotalHeals++;
            
            _healAnimationEndTime = now.AddMilliseconds(Configuration.Global.AnimationDelayMs);

            // 5. Fire event
            MemberHealed?.Invoke(this, new PartyMemberHealedEventArgs
            {
                MemberIndex = memberIndex,
                Timestamp = now,
                DetectedColor = memberState.LastDetectedColor,
                ColorDistance = memberState.LastColorDistance
            });

            _logger.LogInformation("Healed party member {MemberIndex} (distance: {Distance:F1})", 
                memberIndex, memberState.LastColorDistance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing heal sequence for member {MemberIndex}", memberIndex);
        }
        finally
        {
            // Clear current healing member after animation delay
            _ = Task.Delay(Configuration.Global.AnimationDelayMs).ContinueWith(_ => 
            {
                if (_currentHealingMember == memberIndex)
                    _currentHealingMember = -1;
            });
        }
    }

    private async Task<Color?> GetPixelColorSafeAsync(Point pixel)
    {
        try
        {
            var roi = new Rectangle(pixel.X, pixel.Y, 1, 1);
            using var bitmap = await _captureBackend.CaptureAsync(roi);
            return bitmap?.GetPixel(0, 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get pixel color at {Point}", pixel);
            return null;
        }
    }

    private static double CalculateColorDistance(Color color1, Color color2)
    {
        var dr = color1.R - color2.R;
        var dg = color1.G - color2.G;
        var db = color1.B - color2.B;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        _monitoringTimer?.Dispose();
        _memberStates.Clear();
    }

    private class PartyMemberState
    {
        public int Index { get; set; }
        public Color LastDetectedColor { get; set; }
        public double LastColorDistance { get; set; }
        public DateTime LastCheck { get; set; }
        public DateTime? LastHealed { get; set; }
        public DateTime NextAvailableTime { get; set; }
        public int TotalHeals { get; set; }
    }
}