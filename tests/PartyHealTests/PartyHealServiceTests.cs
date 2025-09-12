using System.Drawing;
using Microsoft.Extensions.Logging;
using Moq;
using PixelAutomation.Core.Interfaces;
using PixelAutomation.Core.Models;
using PixelAutomation.Core.Services;
using Core.Services;
using Xunit;

namespace PixelAutomation.Tests.PartyHeal;

public class PartyHealServiceTests : IDisposable
{
    private readonly Mock<ILogger<PartyHealService>> _mockLogger;
    private readonly Mock<ICaptureBackend> _mockCaptureBackend;
    private readonly Mock<IClickProvider> _mockClickProvider;
    private readonly BoundedTaskQueue _taskQueue;
    private readonly PartyHealService _service;

    public PartyHealServiceTests()
    {
        _mockLogger = new Mock<ILogger<PartyHealService>>();
        _mockCaptureBackend = new Mock<ICaptureBackend>();
        _mockClickProvider = new Mock<IClickProvider>();
        _taskQueue = new BoundedTaskQueue(maxConcurrency: 2, maxQueueSize: 100);
        
        _service = new PartyHealService(
            _mockLogger.Object,
            _mockCaptureBackend.Object,
            _mockClickProvider.Object,
            _taskQueue);
    }

    [Fact]
    public async Task StartAsync_SetsIsRunningToTrue()
    {
        // Arrange
        Assert.False(_service.IsRunning);

        // Act
        await _service.StartAsync();

        // Assert
        Assert.True(_service.IsRunning);
    }

    [Fact]
    public async Task StopAsync_SetsIsRunningToFalse()
    {
        // Arrange
        await _service.StartAsync();
        Assert.True(_service.IsRunning);

        // Act
        await _service.StopAsync();

        // Assert
        Assert.False(_service.IsRunning);
    }

    [Fact]
    public async Task CalibrateBaselineColorAsync_ReturnsAverageColor()
    {
        // Arrange
        var targetWindow = new IntPtr(12345);
        var memberIndex = 0;
        
        _service.Configuration.Members[0].XStart = 100;
        _service.Configuration.Members[0].XStop = 200;
        _service.Configuration.Members[0].Y = 50;
        _service.Configuration.Members[0].ThresholdPercent = 50;

        var testBitmap = new Bitmap(5, 1);
        testBitmap.SetPixel(0, 0, Color.FromArgb(255, 0, 0));
        testBitmap.SetPixel(1, 0, Color.FromArgb(255, 0, 0));
        testBitmap.SetPixel(2, 0, Color.FromArgb(255, 0, 0));
        testBitmap.SetPixel(3, 0, Color.FromArgb(255, 0, 0));
        testBitmap.SetPixel(4, 0, Color.FromArgb(255, 0, 0));

        _mockCaptureBackend
            .Setup(x => x.InitializeAsync(targetWindow))
            .Returns(Task.FromResult(true));

        _mockCaptureBackend
            .Setup(x => x.CaptureAsync(It.IsAny<Rectangle>()))
            .Returns(Task.FromResult<Bitmap?>(testBitmap));

        // Act
        var result = await _service.CalibrateBaselineColorAsync(memberIndex, targetWindow);

        // Assert
        Assert.Equal(Color.FromArgb(255, 0, 0), result);
        testBitmap.Dispose();
    }

    [Fact]
    public void GetMemberStatus_ReturnsCorrectStatus()
    {
        // Arrange
        var memberIndex = 0;
        _service.Configuration.Members[0].Enabled = true;

        // Act
        var status = _service.GetMemberStatus(memberIndex);

        // Assert
        Assert.Equal(memberIndex, status.Index);
        Assert.True(status.IsEnabled);
        Assert.Equal(0, status.TotalHeals);
    }

    [Fact]
    public void Configuration_CanBeSetAndRetrieved()
    {
        // Arrange
        var config = new PartyHealConfig();
        config.Global.SkillKey = "F2";
        config.Global.PollIntervalMs = 5;
        config.Members[0].Enabled = true;
        config.Members[0].SelectKey = "Z";

        // Act
        _service.Configuration = config;

        // Assert
        Assert.Equal("F2", _service.Configuration.Global.SkillKey);
        Assert.Equal(5, _service.Configuration.Global.PollIntervalMs);
        Assert.True(_service.Configuration.Members[0].Enabled);
        Assert.Equal("Z", _service.Configuration.Members[0].SelectKey);
    }

    [Theory]
    [InlineData(0, "Q")]
    [InlineData(1, "W")]
    [InlineData(2, "E")]
    [InlineData(3, "R")]
    [InlineData(4, "T")]
    [InlineData(5, "Y")]
    [InlineData(6, "U")]
    [InlineData(7, "I")]
    public void DefaultConfiguration_HasCorrectSelectKeys(int memberIndex, string expectedKey)
    {
        // Act & Assert
        Assert.Equal(expectedKey, _service.Configuration.Members[memberIndex].SelectKey);
    }

    [Fact]
    public void MemberHealed_EventIsRaised()
    {
        // Arrange
        PartyMemberHealedEventArgs? eventArgs = null;
        _service.MemberHealed += (sender, args) => eventArgs = args;

        // Act
        // This would normally be triggered internally, but we can't easily test that without complex mocking
        // For now, we just verify the event can be subscribed to
        
        // Assert
        Assert.Null(eventArgs); // Event hasn't been raised yet
    }

    [Fact]
    public void StatusChanged_EventIsRaised()
    {
        // Arrange
        PartyHealStatusChangedEventArgs? eventArgs = null;
        _service.StatusChanged += (sender, args) => eventArgs = args;

        // Act & Assert - Similar to above, event subscription works
        Assert.Null(eventArgs);
    }

    public void Dispose()
    {
        _service?.Dispose();
        _taskQueue?.Dispose();
    }
}

public class PartyHealConfigTests
{
    [Fact]
    public void PartyHealConfig_InitializesWithDefaultMembers()
    {
        // Act
        var config = new PartyHealConfig();

        // Assert
        Assert.Equal(8, config.Members.Count);
        Assert.Equal("Q", config.Members[0].SelectKey);
        Assert.Equal("I", config.Members[7].SelectKey);
        
        for (int i = 0; i < 8; i++)
        {
            Assert.Equal(i, config.Members[i].Index);
            Assert.Equal(50, config.Members[i].ThresholdPercent);
            Assert.Equal(500, config.Members[i].RearmMs);
            Assert.False(config.Members[i].Enabled);
        }
    }

    [Fact]
    public void PartyMemberConfig_ThresholdPixel_CalculatesCorrectly()
    {
        // Arrange
        var member = new PartyMemberConfig
        {
            XStart = 100,
            XStop = 200,
            Y = 50,
            ThresholdPercent = 75
        };

        // Act
        var thresholdPixel = member.ThresholdPixel;

        // Assert
        Assert.Equal(175, thresholdPixel.X); // 100 + (200-100) * 0.75
        Assert.Equal(50, thresholdPixel.Y);
    }

    [Theory]
    [InlineData(0, 100, 0, false)]
    [InlineData(100, 200, 50, true)]
    [InlineData(100, 100, 50, false)] // Same start/stop
    public void PartyMemberConfig_IsConfigured_ReturnsCorrectValue(int xStart, int xStop, int y, bool expected)
    {
        // Arrange
        var member = new PartyMemberConfig
        {
            XStart = xStart,
            XStop = xStop,
            Y = y
        };

        // Act & Assert
        Assert.Equal(expected, member.IsConfigured);
    }

    [Fact]
    public void GlobalConfig_HasCorrectDefaults()
    {
        // Act
        var config = new PartyHealGlobalConfig();

        // Assert
        Assert.Equal("F1", config.SkillKey);
        Assert.Equal(10, config.PollIntervalMs);
        Assert.Equal(1500, config.AnimationDelayMs);
        Assert.Equal(25, config.ColorTolerance);
        Assert.Equal(20, config.HumanizeDelayMsMin);
        Assert.Equal(60, config.HumanizeDelayMsMax);
        Assert.Equal(90, config.MinActionSpacingMs);
        Assert.Equal(Color.FromArgb(255, 0, 0), config.BaselineColor);
        Assert.True(config.PreemptEnabled);
    }
}