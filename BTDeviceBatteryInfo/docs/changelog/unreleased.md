# Unreleased Changes

## 0.16 — Compact taskbar widget

### Added

- Added an option beside Minimize and in the tray menu to show connected Bluetooth devices as compact pills in the available left side of the taskbar.
- Each pill shows the device icon and battery level; selecting a pill opens the main window.
- The feature uses the current Bluetooth inventory, saves the user's preference, and supports headphones, keyboards, mice, and compatible controllers.
- Device category icons use Material Symbols Rounded, distributed under Apache License 2.0.

### Fixed

- Kept the widget docked with a dynamic width; adjusted its height to prevent bottom clipping.
- Preserved device selection and connection state when Windows changes the endpoint representing a physical device.

### Validation and limitations

- The user confirmed `0.16-dev9` displays correctly; the README includes a screenshot of four pills.
- Explorer hosting is experimental and depends on available taskbar space. DPI behavior and recovery after restarting Explorer remain unverified.

## Fixed

- Connection state is resolved across all endpoints grouped by physical device. Classic endpoints take precedence when both Classic and BLE are present; BLE remains authoritative for BLE-only devices.
- The selected device is now persisted by physical identity, so replacing an endpoint does not fabricate a disconnect. Existing saved endpoint IDs are still accepted.
- Local state-transition logs include redacted Classic/BLE endpoint counts to support hardware validation without recording device identifiers.

## Validation and limitations

- `win-x64` self-contained single-file publish for release `0.16` completed successfully; manual visual review was confirmed on `0.16-dev9`.
- No automated test suite was run for this manual hardware-test build.
- Physical validation is pending for a dual-protocol device, selected-endpoint removal, reconnect, and stability across periodic reconciliation.
- Windows `ProtocolId` identifies the endpoint discovery protocol, not the active audio profile; the Classic-precedence rule is a conservative heuristic for testing.

## 0.14 — Manual refresh and battery recovery

Local prebuild prepared on `2026-09-06`.

## Added

- `Refresh now` in the system-tray menu.
- A temporary visual status while devices and battery information are refreshed.
- Manual endpoint reconciliation that forces a new PnP/GATT battery query.

## Validation and limitations

- Release build and publish validation are pending for this prebuild.
- Battery timing remains device-dependent; Windows controls the duration of native Bluetooth requests.

## 0.13 — Battery and performance

Local build prepared on `2026-09-05`.

## Performance

- Battery queries start when a connected device is detected, without waiting for discovery to finish.
- Each available percentage is published independently.
- GATT candidates are deduplicated by address and limited to two concurrent operations.
- Identical inventory snapshots do not trigger notifications, and unchanged rows are preserved.
- Window placement is saved after dragging and on exit.

## Fixed

- Pending native operations retain container ownership until completion, allowing valid late results to be published.
- Results from previous connections are discarded.
- HFP, AVRCP, BLE, and base endpoints are unified by `ContainerId`.
- Classic headsets read Windows' native `DEVPKEY_Bluetooth_BatteryLevel` property through `CfgMgr32`.
- Empty battery results use progressive retries after the previous query completes.
- The `About` window reports version `0.13`.

## Validation and limitations

- Release build completed with zero warnings and errors.
- Five coordinator scenarios passed through `validation/BatteryQueries`.
- The connected Bose QC Ultra 2 exposed its HFP battery value through the Windows PnP property.
- `dotnet test` discovers no framework tests.
- Hardware coverage remains device-dependent; Windows controls the duration of native Bluetooth requests.
