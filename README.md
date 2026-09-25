# BT Device Battery Info

<a id="english"></a>

Information in: [English](#english) | [Español](#espanol)

Latest release: `0.19` — **Refined themes and Windows light mode**.

BT Device Battery Info is an open-source Windows desktop widget that displays paired or connected Bluetooth devices and their battery level when Windows makes that information available.

The application is lightweight and unobtrusive. It runs from the system tray, shows the widget when requested, and updates the device list as Bluetooth devices connect, disconnect, or change state. Disconnected devices are grouped in a collapsible `Disconnected devices` section with a count. The gear button opens `Options`, where you can configure startup, the taskbar widget, and the theme, and open `About`.

## Features

### Devices and battery

- Detects paired and connected Bluetooth Classic and Bluetooth Low Energy (BLE) devices.
- Displays each device name, category icon, and connection state.
- Shows battery percentage through Windows PnP properties or the standard GATT Battery Service when available, and highlights low battery (15 % or less).
- Groups the endpoints of the same physical device and keeps devices with the same name separate when they have different Windows identities.
- Shows loading placeholders during the initial scan and a clear message when no devices are available.
- Provides `Refresh now` in the widget and system tray to refresh devices and trigger a new battery query.

### Taskbar widget

- Places compact battery indicators for connected headphones, keyboards, mice, and controllers on the left side of the Windows taskbar.
- Lets you choose which device is shown for each category, from `Options` or by clicking an indicator.

### Themes and appearance

- `Elegant Black`: a monochrome dark theme inspired by shadcn/ui, with the Geist Sans font and a subtle gradient on the main window.
- `System`: follows the Windows 11 look and switches automatically between light and dark mode when Windows changes.
- The theme applies to every window, tooltip, menu, and the taskbar widget, with a live preview in `Options`.
- Light and dark app icons that follow the active theme.
- Keyboard navigation with a visible focus ring.

### Application

- `Options` window with switches for Start with Windows and the taskbar widget, the theme selector, and the device per category.
- Tray actions to show or hide the widget, enable or disable Start with Windows, and exit the application.
- Prevents multiple instances from running at the same time and informs the user when the application is already open.
- `About` window with project information and links to Instagram, Twitch, LinkedIn, PayPal, and Patreon.
- Stores settings and diagnostic logs in `%LocalAppData%\BTDeviceBatteryInfo`.

## Screenshots

**Elegant Black theme**

| Main window | Options |
|---|---|
| ![Main window with the Elegant Black theme](BTDeviceBatteryInfo/docs/images/main-window-elegant-black.png) | ![Options window with the Elegant Black theme](BTDeviceBatteryInfo/docs/images/options-window-elegant-black.png) |

![Taskbar widget with the Elegant Black theme](BTDeviceBatteryInfo/docs/images/taskbar-widget-elegant-black.png)

**System theme (Windows 11 dark)**

| Main window | Options |
|---|---|
| ![Main window with the System theme](BTDeviceBatteryInfo/docs/images/main-window.png) | ![Options window with the System theme](BTDeviceBatteryInfo/docs/images/options-window.png) |

![Taskbar widget with the System theme](BTDeviceBatteryInfo/docs/images/taskbar-widget.png)

## Requirements

- Windows 10 version 19041 or later, or Windows 11.
- A Bluetooth adapter and paired Bluetooth devices. Battery reporting depends on the device, profile, driver, and information exposed by Windows.

## Download

Download the [latest version (0.19)](https://github.com/borferkic/BT-Device-Battery-Info/releases/latest) from GitHub Releases and run `BTDeviceBatteryInfo.exe`.

The release is self-contained and does not require the .NET Desktop Runtime. Developers building from source need the .NET 8 SDK or Visual Studio 2022 with the .NET desktop development workload.

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
  -p:IncludeNativeLibrariesForSelfExtract=true `
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
│   ├── Resources/       # Shared WPF styles, themes, and fonts
│   ├── Icon/            # Light and dark application icons
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

Última versión: `0.19` — **Temas refinados y modo claro de Windows**.

BT Device Battery Info es un widget de escritorio open source para Windows que muestra los dispositivos Bluetooth emparejados o conectados y su nivel de batería cuando Windows proporciona esa información.

La aplicación es ligera y discreta. Funciona desde el área de notificaciones, muestra el widget cuando el usuario lo solicita y actualiza la lista cuando los dispositivos Bluetooth se conectan, desconectan o cambian de estado. Los dispositivos desconectados se agrupan en la sección contraíble `Disconnected devices`, que muestra la cantidad. El engranaje abre `Options`, donde se configuran el inicio con Windows, el widget de la barra de tareas y el tema, y desde donde se accede a `About`.

### Funcionalidades

#### Dispositivos y batería

- Detecta dispositivos Bluetooth Classic y Bluetooth Low Energy (BLE) emparejados o conectados.
- Muestra el nombre, el icono de categoría y el estado de conexión de cada dispositivo.
- Muestra el porcentaje de batería mediante propiedades PnP de Windows o el servicio estándar GATT Battery Service cuando está disponible, y resalta la batería baja (15 % o menos).
- Agrupa los endpoints de un mismo dispositivo físico y mantiene separados los dispositivos con el mismo nombre cuando tienen identidades diferentes en Windows.
- Muestra marcadores de carga durante la búsqueda inicial y un mensaje claro cuando no hay dispositivos.
- Incluye `Refresh now` en el widget y el área de notificaciones para actualizar los dispositivos y volver a consultar la batería.

#### Widget de la barra de tareas

- Muestra indicadores compactos de batería para auriculares, teclados, mouse y controles conectados en el lado izquierdo de la barra de tareas de Windows.
- Permite elegir qué dispositivo se muestra en cada categoría, desde `Options` o haciendo clic en un indicador.

#### Temas y apariencia

- `Elegant Black`: tema oscuro monocromo inspirado en shadcn/ui, con la fuente Geist Sans y un gradiente sutil en la ventana principal.
- `System`: sigue el aspecto de Windows 11 y cambia automáticamente entre modo claro y oscuro cuando cambia Windows.
- El tema se aplica a todas las ventanas, tooltips, menús y al widget de la barra de tareas, con vista previa en vivo en `Options`.
- Iconos de la aplicación en versión clara y oscura según el tema activo.
- Navegación con teclado con un anillo de foco visible.

#### Aplicación

- Ventana `Options` con interruptores para el inicio con Windows y el widget de la barra de tareas, el selector de tema y el dispositivo por categoría.
- Opciones en el área de notificaciones para mostrar u ocultar el widget, activar o desactivar el inicio con Windows y salir de la aplicación.
- Evita que se ejecuten varias instancias al mismo tiempo y avisa si la aplicación ya está abierta.
- Ventana `About` con información del proyecto y enlaces a Instagram, Twitch, LinkedIn, PayPal y Patreon.
- Guarda la configuración y los registros en `%LocalAppData%\BTDeviceBatteryInfo`.

### Capturas

**Tema Elegant Black**

| Ventana principal | Opciones |
|---|---|
| ![Ventana principal con el tema Elegant Black](BTDeviceBatteryInfo/docs/images/main-window-elegant-black.png) | ![Ventana de opciones con el tema Elegant Black](BTDeviceBatteryInfo/docs/images/options-window-elegant-black.png) |

![Widget de la barra de tareas con el tema Elegant Black](BTDeviceBatteryInfo/docs/images/taskbar-widget-elegant-black.png)

**Tema System (Windows 11 oscuro)**

| Ventana principal | Opciones |
|---|---|
| ![Ventana principal con el tema System](BTDeviceBatteryInfo/docs/images/main-window.png) | ![Ventana de opciones con el tema System](BTDeviceBatteryInfo/docs/images/options-window.png) |

![Widget de la barra de tareas con el tema System](BTDeviceBatteryInfo/docs/images/taskbar-widget.png)

### Requisitos

- Windows 10 versión 19041 o posterior, o Windows 11.
- Un adaptador Bluetooth y dispositivos Bluetooth emparejados. La información de batería depende del dispositivo, el perfil, el controlador y los datos que Windows exponga.

### Descargar

Descarga la [última versión (0.19)](https://github.com/borferkic/BT-Device-Battery-Info/releases/latest) desde GitHub Releases y ejecuta `BTDeviceBatteryInfo.exe`.

La versión publicada es autocontenida y no requiere el .NET Desktop Runtime. Para compilar desde el código fuente se necesita el .NET 8 SDK o Visual Studio 2022 con la carga de trabajo de desarrollo de escritorio .NET.

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
  -p:IncludeNativeLibrariesForSelfExtract=true `
  --output ".\publish\win-x64"
```

### Licencia

BT Device Battery Info se distribuye bajo la licencia MIT. Consulta el archivo [LICENSE](LICENSE) para ver el texto completo.

Consulta [CHANGELOG.md](CHANGELOG.md) para ver las notas de versión.
