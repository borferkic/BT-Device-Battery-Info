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

Sin pendientes de prioridad media.

## Prioridad baja

Sin pendientes de prioridad baja.

## Completado recientemente

- [x] P-027 — Aviso de nuevas versiones al abrir la aplicación con enlace a la release; las builds `dev` se consideran inferiores a su versión. Validado por el usuario y publicado en `0.22` (`2026-09-25`).

- [x] P-011 — Aviso de batería baja como notificación de Windows, una vez por descarga, con interruptor y umbral en `Options`; validado por el usuario (`2026-09-25`).
- [x] P-006 — Proyecto de pruebas xUnit en CI: temas e idiomas, batería, coordinador, logger, ciclo de vida, `AppSettings` y avisos (55 pruebas, `2026-09-25`).

- [x] P-026 — Batería de mandos Bluetooth en modo X-input mediante `Windows.Gaming.Input`; porcentaje validado por el usuario con el 8BitDo Arcade Stick (`2026-09-25`).
- [x] P-018 — Registro de transiciones con evidencia Classic/BLE y origen del cambio, retención de 14 días y tope de 2 MB por día; validado por el usuario (`2026-09-25`).
- [x] P-010 — Cierre ordenado del servicio Bluetooth, eventos ignorados tras el cierre y reintentos del `DeviceWatcher` con espera progresiva; validado por el usuario (`2026-09-25`).

- [x] P-020 — Selección de idioma inglés/español en `Options` con vista previa en vivo, guardado al iniciar e inglés como predeterminado y respaldo; logs en inglés. Validado por el usuario en `0.21-dev8` y publicado en `0.21` (`2026-09-25`).

- [x] P-017 — Widget compacto de dispositivos conectados en la barra de tareas; cerrado por el usuario (`2026-09-25`).

## Descartados

- P-012 — Distribución y mantenimiento; retirado por el usuario (`2026-09-25`): la aplicación se distribuye como un único `.exe` autocontenido, sin instalador.

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
