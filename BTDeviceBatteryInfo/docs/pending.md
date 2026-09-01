# Pendientes del proyecto

Backlog canónico de BT Device Battery Info. Las tareas se priorizan por impacto observable y se mantienen aquí hasta que exista implementación y evidencia de validación.

Última revisión documental: `2026-09-01`.

## Cómo usar este documento

- `Alta`: afecta el arranque, la función principal, la exactitud del estado o la estabilidad.
- `Media`: mejora una función existente sin bloquear el uso principal.
- `Baja`: soporte, distribución, accesibilidad o mejoras de evolución.
- Cada tarea debe conservar un objetivo observable y criterios de aceptación.
- Una tarea solo pasa a completada después de actualizar el changelog correspondiente y ejecutar la validación aplicable.

## Prioridad alta

### P-001 — Reducir el tiempo de inicio

- [ ] Medir por separado el tiempo desde el lanzamiento hasta que aparece la ventana y hasta que se muestra la primera lista de dispositivos.
- [ ] Comparar arranque en frío y en caliente con cero, uno y varios dispositivos emparejados.
- [x] Mostrar la ventana y el estado inicial sin bloquearla con consultas no esenciales (`2026-09-01`).
- [x] Mantener la hidratación de batería y otras tareas secundarias fuera del camino crítico cuando sea seguro (`2026-09-01`).
- [ ] Definir un objetivo de tiempo después de obtener la línea base y registrarlo en `docs/testing.md`.

Causa técnica confirmada: `MainWindow.Loaded` esperaba una consulta completa de endpoints Bluetooth antes de recibir el inventario inicial. La primera corrección sustituyó esa consulta por publicación incremental mediante `DeviceWatcher`, movió su inicio a segundo plano y pospuso la hidratación de batería.

Observación de QA del `2026-09-01`: la ventana fue localizada visible durante la reproducción, pero la consulta inicial no había terminado después de `55` segundos y no había escrito su registro de finalización. La cifra es una evidencia de este equipo, no todavía un objetivo general de producto.

Evidencia posterior a la primera corrección (`2026-09-01`): en una ejecución real, el watcher retornó en `57` ms y el dispositivo persistido fue seleccionado en el mismo segundo del lanzamiento. La ventana y el proceso respondieron a los `3` y `12` segundos, no hubo nuevas excepciones y el límite de `8008` ms cerró la enumeración con `25` endpoints en caché. Faltan las series en frío/caliente y verificar manualmente que la lista visual no omita dispositivos.

Criterios de aceptación: existe una línea base reproducible, la ventana aparece antes de las consultas secundarias y una prueba repetida demuestra una mejora sin perder dispositivos ni estados.

### P-002 — Actualizar la batería durante la sesión

- [x] Evitar que el caché conserve indefinidamente un porcentaje antiguo o `Battery unavailable` (`2026-09-01`).
- [x] Reintentar PnP/GATT cuando el dispositivo se conecte, cambie de estado o venza una actualización periódica (`2026-09-01`).
- [x] Evitar consultas duplicadas simultáneas para el mismo contenedor (`2026-09-01`).
- [x] Consultar directamente el `DeviceContainer` y probar los endpoints BLE candidatos del mismo dispositivo antes de declarar la batería no disponible (`2026-09-01`).

Criterios de aceptación: un dispositivo BLE/GATT que cambia de nivel refleja el nuevo porcentaje sin reiniciar la aplicación; los fallos transitorios se pueden recuperar. La implementación está aplicada, pero falta confirmar este criterio con un Bose que vuelva a exponer batería después de desconectar y reconectar.

Hallazgo de QA del `2026-09-01`: algunos auriculares Bose conectados muestran `Battery unavailable` de forma intermitente. Los registros contienen consultas agotadas a los cinco segundos y resultados `0/1`. El código actual guarda `null` en `_containerBatteries`, lo trata como resultado definitivo y elige un solo endpoint por `ContainerId`; un fallo transitorio o la elección de un endpoint Classic puede impedir nuevos intentos durante toda la sesión.

### P-003 — Definir el alcance de la lista de dispositivos

- [x] Decidir que la interfaz muestra dispositivos conectados en la lista principal y dispositivos emparejados desconectados en un grupo contraído (`2026-09-01`).
- [x] Alinear la decisión en `README.md`, `docs/architecture.md`, `docs/testing.md` y los mensajes de la interfaz (`2026-09-01`).
- [x] Mantener conectados, desconectados y seleccionados en estados visualmente distinguibles (`2026-09-01`).

Criterios de aceptación: el comportamiento esperado está escrito, probado con dispositivos similares y no hay documentación contradictoria. La expansión y contracción fueron comprobadas mediante UI Automation.

### P-014 — Corregir estados de conexión falsos

- [ ] Determinar la conexión del dispositivo físico usando el conjunto de endpoints del `ContainerId`, sin conservar como seleccionado un endpoint obsoleto.
- [ ] Diferenciar un canal BLE de control todavía presente de una conexión activa del perfil principal cuando Windows exponga información suficiente.
- [ ] Invalidar el inventario y el estado seleccionado después de eventos `Removed`, desconexiones confirmadas y reconciliaciones.
- [ ] Evitar oscilaciones `Disconnected`/`Connected` causadas por distintos endpoints del mismo dispositivo.

Hallazgo de QA del `2026-09-01`: después de desconectar algunos dispositivos, la interfaz puede seguir indicando `Connected`. En una sesión se observaron alternancias repetidas cada 30 segundos; Windows no reportaba endpoints Classic presentes, mientras la aplicación conservaba una instancia activa. `BuildDeviceSnapshot()` selecciona un único endpoint y prioriza cualquiera que tenga `IsConnected`, lo que puede confundir un endpoint BLE auxiliar con la conexión principal o mantener una identidad seleccionada que ya no representa al dispositivo agrupado.

Criterios de aceptación: al desconectar físicamente un dispositivo, la interfaz cambia a `Disconnected` sin volver a `Connected` por un endpoint auxiliar; al reconectarlo, recupera el estado y la batería sin reiniciar la aplicación.

### P-015 — Añadir pantalla About

- [x] Reutilizar el engranaje para abrir una ventana `About` (`2026-09-01`).
- [x] Mostrar avatar, descripción, copyright y enlaces del creador (`2026-09-01`).
- [x] Validar que Instagram, Twitch y LinkedIn están presentes como enlaces separados (`2026-09-01`).

Criterios de aceptación: la ventana mantiene el estilo del widget, los textos son legibles y cada enlace apunta a la red correspondiente. Validación visual y UI Automation completadas.

## Prioridad media

### P-004 — Añadir actualización manual

- [ ] Incorporar `Refresh now` al menú de bandeja.
- [ ] Mostrar un estado temporal y conservar la coalescencia de eventos Bluetooth.
- [ ] Registrar el tiempo de la consulta sin exponer identificadores sensibles.

### P-005 — Alinear la reconexión con la interfaz

- [ ] Decidir si `ReconnectCommand` debe exponerse al usuario o permanecer como lógica interna.
- [ ] Eliminar de `docs/testing.md` las instrucciones para comandos que no tienen control en XAML, o añadir una interfaz explícita y segura.
- [ ] Mantener la regla de no simular una conexión Bluetooth Classic.

### P-006 — Crear pruebas automatizadas

- [ ] Añadir un proyecto de pruebas independiente.
- [ ] Cubrir `ReconnectPolicy`, persistencia de `AppSettings` y transformaciones de batería.
- [ ] Cubrir agrupación de endpoints, selección, coalescencia de refrescos y cancelación de reconexión.
- [ ] Ejecutar las pruebas en CI además de la compilación.

### P-007 — Crear una matriz de validación Bluetooth

- [ ] Registrar modelo, perfil, controlador, versión/arquitectura de Windows y resultado.
- [ ] Probar Bluetooth Classic, BLE, dispositivos sin batería expuesta y varios endpoints del mismo contenedor.
- [ ] Mantener identificadores reales redactados en evidencias compartidas.

### P-008 — Mejorar preferencias y configuración

- [ ] Exponer solo preferencias soportadas desde la bandeja, como `AlwaysOnTop` y opacidad.
- [ ] Validar y acotar valores cargados desde `settings.json`.
- [ ] Manejar errores de guardado en `LocationChanged` y en el arranque sin dejar excepciones asincrónicas sin observar.
- [x] Mantener la ventana visible al iniciar; no reintroducir una opción de inicio minimizado (`2026-09-01`).

La persistencia de posición y la inicialización de ventana ahora registran errores de forma controlada; todavía falta validar límites de valores, concurrencia y recuperación completa.

### P-009 — Mejorar estados vacíos y accesibilidad

- [ ] Diferenciar ausencia de dispositivos, Bluetooth desactivado y error de consulta cuando Windows lo permita.
- [ ] Revisar navegación por teclado, foco, lector de pantalla, contraste y alto contraste.
- [ ] Añadir una acción para abrir la configuración Bluetooth si resulta apropiado.

### P-010 — Endurecer el ciclo de vida

- [ ] Cancelar y liberar correctamente tareas de hidratación, reconciliación y recuperación del watcher durante el cierre.
- [ ] Evitar que eventos asincrónicos publiquen cambios después de liberar sus consumidores.
- [ ] Definir una política de reintento si la recuperación del `DeviceWatcher` falla varias veces.

Criterios de aceptación: cerrar la aplicación durante una consulta no produce excepciones no observadas, bloqueos ni callbacks sobre objetos liberados.

## Prioridad baja

### P-011 — Diagnóstico y soporte

- [ ] Mostrar o exportar un resumen redactado con adaptador, última actualización y fuente de batería.
- [ ] Añadir notificaciones de batería baja con umbral configurable y sin repeticiones excesivas.

### P-012 — Distribución y mantenimiento

- [ ] Preparar un paquete distribuible con versión visible, icono, actualización y desinstalación.
- [ ] Definir un proceso de publicación que no dependa solo de artefactos manuales.

### P-013 — Agrupación configurable

- [ ] Evaluar una preferencia para mostrar por separado o agrupar endpoints que compartan `ContainerId`.

## Completado recientemente

- [x] Eliminar la opción de inicio minimizado y mostrar siempre la ventana al iniciar (`2026-09-01`).
- [x] Crear una skill de proyecto para documentación, QA y changelog (`2026-09-01`).
- [x] Crear este backlog canónico y el registro `Unreleased` (`2026-09-01`).
- [x] Sustituir la consulta Bluetooth inicial bloqueante por descubrimiento incremental con timeout blando (`2026-09-01`).
