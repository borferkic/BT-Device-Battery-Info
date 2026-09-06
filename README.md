# BT Device Battery Info

<a id="english"></a>

Information in: [English](#english) | [Español](#espanol)

Release `0.14` — **Refresco manual y recuperación de batería**.

BT Device Battery Info is an open-source Windows desktop widget that displays paired or connected Bluetooth devices and their battery level when Windows makes that information available.

The application is lightweight and unobtrusive. It runs from the system tray, shows the widget when requested, and updates the device list as Bluetooth devices connect, disconnect, or change state. Disconnected devices are grouped in a collapsed `Disconected Devices` section with a count. The widget gear opens an About window with project information and creator links.

## Features

- Detects paired and connected Bluetooth Classic and Bluetooth Low Energy (BLE) devices.
- Displays each device name and connection state.
- Shows battery percentage through Windows PnP properties or the standard GATT Battery Service when available.
- Keeps devices with the same name separate when they have different Windows device identities.
- Shows a loading indicator during the initial device scan.
- Grows or shrinks the complete widget automatically as devices appear or disappear, without a scrollbar.
- Provides tray actions to show or hide the widget, enable or disable Start with Windows, and exit the application.
- Provides `Refresh now` in the widget and system tray to refresh devices and trigger a new battery query.
- Prevents multiple instances from running at the same time and informs the user when the application is already open.
- Includes an About window with project information and creator links.
- Stores application data and diagnostic logs in `%LocalAppData%\BTDeviceBatteryInfo`.

## Requirements

- Windows 10 version 19041 or later, or Windows 11.
- A Bluetooth adapter and paired Bluetooth devices. Battery reporting depends on the device, profile, driver, and information exposed by Windows.

## Download

Download [version 0.14](https://github.com/borferkic/BT-Device-Battery-Info/releases/tag/v0.14) from GitHub Releases and run `BTDeviceBatteryInfo.exe`.

The release requires the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) for Windows. Developers building from source need the .NET 8 SDK or Visual Studio 2022 with the .NET desktop development workload.

## Support the project

If you find BT Device Battery Info useful, you can support its development with a donation through [PayPal](https://paypal.me/borissdk). Thank you for helping keep the project moving forward.

[![Donate with PayPal](https://www.paypalobjects.com/en_US/i/btn/btn_donateCC_LG.gif)](https://paypal.me/borissdk)

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

The application uses Windows device enumeration to monitor paired and connected Bluetooth endpoints. Connected devices are prioritized in the list. It combines device information with battery data from Windows PnP properties and, when available, the Bluetooth GATT Battery Service. Some devices may appear without a battery percentage because their hardware or driver does not expose it.

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
│   ├── docs/            # Documentation, architecture, testing, and changelog
├── .github/workflows/   # Windows continuous integration
├── CONTRIBUTING.md
└── LICENSE
```

## Open-source license

BT Device Battery Info is distributed under the MIT License. See [LICENSE](LICENSE) for the complete license text.

Contributions, bug reports, and improvements are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for development guidelines.
See [CHANGELOG.md](CHANGELOG.md) for release notes.

## Project documentation

The project documentation is organized in [BTDeviceBatteryInfo/docs/README.md](BTDeviceBatteryInfo/docs/README.md). It contains the architecture, validation procedure, versioned pending work, and unreleased changes.

---

<a id="espanol"></a>

## Español

Release `0.14` — **Refresco manual y recuperación de batería**.

BT Device Battery Info es un widget de escritorio open source para Windows que muestra los dispositivos Bluetooth emparejados o conectados y su nivel de batería cuando Windows proporciona esa información.

La aplicación es ligera y discreta. Funciona desde el área de notificaciones, muestra el widget cuando el usuario lo solicita y actualiza la lista cuando los dispositivos Bluetooth se conectan, desconectan o cambian de estado. Los dispositivos desconectados se agrupan en la sección contraída `Disconected Devices`, que muestra la cantidad y permite expandirse. El engranaje abre una ventana About con información del proyecto y enlaces del creador.

### Funcionalidades

- Detecta dispositivos Bluetooth Classic y Bluetooth Low Energy (BLE) emparejados o conectados.
- Muestra el nombre y el estado de conexión de cada dispositivo.
- Muestra el porcentaje de batería mediante propiedades PnP de Windows o el servicio estándar GATT Battery Service cuando está disponible.
- Mantiene separados los dispositivos con el mismo nombre cuando tienen identidades diferentes en Windows.
- Muestra un indicador de carga durante la búsqueda inicial.
- Aumenta o reduce automáticamente el tamaño completo del widget según aparezcan o desaparezcan dispositivos, sin barra de desplazamiento.
- Incluye opciones en el área de notificaciones para mostrar u ocultar el widget, activar o desactivar el inicio con Windows y salir de la aplicación.
- Incluye `Refresh now` en el widget y el área de notificaciones para actualizar los dispositivos y volver a consultar la batería.
- Evita que se ejecuten varias instancias al mismo tiempo y avisa si la aplicación ya está abierta.
- Incluye una ventana About con información del proyecto y enlaces del creador.
- Guarda la configuración y los registros en `%LocalAppData%\BTDeviceBatteryInfo`.

### Requisitos

- Windows 10 versión 19041 o posterior, o Windows 11.
- Un adaptador Bluetooth y dispositivos Bluetooth emparejados. La información de batería depende del dispositivo, el perfil, el controlador y los datos que Windows exponga.

### Descargar

Descarga la [versión 0.14](https://github.com/borferkic/BT-Device-Battery-Info/releases/tag/v0.14) desde GitHub Releases y ejecuta `BTDeviceBatteryInfo.exe`.

La versión publicada requiere el [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) para Windows. Para compilar desde el código fuente se necesita el .NET 8 SDK o Visual Studio 2022 con la carga de trabajo de desarrollo de escritorio .NET.

### Apoya el proyecto

Si BT Device Battery Info te resulta útil, puedes apoyar su desarrollo con una donación mediante [PayPal](https://paypal.me/borissdk). Gracias por ayudar a que el proyecto siga avanzando.

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
