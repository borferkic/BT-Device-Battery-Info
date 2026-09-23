# Estudio técnico de integración en la barra de tareas (P-017)

## Resumen

FluentFlyout confirma que es técnicamente posible mostrar una interfaz WPF compacta dentro del área visual de la barra de tareas de Windows 11. No usa el panel Widgets ni una ventana AppBar: crea una ventana auxiliar y la hospeda como hija de una ventana de Explorer que representa la barra.

Ese hospedaje no es un punto de extensión oficial para que una aplicación agregue contenido arbitrario a la barra. Es una técnica acoplada a la implementación de Explorer. Para P-017 se recomienda probarla en un prototipo aislado y optativo antes de incorporarla al flujo normal. Si falla la detección del host o no hay espacio seguro, la aplicación debe conservar el modo de bandeja y no cubrir controles de Windows.

La integración se publicó en `0.16` como opción compacta y optativa. El usuario confirmó visualmente la build `0.16-dev9`, incluido el contenedor sin recorte inferior; siguen pendientes las verificaciones de DPI y recuperación de Explorer.

## Fuente revisada

- Repositorio: [unchihugo/FluentFlyout](https://github.com/unchihugo/FluentFlyout).
- Revisión local: commit `b07bd4ad43c8b4b0f79edd707396b119de6b10ba`, clonado superficialmente en una carpeta temporal.
- Referencia visual acordada: [Taskbar Visualizer de FluentFlyout](https://fluentflyout.com/_astro/slide%205%20-%20taskbar%20visualizer.BRJFv7Gk_1XB4eW.webp).
- Ventana y posicionamiento: [TaskbarWindow.xaml.cs](https://github.com/unchihugo/FluentFlyout/blob/b07bd4ad43c8b4b0f79edd707396b119de6b10ba/FluentFlyoutWPF/Windows/TaskbarWindow.xaml.cs) y [TaskbarWindow.xaml](https://github.com/unchihugo/FluentFlyout/blob/b07bd4ad43c8b4b0f79edd707396b119de6b10ba/FluentFlyoutWPF/Windows/TaskbarWindow.xaml).
- Vista y tamaño del control multimedia: [TaskbarWidgetControl.xaml](https://github.com/unchihugo/FluentFlyout/blob/b07bd4ad43c8b4b0f79edd707396b119de6b10ba/FluentFlyoutWPF/Controls/TaskbarWidgetControl.xaml) y [TaskbarWidgetControl.xaml.cs](https://github.com/unchihugo/FluentFlyout/blob/b07bd4ad43c8b4b0f79edd707396b119de6b10ba/FluentFlyoutWPF/Controls/TaskbarWidgetControl.xaml.cs).
- Recuperación de la barra después de reiniciar Explorer: [MainWindow.xaml.cs](https://github.com/unchihugo/FluentFlyout/blob/b07bd4ad43c8b4b0f79edd707396b119de6b10ba/FluentFlyoutWPF/MainWindow.xaml.cs).

El proyecto de referencia usa WPF, igual que BT Device Battery Info, pero su proyecto actual apunta a .NET 10 y Windows 10.0.22000. La aplicación actual apunta a .NET 8 y Windows 10.0.19041. Conviene portar solo el concepto y el mínimo interop necesario, no el proyecto ni sus dependencias.

## Cómo lo implementa FluentFlyout

### Ventana WPF auxiliar

`TaskbarWindow.xaml` declara una ventana sin borde, transparente, con `ShowInTaskbar="False"`, y un `Canvas` donde coloca el widget multimedia y el visualizador. La ventana de taskbar no sustituye a la ventana principal ni a su icono de bandeja.

### Hospedaje en Explorer

Al inicializarse, `TaskbarWindow.xaml.cs` busca la barra principal (`Shell_TrayWnd`) o una secundaria (`Shell_SecondaryTrayWnd`) según el monitor elegido. Cambia los estilos Win32 de la ventana auxiliar de `WS_POPUP` a `WS_CHILD` y llama a `SetParent` para hacerla hija de esa ventana de Explorer.

La llamada Win32 `SetParent` es una API general de ventanas; su uso para añadir una interfaz de terceros como hija de la barra de Explorer no constituye por sí solo un contrato oficial de extensibilidad de la barra. Esta conclusión se infiere al contrastar el código con las superficies que Microsoft documenta para la [barra de tareas](https://learn.microsoft.com/en-us/windows/win32/shell/taskbar-extensions).

### Posicionamiento y zona interactiva

La ventana auxiliar se dimensiona al rectángulo de la barra mediante `SetWindowPos`. FluentFlyout consulta elementos visuales de Explorer mediante UI Automation —entre ellos `TaskbarFrame`, `WidgetsButton` y `SystemTrayIcon`— para calcular posiciones y evitar algunas zonas ocupadas. También contempla izquierda, centro o derecha, distintos monitores, escalado DPI, orientación y relleno manual.

Un temporizador vuelve a calcular la posición cada 1,5 segundos como recuperación ante cambios que no se notificaron. Además, procesa mensajes de DPI. Después aplica `SetWindowRgn` para limitar la región de dibujo e interacción a los controles que muestra. Microsoft documenta `SetWindowRgn` como una función que limita la región visible de una ventana, no como un mecanismo de reserva de espacio de la barra.

Los nombres de clases y los identificadores de UI Automation pertenecen a la estructura visual de Explorer y pueden cambiar. La búsqueda debe tener tiempos límite, caché invalidable y una posición alternativa segura; no se debe depender de una coordenada fija como el relleno de 216 píxeles que FluentFlyout conserva como fallback.

### Recuperación de Explorer

La ventana principal de FluentFlyout escucha el mensaje registrado `TaskbarCreated`. Cuando Explorer se reinicia, espera a que vuelva a estar disponible y recupera el icono de bandeja. En paralelo, `TaskbarWindow` pausa el reposicionamiento durante el reinicio, vuelve a buscar el HWND del host y se vuelve a adjuntar; si su propio HWND ya no existe, solicita a la ventana principal recrear el auxiliar. Los cambios de monitor también reconstruyen la ventana de taskbar.

## Diferencia con mecanismos documentados

- Las [extensiones documentadas de la barra de tareas](https://learn.microsoft.com/en-us/windows/win32/shell/taskbar-extensions) ofrecen funciones asociadas al botón de la aplicación, como vista previa, mini barra de herramientas, progreso y superposición de icono. No describen un espacio donde insertar una cápsula arbitraria junto a Inicio o Búsqueda.
- Una [Application Desktop Toolbar o AppBar](https://learn.microsoft.com/es-es/windows/win32/shell/application-desktop-toolbars) sí tiene un protocolo documentado mediante `SHAppBarMessage`, pero es una ventana anclada al borde de la pantalla y Windows reserva su área de trabajo. Sería una franja separada, no la integración visual mostrada por FluentFlyout.
- El patrón de FluentFlyout coincide más con la referencia visual solicitada, pero la relación con la ventana de Explorer debe tratarse como experimental y sujeta a cambios de Windows.

## Adaptación propuesta a BT Device Battery Info

La aplicación ya es WPF y su ventana principal no aparece en la barra de tareas. El botón actual `Minimize to tray` oculta la ventana y la aplicación mantiene el objeto `BluetoothService` mientras siga ejecutándose. El control nuevo junto a Minimizar debe ser independiente: activar/desactivar la vista acoplada no debe cambiar el comportamiento actual de minimizar a la bandeja.

La adaptación debería seguir estas reglas:

- Crear una ventana WPF auxiliar dedicada a la barra, sin título ni botón de taskbar, con el mismo estilo compacto de P-019.
- Reutilizar el estado y las consultas existentes de `BluetoothService`/`MainViewModel`; no crear otro `BluetoothService`, watcher ni ciclo de consultas de batería.
- Mostrar una pastilla por dispositivo conectado compatible —auriculares, teclado, mouse o joystick— y ocultarla cuando se desconecte, según el criterio actual de P-017.
- Mantener un indicador circular ligado al porcentaje real expuesto por Windows. Si no hay batería disponible, representar ese estado claramente, sin inventar porcentaje.
- Colocar la pastilla al lado izquierdo según la preferencia del usuario, pero calcular la zona libre respecto a Inicio, Búsqueda y los demás elementos de la barra; no asumir que el extremo izquierdo está vacío.
- Limitar visualmente y para interacción el HWND a los píxeles de las pastillas. Si el cálculo falla o no queda espacio, ocultar el widget y mantener disponible la bandeja en lugar de interceptar clics de la barra.
- Mantener la ventana auxiliar fuera de una relación de propiedad que provoque que se oculte al ocultar la ventana principal. Su ciclo de vida debe finalizar explícitamente al salir de la aplicación.

El límite actual del proyecto es .NET 8; por ello, el trabajo debe separar el cálculo de geometría de las llamadas Win32 para probar las coordenadas sin depender de Explorer. La hospedación, el hit testing y la recuperación del Shell requieren validación visual manual en Windows.

## Riesgos y validación necesaria

La región limitada reduce el área de la ventana auxiliar, pero no garantiza por sí sola que nunca cubra elementos de terceros o botones de aplicaciones. El repositorio upstream tiene un [reporte de botones de taskbar cubiertos por el widget](https://github.com/unchihugo/FluentFlyout/issues/865); por lo tanto, esa condición debe probarse directamente y no darse por resuelta porque exista `SetWindowRgn`.

Antes de aceptar P-017, el prototipo debe revisarse al menos en estas condiciones:

- Barra alineada a la izquierda y centrada, con Inicio y Búsqueda visibles u ocultos.
- Muchos botones de aplicaciones abiertos, para confirmar que ninguna pastilla se superpone a un botón ni roba sus clics.
- Escalas DPI distintas y cambio entre monitores; cambio de resolución, posición y visibilidad de la barra.
- Reinicio de Explorer, suspensión/reanudación y dispositivo conectado/desconectado mientras la ventana principal está oculta.
- Cero dispositivos compatibles, un dispositivo y varias pastillas; periférico conectado sin nivel de batería disponible.
- Modo de pantalla completa y comportamiento de la bandeja como mecanismo de recuperación.

La implementación publicada enlaza la presentación al inventario existente, añade el botón junto a Minimizar y guarda la preferencia en la configuración. La clasificación interpreta también la propiedad LE Appearance para periféricos BLE. Los iconos de auriculares, teclado, mouse y control emplean Material Symbols Rounded empaquetados con la aplicación. Al desactivar la opción, la ventana hija se oculta y se reutiliza al reactivarla. La integración se limita a la barra principal `Shell_TrayWnd`; no implementa monitores secundarios ni reserva oficialmente espacio en Explorer. El log local registra el estado y el motivo cuando el acople queda oculto.

## Licencia de la referencia

El archivo `LICENSE` del repositorio contiene GNU GPL versión 3; los archivos de la integración de taskbar declaran `GPL-3.0-or-later`. Este documento describe arquitectura y referencias sin copiar código. Cualquier reutilización o adaptación literal de código debe revisarse por separado respecto de la licencia y de las obligaciones de distribución.

## Estado

P-017 se publicó en `0.16` y pasó la revisión visual básica del usuario. Quedan pendientes las pruebas de cambios de DPI y reinicio de Explorer. La implementación usa `SetParent` y UI Automation para acoplar una ventana auxiliar en Explorer, no un contrato oficial de extensibilidad; si no hay espacio libre, oculta el control y mantiene accesible la ventana desde la bandeja.
