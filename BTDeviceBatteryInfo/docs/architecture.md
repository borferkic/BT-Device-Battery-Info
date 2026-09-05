# Arquitectura

BT Device Battery Info es una aplicación WPF de un solo proyecto para Windows. La solución mantiene la lógica separada por responsabilidad y usa APIs WinRT para enumeración Bluetooth y lectura de batería.

## Flujo principal

```text
App.OnStartup
      │
      ├── SettingsService ──► %LocalAppData%\BTDeviceBatteryInfo\settings.json
      ├── FileLogger      ──► %LocalAppData%\BTDeviceBatteryInfo\Logs
      └── MainWindow.Show
                │
                ▼
         MainWindow.Loaded
                │
                ▼
         MainViewModel.InitializeAsync
                │
                ▼
         BluetoothService
          │              │
          ▼              ▼
  DeviceWatcher      PnP/GATT
          │              │
          └──────► BluetoothDeviceInfo
                            │
                            ▼
                       MainWindow.xaml
```

`StartupService` administra la entrada opcional de inicio con Windows desde el menú de bandeja. No existe una opción de inicio minimizado: la ventana siempre se muestra al iniciar la aplicación.

## Responsabilidades

- `Models/`: contratos de datos sin lógica de presentación.
- `Services/BluetoothService.cs`: consulta endpoints Bluetooth emparejados o conectados, mantiene un inventario en memoria, observa cambios y obtiene batería mediante PnP o GATT cuando Windows lo permite.
- `Services/SettingsService.cs`: carga y guarda preferencias en JSON.
- `Services/StartupService.cs`: administra `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` para el usuario actual.
- `Services/FileLogger.cs`: escribe diagnósticos locales sin registrar direcciones Bluetooth innecesarias.
- `Services/ReconnectPolicy.cs`: calcula los intervalos de los intentos internos de reconexión.
- `ViewModels/MainViewModel.cs`: coordina el refresco, la selección persistida, el estado de la interfaz y la reconexión automática.
- `MainWindow.xaml`: presenta el widget, la lista, el indicador de carga, el acceso `About` y los controles de bandeja definidos en `MainWindow.xaml.cs`.
- `AboutWindow.xaml`: muestra información del proyecto, avatar, copyright y enlaces externos del creador; se abre desde el engranaje del widget.
- `Resources/Styles.xaml`: estilos compartidos de WPF.

## Ciclo de arranque

1. `App.OnStartup` adquiere el mutex de instancia única.
2. Se cargan las preferencias, se crean el logger y `BluetoothService`, y se construye la ventana.
3. La ventana se muestra siempre; su evento `Loaded` solicita primero la inicialización del `ViewModel` y crea después el icono de bandeja.
4. `BluetoothService` inicia el `DeviceWatcher` en segundo plano y devuelve inmediatamente una instantánea del inventario en memoria, aunque el watcher todavía esté enumerando.
5. Cada evento `Added` publica progresivamente la lista. El dispositivo persistido se selecciona cuando aparece; si no aparece, el fallback se decide al completar o agotar la enumeración inicial.
6. `EnumerationCompleted` o un límite blando de ocho segundos cierran el estado de carga inicial. Este límite no retrasa la publicación de los dispositivos encontrados antes.
7. Desde 0.13, cada dispositivo conectado inicia su consulta de batería en segundo plano al aparecer, sin esperar el cierre del descubrimiento. Cada resultado válido se publica de forma independiente.

La primera optimización del arranque eliminó la consulta completa `DeviceInformation.FindAllAsync` del camino crítico. El rendimiento continúa como pendiente de prioridad alta hasta medir repetidamente ventana, primera lista y batería en arranques fríos y calientes; véase `pending.md`.

## Inventario y estados

La consulta usa los protocolos Bluetooth Classic y BLE sin filtrar inicialmente por conexión. Los dispositivos conectados permanecen visibles en la lista principal; los emparejados desconectados se agrupan en el `Expander` `Disconected Devices`, contraído por defecto y con su contador. Al expandirlo, la ventana crece únicamente con las filas desconectadas visibles.

Los endpoints que pertenecen al mismo `ContainerId` se agrupan para representar un dispositivo físico. Cuando Windows actualiza un endpoint desconocido, el servicio realiza una reconciliación retrasada. Un evento `Removed` se considera autoritativo para evitar reinsertar inmediatamente información obsoleta.

La reconciliación completa se ejecuta fuera del arranque inicial. Si el watcher no puede iniciarse, el servicio cierra el estado de carga con error controlado y programa una reconciliación como recuperación.

## Batería

La batería se intenta obtener desde `System.Devices.BatteryLife` y desde la propiedad raw `{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2` (`DEVPKEY_Bluetooth_BatteryLevel`) en el endpoint, el `DeviceContainer` y todos los nodos PnP del contenedor. La búsqueda por dirección incluye los IDs `BTHLE` y todos los `BTHENUM` (incluidos HFP/AVRCP), no solo el formato `BTHENUM\\DEV_<dirección>`. Esto permite unir los nodos HFP/AVRCP/BLE de un auricular Bose mediante su `ContainerId`; Windows suele publicar el valor Classic HFP en el nodo `Hands-Free AG`. GATT estándar `180F`/`2A19` permanece como fallback. Los candidatos se deduplican por dirección. Hay como máximo dos consultas GATT activas en toda la aplicación.

`BatteryQueryRunner` publica el primer resultado válido sin esperar las demás fuentes. Cancela trabajo en cola y observa hasta su finalización las operaciones nativas ya iniciadas, manteniendo ocupado el contenedor para impedir consultas superpuestas. Los ocho segundos ya no descartan resultados tardíos: Windows determina la duración de sus operaciones. Un controlador que no termina una solicitud puede mantener ocupado ese contenedor; no se fuerza otra solicitud sobre él.

Las generaciones de conexión invalidan resultados anteriores cuando cambian los endpoints. Al finalizar una consulta obsoleta se vuelve a evaluar el contenedor actual. Los fallos sin valor previo programan reintentos a los 3, 10 y luego 20 segundos desde la finalización; un valor previo mantiene el intervalo de dos minutos. Los valores de hidratación caducan a los diez minutos. El cierre cancela los reintentos y consultas en cola, y suprime resultados de batería posteriores.

La reconciliación de diez segundos permanece como recuperación y comienza también al cerrar el descubrimiento por timeout. Las instantáneas idénticas no generan notificaciones y el ViewModel reemplaza únicamente filas cuyo contenido cambió. La posición se guarda al terminar el arrastre y al salir, evitando escrituras por cada movimiento.

## Límites técnicos

- La aplicación es de solo lectura respecto al adaptador: no elimina emparejamientos, no desactiva adaptadores y no modifica controladores.
- La API pública de escritorio no ofrece una orden general para conectar perfiles de audio Bluetooth Classic. La reconexión solo puede mostrar el estado que Windows confirma.
- La disponibilidad y exactitud de batería dependen del dispositivo, perfil, controlador y versión de Windows.
