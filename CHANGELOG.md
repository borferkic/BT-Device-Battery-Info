# Changelog

All notable user-facing changes to BT Device Battery Info are documented in this file.

## [Unreleased]

## [0.21] — Language selection and Bluetooth status

### Added

- English and Spanish interface, selectable in `Options` with a live preview; English is the default.
- Detects when Bluetooth is turned off, the adapter is disabled, or no adapter is present, and shows it in the main window.
- `Turn on Bluetooth` button that turns Bluetooth on through Windows, and a `Bluetooth settings` shortcut.

### Fixed

- Devices that reconnect after Bluetooth is turned off and on again are detected without a manual refresh.
- `Refresh now` no longer waits about 30 seconds right after the app starts.
- The refresh indicator no longer stays stuck in the device list; the button shows `Refreshing…` instead.

## [0.20] — Controller battery and Windows device icons

### Added

- Battery level for wireless game controllers in X-input mode over Bluetooth, read through Windows.Gaming.Input (for example, the 8BitDo Arcade Stick).
- The `System` theme uses the Windows device icons for headphones, keyboards, mice, and controllers.
- PayPal and Patreon icons in `About`, next to the Instagram, Twitch, and LinkedIn icons.

### Changed

- `About` shows the app icon instead of the creator avatar and has tighter spacing.

### Fixed

- Social network links in `About` show their icons instead of text placeholders.
- Device selectors in `Options` show "No connected devices" instead of an empty box when a category has no connected device.

## [0.19] — Refined themes and Windows light mode

### Added

- `System` now follows the Windows light or dark app mode and switches live when it changes.
- New app icon with light and dark variants, used in the windows, header, and tray.
- Switches replace the checkboxes in `Options`.
- Loading placeholders and an empty-state message in the device list.
- Keyboard focus ring on buttons, selectors, switches, and the disconnected-devices section.

### Changed

- `Elegant Black` follows shadcn/ui more closely: softer near-black and near-white colors, a subtle gradient on the main window, slimmer buttons, and standard corner radii.
- The taskbar widget, tooltips, and menus now follow the active theme.
- Theme selectors show a check mark on the selected item.
- Window, card, and menu shadows.
- Battery and connection colors update immediately when the theme changes.

### Fixed

- Primary button hover no longer turns gray in `System`.
- The focus ring no longer appears after a mouse click.
- The `About` window now follows the active theme.

## [0.18] — Taskbar device selection

### Added

- Added a taskbar device selector for each category: headphones, keyboard, mouse, and controller.
- Clicking a pill opens the connected-device list for that category and lets the user change the selection.

### Changed

- The `Options` window places selectors in two columns: headphones/controller on the left and keyboard/mouse on the right.
- `Options` is more compact and includes category icons; selectors use the active theme and show only the centered device name.
- Selection is preserved by physical device identity.

## [0.17] — Options and Elegant Black theme (2026-09-23)

### Added

- Added an `Options` window for Windows startup, the taskbar widget, and the `System` / `Elegant Black` themes, with access to `About`.
- Added PayPal and Patreon support links to `About`.

### Changed

- Updated `Elegant Black` with a shadcn/ui-inspired style, Geist Sans, and pill-shaped buttons; `System` retains the previous theme.
- Replaced device and primary toolbar icons with Lucide vectors and moved the taskbar widget option into `Options`.
- Moved the `About` action lower in the `Options` window.

### Fixed

- Fixed a startup crash while applying a theme.

## [0.16] — Compact taskbar widget (2026-09-23)

### Added

- Added an option to show compact pills for connected Bluetooth devices and their battery levels in the left side of the taskbar.
- Added support for headphones, keyboards, mice, and compatible game controllers.

### Fixed

- Preserved device selection and connection state when Windows changes the endpoint representing a physical device.

## [0.15] — HFP/PnP battery recovery

### Fixed

- Restored battery readings for Classic Bluetooth headsets through the Windows HFP/PnP node without manufacturer-specific paths, names, or addresses.
- Disconnected devices discard their previous percentage and show `Battery unavailable`.
- Local logs, always in English, identify whether the value came from `PnP HFP native`, PnP, or GATT without exposing Bluetooth identifiers.

## [0.14] — Manual refresh and battery recovery

### Added

- Added a `Refresh now` button to the widget's lower-right corner and the system-tray menu.
- Added manual refresh through immediate endpoint reconciliation.
- Added a new PnP/GATT battery query when manual refresh is requested.
- Added visual status feedback while devices and battery data are refreshed.

## [0.13] — Battery and performance

### Performance

- Battery queries start as soon as a device is detected, and each percentage is published independently.
- Reduced duplicate GATT queries, unnecessary UI updates, and window-position writes.

### Fixed

- Pending and late Bluetooth query results are handled safely, with progressive retries when battery data is unavailable.
- Results from previous connections are discarded.
- HFP, AVRCP, BLE, and base endpoints are unified by `ContainerId`.
- Bose headsets now use Windows' native `DEVPKEY_Bluetooth_BatteryLevel` property.

Local build `0.13`, validated with a connected Bose QC Ultra 2.

## [0.12] — Reliability and experience

### Added

- Grouped disconnected devices under `Disconected Devices` with a counter and expand/collapse control.
- Added an `About` window with an avatar, Boris SdK copyright, and separate Instagram, Twitch, and LinkedIn links with minimal badges.
- Reorganized the documentation and added a prioritized backlog for the next version.

### Changed

- Improved tracking of paired, connected, and disconnected Bluetooth devices.
- Accelerated initial presentation through incremental Bluetooth discovery without waiting for a complete query or battery read.
- Removed the minimized-on-start option; the window is always shown at startup.

### Fixed

- Protected startup and asynchronous event handlers against unhandled errors that caused termination with `0xe0434352`.
- Added battery retries and PnP/GATT queries across the container and its related endpoints.


## [0.11] — Application Icon

### Added

- Added the BT Device Battery Info icon to the executable, application window, and Windows system tray.

## [0.10] — First Build

Initial public release of BT Device Battery Info.

### Included

- Bluetooth Classic and Bluetooth Low Energy device detection.
- Battery level reporting through Windows PnP properties and the standard GATT Battery Service when available.
- Automatic device-list updates for connected and disconnected devices.
- A compact widget that grows or shrinks without a scrollbar.
- System-tray controls to show or hide the widget, start with Windows, and exit.
- Single-instance protection and an initial loading indicator.
- Open-source distribution under the MIT License.
