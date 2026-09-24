# Pendientes del proyecto

Backlog canónico de BT Device Battery Info. Las tareas se priorizan por impacto observable y se mantienen aquí hasta que exista implementación y evidencia de validación.

Última revisión documental: `2026-09-22`.

## Cómo usar este documento

- `Alta`: afecta el arranque, la función principal, la exactitud del estado o la estabilidad.
- `Media`: mejora una función existente sin bloquear el uso principal.
- `Baja`: soporte, distribución, accesibilidad o mejoras de evolución.
- Cada tarea debe conservar un objetivo observable y criterios de aceptación.
- Una tarea solo pasa a completada después de actualizar el changelog correspondiente y ejecutar la validación aplicable.

## Prioridad alta

### P-014 — Corregir estados de conexión falsos

- [ ] Confirmar en hardware la regla de estado por grupo físico: endpoints Classic como señal principal en contenedores multiprotocolo y BLE para dispositivos exclusivamente BLE. Implementación candidata incluida en `0.16-dev`.
- [ ] Confirmar que la selección por identidad física (`ContainerId`, con fallback disponible) sobrevive al cambio o retirada del endpoint; se mantienen compatibles los IDs guardados por versiones anteriores. Implementación candidata incluida en `0.16-dev`.
- [ ] Confirmar que los cambios de estado salen de la instantánea agrupada y que los conteos redactados Classic/BLE ayudan al diagnóstico. Implementación candidata incluida en `0.16-dev`.
- [ ] Validar con hardware una desconexión Classic mientras queda un endpoint BLE, la desaparición del endpoint seleccionado, la reconexión y la estabilidad durante las reconciliaciones periódicas.

Hallazgo de QA del `2026-09-01`: después de desconectar algunos dispositivos, la interfaz podía seguir indicando `Connected`. En una sesión se observaron alternancias repetidas cada 30 segundos; Windows no reportaba endpoints Classic presentes, mientras la aplicación conservaba una instancia activa. `BuildDeviceSnapshot()` seleccionaba un único endpoint y priorizaba cualquiera que tuviera `IsConnected`, lo que podía confundir un endpoint BLE auxiliar con la conexión principal o mantener una identidad seleccionada que ya no representaba al dispositivo agrupado. `0.16-dev` retiene en memoria durante la sesión de la aplicación si un contenedor tuvo endpoint Classic, aunque Windows lo elimine antes que el BLE.

Criterios de aceptación: la selección y el estado sobreviven al cambio del endpoint representante; en dispositivos multiprotocolo un endpoint BLE auxiliar no mantiene `Connected` si los endpoints Classic reportan desconexión o desaparecen; los dispositivos exclusivamente BLE usan su estado BLE. La prueba física de desconexión/reconexión y estabilidad sigue pendiente antes de cerrar P-014.

### P-016 — Detectar y activar Bluetooth desactivado

- [ ] Detectar cuando el adaptador Bluetooth de Windows esté desactivado y mostrar el mensaje en inglés `Bluetooth is turned off.`.
- [ ] Añadir un botón `Turn on Bluetooth` que intente activar Bluetooth directamente desde la aplicación.
- [ ] Investigar y validar una API oficial de Windows compatible con la versión mínima soportada antes de implementar la activación.
- [ ] No simular una activación: si Windows no permite activarlo por API o requiere intervención del usuario, informar el resultado real y ofrecer una alternativa segura.

Objetivo observable: cuando el usuario apaga Bluetooth desde Windows, el widget deja de presentar la lista como un fallo genérico, explica que Bluetooth está desactivado y ofrece la acción directa solicitada.

Criterios de aceptación: el estado se actualiza al apagar o encender Bluetooth, el botón comunica claramente éxito o motivo de imposibilidad y no modifica otros adaptadores ni emparejamientos.

## Prioridad media

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

### P-017 — Widget compacto de dispositivos conectados en la barra de tareas

- [x] Mostrar dispositivos conectados en pastillas compactas en la barra de tareas; validado visualmente por el usuario en `0.16-dev9` y publicado en `0.16`.
- [ ] Confirmar los cambios de DPI y la recuperación tras reiniciar Explorer; el hospedaje en Explorer sigue siendo experimental.

### P-018 — Registrar conexiones y desconexiones para QA

- [ ] Registrar fecha y hora, dispositivo con identificadores redactados, transición de conexión y fuente/evidencia disponible (por ejemplo, eventos del watcher y estado Classic/BLE).
- [ ] Diferenciar una causa confirmada por Windows de una inferencia; no atribuir un motivo físico que el sistema no haya reportado.
- [ ] Evitar entradas repetidas cuando el estado no cambie y definir límites de tamaño o retención para el registro local.
- [ ] Facilitar el uso del registro local para diagnosticar errores sin exponer direcciones ni identificadores sensibles.

Objetivo observable: ante una conexión o desconexión, QA puede reconstruir cuándo se detectó el cambio y qué evidencia disponible lo acompañó.

Criterios de aceptación: el registro local contiene transiciones fechadas y redactadas con su fuente/evidencia, no duplica estados inalterados, distingue causas conocidas de inferencias y respeta la política de retención definida.

### P-019 — Añadir una vista compacta tipo pastilla

- [ ] Ofrecer un modo compacto horizontal tipo pastilla, alternable con la ventana actual, que ocupe menos espacio en pantalla.
- [ ] Mostrar un icono según el tipo de dispositivo y un indicador circular de batería.
- [ ] Contemplar auriculares, teclados, mouse y joysticks; mostrar cada uno solo mientras Windows lo reporte conectado.
- [ ] Representar con claridad los estados desconectado y batería no disponible.
- [ ] Mantener la información actualizada y hacer que el indicador circular refleje el nivel de batería reportado.

Objetivo observable: el usuario puede consultar de un vistazo el dispositivo y su batería en una pastilla horizontal que ahorra espacio, tanto como vista compacta del widget como para la integración en la barra de tareas de P-017.

Criterios de aceptación: el usuario puede cambiar entre la vista actual y la compacta; la pastilla presenta icono, nivel de batería y estado correctos para los tipos de dispositivo admitidos, e indica claramente cuándo no hay conexión o no existe un nivel de batería disponible. El modo compacto y el de barra de tareas comparten esta presentación y evitan tener que mantener abierta la ventana grande.

### P-020 — Añadir selección de idioma inglés/español

- [ ] Añadir en `Options` una preferencia para elegir inglés o español.
- [ ] Traducir todos los textos de interfaz, botones, tooltips, menús y estados visibles; conservar los mensajes de diagnóstico y logs de la aplicación en inglés.
- [ ] Guardar la preferencia y aplicarla al iniciar; usar inglés como idioma predeterminado y como fallback si falta una traducción.
- [ ] Verificar cambio de idioma en la ventana principal, bandeja, vista compacta y ventanas secundarias.

Objetivo observable: cada usuario puede elegir inglés o español desde `Options` y la selección se conserva entre ejecuciones.

Criterios de aceptación: todos los textos visibles cambian al idioma elegido sin reiniciar la aplicación, la selección persiste y los logs siguen en inglés.

### P-021 — Unificar los iconos vectoriales estilo Lucide

Avance parcial: los dispositivos de la ventana principal y la barra de tareas usan geometrías vectoriales estilo Lucide en `0.17-dev4`; queda pendiente la revisión visual del usuario.

- [x] Reemplazar los pictogramas de dispositivos en la ventana principal y barra de tareas por geometrías vectoriales estilo Lucide.
- [ ] Revisar tamaño, alineación y contraste de los iconos en la build `0.17-dev4` antes de cerrar el pendiente.

Objetivo observable: la interfaz usa una familia coherente de iconos Material Design en todas sus vistas.

Criterios de aceptación: no quedan iconos mezclados de fuentes distintas en los controles cubiertos y todos se ven correctamente en una instalación limpia.

### P-022 — Integrar temas para la ventana principal en 0.17

- [ ] Ofrecer `System` (tema actual de 0.16) y `Elegant Black`, usando los tokens y radios de shadcn/ui para el nuevo tema.
- [ ] Aplicar ambos temas a la ventana grande y guardar la selección del usuario.

### P-023 — Añadir una pantalla Options y actualizar About

- [ ] Cambiar el botón de configuración para abrir `Options` con inicio de Windows, widget de barra de tareas, selector de tema y botón `About`.
- [ ] Mantener el resto de About; tras las redes añadir “You can support my work through PayPal or Patreon.” y enlaces a `https://paypal.me/borissdk` y `https://patreon.com/borissdk`.

### P-024 — Ajustar espacios y selector de tema en Options

- [ ] Eliminar los espacios verticales sobrantes señalados en `Fix.jpg` y hacer que el selector de tema de `fix2.jpg` ajuste su altura al texto, con el padding habitual.
- [ ] Verificar que `Options` quede compacto y que el selector conserve legibilidad y uso normal.

### P-025 — Elegir el dispositivo visible por categoría

- [ ] Permitir elegir auriculares, teclado, mouse y control desde `Options` y desde el menú que abre su pastilla en la barra de tareas.
- [ ] Conservar cada elección por identidad física y mostrar un dispositivo conectado por categoría.

## Completado recientemente

- [x] P-002 — Actualizar la batería durante la sesión con consultas generales, sin filtros por marca/modelo; aceptación confirmada por el responsable (`2026-09-22`).
- [x] P-003 — Definir el alcance de la lista de dispositivos; cierre confirmado por el responsable del proyecto (`2026-09-22`).
- [x] P-004 — Añadir actualización manual; cierre confirmado por el responsable del proyecto (`2026-09-22`).
- [x] P-015 — Añadir pantalla `About`; validación visual y UI Automation completadas (`2026-09-01`).
- [x] Reducir el tiempo de inicio: la ventana y el inventario inicial se publican sin esperar consultas secundarias; validado funcionalmente por el responsable del proyecto (`2026-09-01`).
- [x] Eliminar la opción de inicio minimizado y mostrar siempre la ventana al iniciar (`2026-09-01`).
- [x] Crear una skill de proyecto para documentación, QA y changelog (`2026-09-01`).
- [x] Crear este backlog canónico y el registro `Unreleased` (`2026-09-01`).
- [x] Sustituir la consulta Bluetooth inicial bloqueante por descubrimiento incremental con timeout blando (`2026-09-01`).
