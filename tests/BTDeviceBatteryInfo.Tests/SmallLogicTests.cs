using BTDeviceBatteryInfo.Services;

namespace BTDeviceBatteryInfo.Tests;

public class ReconnectPolicyTests
{
    [Fact]
    public void ProducesOneDelayPerAttempt() =>
        Assert.Equal(3, ReconnectPolicy.Delays(3, 5).Count());

    [Fact]
    public void NegativeAttemptsProduceNoDelays() =>
        Assert.Empty(ReconnectPolicy.Delays(-1, 5));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 5)]
    [InlineData(1000, 300)]
    public void DelayIsClampedBetweenOneAndThreeHundredSeconds(int seconds, int expected) =>
        Assert.All(ReconnectPolicy.Delays(2, seconds), delay => Assert.Equal(TimeSpan.FromSeconds(expected), delay));
}

public class AppLanguageTests
{
    [Theory]
    [InlineData("es", "es")]
    [InlineData("en", "en")]
    [InlineData(null, "en")]
    [InlineData("", "en")]
    [InlineData("fr", "en")]
    [InlineData("ES", "en")]
    public void UnknownLanguagesFallBackToEnglish(string? language, string expected) =>
        Assert.Equal(expected, AppLanguage.Normalize(language));
}
