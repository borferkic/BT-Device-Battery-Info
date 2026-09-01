# Estado del QA

## Revisión actual

- Fecha: `2026-09-01`
- Alcance: documentación, estructura del código, compilación, formato, flujo de inicio, ejecución del binario, hardware disponible y cambios incluidos en `0.12`.
- Estado: `No aprobado para cierre`.
- Bloqueador funcional principal: `P-001` ya tiene una primera optimización, pero todavía no cuenta con la línea base repetida ni un objetivo de tiempo aprobado.

## Validaciones ejecutadas

| Validación | Resultado | Observación |
| --- | --- | --- |
| `dotnet restore` | Correcta | Requirió permitir el acceso a la configuración NuGet del usuario en el entorno de QA. |
| `dotnet format --verify-no-changes` | Correcta después de normalizar | Se estableció el formato de C#/XAML en `.editorconfig`. |
| `dotnet build --configuration Release` | Correcta | 0 advertencias y 0 errores. |
| `dotnet test` | Sin pruebas descubiertas | No existe todavía un proyecto de pruebas automatizadas. |
| `git diff --check` | Correcta | Sin errores de espacios o parches. |
| Ejecución del binario | Correcta con observación | La ventana y el proceso respondieron a los `3` y `12` segundos sin nuevas excepciones; la ventana nativa fue localizada visible aunque `Process.MainWindowHandle` no la enumera por `ShowInTaskbar=False`. |
| Arranque Bluetooth incremental | Correcta, pendiente de repetición | El watcher retornó en `57` ms, el dispositivo persistido se seleccionó en el mismo segundo y el timeout cerró a los `8008` ms con `25` endpoints. |
| Grupo `Disconected Devices` | Correcta | UI Automation encontró el grupo, lo expandió y confirmó el cambio de altura de `306` a `530` px con cuatro dispositivos desconectados visibles. |
| Indicador de batería | Parcial, con reintento | Una sesión obtuvo `1/2` baterías mediante GATT y reintentó resultados vacíos; el Bose probado continuó sin dato expuesto por Windows. |
| Ventana `About` | Correcta | El engranaje abre la ventana con avatar, descripción en inglés, copyright, tres enlaces sociales individuales e insignias minimalistas; la comprobación visual confirmó la composición final. |
| Hardware Bluetooth | Parcial | Se detectaron endpoints y el dispositivo persistido; falta completar la validación visual y funcional de lista, batería, bandeja y reconexión. |

## Hallazgos abiertos

- `P-001`: completar la línea base en frío/caliente, verificar integridad visual de la lista y definir el objetivo después de la primera optimización.
- `P-002`: confirmar con hardware Bose que los reintentos recuperan el nivel de batería después de reconectar.
- `P-003`: validar en más modelos el grupo de dispositivos desconectados.
- `P-014`: corregir estados `Connected` falsos y oscilaciones causadas por múltiples endpoints del mismo dispositivo.
- `P-005`: alinear la reconexión interna con la interfaz y la documentación.
- `P-006`: incorporar pruebas automatizadas.
- `P-007`: crear una matriz de dispositivos, controladores y versiones de Windows.
- `P-008`: validar configuración y manejar errores de persistencia asincrónica.
- `P-010`: endurecer el cierre y la recuperación del `DeviceWatcher`.

## Incidencia reproducida y corregida durante esta revisión

- El binario anterior terminaba aproximadamente a los `15` segundos con el código `0xe0434352`, una excepción .NET no controlada.
- Se añadieron límites de error para el arranque, `Loaded`, cambios de posición y eventos Bluetooth, además de un registro `errors.log` con la excepción completa si vuelve a ocurrir.
- La ejecución posterior permaneció activa durante `55` segundos sin reproducir la terminación. El origen exacto no quedó disponible en los eventos WER del entorno, por lo que se conserva el registro defensivo para futuras evidencias.

## Incidencias de batería y conexión en análisis

- Algunos auriculares Bose conectados no entregan batería de forma consistente. Los registros muestran timeouts y resultados sin datos; ahora el resultado vacío caduca y vuelve a intentarse, pero el Bose probado siguió sin exponer un porcentaje.
- Algunos dispositivos permanecen visualmente en `Connected` después de desconectarlos. El inventario agrupa varios endpoints físicos, pero la selección actual puede priorizar un endpoint BLE auxiliar todavía marcado como conectado.
- Ambos hallazgos requieren una prueba controlada de desconexión/reconexión con hardware antes de considerarse corregidos.

## Criterio para continuar

Continuar con `P-001`: repetir la matriz de medición en frío/caliente y confirmar visualmente que la publicación incremental conserva todos los dispositivos y estados antes de definir el objetivo de tiempo.
