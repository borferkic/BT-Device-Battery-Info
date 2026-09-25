using System.Windows;
using System.Windows.Controls;
using BTDeviceBatteryInfo.Models;
using BTDeviceBatteryInfo.Services;
using BTDeviceBatteryInfo.ViewModels;

namespace BTDeviceBatteryInfo;

public partial class OptionsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Func<bool, Task> _setTaskbarWidgetEnabled;
    private readonly string _originalTheme;

    public OptionsWindow(AppSettings settings, IEnumerable<BluetoothDeviceItem> connectedDevices, Func<bool, Task> setTaskbarWidgetEnabled)
    {
        InitializeComponent();
        _settings = settings;
        _setTaskbarWidgetEnabled = setTaskbarWidgetEnabled;
        _originalTheme = settings.ThemeName;
        StartWithWindowsCheckBox.IsChecked = StartupService.IsEnabled();
        TaskbarWidgetCheckBox.IsChecked = settings.TaskbarWidgetEnabled;
        ThemeComboBox.SelectedIndex = settings.ThemeName == ThemeManager.ElegantBlackTheme ? 1 : 0;
        var devices = connectedDevices.ToArray();
        ConfigureDevicePicker(HeadphonesComboBox, devices, BluetoothDeviceCategory.Headphones, settings.TaskbarHeadphonesDeviceId);
        ConfigureDevicePicker(KeyboardComboBox, devices, BluetoothDeviceCategory.Keyboard, settings.TaskbarKeyboardDeviceId);
        ConfigureDevicePicker(MouseComboBox, devices, BluetoothDeviceCategory.Mouse, settings.TaskbarMouseDeviceId);
        ConfigureDevicePicker(GameControllerComboBox, devices, BluetoothDeviceCategory.GameController, settings.TaskbarGameControllerDeviceId);
    }

    private static void ConfigureDevicePicker(System.Windows.Controls.ComboBox picker, IReadOnlyCollection<BluetoothDeviceItem> devices,
        BluetoothDeviceCategory category, string? selectedId)
    {
        var categoryDevices = devices.Where(device => device.Category == category).ToArray();
        if (categoryDevices.Length == 0)
        {
            picker.Items.Add(new ComboBoxItem
            {
                Content = "No connected devices",
                IsEnabled = false,
                Style = (Style)picker.FindResource("OptionsPickerItemStyle")
            });
            picker.IsEnabled = false;
            return;
        }

        foreach (var device in categoryDevices)
        {
            picker.Items.Add(new ComboBoxItem
            {
                Content = device.Name,
                Tag = device,
                ToolTip = device.Name,
                Style = (Style)picker.FindResource("OptionsPickerItemStyle")
            });
        }
        var selectedIndex = Array.FindIndex(categoryDevices, device =>
            string.Equals(device.PhysicalDeviceId, selectedId, StringComparison.OrdinalIgnoreCase));
        picker.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        picker.ToolTip = string.Join(Environment.NewLine, categoryDevices.Select(device => device.Name));
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string theme)
            ThemeManager.Apply(System.Windows.Application.Current.Resources, theme);
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var startWithWindows = StartWithWindowsCheckBox.IsChecked == true;
            StartupService.SetEnabled(startWithWindows);
            _settings.StartWithWindows = startWithWindows;
            _settings.ThemeName = (ThemeComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? ThemeManager.SystemTheme;
            SaveDeviceSelection(HeadphonesComboBox, BluetoothDeviceCategory.Headphones);
            SaveDeviceSelection(KeyboardComboBox, BluetoothDeviceCategory.Keyboard);
            SaveDeviceSelection(MouseComboBox, BluetoothDeviceCategory.Mouse);
            SaveDeviceSelection(GameControllerComboBox, BluetoothDeviceCategory.GameController);
            await _setTaskbarWidgetEnabled(TaskbarWidgetCheckBox.IsChecked == true);
            await SettingsService.SaveAsync(_settings);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Could not save options.\n\n{ex.Message}", "BT Device Battery Info", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveDeviceSelection(System.Windows.Controls.ComboBox picker, BluetoothDeviceCategory category)
    {
        if (picker.SelectedItem is not ComboBoxItem { Tag: BluetoothDeviceItem device }) return;
        switch (category)
        {
            case BluetoothDeviceCategory.Headphones: _settings.TaskbarHeadphonesDeviceId = device.PhysicalDeviceId; break;
            case BluetoothDeviceCategory.Keyboard: _settings.TaskbarKeyboardDeviceId = device.PhysicalDeviceId; break;
            case BluetoothDeviceCategory.Mouse: _settings.TaskbarMouseDeviceId = device.PhysicalDeviceId; break;
            case BluetoothDeviceCategory.GameController: _settings.TaskbarGameControllerDeviceId = device.PhysicalDeviceId; break;
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
