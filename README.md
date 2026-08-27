# BT Device Battery Info

Version `0.11` — **Application Icon**

BT Device Battery Info is an open-source Windows desktop widget that displays connected Bluetooth devices and their battery level when Windows makes that information available.

The application is lightweight and unobtrusive. It runs from the system tray, shows the widget when requested, and updates the device list as Bluetooth devices connect, disconnect, or change state.

## Features

- Detects connected Bluetooth Classic and Bluetooth Low Energy (BLE) devices.
- Displays each device name and connection state.
- Shows battery percentage through Windows PnP properties or the standard GATT Battery Service when available.
- Keeps devices with the same name separate when they have different Windows device identities.
- Shows a loading indicator during the initial device scan.
- Grows or shrinks the complete widget automatically as devices appear or disappear, without a scrollbar.
- Provides tray actions to show or hide the widget, enable or disable Start with Windows, and exit the application.
- Prevents multiple instances from running at the same time and informs the user when the application is already open.
- Includes a non-interactive, dimmed settings icon reserved for future functionality.
- Stores application data and diagnostic logs in `%LocalAppData%\BTDeviceBatteryInfo`.

## Requirements

- Windows 10 version 19041 or later, or Windows 11.
- .NET 8 SDK or Visual Studio 2022 with the .NET desktop development workload.
- A Bluetooth adapter and paired Bluetooth devices. Battery reporting depends on the device, profile, driver, and information exposed by Windows.

## Build and run

From the repository root:

```powershell
dotnet restore ".\BT Device Battery Info.slnx"
dotnet build ".\BT Device Battery Info.slnx" --configuration Release
dotnet run --project ".\BTDeviceBatteryInfo\BTDeviceBatteryInfo.csproj" --configuration Release
```

To publish a self-contained Windows x64 executable:

```powershell
dotnet publish ".\BTDeviceBatteryInfo\BTDeviceBatteryInfo.csproj" `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  --output ".\publish\win-x64"
```

## How it works

The application uses Windows device enumeration to monitor connected Bluetooth endpoints. It combines device information with battery data from Windows PnP properties and, when available, the Bluetooth GATT Battery Service. Some devices may appear without a battery percentage because their hardware or driver does not expose it.

The widget is read-only with respect to the Bluetooth adapter. It does not remove pairings, disable adapters, or modify device drivers.

## Project structure

```text
.
├── BT Device Battery Info.slnx
├── BTDeviceBatteryInfo/
│   ├── Models/          # Device and application data models
│   ├── Services/        # Bluetooth, battery, settings, logging, and startup logic
│   ├── ViewModels/      # UI state and commands
│   ├── Resources/       # Shared WPF styles
│   ├── docs/            # Architecture, testing, and screenshots
├── .github/workflows/   # Windows continuous integration
├── CONTRIBUTING.md
└── LICENSE
```

## Open-source license

BT Device Battery Info is distributed under the MIT License. See [LICENSE](LICENSE) for the complete license text.

Contributions, bug reports, and improvements are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for development guidelines.
See [CHANGELOG.md](CHANGELOG.md) for release notes.

---

## Español

Versión `0.11` — **Icono de la aplicación**

BT Device Battery Info es un widget de escritorio open source para Windows que muestra los dispositivos Bluetooth conectados y su nivel de batería cuando Windows proporciona esa información.

La aplicación es ligera y discreta. Funciona desde el área de notificaciones, muestra el widget cuando el usuario lo solicita y actualiza la lista cuando los dispositivos Bluetooth se conectan, desconectan o cambian de estado.

### Funcionalidades

- Detecta dispositivos Bluetooth Classic y Bluetooth Low Energy (BLE) conectados.
- Muestra el nombre y el estado de conexión de cada dispositivo.
- Muestra el porcentaje de batería mediante propiedades PnP de Windows o el servicio estándar GATT Battery Service cuando está disponible.
- Mantiene separados los dispositivos con el mismo nombre cuando tienen identidades diferentes en Windows.
- Muestra un indicador de carga durante la búsqueda inicial.
- Aumenta o reduce automáticamente el tamaño completo del widget según aparezcan o desaparezcan dispositivos, sin barra de desplazamiento.
- Incluye opciones en el área de notificaciones para mostrar u ocultar el widget, activar o desactivar el inicio con Windows y salir de la aplicación.
- Evita que se ejecuten varias instancias al mismo tiempo y avisa si la aplicación ya está abierta.
- Incluye un icono de configuración no interactivo reservado para futuras funciones.
- Guarda la configuración y los registros en `%LocalAppData%\BTDeviceBatteryInfo`.

### Requisitos

- Windows 10 versión 19041 o posterior, o Windows 11.
- .NET 8 SDK o Visual Studio 2022 con la carga de trabajo de desarrollo de escritorio .NET.
- Un adaptador Bluetooth y dispositivos Bluetooth emparejados. La información de batería depende del dispositivo, el perfil, el controlador y los datos que Windows exponga.

### Compilar y ejecutar

Desde la raíz del repositorio:

```powershell
dotnet restore ".\BT Device Battery Info.slnx"
dotnet build ".\BT Device Battery Info.slnx" --configuration Release
dotnet run --project ".\BTDeviceBatteryInfo\BTDeviceBatteryInfo.csproj" --configuration Release
```

Para publicar un ejecutable independiente para Windows x64:

```powershell
dotnet publish ".\BTDeviceBatteryInfo\BTDeviceBatteryInfo.csproj" `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  --output ".\publish\win-x64"
```

### Licencia

BT Device Battery Info se distribuye bajo la licencia MIT. Consulta el archivo [LICENSE](LICENSE) para ver el texto completo.

Consulta [CHANGELOG.md](CHANGELOG.md) para ver las notas de versión.
