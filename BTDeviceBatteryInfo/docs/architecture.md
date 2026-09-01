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
7. La hidratación de batería comienza después de cerrar el descubrimiento inicial y no forma parte del camino crítico de presentación.

La primera optimización del arranque eliminó la consulta completa `DeviceInformation.FindAllAsync` del camino crítico. El rendimiento continúa como pendiente de prioridad alta hasta medir repetidamente ventana, primera lista y batería en arranques fríos y calientes; véase `pending.md`.

## Inventario y estados

La consulta usa los protocolos Bluetooth Classic y BLE sin filtrar inicialmente por conexión. Los dispositivos conectados permanecen visibles en la lista principal; los emparejados desconectados se agrupan en el `Expander` `Disconected Devices`, contraído por defecto y con su contador. Al expandirlo, la ventana crece únicamente con las filas desconectadas visibles.

Los endpoints que pertenecen al mismo `ContainerId` se agrupan para representar un dispositivo físico. Cuando Windows actualiza un endpoint desconocido, el servicio realiza una reconciliación retrasada. Un evento `Removed` se considera autoritativo para evitar reinsertar inmediatamente información obsoleta.

La reconciliación completa se ejecuta fuera del arranque inicial. Si el watcher no puede iniciarse, el servicio cierra el estado de carga con error controlado y programa una reconciliación como recuperación.

## Batería

La batería se intenta obtener desde la propiedad PnP del endpoint, el `DeviceContainer`, los nodos PnP del contenedor y el servicio GATT estándar `180F`/característica `2A19` en todos los endpoints candidatos. Los resultados válidos se conservan temporalmente; los resultados vacíos caducan y se reintentan. La interfaz muestra `Battery unavailable` cuando ninguna fuente entrega un porcentaje válido entre 0 y 100.

La actualización de valores obtenidos por hidratación está pendiente: el caché actual puede conservar indefinidamente un valor o un resultado vacío. No debe considerarse resuelto hasta completar una prueba con un dispositivo BLE/GATT que cambie de nivel durante la sesión.

## Límites técnicos

- La aplicación es de solo lectura respecto al adaptador: no elimina emparejamientos, no desactiva adaptadores y no modifica controladores.
- La API pública de escritorio no ofrece una orden general para conectar perfiles de audio Bluetooth Classic. La reconexión solo puede mostrar el estado que Windows confirma.
- La disponibilidad y exactitud de batería dependen del dispositivo, perfil, controlador y versión de Windows.
