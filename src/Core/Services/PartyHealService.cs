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

        // Simple timer like BabeBot HP/MP monitoring
        var pollInterval = Configuration.Global.PollIntervalMs;
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

    public void SetAttackControlCallbacks(Action pauseAttack, Action resumeAttack)
    {
        // Not used in simple version - empty implementation
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

        // Get color at threshold position
        var thresholdPixel = member.ThresholdPixel;
        var roi = new Rectangle(thresholdPixel.X, thresholdPixel.Y, 1, 1);

        using var bitmap = await _captureBackend.CaptureAsync(roi);
        if (bitmap == null)
            throw new InvalidOperationException("Failed to capture screen for calibration");

        var baselineColor = bitmap.GetPixel(0, 0);

        _logger.LogInformation("Calibrated baseline color for member {MemberIndex}: {Color}",
            memberIndex, baselineColor);

        return baselineColor;
    }

    public async Task<(Color fullHpColor, Color currentHpColor)> CalibrateMemberHpColorsAsync(int memberIndex, IntPtr targetWindow)
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

        // Sadece threshold position'dan renk oku
        var thresholdPixel = member.ThresholdPixel;
        var thresholdRoi = new Rectangle(thresholdPixel.X, thresholdPixel.Y, 1, 1);
        using var thresholdBitmap = await _captureBackend.CaptureAsync(thresholdRoi);
        if (thresholdBitmap == null)
            throw new InvalidOperationException("Failed to capture threshold color");

        var currentColor = thresholdBitmap.GetPixel(0, 0);

        Console.WriteLine($"[PartyHeal] Member {memberIndex + 1} Kalibrasyon:");
        Console.WriteLine($"[PartyHeal] Current HP Color: RGB({currentColor.R},{currentColor.G},{currentColor.B}) at ({thresholdPixel.X},{thresholdPixel.Y})");

        // Aynı rengi hem full hem current olarak döndür - basit yaklaşım
        return (currentColor, currentColor);
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
            LastCheck = state.LastCheck,
            LastHealed = state.LastHealed,
            IsOnCooldown = DateTime.Now < state.NextAvailableTime,
            TotalHeals = state.TotalHeals
        };
    }

    // BASİT HEAL LOGIC
    private async void MonitorPartyMembers(object? state)
    {
        if (!_isRunning || _disposed || _targetWindow == IntPtr.Zero)
            return;

        try
        {
            var now = DateTime.Now;

            // Skip if still in heal animation
            if (now < _healAnimationEndTime)
                return;

            var enabledMembers = Configuration.Members.Where(m => m.Enabled && m.IsConfigured).ToList();
            if (!enabledMembers.Any())
                return;

            foreach (var member in enabledMembers)
            {
                var memberState = _memberStates[member.Index];

                // Skip if on cooldown
                if (now < memberState.NextAvailableTime)
                    continue;

                // Get current color at threshold position
                var currentColor = await GetPixelColorSafeAsync(member.ThresholdPixel);
                if (currentColor == null)
                    continue;

                memberState.LastDetectedColor = currentColor.Value;
                memberState.LastCheck = now;

                // ÇOK BASİT: Sadece renk değişimini kontrol et
                bool needsHeal = await CheckNeedsHeal(member);

                if (needsHeal && (now - _lastHealTime >= TimeSpan.FromMilliseconds(Configuration.Global.MinActionSpacingMs)))
                {
                    await ExecuteHealSequence(member.Index);
                    break; // Heal one at a time
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in party heal monitoring");
        }
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

            Console.WriteLine($"[PartyHeal] HEALING Member {memberIndex + 1}: SelectKey='{member.SelectKey}' HealKey='{Configuration.Global.SkillKey}'");

            if (_keyPressCallback == null)
            {
                Console.WriteLine($"[PartyHeal] Key press callback not set!");
                return;
            }

            // 1. Press select key
            Console.WriteLine($"[PartyHeal] Pressing SELECT key '{member.SelectKey}'");
            _keyPressCallback.Invoke(member.SelectKey);

            // 2. Small delay
            var delay = Random.Shared.Next(Configuration.Global.HumanizeDelayMsMin, Configuration.Global.HumanizeDelayMsMax);
            await Task.Delay(delay);

            // 3. Press heal skill key
            Console.WriteLine($"[PartyHeal] Pressing HEAL key '{Configuration.Global.SkillKey}'");
            _keyPressCallback.Invoke(Configuration.Global.SkillKey);

            // 4. Set cooldowns
            memberState.LastHealed = now;
            memberState.NextAvailableTime = now.AddMilliseconds(member.RearmMs);
            memberState.TotalHeals++;

            _healAnimationEndTime = now.AddMilliseconds(Configuration.Global.AnimationDelayMs);

            // 5. Fire event
            MemberHealed?.Invoke(this, new PartyMemberHealedEventArgs
            {
                MemberIndex = memberIndex,
                Timestamp = now,
                DetectedColor = memberState.LastDetectedColor
            });

            _logger.LogInformation("Healed party member {MemberIndex}", memberIndex + 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing heal sequence for member {MemberIndex}", memberIndex);
        }
        finally
        {
            // Clear current healing member after animation
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


    private async Task<bool> CheckNeedsHeal(PartyMemberConfig member)
    {
        try
        {
            // ÇOK BASİT: Kullanıcının belirlediği % noktasındaki rengi kontrol et
            int barWidth = member.XStop - member.XStart;
            if (barWidth <= 0) return false;

            // Eşik noktasını hesapla (kullanıcı %44 demiş, barın %44'ünde ki nokta)
            int thresholdX = member.XStart + (int)(barWidth * (member.HealThresholdPercent / 100.0));

            // Eşik noktasındaki rengi oku
            var currentColor = await GetPixelColorSafeAsync(new Point(thresholdX, member.Y));
            if (currentColor == null) return false;

            // Eğer member state'inde baseline renk yoksa, şu anki rengi baseline yap
            var memberState = _memberStates[member.Index];
            if (memberState.LastDetectedColor == Color.Empty)
            {
                memberState.LastDetectedColor = currentColor.Value;
                Console.WriteLine($"[PartyHeal] Member {member.Index + 1} Baseline set at {member.HealThresholdPercent}% point (x={thresholdX}): RGB({currentColor.Value.R},{currentColor.Value.G},{currentColor.Value.B})");
                return false; // İlk sefer, heal etme
            }

            // Baseline ile şu anki rengi karşılaştır
            bool colorChanged = !IsColorSameSimple(memberState.LastDetectedColor, currentColor.Value);

            if (colorChanged)
            {
                Console.WriteLine($"[PartyHeal] Member {member.Index + 1} NEEDS HEAL! Color changed at {member.HealThresholdPercent}% point:");
                Console.WriteLine($"[PartyHeal] Baseline: RGB({memberState.LastDetectedColor.R},{memberState.LastDetectedColor.G},{memberState.LastDetectedColor.B})");
                Console.WriteLine($"[PartyHeal] Current:  RGB({currentColor.Value.R},{currentColor.Value.G},{currentColor.Value.B})");

                // ÖNEMLİ: Baseline'ı güncelle ki bir sonraki heal'de doğru çalışsın
                memberState.LastDetectedColor = currentColor.Value;
                Console.WriteLine($"[PartyHeal] Member {member.Index + 1} Baseline updated to new color");

                return true;
            }
            else
            {
                Console.WriteLine($"[PartyHeal] Member {member.Index + 1} HP OK at {member.HealThresholdPercent}% point: RGB({currentColor.Value.R},{currentColor.Value.G},{currentColor.Value.B})");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PartyHeal-Error] Check needs heal failed: {ex.Message}");
            return false;
        }
    }

    private bool ColorsAreSimilar(Color color1, Color color2, int tolerance)
    {
        var dr = Math.Abs(color1.R - color2.R);
        var dg = Math.Abs(color1.G - color2.G);
        var db = Math.Abs(color1.B - color2.B);
        return dr <= tolerance && dg <= tolerance && db <= tolerance;
    }

    // BASİT renk karşılaştırması - senin dediğin gibi
    private bool IsColorSameSimple(Color baseline, Color current)
    {
        var dr = Math.Abs(baseline.R - current.R);
        var dg = Math.Abs(baseline.G - current.G);
        var db = Math.Abs(baseline.B - current.B);

        // Basit tolerance - küçük varyasyonlar normal
        return dr <= 25 && dg <= 25 && db <= 25;
    }

    // HP barı için özel renk benzerlik kontrolü - daha akıllı algoritma
    private bool IsColorSimilarToHP(Color baseline, Color current)
    {
        // RGB farkları
        var dr = Math.Abs(baseline.R - current.R);
        var dg = Math.Abs(baseline.G - current.G);
        var db = Math.Abs(baseline.B - current.B);

        // Toplam fark (Manhattan distance)
        var totalDiff = dr + dg + db;

        // HP barları genelde kırmızı tonlarında, büyük farklar gerçek HP kaybını gösterir
        // Küçük varyasyonlar (toplam < 40) normal, büyük farklar (toplam > 40) HP kaybı
        return totalDiff < 40;
    }


    public async Task<double> GetMemberHpPercentageAsync(int memberIndex)
    {
        if (memberIndex < 0 || memberIndex >= 8)
            return 100.0; // Invalid index, assume full HP

        var member = Configuration.Members[memberIndex];
        if (!member.IsConfigured || !member.Enabled)
            return 100.0; // Not configured or disabled, assume full HP

        if (_targetWindow == IntPtr.Zero)
            return 100.0; // No target window

        try
        {
            // Basit: eğer heal gerekiyorsa %44'ün altında, yoksa %100
            bool needsHeal = await CheckNeedsHeal(member);
            return needsHeal ? (member.HealThresholdPercent - 5) : 100.0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating HP percentage for member {MemberIndex}", memberIndex);
            return 100.0; // Error, assume full HP to prevent unnecessary healing
        }
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
        public DateTime LastCheck { get; set; }
        public DateTime? LastHealed { get; set; }
        public DateTime NextAvailableTime { get; set; }
        public int TotalHeals { get; set; }
    }
}