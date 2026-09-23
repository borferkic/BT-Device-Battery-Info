# Changelog

All notable user-facing changes to BT Device Battery Info are documented in this file.

## [Unreleased]

## [0.16] — Widget compacto en la barra de tareas (2026-09-23)

### Añadido

- Opción para mostrar en la zona izquierda de la barra de tareas pastillas compactas con el tipo de dispositivo conectado y su nivel de batería.

### Corregido

- La selección del dispositivo y su estado de conexión se conservan al cambiar el endpoint que Windows usa para representarlo.

## [0.15] — Recuperación de batería HFP/PnP

### Corregido

- Se restableció la lectura de batería para auriculares Bluetooth Classic mediante el nodo Windows HFP/PnP, sin rutas, nombres ni direcciones específicas de fabricante.
- Al desconectar un dispositivo se descarta su porcentaje anterior y se muestra `Battery unavailable`.
- Los registros locales, siempre en inglés, indican si el valor llegó por `PnP HFP native`, PnP o GATT, sin exponer identificadores Bluetooth.

## [0.14] — Refresco manual y recuperación de batería

### Añadido

- Botón `Refresh now` en la esquina inferior derecha del widget y en el menú de bandeja.
- Refresco manual mediante reconciliación inmediata de endpoints.
- Nueva consulta PnP/GATT de batería al solicitar el refresco manual.
- Estado visual durante la actualización de dispositivos y batería.

## [0.13] — Batería y rendimiento

### Rendimiento

- Battery queries start as soon as a device is detected, and each percentage is published independently.
- Reduced duplicate GATT queries, unnecessary UI updates, and window-position writes.

### Fixed

- Pending and late Bluetooth query results are handled safely, with progressive retries when battery data is unavailable.
- Results from previous connections are discarded.
- HFP, AVRCP, BLE, and base endpoints are unified by `ContainerId`.
- Bose headsets now use Windows' native `DEVPKEY_Bluetooth_BatteryLevel` property.

Local build `0.13`, validated with a connected Bose QC Ultra 2. [Details and limitations](BTDeviceBatteryInfo/docs/changelog/unreleased.md).

## [0.12] — Confiabilidad y experiencia

### Añadido

- Se agruparon los dispositivos desconectados en `Disconected Devices`, con contador y control para expandir o contraer el grupo.
- Se añadió una ventana `About` con avatar, copyright de Boris SdK y enlaces individuales de Instagram, Twitch y LinkedIn acompañados por insignias minimalistas.
- Se reorganizó la documentación y se añadió un backlog priorizado para la próxima versión.

### Cambiado

- Se mejoró el seguimiento de dispositivos Bluetooth emparejados, conectados y desconectados.
- Se aceleró la presentación inicial mediante descubrimiento Bluetooth incremental, sin esperar una consulta completa ni la lectura de batería.
- Se eliminó la opción de inicio minimizado; la ventana siempre se muestra al iniciar la aplicación.

### Corregido

- Se protegieron errores no controlados durante el arranque y los eventos asíncronos para evitar el cierre con `0xe0434352`.
- Se añadieron reintentos de batería y consultas PnP/GATT sobre el contenedor y sus endpoints relacionados.

El detalle y la validación están en [Versión 0.12 — Confiabilidad y experiencia](BTDeviceBatteryInfo/docs/changelog/0.12.md).

## [0.11] — Application Icon

### Added

- Added the BT Device Battery Info icon to the executable, application window, and Windows system tray.
- Detailed notes: [Version 0.11 — Application Icon](BTDeviceBatteryInfo/docs/changelog/0.11.md).

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
