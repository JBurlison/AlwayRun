using AlwaysRun.Services;
using FluentAssertions;
using Xunit;

namespace AlwaysRun.Tests;

public class BackoffPolicyTests
{
    private readonly BackoffPolicy _sut = new();

    [Theory]
    [InlineData(0, 2)]      // 2^0 * 2 = 2 seconds
    [InlineData(1, 4)]      // 2^1 * 2 = 4 seconds
    [InlineData(2, 8)]      // 2^2 * 2 = 8 seconds
    [InlineData(3, 16)]     // 2^3 * 2 = 16 seconds
    [InlineData(4, 32)]     // 2^4 * 2 = 32 seconds
    [InlineData(5, 64)]     // 2^5 * 2 = 64 seconds
    [InlineData(6, 128)]    // 2^6 * 2 = 128 seconds
    [InlineData(7, 256)]    // 2^7 * 2 = 256 seconds
    public void GetNextDelay_WhenAttemptIncreases_ShouldExponentialIncrease(int attempt, double expectedSeconds)
    {
        // Act
        var delay = _sut.GetNextDelay(attempt, addJitter: false);

        // Assert
        delay.TotalSeconds.Should().Be(expectedSeconds);
    }

    [Fact]
    public void GetNextDelay_WhenExceedsMax_ShouldCapAtMax()
    {
        // Arrange
        var highAttempt = 20; // Would be 2^20 * 2 = ~2 million seconds without cap

        // Act
        var delay = _sut.GetNextDelay(highAttempt, addJitter: false);

        // Assert
        delay.Should().Be(_sut.MaxDelay);
        delay.TotalMinutes.Should().Be(5);
    }

    [Fact]
    public void GetNextDelay_WhenJitterEnabled_ShouldStayWithinBounds()
    {
        // Arrange
        const int attempt = 3;
        var baseDelay = TimeSpan.FromSeconds(16); // 2^3 * 2
        var jitterRange = baseDelay.TotalMilliseconds * _sut.JitterPercentage;
        var minExpected = baseDelay.TotalMilliseconds - jitterRange;
        var maxExpected = baseDelay.TotalMilliseconds + jitterRange;

        // Act & Assert - Run multiple times to account for randomness
        for (int i = 0; i < 100; i++)
        {
            var delay = _sut.GetNextDelay(attempt, addJitter: true);

            delay.TotalMilliseconds.Should().BeGreaterThanOrEqualTo(minExpected);
            delay.TotalMilliseconds.Should().BeLessThanOrEqualTo(maxExpected);
        }
    }

    [Fact]
    public void GetNextDelay_WhenNegativeAttempt_ShouldTreatAsZero()
    {
        // Act
        var delay = _sut.GetNextDelay(-5, addJitter: false);

        // Assert
        delay.TotalSeconds.Should().Be(2); // Same as attempt 0
    }

    [Fact]
    public void ShouldResetAttempts_WhenProcessRunsLongerThanThreshold_ShouldReturnTrue()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow.AddSeconds(-61); // Ran for 61 seconds

        // Act
        var shouldReset = _sut.ShouldResetAttempts(startTime);

        // Assert
        shouldReset.Should().BeTrue();
    }

    [Fact]
    public void ShouldResetAttempts_WhenProcessRunsShorterThanThreshold_ShouldReturnFalse()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow.AddSeconds(-30); // Ran for 30 seconds

        // Act
        var shouldReset = _sut.ShouldResetAttempts(startTime);

        // Assert
        shouldReset.Should().BeFalse();
    }

    [Fact]
    public void GetNextDelay_WhenAtMaxDelay_WithJitter_ShouldNotExceedMax()
    {
        // Arrange
        const int highAttempt = 100;

        // Act & Assert - Run multiple times
        for (int i = 0; i < 100; i++)
        {
            var delay = _sut.GetNextDelay(highAttempt, addJitter: true);
            delay.Should().BeLessThanOrEqualTo(_sut.MaxDelay);
        }
    }

    [Fact]
    public void DefaultValues_ShouldMatchSpecification()
    {
        // Assert
        _sut.InitialDelay.Should().Be(TimeSpan.FromSeconds(2));
        _sut.MaxDelay.Should().Be(TimeSpan.FromMinutes(5));
        _sut.HealthyResetThreshold.Should().Be(TimeSpan.FromSeconds(60));
        _sut.Multiplier.Should().Be(2.0);
        _sut.JitterPercentage.Should().Be(0.1);
    }

    [Theory]
    [InlineData(0, 5)]      // 2^0 * 5 = 5 seconds
    [InlineData(1, 10)]     // 2^1 * 5 = 10 seconds
    [InlineData(2, 20)]     // 2^2 * 5 = 20 seconds
    [InlineData(3, 40)]     // 2^3 * 5 = 40 seconds
    public void GetNextDelay_WhenCustomInitialDelay_ShouldUseCustomBase(int attempt, double expectedSeconds)
    {
        // Arrange
        var customDelay = TimeSpan.FromSeconds(5);

        // Act
        var delay = _sut.GetNextDelay(attempt, addJitter: false, customInitialDelay: customDelay);

        // Assert
        delay.TotalSeconds.Should().Be(expectedSeconds);
    }
}
