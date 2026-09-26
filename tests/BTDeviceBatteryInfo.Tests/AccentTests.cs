using BTDeviceBatteryInfo.Services;

namespace BTDeviceBatteryInfo.Tests;

public class AccentTests
{
    [Theory]
    [InlineData(0x99, 0xEB, 0xFF, false)] // light accent (dark mode): dark text
    [InlineData(0x46, 0xD9, 0xD3, false)] // app turquoise: dark text
    [InlineData(0x00, 0x5A, 0x9E, true)]  // dark accent (light mode): light text
    [InlineData(0x1A, 0x1A, 0x1A, true)]
    public void TextOnAccentKeepsContrastAndAvoidsPureColors(byte r, byte g, byte b, bool expectLight)
    {
        var text = ThemeManager.ContrastingText(Windows.UI.Color.FromArgb(255, r, g, b));

        Assert.Equal(expectLight, text.R > 0x80);
        Assert.NotEqual((byte)0x00, text.R);
        Assert.NotEqual((byte)0xFF, text.R);
    }
}
