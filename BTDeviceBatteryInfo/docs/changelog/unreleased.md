# Unreleased Changes

## 0.17-dev6

### Changed

- Moved the `About` action lower in the Options window and increased the window height so it matches the supplied placement reference while keeping Save and Cancel in the footer.
- Updated Elegant Black buttons to rounded pill shapes matching the supplied reference; System keeps the existing button corners.
- Embedded Geist Sans for Elegant Black only; System continues using the Windows message font. The font's OFL license is included with the application files.

### Validation

- Release build validation pending; manual visual review is pending.

## 0.17-dev5

### Changed

- Replaced the remaining main-toolbar settings, minimize, and exit glyphs, plus the Options close glyph, with Lucide-style vector icons.

### Validation

- Release publish compiled successfully; visual review is pending.

## 0.17-dev4

### Changed

- Replaced the Material Symbols device glyphs with consistent Lucide-style vector icons in the main window and taskbar pills.
- Removed the taskbar shortcut from the main toolbar; its setting remains available in Options and the tray menu.
- Renamed the theme section to `Theme`, improved the selector contrast, and centered the About action under an information icon.
- Refined rounded window edges in the main, Options, and About windows.

### Validation

- Release publish compiled successfully; visual review is pending.

## 0.17-dev3

### Fixed

- Fixed a startup crash when applying a theme by replacing frozen WPF brushes and using dynamic resources for live theme updates.

### Validation

- Release publish compiled successfully; manual startup and theme-switch review is pending.

## 0.17-dev2

### Added

- Added `Options` for Windows startup, the compact taskbar widget, and the `System` / `Elegant Black` themes; theme changes preview live and persist when saved.
- Added navigation from Options to About and PayPal/Patreon support links while retaining the existing About content.

### Validation

- Release publish compiled successfully; manual visual and behavior review is pending.

## 0.16 — Widget compacto en la barra de tareas

### Añadido

- Opción junto a Minimizar y en el menú de bandeja para mostrar dispositivos Bluetooth conectados en pastillas compactas dentro de la zona izquierda libre de la barra de tareas.
- Cada pastilla presenta el icono del dispositivo y el nivel de batería; al seleccionarla se abre la ventana principal.
- La función usa el inventario Bluetooth actual, guarda la preferencia del usuario y muestra auriculares, teclados, mouse y controles compatibles.
- Los iconos de estas categorías usan Material Symbols Rounded, incluida con su licencia Apache 2.0.

### Corregido

- El widget conserva el acople y el ancho dinámico según los dispositivos mostrados; la altura ajustada evita el recorte inferior.
- La selección y el estado de conexión se conservan al cambiar el endpoint que Windows usa para representar el dispositivo.

### Validación

- El usuario confirmó que `0.16-dev9` aparece correctamente; el screenshot de cuatro pastillas se incluye en el README.
- La técnica de hospedaje en Explorer es experimental y depende del espacio libre en la barra de tareas; quedan pendientes comprobaciones de DPI y recuperación tras reiniciar Explorer.

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
