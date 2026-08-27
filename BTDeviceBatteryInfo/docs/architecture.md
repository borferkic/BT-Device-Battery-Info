# Architecture

BT Device Battery Info is a single-project WPF application organized by responsibility.

## Main flow

```text
DeviceWatcher / WinRT
          │
          ▼
BluetoothService ──► BluetoothDeviceInfo
          │                    │
          ▼                    ▼
      MainViewModel ─────► MainWindow.xaml
          │
          ├── SettingsService ──► %LocalAppData%\BTDeviceBatteryInfo\settings.json
          ├── FileLogger      ──► %LocalAppData%\BTDeviceBatteryInfo\Logs
          └── StartupService  ──► HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

## Responsibilities

- `Models/`: data contracts without UI logic.
- `Services/BluetoothService`: queries connected Bluetooth endpoints, observes changes, and reads battery values through PnP/GATT.
- `Services/SettingsService`: serializes preferences and migrates legacy settings.
- `Services/StartupService`: manages the current user's startup entry.
- `ViewModels/`: exposes WPF state, refreshes devices, and coordinates the reconnect policy.
- `MainWindow.xaml`: presents the widget, loading overlay, and non-interactive gear icon.
- `Resources/`: shared WPF styles.

## Relevant decisions

The initial query requests only connected endpoints so the window appears quickly. The watcher keeps the inventory current, while battery hydration runs separately to avoid blocking the UI.

The widget reserves space for two devices. `MainViewModel.DeviceCount` notifies the WPF container to add or remove a 56 px row for every additional device; the whole window grows without a scrollbar.

When Windows sends an update for an endpoint that has not been registered yet, `BluetoothService` performs one delayed connected-device query to recover it. An explicit `Removed` event is authoritative and is never re-queried, preventing a disconnected device from being reinserted from stale Windows data.

Reconnect does not simulate a successful connection: it requests a check and displays the state that Windows actually returns. This avoids promising a capability that public desktop APIs do not offer for Bluetooth Classic audio.
