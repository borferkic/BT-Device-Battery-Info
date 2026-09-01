# Roadmap

## Estado actual

La versión pública `0.12` contiene la base funcional de descubrimiento Bluetooth Classic/BLE, lectura PnP/GATT, priorización Bose/QuietComfort, widget WPF, bandeja del sistema, preferencias internas, registro local, arranque incremental y agrupación de dispositivos desconectados.

Los cambios locales posteriores a `0.12` están en preparación para la siguiente versión y se documentan en `changelog/unreleased.md`.

## Próximo foco

El primer objetivo es reducir y medir el tiempo de inicio. Después se abordarán la actualización fiable de batería, la definición de dispositivos visibles, la cobertura automatizada y la exposición coherente de las acciones de reconexión.

La lista detallada, con prioridades y criterios de aceptación, está en [Pendientes](pending.md). No se deben duplicar tareas aquí.

## Dirección posterior

- Definir una matriz de validación por modelo de dispositivo, controlador y versión de Windows.
- Preparar un paquete distribuible con icono, versión visible y notas de versión.
- Evaluar actualización y desinstalación sin depender únicamente de publicación manual.
- Revisar accesibilidad, localización y estados de error.
- Evaluar si los endpoints que comparten `ContainerId` deben poder agruparse o mostrarse por separado.

## Límite de alcance

La aplicación no simulará conexiones Bluetooth ni ejecutará acciones destructivas. Cualquier mejora de conexión Bluetooth Classic debe basarse en una capacidad oficial y verificable de Windows.
