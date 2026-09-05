# Pruebas y validación

Este documento separa las comprobaciones reproducibles de las pruebas que dependen de un adaptador Bluetooth y de dispositivos reales.

## Validación automatizada

Ejecutar desde la raíz del repositorio:

```powershell
dotnet restore ".\BT Device Battery Info.slnx"
dotnet format ".\BT Device Battery Info.slnx" --verify-no-changes --no-restore
dotnet build ".\BT Device Battery Info.slnx" --configuration Release --no-restore
dotnet test ".\BT Device Battery Info.slnx" --configuration Release --no-restore
git diff --check
```

No hay una suite basada en `Microsoft.NET.Test.Sdk`, xUnit, NUnit o MSTest: `dotnet test` termina sin pruebas descubiertas. Desde 0.13 existe además un ejecutable de validación del coordinador, sin dependencias externas ni hardware:

```powershell
dotnet run --project BTDeviceBatteryInfo/validation/BatteryQueries/BatteryQueries.csproj -c Release
```

Comprueba publicación temprana, retención de operaciones pendientes, rechazo de valores inválidos, cancelación, resultados tardíos e independencia entre dispositivos. No valida WinRT, la agrupación de endpoints ni el aspecto visual.

La integración continua ejecuta `restore`, `format`, `build` y `test` en `windows-latest`; `test` todavía no descubre una suite de pruebas.

## Matriz mínima manual

La prueba debe registrar resultado, versión de Windows, arquitectura, adaptador, controlador y modelo de dispositivo. No compartir direcciones Bluetooth ni identificadores completos.

| ID | Escenario | Resultado esperado |
| --- | --- | --- |
| T-01 | Inicio normal | La ventana aparece siempre; no existe ni se ofrece un modo de inicio minimizado. |
| T-02 | Carga inicial | La ventana permanece operativa, los dispositivos aparecen progresivamente y el indicador termina al aparecer el primer dispositivo o al completar/agotar en ocho segundos la enumeración inicial. |
| T-03 | Inventario | Los dispositivos conectados aparecen en la lista principal; los emparejados desconectados aparecen dentro de `Disconected Devices`, contraído por defecto y con el número de dispositivos. |
| T-04 | Batería disponible | Se muestra un porcentaje entre 0 y 100 cuando Windows lo proporciona. |
| T-05 | Batería no disponible | Se muestra `Battery unavailable` sin bloquear la interfaz. |
| T-06 | Varios dispositivos | Con uno o dos dispositivos se conserva la altura base; cada dispositivo adicional agrega una fila de 56 px sin barra de desplazamiento. |
| T-07 | Desconexión | El estado cambia, la lista se actualiza y la altura vuelve a la base cuando quedan dos o menos dispositivos. |
| T-08 | Dispositivos similares | Dos dispositivos con identidades diferentes permanecen separados; los endpoints del mismo contenedor se agrupan según la decisión actual. |
| T-09 | Cambios del watcher | Conectar, desconectar, quitar y volver a emparejar dispositivos no deja entradas obsoletas ni duplica filas. |
| T-10 | Bandeja | El menú contiene `Show widget`/`Hide widget`, `Start with Windows` y `Exit`; mostrar, ocultar y salir funcionan. |
| T-11 | Inicio con Windows | El check de `Start with Windows` cambia, se persiste y la entrada se crea o elimina solo para el usuario actual. |
| T-12 | Instancia única | Un segundo lanzamiento informa que la aplicación ya está abierta y no crea otra ventana ni otro watcher. |
| T-13 | Icono y UI | El icono de ventana y bandeja es correcto; el engrane permanece visible, atenuado y no interactivo; `Development in Beta` no cubre la última fila. |
| T-14 | Reconexión automática | Tras desconectar el dispositivo seleccionado, la aplicación espera la política configurada y solo muestra el estado confirmado por Windows. No debe eliminar emparejamientos ni simular una conexión. |
| T-15 | Registros | Se generan logs locales en `%LocalAppData%\BTDeviceBatteryInfo\Logs` sin compartir identificadores sensibles. |
| T-16 | Grupo desconectados | El grupo se puede expandir y contraer; al expandirlo la ventana muestra las filas desconectadas y al contraerlo vuelve a ocultarlas. |
| T-17 | Reintento de batería | Un resultado vacío no bloquea futuros intentos; al reconectar o vencer el intervalo de reintento se vuelven a consultar PnP/GATT sin duplicar consultas simultáneas. |
| T-18 | About | El engranaje abre la ventana `About` con avatar, descripción en inglés, `© 2026 Boris SdK`, y enlaces individuales clicables para Instagram, Twitch y LinkedIn. |

## Rendimiento de inicio

El rendimiento de inicio es la prioridad Alta `P-001` de `pending.md`. Antes de optimizar, registrar cinco ejecuciones en frío y cinco en caliente para cada condición:

- sin dispositivos Bluetooth presentes;
- con un dispositivo emparejado;
- con varios dispositivos emparejados.

Medir como mínimo:

1. lanzamiento del proceso a ventana visible;
2. ventana visible a primer mensaje/lista útil;
3. primer mensaje/lista útil a batería disponible.

Anotar hardware, versión de Windows, configuración y cualquier error del log. El objetivo numérico se definirá después de obtener la línea base.

Evidencia inicial posterior a la primera optimización (`2026-09-01`, una ejecución en el equipo de desarrollo): ventana y proceso respondieron a los 3 y 12 segundos; `DeviceWatcher.Start()` retornó en 57 ms; el dispositivo persistido se seleccionó en el mismo segundo del lanzamiento; el cierre formal por timeout ocurrió a los 8008 ms con 25 endpoints en caché. Esta ejecución demuestra el flujo no bloqueante, pero no reemplaza las diez repeticiones en frío/caliente ni define todavía el objetivo de producto.

Evidencia de interfaz y batería (`2026-09-01`): UI Automation expandió `Disconected Devices` correctamente; la ventana pasó de 306 a 530 px y mostró cuatro dispositivos desconectados. En una sesión prolongada, una consulta obtuvo `1/2` baterías mediante GATT y los resultados vacíos se reintentaron después; el Bose probado continuó sin dato de Windows en esos ciclos.

## Evidencia de una prueba

Validación manual pendiente para 0.13: repetir arranque con varios dispositivos y confirmar que una batería rápida aparece aunque otra no responda; desconectar y reconectar durante una consulta; cerrar mientras Windows responde; arrastrar la ventana y comprobar que conserva su posición al reiniciar. Comparar el tiempo de los registros `Batería disponible en ... ms` y el tiempo hasta el porcentaje visible con la versión anterior. El registro mide desde la programación de la consulta, no desde el lanzamiento del proceso.

```text
Fecha:
Windows y arquitectura:
Adaptador/controlador:
Dispositivo o condición:
ID de escenario:
Pasos ejecutados:
Resultado esperado:
Resultado observado:
Log redactado:
Limitaciones:
```

## Limitaciones conocidas

- No hay hardware Bluetooth presente en todos los entornos de desarrollo.
- La batería depende del perfil, dispositivo, controlador y versión de Windows.
- No se debe afirmar que `dotnet test` valida el comportamiento Bluetooth mientras no exista una suite automatizada y una estrategia de dobles para WinRT.
- Las acciones manuales `RECONNECT` y `FORCE RECONNECT` no forman parte de la interfaz actual; su documentación se mantiene como pendiente hasta decidir si se exponen o se eliminan.
