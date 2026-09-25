namespace BTDeviceBatteryInfo.Tests;

/// <summary>Theme and language dictionaries are swapped at runtime, so every variant must define the same keys.</summary>
public class ResourceParityTests
{
    [Theory]
    [InlineData("Resources/Themes/System.xaml")]
    [InlineData("Resources/Themes/SystemLight.xaml")]
    public void ThemeDefinesSameKeysAsElegantBlack(string theme)
    {
        var expected = RepositoryPaths.ReadKeys("Resources/Themes/ElegantBlack.xaml");
        var actual = RepositoryPaths.ReadKeys(theme);

        Assert.Empty(expected.Except(actual));
        Assert.Empty(actual.Except(expected));
    }

    [Fact]
    public void SpanishDefinesSameStringsAsEnglish()
    {
        var english = RepositoryPaths.ReadKeys("Resources/Strings/Strings.en.xaml");
        var spanish = RepositoryPaths.ReadKeys("Resources/Strings/Strings.es.xaml");

        Assert.NotEmpty(english);
        Assert.Empty(english.Except(spanish));
        Assert.Empty(spanish.Except(english));
    }
}
