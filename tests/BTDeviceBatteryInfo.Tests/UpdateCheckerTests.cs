using BTDeviceBatteryInfo.Services;

namespace BTDeviceBatteryInfo.Tests;

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("0.22", "0.21")]
    [InlineData("0.22", "0.22-dev3")]      // a dev build is lower than its release
    [InlineData("0.22", "0.22-dev10")]
    [InlineData("0.22-dev10", "0.22-dev9")] // dev builds compare by number, not as text
    [InlineData("0.23-dev1", "0.22")]
    [InlineData("1.0", "0.99")]
    [InlineData("0.22.1", "0.22")]
    public void LeftIsNewer(string newer, string older)
    {
        Assert.True(UpdateChecker.CompareVersions(newer, older) > 0);
        Assert.True(UpdateChecker.CompareVersions(older, newer) < 0);
    }

    [Theory]
    [InlineData("0.22", "0.22")]
    [InlineData("0.22", "0.22.0")]
    [InlineData("0.22-dev3", "0.22-dev3")]
    public void EqualVersions(string left, string right) =>
        Assert.Equal(0, UpdateChecker.CompareVersions(left, right));

    [Fact]
    public void CurrentVersionHasNoBuildMetadata() =>
        Assert.DoesNotContain('+', UpdateChecker.CurrentVersion);
}
