using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BTDeviceBatteryInfo.Services;

namespace BTDeviceBatteryInfo;

/// <summary>Retro Y2K motion that XAML triggers cannot scope to one theme.</summary>
internal static class RetroEffects
{
    /// <summary>
    /// Window entrance in Retro Y2K: a short CRT-like flicker and a springy scale from 92 %. Both animations end on
    /// their own, so no animation clock keeps running afterwards. Does nothing in the other themes.
    /// </summary>
    public static void PlayWindowSpawn(FrameworkElement root)
    {
        if (!ThemeManager.IsRetroY2K) return;

        var scale = new ScaleTransform(0.92, 0.92);
        root.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        root.RenderTransform = scale;
        var spring = new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 }
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, spring);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, spring);

        var flicker = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(400), FillBehavior = FillBehavior.Stop };
        flicker.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        flicker.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromPercent(0.10)));
        flicker.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromPercent(0.12)));
        flicker.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromPercent(0.14)));
        root.BeginAnimation(UIElement.OpacityProperty, flicker);
    }
}
