using System.Windows;
using System.Windows.Controls;
using BTDeviceBatteryInfo.Models;
using BTDeviceBatteryInfo.Services;

namespace BTDeviceBatteryInfo;

public partial class OptionsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Func<bool, Task> _setTaskbarWidgetEnabled;
    private readonly string _originalTheme;

    public OptionsWindow(AppSettings settings, Func<bool, Task> setTaskbarWidgetEnabled)
    {
        InitializeComponent();
        _settings = settings;
        _setTaskbarWidgetEnabled = setTaskbarWidgetEnabled;
        _originalTheme = settings.ThemeName;
        StartWithWindowsCheckBox.IsChecked = StartupService.IsEnabled();
        TaskbarWidgetCheckBox.IsChecked = settings.TaskbarWidgetEnabled;
        ThemeComboBox.SelectedIndex = settings.ThemeName == ThemeManager.ElegantBlackTheme ? 1 : 0;
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Content is string theme)
            ThemeManager.Apply(System.Windows.Application.Current.Resources, theme);
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var startWithWindows = StartWithWindowsCheckBox.IsChecked == true;
            StartupService.SetEnabled(startWithWindows);
            _settings.StartWithWindows = startWithWindows;
            _settings.ThemeName = (ThemeComboBox.SelectedItem as ComboBoxItem)?.Content as string ?? ThemeManager.SystemTheme;
            await _setTaskbarWidgetEnabled(TaskbarWidgetCheckBox.IsChecked == true);
            await SettingsService.SaveAsync(_settings);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Could not save options.\n\n{ex.Message}", "BT Device Battery Info", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        new AboutWindow { Owner = this }.ShowDialog();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.Apply(System.Windows.Application.Current.Resources, _originalTheme);
        Close();
    }

    private void DragWindow(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
    }
}
