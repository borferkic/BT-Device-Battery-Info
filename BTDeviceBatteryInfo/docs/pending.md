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

### P-002 — Actualizar la batería durante la sesión

Implementación para `0.13` (`2026-09-05`): consulta desde la detección, publicación independiente, retención de operaciones nativas pendientes, deduplicación por dirección, límite de dos consultas GATT y reintentos progresivos. Las pruebas del coordinador y la compilación no sustituyen la aceptación con hardware; el criterio de latencia permanece pendiente.

- [x] Evitar que el caché conserve indefinidamente un porcentaje antiguo o `Battery unavailable` (`2026-09-01`).
- [x] Reintentar PnP/GATT cuando el dispositivo se conecte, cambie de estado o venza una actualización periódica (`2026-09-01`).
- [x] Evitar consultas duplicadas simultáneas para el mismo contenedor (`2026-09-01`).
- [x] Consultar directamente el `DeviceContainer` y probar los endpoints BLE candidatos del mismo dispositivo antes de declarar la batería no disponible (`2026-09-01`).
- [ ] Reducir el tiempo desde que el dispositivo aparece en la lista hasta que se muestra su batería, sin retrasar la ventana ni el inventario inicial.

Criterios de aceptación: un dispositivo BLE/GATT que cambia de nivel refleja el nuevo porcentaje sin reiniciar la aplicación; los fallos transitorios se pueden recuperar y el indicador aparece en un tiempo perceptiblemente ágil después de detectar el dispositivo. La implementación está aplicada, pero falta confirmar este criterio con un Bose que vuelva a exponer batería después de desconectar y reconectar.

Hallazgo histórico de QA del `2026-09-01`: algunos auriculares Bose conectados mostraban `Battery unavailable` de forma intermitente. La implementación anterior conservaba resultados vacíos sin reintentar. Ese comportamiento ya había sido modificado en 0.12; en 0.13 se corrigen además la espera del descubrimiento y la publicación conjunta que retrasaban resultados disponibles.

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

### P-016 — Detectar y activar Bluetooth desactivado

- [ ] Detectar cuando el adaptador Bluetooth de Windows esté desactivado y mostrar el mensaje en inglés `Bluetooth is turned off.`.
- [ ] Añadir un botón `Turn on Bluetooth` que intente activar Bluetooth directamente desde la aplicación.
- [ ] Investigar y validar una API oficial de Windows compatible con la versión mínima soportada antes de implementar la activación.
- [ ] No simular una activación: si Windows no permite activarlo por API o requiere intervención del usuario, informar el resultado real y ofrecer una alternativa segura.

Objetivo observable: cuando el usuario apaga Bluetooth desde Windows, el widget deja de presentar la lista como un fallo genérico, explica que Bluetooth está desactivado y ofrece la acción directa solicitada.

Criterios de aceptación: el estado se actualiza al apagar o encender Bluetooth, el botón comunica claramente éxito o motivo de imposibilidad y no modifica otros adaptadores ni emparejamientos.

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

### P-017 — Gadget individual en la barra de inicio

- [ ] Permitir fijar un gadget individual para uno de los dispositivos seleccionados.
- [ ] Mostrar en ese gadget el nombre, estado de conexión y batería del dispositivo indicado.
- [ ] Investigar el mecanismo oficial de Windows para integrarlo en la barra de inicio y definir sus limitaciones.
- [ ] Mantener la selección y la actualización del dispositivo aunque la ventana principal esté cerrada o minimizada.

Objetivo observable: el usuario puede elegir un dispositivo y consultar sus datos desde un gadget independiente en la barra de inicio, sin abrir toda la lista.

Criterios de aceptación: el gadget muestra únicamente el dispositivo elegido, actualiza sus datos sin duplicar consultas y ofrece un comportamiento claro cuando el dispositivo está desconectado o no expone batería.

## Completado recientemente

- [x] Reducir el tiempo de inicio: la ventana y el inventario inicial se publican sin esperar consultas secundarias; validado funcionalmente por el responsable del proyecto (`2026-09-01`).
- [x] Eliminar la opción de inicio minimizado y mostrar siempre la ventana al iniciar (`2026-09-01`).
- [x] Crear una skill de proyecto para documentación, QA y changelog (`2026-09-01`).
- [x] Crear este backlog canónico y el registro `Unreleased` (`2026-09-01`).
- [x] Sustituir la consulta Bluetooth inicial bloqueante por descubrimiento incremental con timeout blando (`2026-09-01`).
