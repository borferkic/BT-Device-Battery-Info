# Investigación: batería de mandos 8BitDo

## Resumen

Los mandos 8BitDo conectados por Bluetooth Classic (modos Switch, X-input o D-input) normalmente no exponen su batería mediante `PKEY_Device_BatteryPercentage` ni mediante el servicio GATT Battery Service (`0x180F`). Por eso las lecturas actuales de `BluetoothPnP` y `BatteryQueryRunner` no devuelven un valor para estos dispositivos.

8BitDo no publica documentación oficial del protocolo. Las fuentes útiles son implementaciones de código abierto que leen la batería desde el reporte HID de entrada del propio mando.

Alcance: solo conexiones Bluetooth (Classic y BLE). Las conexiones por receptor inalámbrico 2.4 GHz y por cable USB quedan fuera.

Estado: método 3 implementado en 0.20-dev2 (`Services/GamingInputBattery.cs`). Resultados de hardware del 25 de septiembre de 2026 en la sección "Pruebas con hardware".

## Fuentes revisadas

- SDL, driver HIDAPI para 8BitDo: [SDL_hidapi_8bitdo.c](https://github.com/libsdl-org/SDL/blob/main/src/joystick/hidapi/SDL_hidapi_8bitdo.c).
- Commits de SDL relacionados: [8BitDo (#12661)](https://discourse.libsdl.org/t/sdl-8bitdo-12661/58915) y [soporte Ultimate 3 (#15927)](https://discourse.libsdl.org/t/sdl-support-8bitdo-ultimate-3-15927/68762).
- Aplicación de bandeja en Python para Windows (solo su ruta BLE): [Swif7ify/8BitDo-Battery-Tray-Display](https://github.com/Swif7ify/8BitDo-Battery-Tray-Display).
- Ajustes de batería en Batocera: [8bitdo battery quirks](https://github.com/batocera-linux/batocera.linux/commit/ce894a8e7730f5c0a67a507e50489ffb6614ca01).

## Método 1: reporte HID de entrada (SDL)

SDL obtiene la batería del reporte de estado normal del mando. No necesita un feature report adicional.

| Dato | Valor |
| --- | --- |
| Vendor ID | `0x2DC8` |
| Report ID | `0x01` (Bluetooth) |
| Byte | `data[14]` (índice contado con el Report ID en `data[0]`) |
| Nivel | `data[14] & 0x7F` (porcentaje) |
| Cargando | `data[14] >> 7` |
| Carga completa | cargando y nivel igual a `100` |

Modelos soportados por el driver: SF30 Pro, SN30 Pro, Pro 2, Pro 3, Ultimate 2 Wireless y Ultimate 3. Los Product IDs exactos están en el mismo archivo de SDL. SDL activa la batería solo en algunos modelos mediante el flag `powerstate_supported`; hay que revisar qué modelos lo tienen activo.

## Método 2: BLE GATT (ya implementado)

Si el mando se conecta por BLE y expone el servicio `0x180F`, la ruta actual de la aplicación ya debería leer el porcentaje exacto mediante `PKEY_Device_BatteryPercentage`.

## Propuesta de implementación

1. Añadir `Services/HidBatteryReader.cs`, que busque dispositivos HID con VID `0x2DC8` mediante `HidDevice.GetDeviceSelector` y `Windows.Devices.HumanInterfaceDevice`.
2. Suscribirse a `HidDevice.InputReportReceived`, filtrar por los Report IDs indicados y decodificar el byte 14.
3. Asociar la lectura HID con el `BluetoothDeviceInfo` existente, usando el contenedor del dispositivo o la dirección Bluetooth.
4. Mantener PnP y GATT como fuentes preferentes cuando estén disponibles.

## Riesgos y preguntas abiertas

- [ ] Confirmar con un volcado real que el búfer de WinRT incluye el Report ID en el byte 0, igual que SDL.
- [x] Confirmar qué modelo 8BitDo y qué modo de conexión usa el equipo de prueba: Arcade Stick en X-input y SN30 Pro en Android.
- [x] Verificar si Windows permite abrir el HID del mando en modo lectura: sí, pero sin reportes con un remapeador activo.
- [ ] En modo X-input el mando puede presentarse como mando Xbox (otro VID/PID); revisar si en ese caso el reporte conserva el byte de batería.
- [x] Registrar la tarea en `pending.md` (P-026).

## Pruebas con hardware (25 de septiembre de 2026)

Equipo: 8BitDo Arcade Stick y 8BitDo SN30 Pro, ambos por Bluetooth. Herramientas de diagnóstico de solo lectura: volcado de reportes HID y consulta de `Windows.Gaming.Input`.

| Mando | Modo | VID/PID | HID de entrada | `Windows.Gaming.Input` |
| --- | --- | --- | --- | --- |
| Arcade Stick | X-input (se presenta como Xbox One S) | `045E:02E0` | Se abre, sin reportes | `Discharging`, 100/1000 mWh → 10 % |
| SN30 Pro | Android | `2DC8:2101` | Se abre, sin reportes | Sin informe de batería |

- Windows permite abrir los mandos en modo lectura (`FileAccessMode.Read`), pero no se recibieron reportes; en el equipo hay un mando Xbox 360 virtual (`045E:028E`, cableado) de un remapeador, que probablemente captura los reportes.
- El SN30 Pro en modo Android usa el PID `0x2101`, que no está en SDL. SDL solo activa la batería del SN30 Pro (`0x6101`) tras pedir el feature report `0x06`, que cambia el modo de reporte del mando; se descarta por la regla de solo lectura.
- `RawGameController.NonRoamableId` es opaco y no permite obtener el `ContainerId`. La asociación se hace por VID/PID con las interfaces HID de mando (Usage Page `0x01`, Usage `0x04`/`0x05`) del mismo contenedor, y se descarta cualquier coincidencia ambigua o de mandos cableados.

## Método 3: Windows.Gaming.Input (implementado)

`GamingInputBattery.ReadAsync` usa `RawGameController.TryGetBatteryReport()` y calcula el porcentaje como capacidad restante entre capacidad total. Se ejecuta 1,5 s después de las otras fuentes para que PnP y GATT tengan prioridad. En X-input por Bluetooth la batería suele llegar en niveles gruesos, no al 1 %.

## Decisión sobre el SN30 Pro para Xbox

El SN30 Pro de prueba es la versión para Xbox: por Bluetooth solo funciona en modo Android (PID `0x2101`) y usa X-input únicamente por cable USB. En modo Android no informa batería por ninguna vía, así que no se implementará nada específico para este modelo. El código de `GamingInputBattery` se mantiene para otros mandos de 8BitDo (y de otras marcas) que funcionen en X-input por Bluetooth.
