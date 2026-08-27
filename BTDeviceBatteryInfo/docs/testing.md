# Testing and validation

## Automated build

Continuous integration restores and builds on `windows-latest`. There is no automated hardware test suite because device discovery and battery reporting depend on the adapter, drivers, and paired devices.

## Minimum manual validation

1. Pair and turn on at least one Bluetooth device.
2. Start the application and confirm that the loading ring and `Loading app, please wait...` are visible while the initial query runs.
3. Confirm that the list shows only connected devices.
4. Confirm that battery shows a percentage when Windows provides one and `Battery unavailable` otherwise.
5. With one or two devices, confirm that the widget keeps its base height. Connect a third device and confirm that it grows by one row per additional device without showing a scrollbar.
6. Disconnect devices until two or fewer remain and confirm that the widget returns to its base height.
7. With two similar devices connected, confirm that both remain visible. Disconnect one and confirm that it stays absent; reconnect it and confirm that it returns.
8. Confirm that `Development in Beta` appears in the lower-right corner without covering the last device.
9. Confirm that the gear remains visible, dimmed, and does not open a window.
10. Confirm that the tray menu contains `Show widget`/`Hide widget`, `Start with Windows`, and `Exit`; test the visibility and exit actions.
11. Toggle `Start with Windows` and confirm that its check mark changes and persists after reopening the tray menu.
11. Run `RECONNECT` and `FORCE RECONNECT`; confirm that the state reflects Windows and that no pairings are removed.
12. Review logs in `%LocalAppData%\BTDeviceBatteryInfo\Logs` without sharing unnecessary Bluetooth identifiers.

## Information to include in a report

- Windows version and architecture.
- Bluetooth adapter and driver, if known.
- Exact device name and model.
- Pairing and connection state.
- Exact steps, expected result, and observed result.
- A redacted log excerpt, if needed.
