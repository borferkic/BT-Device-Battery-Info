# Lineamientos para trasladar y continuar el proyecto

Este documento permite retomar **BT Device Battery Info** en otra PC sin depender de las memorias del agente anterior. Léelo junto con [las instrucciones locales](../AGENTS.md) antes de trabajar. Describe el estado observado el 24 de septiembre de 2026; vuelve a comprobar versiones, archivos y pendientes al abrir la copia trasladada.

## Qué trasladar antes de formatear

- Conserva una copia completa de esta carpeta de trabajo, incluido `.git` si quieres mantener el historial local. La URL de `origin` es `https://github.com/borferkic/BT-Device-Battery-Info.git`, pero una copia descargada del remoto puede omitir cambios locales y archivos excluidos por Git.
- Revisa `git status --short` y guarda también todo cambio sin publicar. En la revisión de este documento, `BTDeviceBatteryInfo/AGENTS.md` tenía una modificación local sobre la versión `0.18` y la limpieza de builds de desarrollo. Este documento nuevo también será local hasta que se incluya expresamente en una copia o publicación.
- Decide si necesitas los ejecutables históricos: `Builds/` (raíz del repositorio) está excluida por Git. La revisión local mostró carpetas publicadas `0.10` a `0.18` y una carpeta `0.18-dev4`. Cópialas por separado si quieres conservar esos artefactos; comprueba el inventario otra vez antes de formatear.
- Si deseas conservar preferencias y diagnósticos, copia por separado `%LocalAppData%\BTDeviceBatteryInfo\settings.json` y `%LocalAppData%\BTDeviceBatteryInfo\Logs`. No incluyas logs ni configuraciones personales en publicaciones. La aplicación crea su directorio de datos en el perfil de la nueva PC.
- Revisa otros archivos excluidos antes de hacer la copia. `.gitignore` excluye, entre otros, `bin/`, `obj/`, `publish/`, `artifacts/`, `Builds/`, `Builds-Test/` y `docs/improvement-plan.md`. Las carpetas generadas se pueden recrear; un plan local o una build histórica quizá no.
- No presupongas que los emparejamientos Bluetooth, controladores, configuración de Windows o resultados de pruebas físicas viajan con el código. Registra el adaptador, controlador, versión de Windows y dispositivos necesarios para reproducir una incidencia, sin compartir direcciones Bluetooth ni identificadores completos.

## Primeros pasos en la nueva PC

1. Instala Windows con soporte para las API Bluetooth usadas por el proyecto y el SDK de .NET 8 para escritorio. Abre la solución `BT Device Battery Info.slnx` desde la raíz del repositorio; el proyecto WPF está en `BTDeviceBatteryInfo/BTDeviceBatteryInfo.csproj` y apunta a `net8.0-windows10.0.19041.0` con WPF y Windows Forms.
2. Comprueba `git status --short`, `git log -1 --oneline`, los tags y la versión del `.csproj`. A la fecha de este traspaso, la versión del proyecto es `0.18` y existe el tag `v0.18`. Trata esta información como una referencia histórica, no como sustituto del estado de la nueva copia.
3. Lee [la arquitectura](architecture.md), [los pendientes](pending.md), [las pruebas](testing.md) y [el estándar documental](documentation-standard.md). `pending.md` es el backlog canónico. Algunas notas de `testing.md` y `docs/README.md` pueden describir versiones anteriores; contrástalas con el código, `CHANGELOG.md` y la versión actual antes de planificar o informar resultados.
4. Para preparar dependencias, usa `dotnet restore ".\BT Device Battery Info.slnx"` desde la raíz. Las instrucciones de compilación y validación están en [pruebas](testing.md) y [CONTRIBUTING.md](../../CONTRIBUTING.md). Antes de generar **cualquier build**, presenta al usuario la lista concreta de tareas y espera su confirmación explícita, según `AGENTS.md`. Si ya aprobó ese alcance, ejecútalo sin pedir la misma confirmación otra vez.

## Forma de trabajar con el usuario

- Comunícate con el usuario exclusivamente en español. Escribe en español los documentos y registros nuevos de trabajo, conservando los identificadores, comandos y cadenas de producto que deban permanecer literales.
- Mantén en inglés los mensajes nuevos del log de la aplicación, los changelogs de versiones y las notas públicas de GitHub. En las notas públicas de release incluye únicamente funciones añadidas y correcciones; registra validación, límites y cobertura pendiente en la documentación local.
- Mantén el trabajo local hasta que el usuario autorice expresamente una operación Git o remota. No interpretes una solicitud de análisis, documentación o inventario como permiso para hacer `git add`, commit, push, tag, release, mover material privado o borrar archivos. `README.md` requiere autorización textual antes de editarlo.
- Antes de editar, inspecciona el estado Git, el diff, el código responsable y los documentos relacionados. Analiza la ruta o propiedad real de Windows antes de modificar Bluetooth o batería. Haz cambios acotados y explica el resultado y la evidencia sin confundir compilación con prueba física o visual.
- Guarda las nuevas decisiones e instrucciones específicas del proyecto en `BTDeviceBatteryInfo/AGENTS.md`, dentro de esta copia. Actualiza este documento de traspaso si cambian reglas que un futuro agente necesite conocer.
- Para una build de revisión usa `Builds/<versión>-dev<número>` con número consecutivo, sin palabras descriptivas. Conserva las builds publicadas en carpetas de versión. Si se autoriza limpiar builds antiguas, comprueba primero los nombres y limita la eliminación a las carpetas `dev` indicadas, incluido todo su contenido.
- Espera la prueba manual del usuario antes de registrar sus resultados en QA, changelog o pendientes. Los pendientes de `AGENTS.md` deben mantenerse concisos, con un máximo de dos líneas por pendiente.

## Arquitectura y límites que conviene preservar

- `Models/` contiene los datos; `Services/BluetoothService.cs` administra el inventario, el `DeviceWatcher` y las consultas; `Services/BluetoothPnP.cs` consulta propiedades nativas de Windows; `ViewModels/MainViewModel.cs` coordina selección, refresco y estado; `MainWindow.xaml`, `OptionsWindow.xaml` y `TaskbarWidgetController.cs` presentan la interfaz.
- Los endpoints de un mismo dispositivo se agrupan por `System.Devices.Aep.ContainerId`. `IsConnected` pertenece al endpoint, no confirma por sí solo el estado físico completo. La implementación actual usa precedencia Classic cuando hay endpoints Classic y BLE en el grupo, y BLE para dispositivos solo BLE. `ProtocolId` indica el protocolo de descubrimiento, no demuestra qué perfil de audio está activo.
- Para batería de auriculares Classic, examina los devnodes genéricos del contenedor y `DEVPKEY_Bluetooth_BatteryLevel` antes de concluir que GATT no da resultado. La ruta HFP/PnP usa `CfgMgr32`, `ContainerId` y `BthHFEnum`; GATT `180F`/`2A19` es una alternativa para BLE. No añadas filtros por marca, nombres, direcciones o rutas de registro de un dispositivo concreto. En la lectura nativa de `ContainerId`, `DEVPROP_TYPE_GUID` se observó como valor `13`.
- La aplicación no debe eliminar emparejamientos, alterar controladores ni simular una conexión. Las API públicas de Windows no ofrecen una orden general fiable para conectar perfiles de audio Bluetooth Classic; muestra solo el estado que Windows confirma.
- Las preferencias y los logs viven en `%LocalAppData%\BTDeviceBatteryInfo`. Redacta identificadores de dispositivos en evidencias compartidas y conserva en los logs solo los datos necesarios para diagnosticar.

## Estado de trabajo que requiere atención

- `P-014` permanece abierto en [pendientes](pending.md): la agrupación y selección por identidad física están implementadas, pero falta una validación controlada con hardware multiprotocolo. Comprueba desconexión Classic mientras BLE sigue presente, retirada del endpoint representante, reconexión, recuperación de batería y estabilidad en varios ciclos de reconciliación. No declares resuelto el problema por una compilación o por inspección estática.
- `P-016` requiere investigar una API oficial para detectar y, si Windows lo permite, activar el adaptador Bluetooth. El botón solicitado no debe prometer éxito sin confirmación de Windows.
- `P-017` tiene la presentación compacta confirmada por el usuario en una build anterior; siguen pendientes pruebas de DPI y recuperación tras reiniciar Explorer. Consulta el estado actual antes de cerrar la tarea.
- La validación `tests/BatteryQueries` cubre escenarios del coordinador de batería, pero no sustituye pruebas de WinRT, de agrupación de endpoints ni de interfaz. `dotnet test` sobre la solución no descubre una suite de pruebas de marco convencional según la documentación actual. Registra por separado cada comando ejecutado y cada prueba manual confirmada.

## Publicación y trazabilidad

- Mantén sincronizadas la versión del `.csproj`, la versión visible en `AboutWindow.xaml`, el artefacto y las notas de la versión cuando el usuario autorice una release.
- Una publicación autocontenida de archivo único para `win-x64` puede requerir primero `dotnet restore .\BTDeviceBatteryInfo\BTDeviceBatteryInfo.csproj --runtime win-x64`; de otro modo puede aparecer `NETSDK1047`. Guarda el ejecutable solicitado en `Builds/<versión>/`, en la raíz del repositorio.
- Distingue siempre entre código compilado, ejecutable publicado, comprobación visual del usuario, prueba física Bluetooth y publicación remota. Registra la evidencia concreta de cada etapa en [pendientes](pending.md) o en `CHANGELOG.md` cuando esa etapa haya ocurrido.
