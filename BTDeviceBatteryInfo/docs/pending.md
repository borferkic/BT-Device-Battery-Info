# Pendientes del proyecto

Backlog canónico de BT Device Battery Info. Las tareas se priorizan por impacto observable y se mantienen aquí hasta que exista implementación y evidencia de validación.

Última revisión documental: `2026-09-25`.

## Cómo usar este documento

- `Alta`: afecta el arranque, la función principal, la exactitud del estado o la estabilidad.
- `Media`: mejora una función existente sin bloquear el uso principal.
- `Baja`: soporte, distribución, accesibilidad o mejoras de evolución.
- Cada tarea debe conservar un objetivo observable y criterios de aceptación.
- Una tarea solo pasa a completada después de actualizar el changelog correspondiente y ejecutar la validación aplicable.

## Prioridad alta

Sin pendientes de prioridad alta (P-014 y P-016 cerrados en `0.21`).

## Prioridad media

### P-006 — Crear pruebas automatizadas

- [x] Añadir un proyecto de pruebas independiente: `tests/BTDeviceBatteryInfo.Tests` (xUnit, 33 pruebas), que sustituye a `tests/BatteryQueries` (`2026-09-25`).
- [x] Cubrir `ReconnectPolicy`, `AppLanguage.Normalize`, la lógica de batería de las tarjetas y el coordinador `BatteryQueryRunner`.
- [x] Verificar que los tres temas y los dos idiomas definen las mismas claves.
- [ ] Cubrir la persistencia de `AppSettings`.
- [ ] Cubrir agrupación de endpoints, selección, coalescencia de refrescos y cancelación de reconexión.
- [x] Ejecutar las pruebas en CI además de la compilación (`dotnet test` sobre la solución).

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

### P-018 — Registrar conexiones y desconexiones para QA

- [ ] Registrar fecha y hora, dispositivo con identificadores redactados, transición de conexión y fuente/evidencia disponible (por ejemplo, eventos del watcher y estado Classic/BLE).
- [ ] Diferenciar una causa confirmada por Windows de una inferencia; no atribuir un motivo físico que el sistema no haya reportado.
- [ ] Evitar entradas repetidas cuando el estado no cambie y definir límites de tamaño o retención para el registro local.
- [ ] Facilitar el uso del registro local para diagnosticar errores sin exponer direcciones ni identificadores sensibles.

Objetivo observable: ante una conexión o desconexión, QA puede reconstruir cuándo se detectó el cambio y qué evidencia disponible lo acompañó.

Criterios de aceptación: el registro local contiene transiciones fechadas y redactadas con su fuente/evidencia, no duplica estados inalterados, distingue causas conocidas de inferencias y respeta la política de retención definida.

### P-026 — Batería de mandos Bluetooth (8BitDo)

- [x] Leer la batería de mandos en modo X-input con `Windows.Gaming.Input` como fuente de último recurso (0.20-dev2; confirmado con el Arcade Stick).
- [ ] Validar el porcentaje frente al nivel real. El SN30 Pro para Xbox queda fuera: por Bluetooth solo tiene modo Android, sin batería.

## Completado recientemente

- [x] P-020 — Selección de idioma inglés/español en `Options` con vista previa en vivo, guardado al iniciar e inglés como predeterminado y respaldo; logs en inglés. Validado por el usuario en `0.21-dev8` y publicado en `0.21` (`2026-09-25`).

- [x] P-017 — Widget compacto de dispositivos conectados en la barra de tareas; cerrado por el usuario (`2026-09-25`).

## Descartados

- P-007 — Matriz de validación Bluetooth; retirado por el usuario (`2026-09-25`): las pruebas reales con hardware son suficientes.
- P-013 — Agrupación configurable; retirado por el usuario (`2026-09-25`): se mantiene la agrupación por dispositivo físico.
- P-019 — Vista compacta tipo pastilla; retirado por el usuario (`2026-09-25`): el widget de la barra de tareas ya cubre esa función.

- P-008 — Mejorar preferencias y configuración; retirado del backlog por el usuario (`2026-09-25`).
- P-009 — Mejorar estados vacíos y accesibilidad; retirado del backlog por el usuario (`2026-09-25`). Lo ya hecho (anillo de foco, estados de Bluetooth y acceso a su configuración) se mantiene.

- [x] P-005 — La reconexión se mantiene como lógica interna y automática, sin controles en la interfaz ni conexiones simuladas; `testing.md` actualizado. Cerrado por el usuario (`2026-09-25`).

- [x] P-014 — Estados de conexión por grupo físico (Classic prioritario en multiprotocolo, BLE en dispositivos solo BLE) y detección de reconexiones sin `Refresh`; validado por el usuario con hardware en `0.21-dev7` (`2026-09-25`).

- [x] P-016 — Detectar Bluetooth desactivado (`Windows.Devices.Radios`), mostrar `Bluetooth is turned off.` y encenderlo con `Turn on Bluetooth` o abrir la configuración; validado por el usuario en `0.21-dev2` (`2026-09-25`, `SetStateAsync` devolvió `Allowed`).

- [x] P-025 — Elegir el dispositivo visible por categoría desde `Options` y desde la pastilla, conservado por identidad física; publicado en `0.18`.
- [x] P-024 — `Options` compacto y selector de tema con altura ajustada al texto (34 px, `h-9`); publicado en `0.19`.
- [x] P-023 — Pantalla `Options` (inicio con Windows, widget, tema y `About`) y enlaces de PayPal y Patreon en `About`; publicado en `0.17`, iconos en `0.20`.
- [x] P-022 — Temas `System` y `Elegant Black` aplicados y guardados; publicado en `0.17`, refinado en `0.19` (tokens shadcn/ui, modo claro de Windows).
- [x] P-021 — Iconos vectoriales unificados: Lucide en la interfaz y glifos de Windows para dispositivos en `System`; publicado en `0.17`–`0.20`.

- [x] P-002 — Actualizar la batería durante la sesión con consultas generales, sin filtros por marca/modelo; aceptación confirmada por el responsable (`2026-09-22`).
- [x] P-003 — Definir el alcance de la lista de dispositivos; cierre confirmado por el responsable del proyecto (`2026-09-22`).
- [x] P-004 — Añadir actualización manual; cierre confirmado por el responsable del proyecto (`2026-09-22`).
- [x] P-015 — Añadir pantalla `About`; validación visual y UI Automation completadas (`2026-09-01`).
- [x] Reducir el tiempo de inicio: la ventana y el inventario inicial se publican sin esperar consultas secundarias; validado funcionalmente por el responsable del proyecto (`2026-09-01`).
- [x] Eliminar la opción de inicio minimizado y mostrar siempre la ventana al iniciar (`2026-09-01`).
- [x] Crear una skill de proyecto para documentación, QA y changelog (`2026-09-01`).
- [x] Crear este backlog canónico y el registro `Unreleased` (`2026-09-01`).
- [x] Sustituir la consulta Bluetooth inicial bloqueante por descubrimiento incremental con timeout blando (`2026-09-01`).
