# Unreleased Changes

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
